// Services/Scheduling/ScheduleService.cs
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediCare.App.Data;
using MediCare.App.Models.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace MediCare.App.Services.Scheduling
{
    public class ScheduleService : IScheduleService
    {
        private readonly MediCareContext _db;
        private const double WEEKLY_LIMIT_HOURS = 48.0;

        public ScheduleService(MediCareContext db) => _db = db;

        // ---------- Helpers ----------
        private static (DateTime weekStartLocal, DateTime weekEndLocal) GetWeekBoundsMondayToSunday(DateTime anyLocalDate)
        {
            // Monday 00:00 through next Monday 00:00 (Sunday=0)
            var dow = (int)anyLocalDate.DayOfWeek;
            int offsetToMonday = dow == 0 ? -6 : 1 - dow;
            var monday = anyLocalDate.Date.AddDays(offsetToMonday);
            var nextMonday = monday.AddDays(7);
            return (monday, nextMonday);
        }

        private static DateTime ToUtc(DateTime local, TimeZoneInfo tz) =>
            TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), tz);

        private static DateTime ToUtc(DateTime dateOnlyLocal, TimeZoneInfo tz, bool endOfDay)
        {
            var local = endOfDay ? dateOnlyLocal.Date.AddDays(1).AddSeconds(-1) : dateOnlyLocal.Date;
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), tz);
        }

        private static DateTime ToLocal(DateTime utc, TimeZoneInfo tz) =>
            TimeZoneInfo.ConvertTimeFromUtc(utc, tz);

        // ---------- Interface implementations ----------

        public async Task<WeeklyHoursResult> GetWeeklyHoursAsync(
            string doctorId,
            DateTime weekAnyLocalDate,
            TimeZoneInfo tz,
            CancellationToken ct)
        {
            var (weekStartLocal, weekEndLocal) = GetWeekBoundsMondayToSunday(weekAnyLocalDate);
            var weekStartUtc = ToUtc(weekStartLocal, tz);
            var weekEndUtc = ToUtc(weekEndLocal, tz);

            var weekAssignments = await _db.DutyAssignments
                .Where(a => a.DoctorId == doctorId &&
                            a.EndUtc > weekStartUtc &&
                            a.StartUtc < weekEndUtc)
                .OrderBy(a => a.StartUtc)
                .ToListAsync(ct);

            double total = 0;
            foreach (var a in weekAssignments)
            {
                var s = a.StartUtc < weekStartUtc ? weekStartUtc : a.StartUtc;
                var e = a.EndUtc > weekEndUtc ? weekEndUtc : a.EndUtc;
                total += (e - s).TotalHours;
            }

            return new WeeklyHoursResult(total, Math.Max(0, WEEKLY_LIMIT_HOURS - total), weekAssignments);
        }

        public async Task<(bool ok, string? error)> AssignDutyAsync(
            string doctorId,
            int branchId,
            DateTime startLocal,
            DateTime endLocal,
            string createdByUserId,
            TimeZoneInfo tz,
            CancellationToken ct)
        {
            if (endLocal <= startLocal)
                return (false, "End time must be after start time.");

            var startUtc = ToUtc(startLocal, tz);
            var endUtc = ToUtc(endLocal, tz);

            // 1) Overlap check
            bool overlaps = await _db.DutyAssignments
                .AnyAsync(a => a.DoctorId == doctorId &&
                               a.EndUtc > startUtc &&
                               a.StartUtc < endUtc, ct);
            if (overlaps)
                return (false, "This duty overlaps with an existing assignment.");

            // 2) Against approved block requests (date-based)
            bool blocked = await _db.DoctorBlockRequests
                .AnyAsync(r => r.DoctorId == doctorId &&
                               r.Status == BlockRequestStatus.Approved &&
                               r.EndDateUtc >= startUtc.Date &&
                               r.StartDateUtc <= endUtc.Date, ct);
            if (blocked)
                return (false, "Doctor has an approved block request on this date/time.");

            // 3) Weekly 48-hour rule (Mon–Sun)
            var weekly = await GetWeeklyHoursAsync(doctorId, ToLocal(startUtc, tz), tz, ct);
            var addHours = (endUtc - startUtc).TotalHours;
            if (weekly.TotalHours + addHours > WEEKLY_LIMIT_HOURS)
                return (false, $"This assignment exceeds the 48-hour weekly limit. Hours left: {Math.Max(0, WEEKLY_LIMIT_HOURS - weekly.TotalHours):0.##}");

            _db.DutyAssignments.Add(new DutyAssignment
            {
                DoctorId = doctorId,
                BranchId = branchId,
                StartUtc = startUtc,
                EndUtc = endUtc,
                CreatedByUserId = createdByUserId
            });

            await _db.SaveChangesAsync(ct);
            return (true, null);
        }

        public async Task<bool> ApproveBlockAsync(int requestId, string adminUserId, CancellationToken ct)
        {
            var req = await _db.DoctorBlockRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct);
            if (req == null || req.Status != BlockRequestStatus.Pending) return false;

            req.Status = BlockRequestStatus.Approved;
            req.DecisionByUserId = adminUserId;
            req.DecisionAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> DeclineBlockAsync(int requestId, string adminUserId, CancellationToken ct)
        {
            var req = await _db.DoctorBlockRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct);
            if (req == null || req.Status != BlockRequestStatus.Pending) return false;

            req.Status = BlockRequestStatus.Rejected;
            req.DecisionByUserId = adminUserId;
            req.DecisionAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<(bool ok, string? error, int requestId)> RequestBlockAsync(
            string doctorId,
            DateTime startLocal,
            DateTime endLocal,
            TimeZoneInfo tz,
            CancellationToken ct = default)
        {
            if (endLocal < startLocal)
                return (false, "End must be on/after start.", 0);

            // Date-based block
            var startDateUtc = ToUtc(startLocal.Date, tz, endOfDay: false);
            var endDateUtc = ToUtc(endLocal.Date, tz, endOfDay: true);

            // Avoid duplicate/overlap pending
            bool overlapPending = await _db.DoctorBlockRequests.AnyAsync(r =>
                r.DoctorId == doctorId &&
                r.Status == BlockRequestStatus.Pending &&
                r.EndDateUtc >= startDateUtc &&
                r.StartDateUtc <= endDateUtc, ct);
            if (overlapPending) return (false, "A pending block overlaps this period.", 0);

            // Optional: guard approved overlap
            bool overlapApproved = await _db.DoctorBlockRequests.AnyAsync(r =>
                r.DoctorId == doctorId &&
                r.Status == BlockRequestStatus.Approved &&
                r.EndDateUtc >= startDateUtc &&
                r.StartDateUtc <= endDateUtc, ct);
            if (overlapApproved) return (false, "An approved block already covers this period.", 0);

            var req = new DoctorBlockRequest
            {
                DoctorId = doctorId,
                StartDateUtc = startDateUtc,
                EndDateUtc = endDateUtc,
                Status = BlockRequestStatus.Pending,
                RequestedAtUtc = DateTime.UtcNow
            };

            _db.DoctorBlockRequests.Add(req);
            await _db.SaveChangesAsync(ct);

            return (true, null, req.Id);
        }

        public async Task<(bool ok, string? error)> UpdateAssignmentAsync(
        int assignmentId,
        string newDoctorId,
        int newBranchId,
        DateTime newStartLocal,
        DateTime newEndLocal,
        TimeZoneInfo tz,
        CancellationToken ct)
        {
            if (newEndLocal <= newStartLocal)
                return (false, "End time must be after start time.");

            var a = await _db.DutyAssignments.FirstOrDefaultAsync(x => x.Id == assignmentId, ct);
            if (a == null) return (false, "Assignment not found.");

            var newStartUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(newStartLocal, DateTimeKind.Unspecified), tz);
            var newEndUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(newEndLocal, DateTimeKind.Unspecified), tz);

            // 1) Overlap check for the *new* doctor, excluding this assignment
            bool overlaps = await _db.DutyAssignments
                .AnyAsync(x => x.Id != assignmentId &&
                               x.DoctorId == newDoctorId &&
                               x.EndUtc > newStartUtc &&
                               x.StartUtc < newEndUtc, ct);
            if (overlaps) return (false, "This duty overlaps with an existing assignment for the selected doctor.");

            // 2) Approved block check for the *new* doctor
            bool blocked = await _db.DoctorBlockRequests
                .AnyAsync(r => r.DoctorId == newDoctorId &&
                               r.Status == BlockRequestStatus.Approved &&
                               r.EndDateUtc >= newStartUtc.Date &&
                               r.StartDateUtc <= newEndUtc.Date, ct);
            if (blocked) return (false, "Selected doctor has an approved block during this time.");

            // 3) Weekly 48h rule on the *new* doctor.
            // If editing the same doctor within the same week, subtract the current assignment's hours from that week's total first.
            var (weekStartLocal, weekEndLocal) = GetWeekBoundsMondayToSunday(TimeZoneInfo.ConvertTimeFromUtc(newStartUtc, tz));
            var weekStartUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(weekStartLocal, DateTimeKind.Unspecified), tz);
            var weekEndUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(weekEndLocal, DateTimeKind.Unspecified), tz);

            // current weekly total for the *new* doctor (excluding the current assignment)
            var weeklyAssignments = await _db.DutyAssignments
                .Where(x => x.DoctorId == newDoctorId &&
                            x.Id != assignmentId &&
                            x.EndUtc > weekStartUtc &&
                            x.StartUtc < weekEndUtc)
                .ToListAsync(ct);

            double weeklyHours = 0;
            foreach (var w in weeklyAssignments)
            {
                var s = w.StartUtc < weekStartUtc ? weekStartUtc : w.StartUtc;
                var e = w.EndUtc > weekEndUtc ? weekEndUtc : w.EndUtc;
                weeklyHours += (e - s).TotalHours;
            }

            // If the assignment currently belongs to the *same* doctor and overlaps the same week,
            // subtract the old (clipped) hours, because we’re replacing them.
            if (a.DoctorId == newDoctorId &&
                a.EndUtc > weekStartUtc && a.StartUtc < weekEndUtc)
            {
                var s = a.StartUtc < weekStartUtc ? weekStartUtc : a.StartUtc;
                var e = a.EndUtc > weekEndUtc ? weekEndUtc : a.EndUtc;
                weeklyHours += 0; // already excluded because query had x.Id != assignmentId
            }

            var newAddHours = (newEndUtc - newStartUtc).TotalHours;
            if (weeklyHours + newAddHours > WEEKLY_LIMIT_HOURS)
                return (false, $"Weekly 48-hour limit exceeded for the selected doctor. Hours left this week: {Math.Max(0, WEEKLY_LIMIT_HOURS - weeklyHours):0.##}h.");

            // 4) Apply updates (old doctor automatically “gets hours back” because the old record is changed)
            a.DoctorId = newDoctorId;
            a.BranchId = newBranchId;
            a.StartUtc = newStartUtc;
            a.EndUtc = newEndUtc;

            await _db.SaveChangesAsync(ct);
            return (true, null);
        }

        public async Task<(bool ok, string? error)> DeleteAssignmentAsync(int assignmentId, CancellationToken ct)
        {
            var a = await _db.DutyAssignments.FirstOrDefaultAsync(x => x.Id == assignmentId, ct);
            if (a == null) return (false, "Assignment not found.");

            _db.DutyAssignments.Remove(a);
            await _db.SaveChangesAsync(ct);
            return (true, null);
        }

    }
}

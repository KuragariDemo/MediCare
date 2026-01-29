using MediCare.App.Data;
using MediCare.App.Models.Scheduling;
using MediCare.App.Services.Scheduling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MediCare.App.Controllers.Admin
{
    [Authorize(Roles = "AdminPlus,Admin")]
    [Route("Admin/Schedule")]
    public class ScheduleManagementController : Controller
    {
        private readonly MediCareContext _db;
        private readonly IScheduleService _svc;

        // Windows server: "SE Asia Standard Time"; Linux: "Asia/Bangkok"
        private static readonly TimeZoneInfo TZ =
#if WINDOWS
            TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
#else
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok");
#endif

        public ScheduleManagementController(MediCareContext db, IScheduleService svc)
        {
            _db = db; _svc = svc;
        }

        // ---------- Page ----------
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            ViewBag.Branches = await _db.ClinicBranches
                .OrderBy(b => b.Name)
                .ToListAsync();

            return View("~/Views/Admin/ScheduleManagement.cshtml");
        }

        // ---------- Doctors (paged + search) ----------
        // Joins AspNetUsers (ApplicationUser) for name/email; left-joins Doctors for speciality
        [HttpGet("Doctors")]
        public async Task<IActionResult> Doctors(string? q, int page = 1, int pageSize = 10)
        {
            var query =
                from doc in _db.Doctors                       // has UserId (string)
                join u in _db.Users on doc.UserId equals u.Id  // ApplicationUser
                select new
                {
                    Id = u.Id,                                        // use UserId as DoctorId everywhere
                    FullName = (string?)EF.Property<string>(u, "FullName") ?? u.UserName ?? "(no name)",
                    Email = u.Email ?? "-",
                    Speciality = doc.Speciality
                };

            if (!string.IsNullOrWhiteSpace(q))
            {
                var s = q.Trim().ToLower();
                query = query.Where(d =>
                    d.FullName.ToLower().Contains(s) ||
                    d.Email.ToLower().Contains(s) ||
                    (d.Speciality ?? "").ToLower().Contains(s));
            }

            var total = await query.CountAsync();
            var items = await query
                .OrderBy(d => d.FullName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Json(new
            {
                total,
                items,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(total / (double)pageSize)
            });
        }

        // ---------- Assignments (paged + search) ----------
        [HttpGet("Assignments")]
        public async Task<IActionResult> Assignments(string? q, int page = 1, int pageSize = 10)
        {
            var query =
                from a in _db.DutyAssignments
                join u in _db.Users on a.DoctorId equals u.Id
                join b in _db.ClinicBranches on a.BranchId equals b.Id
                join d in _db.Doctors on u.Id equals d.UserId into dd
                from d in dd.DefaultIfEmpty()
                select new
                {
                    a,
                    DoctorId = u.Id,
                    DoctorName = (string?)EF.Property<string>(u, "FullName") ?? u.UserName ?? "(no name)",
                    BranchName = b.Name,
                    Speciality = d != null ? d.Speciality : null
                };

            if (!string.IsNullOrWhiteSpace(q))
            {
                var s = q.Trim().ToLower();
                query = query.Where(x =>
                    x.DoctorName.ToLower().Contains(s) ||
                    (x.BranchName ?? "").ToLower().Contains(s));
            }

            var total = await query.CountAsync();
            var rows = await query
                .OrderByDescending(x => x.a.StartUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = rows.Select(x => new
            {
                Id = x.a.Id, // 👈 this is the ID we will pass to openEdit/cancelAssignment
                DoctorId = x.DoctorId,
                DoctorName = x.DoctorName,
                Branch = x.BranchName,
                StartLocal = TimeZoneInfo.ConvertTimeFromUtc(x.a.StartUtc, TZ).ToString("yyyy-MM-dd HH:mm"),
                EndLocal = TimeZoneInfo.ConvertTimeFromUtc(x.a.EndUtc, TZ).ToString("yyyy-MM-dd HH:mm"),
                Hours = Math.Round((x.a.EndUtc - x.a.StartUtc).TotalHours, 2)
            });

            return Json(new
            {
                total,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(total / (double)pageSize),
                items
            });
        }

        // ---------- Create assignment ----------
        public record AssignDutyDto(string DoctorId, int BranchId, DateTime Date, string StartTime, string EndTime);

        [HttpPost("Assign")]
        public async Task<IActionResult> Assign([FromBody] AssignDutyDto dto, CancellationToken ct)
        {
            if (!TimeOnly.TryParse(dto.StartTime, out var st) || !TimeOnly.TryParse(dto.EndTime, out var et))
                return BadRequest(new { ok = false, error = "Invalid time format." });

            var startLocal = dto.Date.Date.AddHours(st.Hour).AddMinutes(st.Minute);
            var endLocal = dto.Date.Date.AddHours(et.Hour).AddMinutes(et.Minute);

            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";

            var (ok, error) = await _svc.AssignDutyAsync(dto.DoctorId, dto.BranchId, startLocal, endLocal, adminId, TZ, ct);
            if (!ok) return BadRequest(new { ok = false, error });

            return Json(new { ok = true });
        }

        // ---------- Check weekly hours ----------
        [HttpGet("WeeklyHours")]
        public async Task<IActionResult> WeeklyHours(string doctorId, DateTime weekOfLocal)
        {
            var result = await _svc.GetWeeklyHoursAsync(doctorId, weekOfLocal, TZ, HttpContext.RequestAborted);
            var items = result.Assignments.Select(a => new
            {
                StartLocal = TimeZoneInfo.ConvertTimeFromUtc(a.StartUtc, TZ).ToString("yyyy-MM-dd HH:mm"),
                EndLocal = TimeZoneInfo.ConvertTimeFromUtc(a.EndUtc, TZ).ToString("yyyy-MM-dd HH:mm"),
                Hours = Math.Round((a.EndUtc - a.StartUtc).TotalHours, 2),
                BranchId = a.BranchId
            });

            return Json(new { totalHours = result.TotalHours, hoursLeft = result.HoursLeft, items });
        }

        // ---------- Pending block requests (paged + search) ----------
        [HttpGet("BlockRequests")]
        public async Task<IActionResult> BlockRequests(string? q, int page = 1, int pageSize = 10)
        {
            var query =
                from r in _db.DoctorBlockRequests
                join u in _db.Users on r.DoctorId equals u.Id
                where r.Status == BlockRequestStatus.Pending
                select new
                {
                    r,
                    DoctorId = u.Id,
                    DoctorName = (string?)EF.Property<string>(u, "FullName") ?? u.UserName ?? "(no name)"
                };

            if (!string.IsNullOrWhiteSpace(q))
            {
                var s = q.Trim().ToLower();
                query = query.Where(x => x.DoctorName.ToLower().Contains(s));
            }

            var total = await query.CountAsync();
            var rows = await query
                .OrderByDescending(x => x.r.RequestedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = rows.Select(x => new
            {
                RequestId = x.r.Id,
                DoctorId = x.DoctorId,
                DoctorName = x.DoctorName,
                StartDate = TimeZoneInfo.ConvertTimeFromUtc(x.r.StartDateUtc, TZ).ToString("yyyy-MM-dd"),
                EndDate = TimeZoneInfo.ConvertTimeFromUtc(x.r.EndDateUtc, TZ).ToString("yyyy-MM-dd"),
                x.r.Reason
            });

            return Json(new
            {
                total,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(total / (double)pageSize),
                items
            });
        }

        // ---------- Approve / Decline ----------
        [HttpPost("BlockRequests/{id}/approve")]
        public async Task<IActionResult> ApproveBlock(int id, CancellationToken ct)
        {
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
            var ok = await _svc.ApproveBlockAsync(id, adminId, ct);
            return ok ? Json(new { ok = true }) : BadRequest(new { ok = false, error = "Cannot approve." });
        }

        [HttpPost("BlockRequests/{id}/decline")]
        public async Task<IActionResult> DeclineBlock(int id, CancellationToken ct)
        {
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
            var ok = await _svc.DeclineBlockAsync(id, adminId, ct);
            return ok ? Json(new { ok = true }) : BadRequest(new { ok = false, error = "Cannot decline." });
        }

        // ---------- Assignment details (for modal) ----------
        [HttpGet("Assignments/{id}")]
        public async Task<IActionResult> AssignmentById(int id)
        {
            // 1) DB query first (no timezone logic yet)
            var raw = await (
                from a in _db.DutyAssignments
                join u in _db.Users on a.DoctorId equals u.Id
                join b in _db.ClinicBranches on a.BranchId equals b.Id
                join d in _db.Doctors on u.Id equals d.UserId into dd
                from d in dd.DefaultIfEmpty()
                where a.Id == id
                select new
                {
                    a.Id,
                    a.StartUtc,
                    a.EndUtc,
                    DoctorId = u.Id,
                    DoctorName = (string?)EF.Property<string>(u, "FullName") ?? u.UserName ?? "(no name)",
                    BranchId = b.Id,
                    BranchName = b.Name,
                    DoctorNumericId = d != null ? d.Id : (int?)null
                }
            ).FirstOrDefaultAsync();

            if (raw == null) return NotFound();

            // 2) timezone conversion on app side
            var item = new
            {
                raw.Id,
                raw.DoctorId,
                raw.DoctorName,
                raw.BranchId,
                raw.BranchName,
                StartLocal = TimeZoneInfo.ConvertTimeFromUtc(raw.StartUtc, TZ).ToString("yyyy-MM-ddTHH:mm"), // good for <input type="datetime-local">
                EndLocal = TimeZoneInfo.ConvertTimeFromUtc(raw.EndUtc, TZ).ToString("yyyy-MM-ddTHH:mm"),
                raw.DoctorNumericId
            };

            return Json(item);
        }

        // ---------- Update assignment ----------
        public record EditAssignmentDto(string DoctorId, int BranchId, DateTime StartLocal, DateTime EndLocal);

        [HttpPut("Assignments/{id}")]
        public async Task<IActionResult> UpdateAssignment(int id, [FromBody] EditAssignmentDto dto, CancellationToken ct)
        {
            var (ok, error) = await _svc.UpdateAssignmentAsync(id, dto.DoctorId, dto.BranchId, dto.StartLocal, dto.EndLocal, TZ, ct);
            return ok ? Json(new { ok = true }) : BadRequest(new { ok = false, error });
        }

        // ---------- Delete assignment ----------
        [HttpDelete("Assignments/{id}")]
        public async Task<IActionResult> DeleteAssignment(int id, CancellationToken ct)
        {
            var (ok, error) = await _svc.DeleteAssignmentAsync(id, ct);
            return ok ? Json(new { ok = true }) : BadRequest(new { ok = false, error });
        }
    }
}

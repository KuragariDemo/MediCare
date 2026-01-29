using MediCare.App.Data;
using MediCare.App.Models.Scheduling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;

namespace MediCare.App.Controllers.Doctor
{
    [Authorize(Roles = "Doctor")]
    [Route("Doctors/DoctorSchedule")]
    public class DoctorScheduleController : Controller
    {
        private readonly MediCareContext _db;

        private static readonly string[] TzCandidates = new[] { "SE Asia Standard Time", "Asia/Bangkok" };
        private readonly TimeZoneInfo _displayTz;

        public DoctorScheduleController(MediCareContext db)
        {
            _db = db;
            _displayTz = ResolveTz();
        }

        private static TimeZoneInfo ResolveTz()
        {
            foreach (var id in TzCandidates)
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch { /* try next */ }
            }
            return TimeZoneInfo.Utc; // fallback
        }

        // PAGE: GET /Doctors/DoctorSchedule
        [HttpGet("")]
        public IActionResult Index()
        {
            return View("/Views/Doctors/DoctorSchedule.cshtml");
        }

        // JSON: GET /Doctors/DoctorSchedule/Month?year=YYYY&month=M
        [HttpGet("Month")]
        public async Task<IActionResult> GetMonth(int year, int month)
        {
            var doctorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(doctorUserId)) return Unauthorized();

            var monthStartLocal = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
            var monthEndLocal = monthStartLocal.AddMonths(1);

            var monthStartUtc = TimeZoneInfo.ConvertTimeToUtc(monthStartLocal, _displayTz);
            var monthEndUtc = TimeZoneInfo.ConvertTimeToUtc(monthEndLocal, _displayTz);

            var assignments = await _db.DutyAssignments
                .AsNoTracking()
                .Where(a => a.DoctorId == doctorUserId &&
                            a.EndUtc > monthStartUtc && a.StartUtc < monthEndUtc)
                .Join(_db.ClinicBranches,
                      a => a.BranchId,
                      b => b.Id,
                      (a, b) => new { a.Id, a.StartUtc, a.EndUtc, a.BranchId, BranchName = b.Name })
                .ToListAsync();

            var blocks = await _db.DoctorBlockRequests
                .AsNoTracking()
                .Where(b => b.DoctorId == doctorUserId &&
                            b.Status == BlockRequestStatus.Approved &&
                            b.EndDateUtc > monthStartUtc && b.StartDateUtc < monthEndUtc)
                .ToListAsync();

            var days = new List<MonthDayDto>();
            var d = monthStartLocal.Date;
            while (d < monthEndLocal.Date)
            {
                var dayStartLocal = d;
                var dayEndLocal = d.AddDays(1);

                var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(dayStartLocal, _displayTz);
                var dayEndUtc = TimeZoneInfo.ConvertTimeToUtc(dayEndLocal, _displayTz);

                var duties = assignments
                    .Where(a => a.EndUtc > dayStartUtc && a.StartUtc < dayEndUtc)
                    .Select(a =>
                    {
                        var stLocal = TimeZoneInfo.ConvertTimeFromUtc(a.StartUtc, _displayTz);
                        var enLocal = TimeZoneInfo.ConvertTimeFromUtc(a.EndUtc, _displayTz);

                        var showStart = stLocal < dayStartLocal ? dayStartLocal : stLocal;
                        var showEnd = enLocal > dayEndLocal ? dayEndLocal : enLocal;

                        return new DutyItemDto
                        {
                            Id = a.Id,
                            BranchId = a.BranchId,
                            BranchName = a.BranchName,
                            Start = showStart.ToString("HH:mm"),
                            End = showEnd.ToString("HH:mm")
                        };
                    })
                    .OrderBy(x => x.Start)
                    .ToList();

                var blocked = blocks.Any(b => b.EndDateUtc > dayStartUtc && b.StartDateUtc < dayEndUtc);

                days.Add(new MonthDayDto
                {
                    Date = d.ToString("yyyy-MM-dd"),
                    Duties = duties,
                    IsBlocked = blocked
                });

                d = d.AddDays(1);
            }

            return Json(new MonthResponseDto
            {
                Year = year,
                Month = month,
                MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month),
                Days = days
            });
        }

        // JSON: GET /Doctors/DoctorSchedule/Day?date=yyyy-MM-dd
        [HttpGet("Day")]
        public async Task<IActionResult> GetDay(string date)
        {
            var doctorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(doctorUserId)) return Unauthorized();

            if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var localDate))
            {
                return BadRequest("Invalid date");
            }

            var dayStartLocal = localDate.Date;
            var dayEndLocal = dayStartLocal.AddDays(1);

            var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(dayStartLocal, _displayTz);
            var dayEndUtc = TimeZoneInfo.ConvertTimeToUtc(dayEndLocal, _displayTz);

            var assignments = await _db.DutyAssignments
                .AsNoTracking()
                .Where(a => a.DoctorId == doctorUserId &&
                            a.EndUtc > dayStartUtc && a.StartUtc < dayEndUtc)
                .Join(_db.ClinicBranches,
                      a => a.BranchId,
                      b => b.Id,
                      (a, b) => new { a.Id, a.StartUtc, a.EndUtc, a.BranchId, BranchName = b.Name })
                .ToListAsync();

            var duties = assignments.Select(a =>
            {
                var stLocal = TimeZoneInfo.ConvertTimeFromUtc(a.StartUtc, _displayTz);
                var enLocal = TimeZoneInfo.ConvertTimeFromUtc(a.EndUtc, _displayTz);

                var showStart = stLocal < dayStartLocal ? dayStartLocal : stLocal;
                var showEnd = enLocal > dayEndLocal ? dayEndLocal : enLocal;

                return new DutyItemDto
                {
                    Id = a.Id,
                    BranchId = a.BranchId,
                    BranchName = a.BranchName,
                    Start = showStart.ToString("HH:mm"),
                    End = showEnd.ToString("HH:mm")
                };
            }).OrderBy(x => x.Start).ToList();

            var blocked = await _db.DoctorBlockRequests
                .AsNoTracking()
                .Where(b => b.DoctorId == doctorUserId &&
                            b.Status == BlockRequestStatus.Approved &&
                            b.EndDateUtc > dayStartUtc && b.StartDateUtc < dayEndUtc)
                .FirstOrDefaultAsync();

            return Json(new DayResponseDto
            {
                Date = localDate.ToString("yyyy-MM-dd"),
                Duties = duties,
                IsBlocked = blocked != null,
                BlockReason = blocked?.Reason ?? ""
            });
        }


    }

    // ===== DTOs =====
    public class MonthResponseDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = "";
        public List<MonthDayDto> Days { get; set; } = new();
    }

    public class MonthDayDto
    {
        public string Date { get; set; } = ""; // yyyy-MM-dd
        public bool IsBlocked { get; set; }
        public List<DutyItemDto> Duties { get; set; } = new();
    }

    public class DayResponseDto
    {
        public string Date { get; set; } = "";
        public bool IsBlocked { get; set; }
        public string BlockReason { get; set; } = "";
        public List<DutyItemDto> Duties { get; set; } = new();
    }

    public class DutyItemDto
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public string BranchName { get; set; } = "";
        public string Start { get; set; } = ""; // HH:mm
        public string End { get; set; } = "";   // HH:mm
    }
}

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MediCare.App.Data;
using MediCare.App.ViewModels.Doctor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediCare.App.Controllers.Doctor
{
    [Authorize(Roles = "Doctor")]
    [Route("Doctor/Appointments")]
    public class DoctorAppointmentsController : Controller
    {
        private readonly MediCareContext _db;

        // Bangkok timezone
        private static readonly TimeZoneInfo TZ =
#if WINDOWS
            TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
#else
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok");
#endif

        public DoctorAppointmentsController(MediCareContext db)
        {
            _db = db;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index([FromQuery] string? q)
        {
            var meUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var doctorEntity = await _db.Doctors
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == meUserId);

            // If this logged-in user is not mapped to a Doctor row
            if (doctorEntity == null)
            {
                var emptyVm = new DoctorAppointmentsVM
                {
                    SearchTerm = q ?? "",
                    TodayDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, TZ))
                };

                return View("~/Views/Doctors/DoctorAppointments.cshtml", emptyVm);
            }

            var doctorDbId = doctorEntity.Id;

            var baseQuery = _db.Appointments
                .Where(a => a.DoctorId == doctorDbId);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var qLower = q.Trim().ToLower();

                baseQuery = baseQuery.Where(a =>
                    a.PatientName.ToLower().Contains(qLower) ||
                    a.PatientEmail.ToLower().Contains(qLower) ||
                    a.PatientPhone.ToLower().Contains(qLower) ||
                    a.Branch.ToLower().Contains(qLower) ||
                    a.DutyDate.ToString("yyyy-MM-dd").Contains(qLower) ||
                    a.DutyDate.ToString("dd/MM/yyyy").Contains(qLower)
                );
            }

            var appts = await baseQuery
                .OrderBy(a => a.DutyDate)
                .ThenBy(a => a.StartTime)
                .ToListAsync();

            var patientUserIds = appts
                .Select(a => a.PatientId)
                .Distinct()
                .ToList();

            var patientsLookup = await _db.Patients
                .Where(p => patientUserIds.Contains(p.UserId))
                .Select(p => new
                {
                    p.UserId,
                    p.Gender
                })
                .ToDictionaryAsync(p => p.UserId, p => p.Gender ?? "-");

            var mapped = appts.Select(a =>
            {
                var startLocal = a.DutyDate.Date.Add(a.StartTime);

                var gender = patientsLookup.TryGetValue(a.PatientId, out var g)
                    ? g
                    : "-";

                return new DoctorAppointmentsVM.AppointmentRow
                {
                    PatientName = a.PatientName,
                    PatientEmail = a.PatientEmail,
                    PatientPhone = a.PatientPhone,
                    Gender = gender,
                    Reason = string.IsNullOrWhiteSpace(a.ReasonNote) ? "Consultation" : a.ReasonNote,
                    BranchName = a.Branch,
                    StartDateTimeLocal = startLocal
                };
            })
            .ToList();

            var nowLocal = TimeZoneInfo.ConvertTime(DateTime.UtcNow, TZ);
            var todayDateOnly = DateOnly.FromDateTime(nowLocal);

            var todaysOnly = mapped
                .Where(r => DateOnly.FromDateTime(r.StartDateTimeLocal) == todayDateOnly)
                .OrderBy(r => r.StartDateTimeLocal)
                .ToList();

            var vm = new DoctorAppointmentsVM
            {
                SearchTerm = q ?? "",
                TodayDate = todayDateOnly,
                AllAppointments = mapped
                    .OrderBy(r => r.StartDateTimeLocal)
                    .ToList(),
                TodayAppointments = todaysOnly
            };

            return View("~/Views/Doctors/DoctorAppointments.cshtml", vm);
        }
    }
}

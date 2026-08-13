using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Threading.Tasks;
using MediCare.App.Data;
using MediCare.App.Models;
using MediCare.App.ViewModels.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatientModel = MediCare.App.Models.Patient;

namespace MediCare.App.Controllers.Patient
{
    [Authorize(Roles = "Patient")]
    [Route("Patient/Dashboard")]
    public class DashboardController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly MediCareContext _db;

        private static readonly TimeZoneInfo TZ = TimeZoneInfo.FindSystemTimeZoneById(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "SE Asia Standard Time" : "Asia/Bangkok");

        public DashboardController(SignInManager<ApplicationUser> signInManager, MediCareContext db)
        {
            _signInManager = signInManager;
            _db = db;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            var meId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == meId);
            var fullName = patient?.FullName;
            if (string.IsNullOrWhiteSpace(fullName))
                fullName = User.FindFirstValue(ClaimTypes.Name) ?? "Patient";

            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TZ);

            var appts = await _db.Appointments
                .AsNoTracking()
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Doctor).ThenInclude(d => d.Specialty)
                .Where(a => a.PatientId == meId)
                .ToListAsync();

            var upcoming = appts
                .Where(a => a.DutyDate.Date + a.StartTime >= nowLocal)
                .OrderBy(a => a.DutyDate).ThenBy(a => a.StartTime)
                .Select(a => new UpcomingAppointmentVM
                {
                    AppointmentId = a.Id,
                    DoctorName = a.Doctor?.User?.FullName ?? a.Doctor?.User?.UserName ?? "Doctor",
                    DoctorSpecialty = a.Doctor?.Specialty?.Name ?? a.Doctor?.Speciality ?? "General",
                    Branch = a.Branch,
                    DutyDate = a.DutyDate,
                    StartTime = a.StartTime,
                    EndTime = a.EndTime
                })
                .ToList();

            var prescriptionsCount = await _db.AppointmentPrescriptions
                .CountAsync(p => p.Appointment.PatientId == meId);

            var vm = new PatientDashboardVM
            {
                DisplayName = fullName,
                Initials = BuildInitials(fullName),
                PatientId = patient?.Id ?? 0,
                UpcomingAppointmentsCount = upcoming.Count,
                UnpaidBillsCount = appts.Count(a => a.PaymentStatus == PaymentStatus.Pending),
                UnpaidBillsTotal = appts.Where(a => a.PaymentStatus == PaymentStatus.Pending).Sum(a => a.TotalAmount),
                PrescriptionsCount = prescriptionsCount,
                NextAppointment = upcoming.FirstOrDefault(),
                UpcomingAppointments = upcoming.Take(3).ToList()
            };

            return View("~/Views/Patient/Dashboard.cshtml", vm);
        }

        [HttpGet("Profile")]
        public IActionResult PatientProfile() =>
            RedirectToAction("Index", "PatientProfile");

        private static string BuildInitials(string fullName)
        {
            var parts = (fullName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "PT";
            if (parts.Length == 1) return parts[0].Substring(0, 1).ToUpper();
            return (parts[0].Substring(0, 1) + parts[^1].Substring(0, 1)).ToUpper();
        }
    }
}

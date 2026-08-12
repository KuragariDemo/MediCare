using MediCare.App.Data;
using MediCare.App.Models;
using MediCare.App.ViewModels.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MediCare.App.Controllers.Patient
{
    [Authorize(Roles = "Patient")]
    [Route("Patient/Appointments")]
    public class PatientAppointmentsController : Controller
    {
        private readonly MediCareContext _db;

        // Windows server: "SE Asia Standard Time"; Linux: "Asia/Bangkok"
        private static readonly TimeZoneInfo TZ =
#if WINDOWS
            TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
#else
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok");
#endif

        public PatientAppointmentsController(MediCareContext db)
        {
            _db = db;
        }

        // ----------------------------
        // PAGE: GET /Patient/Appointments/My
        // ----------------------------
        [HttpGet("My")]
        public async Task<IActionResult> My()
        {
            var meId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var patientName = await _db.Patients
                .AsNoTracking()
                .Where(p => p.UserId == meId)
                .Select(p => p.FullName)
                .FirstOrDefaultAsync();
            ViewBag.DisplayName = string.IsNullOrWhiteSpace(patientName)
                ? (User.FindFirstValue(ClaimTypes.Name) ?? "Patient")
                : patientName;

            // get all appts + include doctor info
            var appts = await _db.Appointments
                .AsNoTracking()
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)      // for DoctorName
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.Specialty) // for Specialty.Name
                .Where(a => a.PatientId == meId)
                .OrderBy(a => a.DutyDate)
                .ThenBy(a => a.StartTime)
                .ToListAsync();

            var vm = new PatientMyAppointmentsPageVM();

            foreach (var a in appts)
            {

                var localStart = a.DutyDate.Date + a.StartTime;
                var localEnd = a.DutyDate.Date + a.EndTime;

                vm.Items.Add(new PatientAppointmentRowVM
                {
                    AppointmentId = a.Id,
                    DoctorId = a.DoctorId,

                    DoctorName =
                        a.Doctor?.User?.FullName
                        ?? a.Doctor?.User?.UserName
                        ?? "Doctor",

                    DoctorSpecialty =
                        a.Doctor?.Specialty?.Name
                        ?? a.Doctor?.Speciality
                        ?? "General",

                    BranchName = a.Branch,
                    BranchAddress = null, // we fill below

                    LocalDateTimeStart = localStart,
                    LocalDateTimeEnd = localEnd
                });
            }

            // Get branch addresses for display
            var branchMap = await _db.ClinicBranches
                .AsNoTracking()
                .ToDictionaryAsync(b => b.Name, b => b.Address);

            foreach (var row in vm.Items)
            {
                if (branchMap.TryGetValue(row.BranchName, out var addr))
                {
                    row.BranchAddress = addr ?? "";
                }
            }

            return View("/Views/Patient/MyAppointments.cshtml", vm);
        }

        [HttpGet("Duties")]
        public async Task<IActionResult> Duties([FromQuery] int doctorId)
        {
            var nowUtc = DateTime.UtcNow;

            // 1. find that doctor's UserId (string)
            var doctorUserId = await _db.Doctors
                .Where(d => d.Id == doctorId)
                .Select(d => d.UserId)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(doctorUserId))
            {
                return Json(Array.Empty<object>());
            }

            // 2. get upcoming duty assignments
            var duties = await _db.DutyAssignments
                .AsNoTracking()
                .Where(d => d.DoctorId == doctorUserId && d.EndUtc > nowUtc)
                .OrderBy(d => d.StartUtc)
                .Take(30)
                .ToListAsync();

            // 3. shape data for JS
            var result = new System.Collections.Generic.List<object>();

            foreach (var duty in duties)
            {
                var localStart = TimeZoneInfo.ConvertTimeFromUtc(duty.StartUtc, TZ);
                var localEnd = TimeZoneInfo.ConvertTimeFromUtc(duty.EndUtc, TZ);

                var branchName = await _db.ClinicBranches
                    .Where(b => b.Id == duty.BranchId)
                    .Select(b => b.Name)
                    .FirstOrDefaultAsync() ?? "Unknown";

                result.Add(new
                {
                    id = duty.Id,
                    dutyDate = localStart.ToString("yyyy-MM-dd"),
                    start = localStart.ToString("HH:mm"),
                    end = localEnd.ToString("HH:mm"),
                    branch = branchName
                });
            }

            return Json(result);
        }

        [HttpPost("Reschedule")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reschedule([FromForm] int appointmentId, [FromForm] int dutyId)
        {
            var meId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var appt = await _db.Appointments
                .Where(a => a.Id == appointmentId && a.PatientId == meId)
                .FirstOrDefaultAsync();

            if (appt == null)
            {
                return BadRequest(new { ok = false, message = "Appointment not found or not yours." });
            }

            var duty = await _db.DutyAssignments
                .FirstOrDefaultAsync(d => d.Id == dutyId);

            if (duty == null)
            {
                return BadRequest(new { ok = false, message = "Duty not available." });
            }

            // convert UTC duty window -> local time window
            var localStart = TimeZoneInfo.ConvertTimeFromUtc(duty.StartUtc, TZ);
            var localEnd = TimeZoneInfo.ConvertTimeFromUtc(duty.EndUtc, TZ);

            // get branch name
            var branchName = await _db.ClinicBranches
                .Where(b => b.Id == duty.BranchId)
                .Select(b => b.Name)
                .FirstOrDefaultAsync() ?? "Unknown";

            // update appointment slot
            appt.DutyDate = localStart.Date;
            appt.StartTime = localStart.TimeOfDay;
            appt.EndTime = localEnd.TimeOfDay;
            appt.Branch = branchName;

            await _db.SaveChangesAsync();

            return Json(new { ok = true });
        }

        [HttpPost("Cancel")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel([FromForm] int appointmentId)
        {
            var meId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var appt = await _db.Appointments
                .Where(a => a.Id == appointmentId && a.PatientId == meId)
                .FirstOrDefaultAsync();

            if (appt == null)
            {
                return BadRequest(new { ok = false, message = "Appointment not found or not yours." });
            }

            _db.Appointments.Remove(appt);
            await _db.SaveChangesAsync();

            return Json(new { ok = true });
        }

        // ==========================
        // PAGE: GET Patient/Appointments/Recent
        // ==========================
        [HttpGet("Recent")]
        public async Task<IActionResult> Recent()
        {
            var meId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (meId == null)
            {
                return Unauthorized();
            }

            // pull last 10 appointments newest first
            var appts = await _db.Appointments
                .Where(a => a.PatientId == meId)
                .OrderByDescending(a => a.DutyDate)
                .ThenByDescending(a => a.StartTime)
                .Take(10)
                .Select(a => new
                {
                    a.Id,
                    a.DutyDate,
                    a.StartTime,
                    a.EndTime,
                    Doctor = a.Doctor,
                    DoctorUser = a.Doctor.User,
                    Prescription = a.Prescription,
                    Feedback = a.Feedback
                })
                .ToListAsync();

            var vm = new RecentAppointmentsVM
            {
                Appointments = appts.Select(a => new RecentAppointmentRowVM
                {
                    AppointmentId = a.Id,
                    DutyDate = TimeZoneInfo.ConvertTimeFromUtc(
                        // assume DutyDate is stored in UTC midnight? 
                        // if DutyDate actually in local clinic time already, skip conversion
                        a.DutyDate.Kind == DateTimeKind.Utc ? a.DutyDate : DateTime.SpecifyKind(a.DutyDate, DateTimeKind.Utc),
                        TZ
                    ).Date,

                    StartTime = a.StartTime,
                    EndTime = a.EndTime,

                    DoctorName = a.DoctorUser.FullName ?? a.DoctorUser.UserName,
                    DoctorEmail = a.DoctorUser.Email ?? "",
                    DoctorSpecialty = a.Doctor.Speciality ?? (a.Doctor.Specialty != null ? a.Doctor.Specialty.Name : ""),

                    HasPrescription = (a.Prescription != null),
                    PrescriptionText = a.Prescription != null ? a.Prescription.PrescriptionText : null,

                    ExistingFeedbackText = a.Feedback != null ? a.Feedback.FeedbackText : null
                }).ToList()
            };

            return View("/Views/Patient/RecentAppointments.cshtml", vm);

        }

        // ==========================
        // POST: Patient/Appointments/SubmitFeedback
        // AJAX endpoint
        // ==========================
        [HttpPost("SubmitFeedback")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitFeedback([FromForm] int appointmentId, [FromForm] string feedbackText)
        {
            var meId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (meId == null)
                return Unauthorized();

            // SECURITY CHECK:
            var appt = await _db.Appointments
                .Where(a => a.Id == appointmentId && a.PatientId == meId)
                .FirstOrDefaultAsync();

            if (appt == null)
                return NotFound(new { ok = false, message = "Appointment not found" });

            // Insert or update feedback
            var existing = await _db.AppointmentFeedbacks
                .FirstOrDefaultAsync(f => f.AppointmentId == appointmentId);

            if (existing == null)
            {
                var newFb = new AppointmentFeedback
                {
                    AppointmentId = appointmentId,
                    PatientId = meId,
                    FeedbackText = feedbackText
                };
                _db.AppointmentFeedbacks.Add(newFb);
            }
            else
            {
                existing.FeedbackText = feedbackText;
            }

            await _db.SaveChangesAsync();

            return Json(new { ok = true });
        }


    }
}


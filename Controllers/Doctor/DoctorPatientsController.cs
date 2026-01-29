using MediCare.App.Data;
using MediCare.App.ViewModels.Doctor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MediCare.App.Controllers.Doctor
{
    [Authorize(Roles = "Doctor")]
    [Route("Doctor/Patients")]
    public class DoctorPatientsController : Controller
    {
        private readonly MediCareContext _db;

        // Windows server: "SE Asia Standard Time"; Linux: "Asia/Bangkok"
        private static readonly TimeZoneInfo TZ =
#if WINDOWS
            TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
#else
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok");
#endif

        public DoctorPatientsController(MediCareContext db)
        {
            _db = db;
        }

        // PAGE: GET /Doctor/Patients/Records
        [HttpGet("Records")]
        public async Task<IActionResult> Records()
        {
            // get current doctor user id
            var myUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Resolve to Doctor.Id (int)
            var doctorObj = await _db.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == myUserId);

            if (doctorObj == null)
            {
                return Unauthorized(); // doctor account not mapped
            }

            var doctorId = doctorObj.Id;

            // Fetch recent appointments for THIS doctor
            var appts = await _db.Appointments
                .AsNoTracking()
                .Include(a => a.Feedback)
                .Include(a => a.Prescription)
                .Where(a => a.DoctorId == doctorId)
                .OrderByDescending(a => a.DutyDate)
                .ThenByDescending(a => a.StartTime)
                .Take(50)
                .ToListAsync();

            var vm = new PatientRecordPageVM
            {
                DoctorDisplayName =
                    doctorObj.User.FullName ??
                    doctorObj.User.UserName ??
                    ("Dr. " + doctorObj.Id.ToString())
            };

            foreach (var a in appts)
            {
                // convert appointment date to doctor's local time if needed
                // NOTE: If DutyDate is already local (not UTC), you can just use .DutyDate directly.
                var localDate = a.DutyDate;
                if (a.DutyDate.Kind == DateTimeKind.Utc)
                {
                    localDate = TimeZoneInfo.ConvertTimeFromUtc(a.DutyDate, TZ);
                }

                vm.Records.Add(new PatientRecordRowVM
                {
                    AppointmentId = a.Id,
                    PatientName = a.PatientName,
                    PatientEmail = a.PatientEmail,
                    ReasonNote = a.ReasonNote ?? "-",

                    DutyDate = localDate,
                    StartTime = a.StartTime,

                    HasFeedback = (a.Feedback != null && !string.IsNullOrWhiteSpace(a.Feedback.FeedbackText)),
                    FeedbackText = a.Feedback != null ? a.Feedback.FeedbackText : null,

                    HasPrescription = (a.Prescription != null && !string.IsNullOrWhiteSpace(a.Prescription.PrescriptionText)),
                    PrescriptionText = a.Prescription != null ? a.Prescription.PrescriptionText : null
                });
            }

            return View("/Views/Doctors/PatientRecords.cshtml", vm);
        }

        // POST /Doctor/Patients/SavePrescription
        [HttpPost("SavePrescription")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePrescription([FromForm] int appointmentId, [FromForm] string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return BadRequest(new { ok = false, message = "Prescription cannot be empty." });

            // verify that this appointment belongs to THIS doctor
            var myUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var doctorId = await _db.Doctors
                .Where(d => d.UserId == myUserId)
                .Select(d => d.Id)
                .FirstOrDefaultAsync();

            if (doctorId == 0)
                return Unauthorized();

            var appt = await _db.Appointments
                .Include(a => a.Prescription)
                .FirstOrDefaultAsync(a => a.Id == appointmentId && a.DoctorId == doctorId);

            if (appt == null)
                return NotFound(new { ok = false, message = "Appointment not found." });

            if (appt.Prescription == null)
            {
                appt.Prescription = new MediCare.App.Models.AppointmentPrescription
                {
                    AppointmentId = appointmentId,
                    PrescriptionText = text,
                    SubmittedAt = DateTime.UtcNow
                };

                _db.AppointmentPrescriptions.Add(appt.Prescription);
            }
            else
            {
                appt.Prescription.PrescriptionText = text;
                appt.Prescription.SubmittedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            return Json(new { ok = true });
        }
    }
}

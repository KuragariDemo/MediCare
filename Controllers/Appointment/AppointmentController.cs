using System;
using System.Linq;
using System.Security.Claims;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using MediCare.App.Data;
using MediCare.App.Models;
using MediCare.App.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediCare.Models; // for ClinicBranch

namespace MediCare.App.Controllers
{
    [Authorize(Roles = "Patient,Admin,AdminPlus,Doctor")]
    [Route("[controller]")]
    public class AppointmentController : Controller
    {
        private readonly MediCareContext _db;

        public AppointmentController(MediCareContext db)
        {
            _db = db;
        }

        // =========================
        //   MAIN PAGE (Wizard)
        // =========================
        [HttpGet("")]
        public IActionResult Index()
        {
            return View("~/Views/Appointment/Index.cshtml");
        }

        // Optional: direct Book page (for standalone use)
        [HttpGet("Book")]
        public async Task<IActionResult> Book(int doctorId)
        {
            var doc = await _db.Doctors
                .AsNoTracking()
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == doctorId);

            if (doc == null) return NotFound();

            var doctorName =
                !string.IsNullOrWhiteSpace(doc.User.FullName)
                    ? doc.User.FullName
                    : (!string.IsNullOrWhiteSpace(doc.User.UserName)
                        ? doc.User.UserName
                        : $"Doctor #{doc.Id}");

            var vm = new AppointmentBookVm
            {
                DoctorId = doc.Id,
                DoctorName = doctorName
            };

            return View("~/Views/Appointment/Book.cshtml", vm);
        }

        // =========================
        //   DUTY SCHEDULE (AJAX)
        // =========================
        [HttpGet("Duties")]
        public async Task<IActionResult> Duties(int doctorId)
        {
            var doc = await _db.Doctors
                .AsNoTracking()
                .Select(d => new { d.Id, d.UserId })
                .FirstOrDefaultAsync(d => d.Id == doctorId);

            if (doc == null) return NotFound();

            var tzId = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "SE Asia Standard Time"
                : "Asia/Bangkok";
            var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);

            var duties = await (
                from a in _db.DutyAssignments.AsNoTracking()
                join b in _db.ClinicBranches.AsNoTracking()
                    on a.BranchId equals b.Id
                where a.DoctorId == doc.UserId && a.EndUtc > DateTime.UtcNow
                orderby a.StartUtc
                select new { a.StartUtc, a.EndUtc, BranchName = b.Name }
            ).ToListAsync();

            var data = duties.Select(x =>
            {
                var startLocal = TimeZoneInfo.ConvertTimeFromUtc(x.StartUtc, tz);
                var endLocal = TimeZoneInfo.ConvertTimeFromUtc(x.EndUtc, tz);
                return new
                {
                    date = startLocal.ToString("yyyy-MM-dd"),
                    start = startLocal.ToString("HH:mm"),
                    end = endLocal.ToString("HH:mm"),
                    branch = x.BranchName
                };
            });

            return Json(data);
        }

        // =========================
        //   CONSULTATION FEE
        // =========================
        [HttpGet("FeePreview")]
        public async Task<IActionResult> FeePreview(int doctorId)
        {
            var fee = await ResolveConsultationFeeAsync(doctorId);
            return Json(new { fee, total = fee });
        }

        // =========================
        //   CREATE APPOINTMENT
        // =========================
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AppointmentBookVm vm)
        {
            if (!ModelState.IsValid)
            {
                if (Request.Headers["Accept"].ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { ok = false, error = "Invalid form data." });

                return View("~/Views/Appointment/Book.cshtml", vm);
            }

            // Find doctor for mapping
            var doc = await _db.Doctors
                .AsNoTracking()
                .Select(d => new { d.Id, d.UserId })
                .FirstOrDefaultAsync(d => d.Id == vm.DoctorId);

            if (doc == null)
                return NotFound(new { ok = false, error = "Doctor not found." });

            // Timezone
            var tzId = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "SE Asia Standard Time"
                : "Asia/Bangkok";
            var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);

            // Convert to UTC
            var startLocal = vm.DutyDate.Date + vm.StartTime;
            var endLocal = vm.DutyDate.Date + vm.EndTime;
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, tz);
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, tz);

            // Check slot
            var slotExists = await (
                from a in _db.DutyAssignments
                join b in _db.ClinicBranches on a.BranchId equals b.Id
                where a.DoctorId == doc.UserId
                      && a.StartUtc == startUtc
                      && a.EndUtc == endUtc
                      && (string.IsNullOrEmpty(vm.Branch) || b.Name == vm.Branch)
                select a
            ).AnyAsync();

            if (!slotExists)
                return Conflict(new { ok = false, error = "Selected slot is no longer available." });

            // ====== Fee ======
            var fee = await ResolveConsultationFeeAsync(vm.DoctorId);
            var total = fee;

            // ====== Payment logic ======
            var method = vm.PaymentChoice == "card"
                ? PaymentMethod.Card
                : PaymentMethod.CashAtClinic;

            PaymentStatus status;
            string? paymentRef = null;

            if (method == PaymentMethod.Card)
            {
                var cardNum = (vm.CardNumber ?? "").Replace(" ", "");
                if (cardNum.Length < 12)
                    return BadRequest(new { ok = false, error = "Invalid card number." });

                status = PaymentStatus.Paid;
                paymentRef = $"CARD-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            }
            else
            {
                // Cash at clinic → unpaid until admin approves
                status = PaymentStatus.Pending;
            }

            // ====== Create Appointment ======
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "ANON";

            var entity = new MediCare.App.Models.Appointment
            {
                PatientId = userId,
                DoctorId = vm.DoctorId,
                DutyDate = vm.DutyDate.Date,
                StartTime = vm.StartTime,
                EndTime = vm.EndTime,
                Branch = vm.Branch,

                PatientName = vm.PatientName,
                PatientEmail = vm.PatientEmail,
                PatientPhone = vm.PatientPhone,
                ReasonNote = vm.ReasonNote,

                ConsultationFee = fee,
                TotalAmount = total,
                PaymentMethod = method,
                PaymentStatus = status,
                PaymentRef = paymentRef
            };

            _db.Appointments.Add(entity);
            await _db.SaveChangesAsync();

            // ====== Response ======
            if (Request.Headers["Accept"].ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase))
                return Json(new { ok = true, id = entity.Id });

            TempData["Success"] = status == PaymentStatus.Paid
                ? "Booking confirmed and payment received."
                : "Booking confirmed. Please pay at the clinic.";

            return RedirectToAction(nameof(Confirmation), new { id = entity.Id });
        }

        // =========================
        //   CONFIRMATION PAGE
        // =========================
        [HttpGet("Confirmation/{id:int}")]
        public async Task<IActionResult> Confirmation(int id)
        {
            var a = await _db.Appointments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (a == null) return NotFound();

            // You can reuse Index.cshtml’s layout to show confirmation summary
            return View("~/Views/Appointment/Confirmation.cshtml", a);
        }

        // =========================
        //   FEE HELPER
        // =========================
        private async Task<decimal> ResolveConsultationFeeAsync(int doctorId)
        {
            var doc = await _db.Doctors
                .AsNoTracking()
                .Select(d => new { d.Id, d.SpecialtyId })
                .FirstOrDefaultAsync(d => d.Id == doctorId);

            if (doc?.SpecialtyId == null)
                return 0m;

            var fee = await _db.DoctorSpecialties
                .AsNoTracking()
                .Where(s => s.Id == doc.SpecialtyId && s.IsActive)
                .Select(s => (decimal?)s.Fee)
                .FirstOrDefaultAsync();

            return fee ?? 0m;
        }

        // POST /Admin/Appointments/MarkPaid/123
        [Authorize(Roles = "Admin,AdminPlus")]
        [HttpPost("MarkPaid/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPaid(int id)
        {
            var appt = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == id);
            if (appt == null)
                return NotFound(new { ok = false, error = "Appointment not found." });

            if (appt.PaymentMethod != PaymentMethod.CashAtClinic)
                return BadRequest(new { ok = false, error = "Only cash-at-clinic bookings can be approved." });

            if (appt.PaymentStatus == PaymentStatus.Paid)
                return BadRequest(new { ok = false, error = "This appointment is already marked as paid." });

            // ✅ Update status
            appt.PaymentStatus = PaymentStatus.Paid;
            appt.PaymentRef = (appt.PaymentRef ?? "CASH") + $"-APPROVED-{DateTime.UtcNow:yyyyMMddHHmmss}";
            await _db.SaveChangesAsync();

            // Return JSON (for AJAX)
            if (Request.Headers["Accept"].ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase))
                return Json(new { ok = true });

            TempData["Success"] = "Payment recorded successfully.";
            return RedirectToAction("Index");
        }
    }
}

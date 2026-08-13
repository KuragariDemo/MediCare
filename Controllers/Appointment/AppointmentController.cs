using System;
using System.Collections.Generic;
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

        private static readonly TimeZoneInfo ClinicTz = TimeZoneInfo.FindSystemTimeZoneById(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "SE Asia Standard Time" : "Asia/Bangkok");

        private bool WantsJson() =>
            Request.Headers["Accept"].ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

        public record SlotInfo(string Date, string Start, string End, string Branch, string Label);

        private async Task<List<SlotInfo>> GetAvailableSlotsAsync(string doctorUserId)
        {
            var duties = await (
                from a in _db.DutyAssignments.AsNoTracking()
                join b in _db.ClinicBranches.AsNoTracking()
                    on a.BranchId equals b.Id
                where a.DoctorId == doctorUserId && a.EndUtc > DateTime.UtcNow
                orderby a.StartUtc
                select new { a.StartUtc, a.EndUtc, BranchName = b.Name }
            ).ToListAsync();

            return duties.Select(x =>
            {
                var startLocal = TimeZoneInfo.ConvertTimeFromUtc(x.StartUtc, ClinicTz);
                var endLocal = TimeZoneInfo.ConvertTimeFromUtc(x.EndUtc, ClinicTz);
                return new SlotInfo(
                    Date: startLocal.ToString("yyyy-MM-dd"),
                    Start: startLocal.ToString("HH:mm"),
                    End: endLocal.ToString("HH:mm"),
                    Branch: x.BranchName,
                    Label: $"{startLocal:ddd, dd MMM yyyy} · {startLocal:HH:mm}-{endLocal:HH:mm} · {x.BranchName}");
            }).ToList();
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
                DoctorName = doctorName,
                ConsultationFee = await ResolveConsultationFeeAsync(doctorId),
            };
            vm.TotalAmount = vm.ConsultationFee;

            ViewBag.AvailableSlots = await GetAvailableSlotsAsync(doc.UserId);

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

            var slots = await GetAvailableSlotsAsync(doc.UserId);
            var data = slots.Select(s => new
            {
                date = s.Date,
                start = s.Start,
                end = s.End,
                branch = s.Branch
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
            // Find doctor for mapping
            var doc = await _db.Doctors
                .AsNoTracking()
                .Select(d => new { d.Id, d.UserId })
                .FirstOrDefaultAsync(d => d.Id == vm.DoctorId);

            if (!ModelState.IsValid)
            {
                if (WantsJson())
                    return BadRequest(new { ok = false, error = "Invalid form data." });

                if (doc != null)
                    ViewBag.AvailableSlots = await GetAvailableSlotsAsync(doc.UserId);

                return View("~/Views/Appointment/Book.cshtml", vm);
            }

            if (doc == null)
                return NotFound(new { ok = false, error = "Doctor not found." });

            // Convert to UTC
            var startLocal = vm.DutyDate.Date + vm.StartTime;
            var endLocal = vm.DutyDate.Date + vm.EndTime;
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, ClinicTz);
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, ClinicTz);

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

            // Prevent double-booking: reject if another appointment already holds this exact slot.
            var alreadyBooked = await _db.Appointments.AnyAsync(a =>
                a.DoctorId == vm.DoctorId &&
                a.DutyDate == vm.DutyDate.Date &&
                a.StartTime == vm.StartTime &&
                a.EndTime == vm.EndTime);

            if (alreadyBooked)
                return Conflict(new { ok = false, error = "This time slot was just booked by someone else. Please choose another slot." });

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

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Race condition: two requests passed the checks above at the same time.
                var stillThere = await _db.Appointments.AnyAsync(a =>
                    a.DoctorId == vm.DoctorId &&
                    a.DutyDate == vm.DutyDate.Date &&
                    a.StartTime == vm.StartTime &&
                    a.EndTime == vm.EndTime &&
                    a.Id != entity.Id);

                if (stillThere)
                    return Conflict(new { ok = false, error = "This time slot was just booked by someone else. Please choose another slot." });

                throw;
            }

            // ====== Response ======
            if (WantsJson())
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
                .Include(x => x.Doctor)
                    .ThenInclude(d => d.User)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (a == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isOwner = !string.IsNullOrEmpty(userId) && a.PatientId == userId;
            var isStaff = User.IsInRole("Admin") || User.IsInRole("AdminPlus") || User.IsInRole("Doctor");
            if (!isOwner && !isStaff)
                return Forbid();

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
            if (WantsJson())
                return Json(new { ok = true });

            TempData["Success"] = "Payment recorded successfully.";
            return RedirectToAction("Index");
        }
    }
}

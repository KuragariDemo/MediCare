using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using MediCare.App.Data;
using MediCare.App.Models;
using MediCare.App.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediCare.App.Controllers.Admin
{
    [Authorize(Roles = "AdminPlus,Admin")]
    [Route("Admin/Dashboard")]
    public class DashboardController : Controller
    {
        private readonly MediCareContext _db;

        private static readonly TimeZoneInfo TZ = TimeZoneInfo.FindSystemTimeZoneById(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "SE Asia Standard Time" : "Asia/Bangkok");

        public DashboardController(MediCareContext db)
        {
            _db = db;
        }

        // GET: /Admin/Dashboard  OR /Admin/Dashboard/Index
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TZ);
            var today = nowLocal.Date;
            var monthStartUtc = new DateTime(nowLocal.Year, nowLocal.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
            monthStartUtc = TimeZoneInfo.ConvertTimeToUtc(monthStartUtc, TZ);

            var vm = new AdminDashboardVM
            {
                TotalDoctors = await _db.Doctors.CountAsync(),
                TotalPatients = await _db.Patients.CountAsync(),
            };

            var appts = await _db.Appointments
                .AsNoTracking()
                .Include(a => a.Doctor).ThenInclude(d => d.Specialty)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .ToListAsync();

            vm.TodayAppointments = appts.Count(a => a.DutyDate.Date == today);
            vm.TodayPendingPayments = appts.Count(a => a.DutyDate.Date == today && a.PaymentStatus == PaymentStatus.Pending);
            vm.RevenueThisMonth = appts
                .Where(a => a.PaymentStatus == PaymentStatus.Paid && a.CreatedAt >= monthStartUtc)
                .Sum(a => a.TotalAmount);

            vm.Last7Days = Enumerable.Range(0, 7)
                .Select(i => today.AddDays(-6 + i))
                .Select(d => new DayCount { Date = d, Count = appts.Count(a => a.DutyDate.Date == d) })
                .ToList();

            var total = appts.Count;
            vm.DepartmentShares = appts
                .GroupBy(a => a.Doctor?.Specialty?.Name ?? a.Doctor?.Speciality ?? "Other")
                .Select(g => new DeptShare
                {
                    Name = g.Key,
                    Count = g.Count(),
                    Percent = total == 0 ? 0 : (int)Math.Round(g.Count() * 100.0 / total)
                })
                .OrderByDescending(d => d.Count)
                .Take(4)
                .ToList();

            vm.RecentBookings = appts
                .OrderByDescending(a => a.CreatedAt)
                .Take(5)
                .Select(a => new RecentBooking
                {
                    PatientName = a.PatientName,
                    DoctorName = a.Doctor?.User?.FullName ?? a.Doctor?.User?.UserName ?? "Doctor",
                    CreatedAt = a.CreatedAt,
                    Amount = a.TotalAmount,
                    Paid = a.PaymentStatus == PaymentStatus.Paid
                })
                .ToList();

            return View("~/Views/Admin/Dashboard.cshtml", vm);
        }

        // GET: /Admin/Dashboard/AppointmentTable
        [HttpGet("AppointmentTable")]
        public IActionResult AppointmentTable()
        {
            return View("~/Views/Admin/AppointmentTable.cshtml");
        }

        // GET: /Admin/Dashboard/List
        // -> used by AppointmentTable JS to render rows
        [HttpGet("List")]
        public async Task<IActionResult> List()
        {
            var items = await _db.Appointments
                .AsNoTracking()
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new
                {
                    id = a.Id,
                    patientName = a.PatientName,
                    patientEmail = a.PatientEmail,
                    patientPhone = a.PatientPhone,
                    created = a.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                    method = a.PaymentMethod.ToString(),    // "CashAtClinic" / "Card"
                    status = a.PaymentStatus.ToString()     // "Pending" / "Paid" / ...
                })
                .ToListAsync();

            return Json(items);
        }

        // GET: /Admin/Dashboard/ReportCsv/{id}
        [HttpGet("ReportCsv/{id}")]
        public async Task<IActionResult> ReportCsv(int id)
        {
            var row = await _db.Appointments
                .AsNoTracking()
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.Specialty)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (row == null)
            {
                return NotFound("Appointment not found");
            }

            // Patient (snapshot stored on Appointment, so no Patient nav needed)
            var patientName = row.PatientName ?? "Unknown";
            var patientEmail = row.PatientEmail ?? "";
            var patientPhone = row.PatientPhone ?? "";

            // Booking timestamp
            var bookedDate = row.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

            // Payment info
            var paymentMethod = row.PaymentMethod.ToString();   // enum -> string
            var paymentStatus = row.PaymentStatus.ToString();   // enum -> string
            var paymentAmount = row.TotalAmount.ToString("0.00");

            // Doctor info
            var doctorName = row.Doctor?.User?.FullName ?? "Unknown Doctor";

            var doctorSpecialty =
                row.Doctor?.Specialty?.Name   // from FK relation if available
                ?? row.Doctor?.Speciality     // fallback string column snapshot
                ?? "(none)";

            // build CSV text
            var sb = new StringBuilder();
            sb.AppendLine("Patient Name,Email,Phone,Booked Date,Payment Method,Payment Status,Amount,Doctor Name,Doctor Specialty");
            sb.AppendLine(
                $"{EscapeCsv(patientName)}," +
                $"{EscapeCsv(patientEmail)}," +
                $"{EscapeCsv(patientPhone)}," +
                $"{EscapeCsv(bookedDate)}," +
                $"{EscapeCsv(paymentMethod)}," +
                $"{EscapeCsv(paymentStatus)}," +
                $"{EscapeCsv(paymentAmount)}," +
                $"{EscapeCsv(doctorName)}," +
                $"{EscapeCsv(doctorSpecialty)}"
            );

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"AppointmentReport_{id}.csv";
            return File(bytes, "text/csv", fileName);
        }

        // CSV escaping utility
        private static string EscapeCsv(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return "";

            if (input.Contains(",") || input.Contains("\"") || input.Contains("\n"))
            {
                return "\"" + input.Replace("\"", "\"\"") + "\"";
            }

            return input;
        }
    }
}

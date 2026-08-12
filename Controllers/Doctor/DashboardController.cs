using System;
using System.Linq;
using System.Runtime.InteropServices;
using MediCare.App.Data;
using MediCare.App.Models;
using MediCare.App.ViewModels.Doctor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace MediCare.App.Controllers.Doctor
{
    [Authorize(Roles = "Doctor")]
    [Route("Doctors/Dashboard")]
    public class DashboardController : Controller
    {
        private readonly MediCareContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        private static readonly TimeZoneInfo TZ = TimeZoneInfo.FindSystemTimeZoneById(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "SE Asia Standard Time" : "Asia/Bangkok");

        public DashboardController(
            MediCareContext db,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _db = db;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpGet("", Name = "DoctorDashboardHome")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var doctor = await _db.Doctors
                                  .Include(d => d.User)
                                  .Include(d => d.Specialty)
                                  .FirstOrDefaultAsync(d => d.UserId == user.Id);

            ViewBag.DoctorName = doctor?.User.FullName ?? user.FullName ?? user.UserName ?? "Doctor";
            ViewBag.Speciality = doctor?.Specialty?.Name ?? doctor?.Speciality ?? "N/A"; // display

            var vm = new DoctorDashboardVM();

            if (doctor != null)
            {
                var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TZ);
                var today = nowLocal.Date;

                var appts = await _db.Appointments
                    .AsNoTracking()
                    .Where(a => a.DoctorId == doctor.Id)
                    .ToListAsync();

                var todays = appts.Where(a => a.DutyDate.Date == today)
                    .OrderBy(a => a.StartTime)
                    .ToList();

                vm.TodaySchedule = todays.Select(a =>
                {
                    var endsAt = today + a.EndTime;
                    var startsAt = today + a.StartTime;
                    return new TodayAppointmentVM
                    {
                        AppointmentId = a.Id,
                        PatientName = a.PatientName,
                        ReasonNote = a.ReasonNote,
                        StartTime = a.StartTime,
                        EndTime = a.EndTime,
                        IsCompleted = endsAt < nowLocal,
                        IsInProgress = startsAt <= nowLocal && nowLocal <= endsAt
                    };
                }).ToList();

                vm.TodayCount = todays.Count;
                vm.TodayCompletedCount = vm.TodaySchedule.Count(a => a.IsCompleted);
                vm.TodayUpcomingCount = vm.TodayCount - vm.TodayCompletedCount;
                vm.TotalPatientsCount = appts.Select(a => a.PatientId).Distinct().Count();

                vm.RecentPatients = appts
                    .OrderByDescending(a => a.DutyDate).ThenByDescending(a => a.StartTime)
                    .GroupBy(a => a.PatientId)
                    .Select(g => g.First())
                    .Take(5)
                    .Select(a => new RecentPatientVM
                    {
                        PatientName = a.PatientName,
                        LastVisit = a.DutyDate,
                        ReasonNote = a.ReasonNote,
                        Paid = a.PaymentStatus == PaymentStatus.Paid
                    })
                    .ToList();
            }

            return View("~/Views/Doctors/Dashboard.cshtml", vm);
        }

        // Doctors/Dashboard/Profile
        [HttpGet("Profile", Name = "DoctorDashboardProfile")]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var doctor = await _db.Doctors
                                  .Include(d => d.User)
                                  .Include(d => d.Specialty)
                                  .FirstOrDefaultAsync(d => d.UserId == user.Id);

            // for read view
            ViewBag.FullName = doctor?.User.FullName ?? user.FullName ?? user.UserName ?? "Doctor";
            ViewBag.Email = doctor?.User.Email ?? user.Email;
            ViewBag.SpecialtyName = doctor?.Specialty?.Name ?? doctor?.Speciality ?? "N/A";
            ViewBag.Bio = doctor?.Bio ?? "No biography provided yet.";
            ViewBag.Phone = doctor?.User.PhoneNumber ?? "Not provided";

            // for edit dropdown (from table)
            var list = await _db.DoctorSpecialties
                                .Where(s => s.IsActive)
                                .OrderBy(s => s.Name)
                                .Select(s => new SelectListItem
                                {
                                    Value = s.Id.ToString(),
                                    Text = s.Name,
                                    Selected = (doctor != null && doctor.SpecialtyId == s.Id)
                                })
                                .ToListAsync();
            ViewBag.Specialties = list;

            return View("~/Views/Doctors/DoctorProfile.cshtml");
        }

        [HttpPost("Logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpPost("ChangePassword")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
            {
                TempData["Error"] = "Please fill in all password fields.";
                return RedirectToRoute("DoctorDashboardProfile");
            }
            if (newPassword != confirmPassword)
            {
                TempData["Error"] = "New password and confirmation do not match.";
                return RedirectToRoute("DoctorDashboardProfile");
            }

            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join("; ", result.Errors.Select(e => e.Description));
                return RedirectToRoute("DoctorDashboardProfile");
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["Success"] = "Password changed successfully.";
            return RedirectToRoute("DoctorDashboardProfile");
        }

        [HttpPost("Profile/Update")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(
            string fullName,
            string email,
            string? phone,
            int? specialtyId,   // <— from the dropdown
            string? bio)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var doctor = await _db.Doctors
                                  .Include(d => d.User)
                                  .FirstOrDefaultAsync(d => d.UserId == user.Id);
            if (doctor == null)
            {
                TempData["Error"] = "Doctor profile not found.";
                return RedirectToRoute("DoctorDashboardProfile");
            }

            // basic validation
            if (string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(email) || !email.Contains("@") ||
                !specialtyId.HasValue)
            {
                TempData["Error"] = "Please fill required fields correctly.";
                return RedirectToRoute("DoctorDashboardProfile");
            }

            // Verify selected specialty exists
            var specName = await _db.DoctorSpecialties
                                    .Where(s => s.Id == specialtyId.Value && s.IsActive)
                                    .Select(s => s.Name)
                                    .FirstOrDefaultAsync();
            if (specName == null)
            {
                TempData["Error"] = "Invalid specialty selected.";
                return RedirectToRoute("DoctorDashboardProfile");
            }

            // unique email if changed
            if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                var existing = await _userManager.FindByEmailAsync(email);
                if (existing != null && existing.Id != user.Id)
                {
                    TempData["Error"] = "That email is already in use.";
                    return RedirectToRoute("DoctorDashboardProfile");
                }

                var setEmail = await _userManager.SetEmailAsync(user, email);
                if (!setEmail.Succeeded)
                {
                    TempData["Error"] = string.Join("; ", setEmail.Errors.Select(e => e.Description));
                    return RedirectToRoute("DoctorDashboardProfile");
                }

                var setUserName = await _userManager.SetUserNameAsync(user, email);
                if (!setUserName.Succeeded)
                {
                    TempData["Error"] = string.Join("; ", setUserName.Errors.Select(e => e.Description));
                    return RedirectToRoute("DoctorDashboardProfile");
                }
            }

            // full name + phone
            user.FullName = fullName;
            var setPhone = await _userManager.SetPhoneNumberAsync(user, phone ?? string.Empty);
            if (!setPhone.Succeeded)
            {
                TempData["Error"] = string.Join("; ", setPhone.Errors.Select(e => e.Description));
                return RedirectToRoute("DoctorDashboardProfile");
            }

            // doctor fields (keep legacy string in sync for now)
            doctor.SpecialtyId = specialtyId.Value;
            doctor.Speciality = specName;                         // legacy string kept in sync
            doctor.Bio = string.IsNullOrWhiteSpace(bio) ? null : bio.Trim();
            doctor.UpdatedByUserId = user.Id;
            doctor.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["Success"] = "Profile updated successfully.";
            return RedirectToRoute("DoctorDashboardProfile");
        }

        [HttpGet("LeaveRequests", Name = "DoctorDashboardLeaveRequests")]
        public IActionResult LeaveRequests()
        {
            return View("~/Views/Doctors/LeaveRequests.cshtml");
        }
    }
}

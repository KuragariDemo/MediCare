using MediCare.App.Data;
using MediCare.App.Models;
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
            return View("~/Views/Doctors/Dashboard.cshtml");
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

using MediCare.App.Data;
using MediCare.App.Models;
using MediCare.App.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MediCare.App.Controllers.Admin
{
    [Authorize(Roles = "AdminPlus,Admin")] // high-level gate
    public class AccountsController : Controller
    {
        private readonly MediCareContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AccountsController(
            MediCareContext db,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _db = db;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // ---------- Helpers ----------

        private async Task EnsureRoleAsync(string roleName)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                var created = await _roleManager.CreateAsync(new IdentityRole(roleName));
                if (!created.Succeeded && !await _roleManager.RoleExistsAsync(roleName))
                {
                    throw new InvalidOperationException($"Failed to ensure role '{roleName}' exists.");
                }
            }
        }

        private Task<List<AdminProfile>> LoadAdminsAsync()
        {
            return _db.AdminProfiles
                      .Include(a => a.User)
                      .OrderByDescending(a => a.CreatedAtUtc)
                      .ToListAsync();
        }

        // ---------- Dashboard (Overview) ----------
        public async Task<IActionResult> Index()
        {
            var admins = await _db.AdminProfiles
                                  .Include(a => a.User)
                                  .ToListAsync();

            var doctors = await _db.Doctors
                                   .Include(d => d.User)
                                   .Include(d => d.CreatedByUser)
                                   .Include(d => d.Specialty)
                                   .ToListAsync();

            var patients = await _db.Patients
                                    .Include(p => p.User)
                                    .ToListAsync();

            ViewBag.Doctors = doctors;
            ViewBag.Patients = patients;
            return View(admins);
        }

        // ---------- Admin Accounts Page (list + modal create) ----------
        [Authorize(Roles = "AdminPlus")]
        [HttpGet]
        public async Task<IActionResult> CreateAdmin()
        {
            var admins = await LoadAdminsAsync();
            return View("~/Views/Admin/AdminAccCreation.cshtml", admins);
        }

        [Authorize(Roles = "AdminPlus")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAdmin(string fullName, string email, string password, AdminLevel level)
        {
            async Task<IActionResult> ReRenderWithErrors()
            {
                var admins = await LoadAdminsAsync();
                return View("~/Views/Admin/AdminAccCreation.cshtml", admins);
            }

            if (string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError("", "All fields are required.");
                return await ReRenderWithErrors();
            }

            fullName = fullName.Trim();
            email = email.Trim();

            await EnsureRoleAsync("Admin");
            await EnsureRoleAsync("AdminPlus");

            var existing = await _userManager.FindByEmailAsync(email);
            if (existing != null)
            {
                ModelState.AddModelError("", "An account with this email already exists.");
                return await ReRenderWithErrors();
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                foreach (var e in createResult.Errors)
                    ModelState.AddModelError("", e.Description);
                return await ReRenderWithErrors();
            }

            var roleName = level == AdminLevel.AdminPlus ? "AdminPlus" : "Admin";
            var addRoleResult = await _userManager.AddToRoleAsync(user, roleName);
            if (!addRoleResult.Succeeded)
            {
                foreach (var e in addRoleResult.Errors)
                    ModelState.AddModelError("", e.Description);

                await _userManager.DeleteAsync(user); // rollback Identity user
                return await ReRenderWithErrors();
            }

            _db.AdminProfiles.Add(new AdminProfile
            {
                UserId = user.Id,
                Level = level,
                IsActive = true,
                CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                CreatedAtUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            TempData["Success"] = "Admin account created successfully.";
            return RedirectToAction(nameof(CreateAdmin));
        }

        // ---------- Create Doctor (GET/POST) ----------
        [Authorize(Roles = "AdminPlus")]
        [HttpGet]
        public async Task<IActionResult> CreateDoctor()
        {
            // 1️⃣ Load doctor list (for display)
            var doctors = await _db.Doctors
                .Include(d => d.User)
                .Include(d => d.Specialty)
                .OrderByDescending(d => d.CreatedAtUtc)
                .ToListAsync();

            // 2️⃣ Load specialties for modal dropdown
            ViewBag.Specialties = await _db.DoctorSpecialties
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = s.Name
                })
                .ToListAsync();

            // 3️⃣ Pass the doctor list to the view
            return View("~/Views/Doctors/CreateDoctorAcc.cshtml", doctors);
        }

        [Authorize(Roles = "AdminPlus")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDoctor(DoctorCreateVm vm)
        {
            // repopulate dropdown on failure
            vm.Specialties = await _db.DoctorSpecialties
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = s.Name
                })
                .ToListAsync();

            if (!ModelState.IsValid)
                return View("~/Views/Doctors/CreateDoctorAcc.cshtml", vm);

            var specialtyExists = await _db.DoctorSpecialties
                .AnyAsync(s => s.Id == vm.SpecialtyId && s.IsActive);
            if (!specialtyExists)
            {
                ModelState.AddModelError(nameof(vm.SpecialtyId), "Selected specialty is invalid.");
                return View("~/Views/Doctors/CreateDoctorAcc.cshtml", vm);
            }

            await EnsureRoleAsync("Doctor");

            var user = new ApplicationUser
            {
                UserName = vm.Email.Trim(),
                Email = vm.Email.Trim(),
                FullName = vm.FullName.Trim(),
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(user, vm.Password);
            if (!createResult.Succeeded)
            {
                foreach (var e in createResult.Errors)
                    ModelState.AddModelError("", $"{e.Code}: {e.Description}");
                return View("~/Views/Doctors/CreateDoctorAcc.cshtml", vm);
            }

            var roleResult = await _userManager.AddToRoleAsync(user, "Doctor");
            if (!roleResult.Succeeded)
            {
                foreach (var e in roleResult.Errors)
                    ModelState.AddModelError("", $"{e.Code}: {e.Description}");

                await _userManager.DeleteAsync(user); // rollback
                return View("~/Views/Doctors/CreateDoctorAcc.cshtml", vm);
            }

            try
            {
                _db.Doctors.Add(new MediCare.App.Models.Doctor
                {
                    UserId = user.Id,
                    SpecialtyId = vm.SpecialtyId,
                    Bio = string.IsNullOrWhiteSpace(vm.Bio) ? null : vm.Bio.Trim(),
                    CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                    CreatedAtUtc = DateTime.UtcNow,
                    Speciality = await _db.DoctorSpecialties
                        .Where(s => s.Id == vm.SpecialtyId)
                        .Select(s => s.Name)
                        .FirstAsync()
                });

                await _db.SaveChangesAsync();
            }
            catch
            {
                await _userManager.RemoveFromRoleAsync(user, "Doctor");
                await _userManager.DeleteAsync(user);

                ModelState.AddModelError("", "Failed to save doctor profile. Nothing was created.");
                return View("~/Views/Doctors/CreateDoctorAcc.cshtml", vm);
            }

            TempData["Success"] = "Doctor account created successfully.";
            return RedirectToAction(nameof(Index), "Accounts");
        }

        // ---------- Change Admin Level ----------
        [Authorize(Roles = "AdminPlus")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetAdminLevel(int id, AdminLevel level)
        {
            var profile = await _db.AdminProfiles
                                   .Include(a => a.User)
                                   .FirstOrDefaultAsync(a => a.Id == id);
            if (profile == null)
                return NotFound();

            await EnsureRoleAsync("Admin");
            await EnsureRoleAsync("AdminPlus");

            profile.Level = level;
            await _db.SaveChangesAsync();

            var roles = await _userManager.GetRolesAsync(profile.User);
            if (roles.Any())
                await _userManager.RemoveFromRolesAsync(profile.User, roles);

            var newRole = level == AdminLevel.AdminPlus ? "AdminPlus" : "Admin";
            await _userManager.AddToRoleAsync(profile.User, newRole);

            TempData["Success"] = "Admin level updated.";
            return RedirectToAction(nameof(CreateAdmin));
        }

        // ---------- Delete Admin Account ----------
        // Only AdminPlus can delete admins (including downgrade-yourself edge case: allowed unless you block it)
        [Authorize(Roles = "AdminPlus")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAdmin(int id)
        {
            var profile = await _db.AdminProfiles
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (profile == null)
            {
                TempData["Error"] = "Admin account not found.";
                return RedirectToAction(nameof(CreateAdmin));
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (profile.UserId == currentUserId)
            {
                TempData["Error"] = "You cannot delete your own administrator account.";
                return RedirectToAction(nameof(CreateAdmin));
            }

            // 1️⃣ Remove the domain profile first (in case cascade isn’t configured)
            _db.AdminProfiles.Remove(profile);
            await _db.SaveChangesAsync();

            // 2️⃣ Then remove the identity user (if still present)
            if (profile.User != null)
            {
                var roles = await _userManager.GetRolesAsync(profile.User);
                if (roles.Any())
                    await _userManager.RemoveFromRolesAsync(profile.User, roles);

                await _userManager.DeleteAsync(profile.User);
            }

            TempData["Success"] = "Admin account deleted successfully.";
            return RedirectToAction(nameof(CreateAdmin));
        }

        [Authorize(Roles = "AdminPlus")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDoctor(int id)
        {
            var doctor = await _db.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (doctor == null)
            {
                TempData["Error"] = "Doctor not found.";
                return RedirectToAction(nameof(CreateDoctor));
            }

            // Delete the identity user (account)
            if (doctor.User != null)
                await _userManager.DeleteAsync(doctor.User);

            _db.Doctors.Remove(doctor);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Doctor account deleted successfully.";
            return RedirectToAction(nameof(CreateDoctor));
        }


    }
}

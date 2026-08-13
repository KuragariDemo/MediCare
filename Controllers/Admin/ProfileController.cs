using MediCare.App.Data;
using MediCare.App.Models;
using MediCare.App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediCare.App.Controllers.Admin
{
    [Authorize(Roles = "AdminPlus,Admin")]
    [Route("Admin/Profile")]
    public class ProfileController : Controller
    {
        private readonly MediCareContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IWebHostEnvironment _env;

        public ProfileController(
            MediCareContext db,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IWebHostEnvironment env)
        {
            _db = db;
            _userManager = userManager;
            _signInManager = signInManager;
            _env = env;
        }

        //Admin/Profile  (or /Admin/Profile/Index)
        [HttpGet("")]
        [HttpGet("Index", Name = "AdminProfileIndex")]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // admin profile
            var admin = await _db.AdminProfiles
                                 .Include(a => a.User)
                                 .FirstOrDefaultAsync(a => a.UserId == user.Id);

            // role mapping
            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "Admin";
            var roleLabel = role switch
            {
                "AdminPlus" => "Administrator",
                "Admin" => "Co.Administrator",
                _ => "Admin"
            };

            ViewBag.FullName = user.FullName ?? user.UserName ?? "Admin User";
            ViewBag.Email = user.Email ?? "";
            ViewBag.RoleRaw = role;
            ViewBag.Role = roleLabel;
            ViewBag.AvatarUrl = AvatarStorage.GetAvatarUrl(_env, user.Id);

            return View("~/Views/Admin/AdminProfile.cshtml");
        }

        //Admin/Profile/UploadAvatar
        [HttpPost("UploadAvatar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadAvatar(IFormFile avatarFile)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var (ok, error) = AvatarStorage.Validate(avatarFile);
            if (!ok)
            {
                TempData["Error"] = error;
                return RedirectToRoute("AdminProfileIndex");
            }

            await AvatarStorage.SaveAsync(_env, user.Id, avatarFile);
            TempData["Success"] = "Profile picture updated.";
            return RedirectToRoute("AdminProfileIndex");
        }

        //Admin/Profile/Update (save edits)
        [HttpPost("Update")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(string fullName, string email)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // Basic validation
            if (string.IsNullOrWhiteSpace(fullName))
            {
                TempData["Error"] = "Full name is required.";
                return RedirectToRoute("AdminProfileIndex");
            }
            if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
            {
                TempData["Error"] = "A valid email is required.";
                return RedirectToRoute("AdminProfileIndex");
            }

            // Ensure unique email if changed
            if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                var existing = await _userManager.FindByEmailAsync(email);
                if (existing != null && existing.Id != user.Id)
                {
                    TempData["Error"] = "That email is already in use.";
                    return RedirectToRoute("AdminProfileIndex");
                }

                var setEmail = await _userManager.SetEmailAsync(user, email);
                if (!setEmail.Succeeded)
                {
                    TempData["Error"] = string.Join("; ", setEmail.Errors.Select(e => e.Description));
                    return RedirectToRoute("AdminProfileIndex");
                }

                var setUserName = await _userManager.SetUserNameAsync(user, email);
                if (!setUserName.Succeeded)
                {
                    TempData["Error"] = string.Join("; ", setUserName.Errors.Select(e => e.Description));
                    return RedirectToRoute("AdminProfileIndex");
                }
            }

            // Full name
            user.FullName = fullName;
            var updateRes = await _userManager.UpdateAsync(user);
            if (!updateRes.Succeeded)
            {
                TempData["Error"] = string.Join("; ", updateRes.Errors.Select(e => e.Description));
                return RedirectToRoute("AdminProfileIndex");
            }

            TempData["Success"] = "Profile updated successfully.";
            return RedirectToRoute("AdminProfileIndex");
        }

        //Admin/Profile/ChangePassword
        [HttpPost("ChangePassword")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
            {
                TempData["Error"] = "Please fill in all password fields.";
                return RedirectToRoute("AdminProfileIndex");
            }
            if (newPassword != confirmPassword)
            {
                TempData["Error"] = "New password and confirmation do not match.";
                return RedirectToRoute("AdminProfileIndex");
            }

            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join("; ", result.Errors.Select(e => e.Description));
                return RedirectToRoute("AdminProfileIndex");
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["Success"] = "Password changed successfully.";
            return RedirectToRoute("AdminProfileIndex");
        }

        //Admin/Profile/Logout
        [HttpPost("Logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
    }
}

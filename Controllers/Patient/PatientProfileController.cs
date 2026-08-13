using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MediCare.App.Data;
using MediCare.App.Models;
using MediCare.App.Services;
using MediCare.App.ViewModels.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatientModel = MediCare.App.Models.Patient;

namespace MediCare.App.Controllers.Patients
{
    [Authorize(Roles = "Patient")]
    public class PatientProfileController : Controller
    {
        private readonly MediCareContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IWebHostEnvironment _env;

        public PatientProfileController(MediCareContext db, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IWebHostEnvironment env)
        {
            _db = db;
            _userManager = userManager;
            _signInManager = signInManager;
            _env = env;
        }

        private string GetMyUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? throw new Exception("Not logged in");
        }

        private (string displayName, string initials) BuildDisplay(string fullName)
        {
            var dn = string.IsNullOrWhiteSpace(fullName) ? "Patient" : fullName.Trim();
            string initials = "PT";

            var parts = dn.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                initials = parts[0].Substring(0, 1).ToUpper();
            }
            else if (parts.Length > 1)
            {
                var firstPart = parts[0];
                var lastPart = parts[parts.Length - 1];

                var firstLetter = !string.IsNullOrEmpty(firstPart)
                    ? firstPart.Substring(0, 1).ToUpper()
                    : "P";

                var lastLetter = !string.IsNullOrEmpty(lastPart)
                    ? lastPart.Substring(0, 1).ToUpper()
                    : "T";

                initials = firstLetter + lastLetter;
            }

            return (dn, initials);
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var myUserId = GetMyUserId();

            var appUser = await _userManager.Users
                .Where(u => u.Id == myUserId)
                .FirstOrDefaultAsync();

            if (appUser == null)
                return Unauthorized();

            var patient = await _db.Patients
                .Where(p => p.UserId == myUserId)
                .FirstOrDefaultAsync();

            if (patient == null)
            {
                patient = new PatientModel
                {
                    UserId = myUserId,
                    FullName = appUser.UserName ?? appUser.Email ?? "Patient"
                };
                _db.Patients.Add(patient);
                await _db.SaveChangesAsync();
            }

            var (displayName, initials) = BuildDisplay(patient.FullName);

            var vm = new PatientProfileVM
            {
                PatientId = patient.Id,
                UserId = patient.UserId,
                FullName = patient.FullName ?? "",
                Email = appUser.Email ?? "",
                PhoneNumber = patient.PhoneNumber,
                Address = patient.Address,
                DateOfBirth = patient.DateOfBirth,
                Gender = patient.Gender,
                DisplayName = displayName,
                Initials = initials,
                AvatarUrl = AvatarStorage.GetAvatarUrl(_env, myUserId),
                StatusMessage = null
            };

            return View("~/Views/Patient/PatientProfile.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(PatientProfileVM form)
        {
            var myUserId = GetMyUserId();

            // force correct user id server-side
            form.UserId = myUserId;

            var appUser = await _userManager.Users
                .Where(u => u.Id == myUserId)
                .FirstOrDefaultAsync();

            var patient = await _db.Patients
                .Where(p => p.Id == form.PatientId && p.UserId == myUserId)
                .FirstOrDefaultAsync();

            if (appUser == null || patient == null)
                return NotFound();

            if (!ModelState.IsValid)
            {
                var (dnBad, initBad) = BuildDisplay(form.FullName);
                form.DisplayName = dnBad;
                form.Initials = initBad;
                form.StatusMessage = null;

                return View("~/Views/Patient/PatientProfile.cshtml", form);
            }

            // update identity user
            if (appUser.Email != form.Email)
            {
                appUser.Email = form.Email;
                appUser.UserName = form.Email;
            }

            // update patient domain info
            patient.FullName = form.FullName;
            patient.PhoneNumber = form.PhoneNumber;
            patient.Address = form.Address;
            patient.DateOfBirth = form.DateOfBirth;
            patient.Gender = form.Gender;

            // commit ApplicationUser first
            var idResult = await _userManager.UpdateAsync(appUser);
            if (!idResult.Succeeded)
            {
                foreach (var err in idResult.Errors)
                    ModelState.AddModelError("", err.Description);

                var (dnErr, initErr) = BuildDisplay(form.FullName);
                form.DisplayName = dnErr;
                form.Initials = initErr;
                form.StatusMessage = null;

                return View("~/Views/Patient/PatientProfile.cshtml", form);
            }

            // commit Patient record
            _db.Patients.Update(patient);
            await _db.SaveChangesAsync();

            // rebuild display props for success state
            var (dnOk, initOk) = BuildDisplay(form.FullName);
            form.DisplayName = dnOk;
            form.Initials = initOk;
            form.StatusMessage = "Profile updated successfully.";

            return View("~/Views/Patient/PatientProfile.cshtml", form);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadAvatar(IFormFile avatarFile)
        {
            var myUserId = GetMyUserId();

            var (ok, error) = AvatarStorage.Validate(avatarFile);
            if (!ok)
            {
                TempData["Error"] = error;
                return RedirectToAction(nameof(Index));
            }

            await AvatarStorage.SaveAsync(_env, myUserId, avatarFile);
            TempData["Success"] = "Profile picture updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            var myUserId = GetMyUserId();
            var appUser = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == myUserId);
            if (appUser == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
            {
                TempData["Error"] = "Please fill in all password fields.";
                return RedirectToAction(nameof(Index));
            }
            if (newPassword != confirmPassword)
            {
                TempData["Error"] = "New password and confirmation do not match.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _userManager.ChangePasswordAsync(appUser, currentPassword, newPassword);
            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join("; ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            await _signInManager.RefreshSignInAsync(appUser);
            TempData["Success"] = "Password changed successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace MediCare.App.Services
{
    /// <summary>
    /// Stores profile-picture uploads on disk, keyed by user id, without needing a
    /// new database column/migration: wwwroot/uploads/avatars/{userId}.{ext}.
    /// Existence of that file (checked at read time) is the only "state" — no DB change.
    /// </summary>
    public static class AvatarStorage
    {
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        private const long MaxSizeBytes = 2 * 1024 * 1024; // 2 MB

        private static string FolderPath(IWebHostEnvironment env) =>
            Path.Combine(env.WebRootPath, "uploads", "avatars");

        public static (bool ok, string? error) Validate(IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return (false, "Please choose an image file.");

            if (file.Length > MaxSizeBytes)
                return (false, "Image must be 2 MB or smaller.");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                return (false, "Only JPG, PNG, WEBP, or GIF images are allowed.");

            return (true, null);
        }

        public static async Task<string> SaveAsync(IWebHostEnvironment env, string userId, IFormFile file)
        {
            var folder = FolderPath(env);
            Directory.CreateDirectory(folder);

            // Remove any existing avatar for this user (possibly a different extension)
            // before saving the new one, so stale files don't linger.
            DeleteExisting(env, userId);

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var safeUserId = string.Concat(userId.Where(c => char.IsLetterOrDigit(c) || c == '-'));
            var fileName = $"{safeUserId}{ext}";
            var fullPath = Path.Combine(folder, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/uploads/avatars/{fileName}";
        }

        public static string? GetAvatarUrl(IWebHostEnvironment env, string userId)
        {
            var folder = FolderPath(env);
            if (!Directory.Exists(folder)) return null;

            var safeUserId = string.Concat(userId.Where(c => char.IsLetterOrDigit(c) || c == '-'));
            foreach (var ext in AllowedExtensions)
            {
                var candidate = Path.Combine(folder, $"{safeUserId}{ext}");
                if (File.Exists(candidate))
                    return $"/uploads/avatars/{safeUserId}{ext}";
            }

            return null;
        }

        private static void DeleteExisting(IWebHostEnvironment env, string userId)
        {
            var folder = FolderPath(env);
            var safeUserId = string.Concat(userId.Where(c => char.IsLetterOrDigit(c) || c == '-'));
            foreach (var ext in AllowedExtensions)
            {
                var candidate = Path.Combine(folder, $"{safeUserId}{ext}");
                if (File.Exists(candidate))
                {
                    try { File.Delete(candidate); } catch (IOException) { /* best-effort cleanup */ }
                }
            }
        }
    }
}

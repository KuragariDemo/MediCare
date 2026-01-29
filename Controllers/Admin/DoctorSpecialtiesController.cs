using System;
using System.Linq;
using System.Threading.Tasks;
using MediCare.App.Data;
using MediCare.App.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediCare.App.Controllers.Admin
{
    [Authorize(Roles = "Admin,AdminPlus")]
    [Route("Admin/[controller]")]
    public class DoctorSpecialtiesController : Controller
    {
        private readonly MediCareContext _db;

        public DoctorSpecialtiesController(MediCareContext db)
        {
            _db = db;
        }

        // View
        [HttpGet("")]
        public IActionResult Index()
        {
            return View("~/Views/Admin/DoctorSpecialties.cshtml");
        }

        // ===== API =====

        // List (returns RowVersion as base64)
        [HttpGet("List")]
        public async Task<IActionResult> List()
        {
            var items = await _db.DoctorSpecialties
                .AsNoTracking()
                .OrderBy(s => s.Name)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.IsActive,
                    s.Fee,
                    rowVersion = s.RowVersion != null ? Convert.ToBase64String(s.RowVersion) : null
                })
                .ToListAsync();

            return Json(new { ok = true, data = items });
        }

        // DTOs
        public record CreateDto(string Name, decimal? Fee);
        public record UpdateDto(string Name, bool? IsActive, byte[]? RowVersion, decimal? Fee);

        // Create
        [ValidateAntiForgeryToken]
        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromForm] CreateDto dto)
        {
            var name = (dto.Name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { ok = false, error = "Name is required." });

            var exists = await _db.DoctorSpecialties.AnyAsync(s => s.Name.ToLower() == name.ToLower());
            if (exists)
                return Conflict(new { ok = false, error = $"Specialty \"{name}\" already exists." });

            var fee = Math.Max(0m, dto.Fee ?? 0m);

            var spec = new DoctorSpecialty
            {
                Name = name,
                Slug = ToSlug(name),
                IsActive = true,
                Fee = fee
            };

            _db.DoctorSpecialties.Add(spec);
            await _db.SaveChangesAsync();

            return Json(new
            {
                ok = true,
                message = "Created",
                data = new
                {
                    spec.Id,
                    spec.Name,
                    spec.IsActive,
                    spec.Fee,
                    rowVersion = spec.RowVersion != null ? Convert.ToBase64String(spec.RowVersion) : null
                }
            });
        }

        // Update (requires RowVersion)
        [ValidateAntiForgeryToken]
        [HttpPut("Update/{id:int}")]
        public async Task<IActionResult> Update(int id, [FromForm] UpdateDto dto)
        {
            var spec = await _db.DoctorSpecialties.FirstOrDefaultAsync(x => x.Id == id);
            if (spec == null)
                return NotFound(new { ok = false, error = "Not found." });

            if (dto.RowVersion == null)
                return BadRequest(new { ok = false, error = "Missing RowVersion." });

            // concurrency
            _db.Entry(spec).Property(s => s.RowVersion).OriginalValue = dto.RowVersion;

            if (!string.IsNullOrWhiteSpace(dto.Name))
            {
                var name = dto.Name.Trim();
                var nameExists = await _db.DoctorSpecialties
                    .AnyAsync(s => s.Id != id && s.Name.ToLower() == name.ToLower());
                if (nameExists)
                    return Conflict(new { ok = false, error = $"Specialty \"{name}\" already exists." });

                spec.Name = name;
                spec.Slug = ToSlug(name);
            }

            if (dto.IsActive.HasValue)
                spec.IsActive = dto.IsActive.Value;

            if (dto.Fee.HasValue)
                spec.Fee = Math.Max(0m, dto.Fee.Value);

            spec.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync();
                return Json(new
                {
                    ok = true,
                    message = "Updated",
                    data = new
                    {
                        spec.Id,
                        spec.Name,
                        spec.IsActive,
                        spec.Fee,
                        rowVersion = spec.RowVersion != null ? Convert.ToBase64String(spec.RowVersion) : null
                    }
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { ok = false, error = "Concurrency conflict. Reload and try again." });
            }
        }

        // Delete
        [ValidateAntiForgeryToken]
        [HttpDelete("Delete/{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var spec = await _db.DoctorSpecialties.FindAsync(id);
            if (spec == null)
                return NotFound(new { ok = false, error = "Not found." });

            _db.DoctorSpecialties.Remove(spec);
            await _db.SaveChangesAsync();
            return Json(new { ok = true, message = "Deleted" });
        }

        // helpers
        private static string ToSlug(string text)
        {
            var s = text.Trim().ToLowerInvariant();
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                s = s.Replace(c, '-');
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", "-");
            s = System.Text.RegularExpressions.Regex.Replace(s, @"[^a-z0-9\\-]", "");
            return s;
        }
    }
}

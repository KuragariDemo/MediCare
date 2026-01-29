using MediCare.App.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace MediCare.App.Controllers.Api
{
    [Authorize(Roles = "Patient,Admin,AdminPlus,Doctor")]
    [ApiController]
    [Route("api/doctors")]
    [Produces("application/json")]
    public class DoctorsApiController : ControllerBase
    {
        private readonly MediCareContext _db;
        public DoctorsApiController(MediCareContext db) => _db = db;

        // GET /api/doctors/specialties?sort=alpha|count
        // Returns: { total, items: [{ id, name, count }, ...] }
        [HttpGet("specialties")]
        public async Task<IActionResult> Specialties([FromQuery] string? sort = "alpha")
        {
            // Count doctors grouped by SpecialtyId (only where SpecialtyId is set)
            var counts = await _db.Doctors
                .Where(d => d.SpecialtyId != null)
                .GroupBy(d => d.SpecialtyId!.Value)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Id, x => x.Count);

            // Materialize the active specialties (avoid EF translating dictionary access)
            var specialties = await _db.DoctorSpecialties
                .AsNoTracking()
                .Where(s => s.IsActive)
                .Select(s => new { s.Id, s.Name })
                .ToListAsync();

            // Helper avoids 'out var' inline (prevents CS8198 in some setups)
            int GetCount(int id) => counts.TryGetValue(id, out int v) ? v : 0;

            var items = specialties
                .Select(s => new
                {
                    id = s.Id,
                    name = s.Name,
                    count = GetCount(s.Id)
                })
                .ToList();

            items = (sort?.ToLowerInvariant()) switch
            {
                "count" => items.OrderByDescending(x => x.count).ThenBy(x => x.name).ToList(),
                _ => items.OrderBy(x => x.name).ToList()
            };

            var totalDoctors = counts.Values.Sum();
            return Ok(new { total = totalDoctors, items });
        }

        // GET /api/doctors/search?q=...&specialtyId=123&sort=Name|Speciality
        // Returns: [{ id, fullName, email, speciality, bio }, ...]
        [HttpGet("search")]
        public async Task<IActionResult> Search(
            [FromQuery] string? q,
            [FromQuery] int? specialtyId,
            [FromQuery] string? sort = "Name")
        {
            q ??= string.Empty;
            var qNorm = q.Trim().ToLower();

            var query = _db.Doctors
                .AsNoTracking()
                .Include(d => d.User)
                .Include(d => d.Specialty) // pull name from DoctorSpecialties
                .AsQueryable();

            if (specialtyId.HasValue)
            {
                query = query.Where(d => d.SpecialtyId == specialtyId.Value);
            }

            if (!string.IsNullOrWhiteSpace(qNorm))
            {
                query = query.Where(d =>
                    (((d.User.FullName ?? d.User.UserName) ?? string.Empty).ToLower().Contains(qNorm)) ||
                    ((d.User.Email ?? string.Empty).ToLower().Contains(qNorm)));
            }

            query = sort?.Equals("Speciality", System.StringComparison.OrdinalIgnoreCase) == true
                ? query.OrderBy(d => d.Specialty!.Name).ThenBy(d => d.User.FullName ?? d.User.UserName)
                : query.OrderBy(d => d.User.FullName ?? d.User.UserName);

            var result = await query
                .Select(d => new
                {
                    id = d.Id,
                    fullName = d.User.FullName ?? d.User.UserName,
                    email = d.User.Email,
                    // Prefer table-based name; fall back to legacy string if still present
                    speciality = d.Specialty != null ? d.Specialty.Name : d.Speciality,
                    bio = d.Bio
                })
                .Take(100)
                .ToListAsync();

            return Ok(result);
        }
    }
}

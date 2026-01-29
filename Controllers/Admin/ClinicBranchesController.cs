using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using MediCare.App.Data;
using MediCare.Models;
using MediCare.ViewModels.Branches;

namespace MediCare.Controllers.Admin
{
    [Route("Admin/[controller]/[action]")]
    public class ClinicBranchesController : Controller
    {
        private readonly MediCareContext _db;
        private const int PageSize = 10;

        public ClinicBranchesController(MediCareContext db) => _db = db;

        // GET: /Admin/ClinicBranches/Index?q=&page=
        [HttpGet]
        public async Task<IActionResult> Index(string? q, int page = 1)
        {
            var query = _db.ClinicBranches.AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(b =>
                    b.Name.Contains(q) ||
                    (b.Address != null && b.Address.Contains(q)) ||
                    (b.Phone != null && b.Phone.Contains(q)) ||
                    (b.Email != null && b.Email.Contains(q)));
            }

            var total = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));

            var items = await query
                .OrderBy(b => b.Name)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            var vm = new ClinicBranchesIndexVm
            {
                Items = items,
                Query = q,
                Page = page,
                TotalPages = totalPages
            };

            return View("~/Views/Admin/ClinicBranchesManagement.cshtml", vm);
        }

        // POST: /Admin/ClinicBranches/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ClinicBranchesIndexVm.BranchForm form, string? q, int page = 1)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please fill the form correctly.";
                return RedirectToAction(nameof(Index), new { q, page });
            }

            var b = new ClinicBranch
            {
                Name = form.Name.Trim(),
                Address = form.Address?.Trim(),
                Phone = form.Phone?.Trim(),
                Email = form.Email?.Trim(),
                IsActive = true
            };

            _db.ClinicBranches.Add(b);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Branch created.";
            return RedirectToAction(nameof(Index), new { q, page });
        }

        // POST: /Admin/ClinicBranches/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ClinicBranchesIndexVm.BranchForm form, string? q, int page = 1)
        {
            if (form.Id is null)
            {
                TempData["Error"] = "Missing branch id.";
                return RedirectToAction(nameof(Index), new { q, page });
            }
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please fill the form correctly.";
                return RedirectToAction(nameof(Index), new { q, page });
            }

            var b = await _db.ClinicBranches.FindAsync(form.Id.Value);
            if (b == null)
            {
                TempData["Error"] = "Branch not found.";
                return RedirectToAction(nameof(Index), new { q, page });
            }

            b.Name = form.Name.Trim();
            b.Address = form.Address?.Trim();
            b.Phone = form.Phone?.Trim();
            b.Email = form.Email?.Trim();

            await _db.SaveChangesAsync();

            TempData["Success"] = "Branch updated.";
            return RedirectToAction(nameof(Index), new { q, page });
        }

        // POST: /Admin/ClinicBranches/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string? q, int page = 1)
        {
            var b = await _db.ClinicBranches.FindAsync(id);
            if (b == null)
            {
                TempData["Error"] = "Branch not found.";
                return RedirectToAction(nameof(Index), new { q, page });
            }

            _db.ClinicBranches.Remove(b);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Branch deleted.";
            return RedirectToAction(nameof(Index), new { q, page });
        }
    }
}

using System.Linq;
using System.Threading.Tasks;
using MediCare.App.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediCare.Controllers
{
    // Serves the public "Our Branches" page. Previously this route had no backing
    // controller at all — Views/Branches/Index.cshtml was unreachable, and its content
    // was 3 hardcoded fake branches rather than the real ClinicBranches table.
    public class BranchesController : Controller
    {
        private readonly MediCareContext _db;

        public BranchesController(MediCareContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var branches = await _db.ClinicBranches
                .AsNoTracking()
                .Where(b => b.IsActive)
                .OrderBy(b => b.Name)
                .ToListAsync();

            return View(branches);
        }
    }
}

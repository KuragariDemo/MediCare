using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using MediCare.App.Data;
using MediCare.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediCare.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly MediCareContext _db;

        public HomeController(ILogger<HomeController> logger, MediCareContext db)
        {
            _logger = logger;
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.FeaturedDoctors = await _db.Doctors
                .AsNoTracking()
                .Include(d => d.User)
                .Include(d => d.Specialty)
                .OrderByDescending(d => d.Id)
                .Take(4)
                .Select(d => new
                {
                    Name = d.User.FullName ?? d.User.UserName ?? "Doctor",
                    Specialty = d.Specialty != null ? d.Specialty.Name : (d.Speciality ?? "General")
                })
                .ToListAsync();

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

using System.Threading.Tasks;
using MediCare.App.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace MediCare.App.Controllers.Patient
{
    [Authorize(Roles = "Patient")]
    [Route("Patient/Dashboard")]
    public class DashboardController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;

        public DashboardController(SignInManager<ApplicationUser> signInManager)
        {
            _signInManager = signInManager;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public IActionResult Index() =>
            View("~/Views/Patient/Dashboard.cshtml");

        [HttpGet("Profile")]
        public IActionResult PatientProfile() =>
            View("~/Views/Patient/PatientProfile.cshtml");

    }
}

using Microsoft.AspNetCore.Mvc;

namespace MediCare.Controllers
{
    // Serves the public "About Us" marketing page. Previously this route had no
    // backing controller at all — Views/About/Index.cshtml was unreachable.
    public class AboutController : Controller
    {
        public IActionResult Index() => View();
    }
}

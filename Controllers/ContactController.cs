using MediCare.App.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace MediCare.Controllers
{
    // Serves the public "Contact Us" page and handles the contact form submit.
    // Previously this route had no backing controller at all — Views/Contact/Index.cshtml
    // was unreachable, and the form itself had no method/action and no server endpoint.
    //
    // NOTE: there is no email/SMTP integration or ContactMessage table in this project yet,
    // so a submission here is validated and logged server-side but not persisted or emailed.
    // Wiring that up is a deliberate follow-up (needs an SMTP config or a new DB table +
    // migration) rather than something silently faked.
    public class ContactController : Controller
    {
        private readonly ILogger<ContactController> _logger;

        public ContactController(ILogger<ContactController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Send(ContactMessageVm vm)
        {
            if (!ModelState.IsValid)
            {
                if (Request.Headers["Accept"].ToString().Contains("application/json"))
                    return BadRequest(new { ok = false, error = "Please fill in all required fields correctly." });

                return View("Index", vm);
            }

            _logger.LogInformation(
                "Contact form submission from {Name} <{Email}> ({Phone}) — Subject: {Subject}\n{Message}",
                vm.Name, vm.Email, vm.Phone, vm.Subject, vm.Message);

            if (Request.Headers["Accept"].ToString().Contains("application/json"))
                return Ok(new { ok = true });

            TempData["Success"] = "Thanks — your message has been received. We'll get back to you soon.";
            return RedirectToAction(nameof(Index));
        }
    }
}

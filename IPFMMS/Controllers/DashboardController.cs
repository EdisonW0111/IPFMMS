using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IPFMMS.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            if (User.IsInRole("Admin"))
            {
                return RedirectToAction(nameof(Admin));
            }

            if (User.IsInRole("User"))
            {
                return RedirectToAction(nameof(Finance));
            }

            return Forbid();
        }

        [HttpGet]
        [Authorize(Roles = "User")]
        public IActionResult Finance()
        {
            return View();
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult Admin()
        {
            return View();
        }
    }
}
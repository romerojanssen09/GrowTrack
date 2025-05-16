using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_Creation.Controllers
{
    public class PagesController : Controller
    {
        [Authorize]
        public IActionResult Dashboard()
        {
            return View("~/Views/Pages/Dashboard.cshtml");
        }
    }
}

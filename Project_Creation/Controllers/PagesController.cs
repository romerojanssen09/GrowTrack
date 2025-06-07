using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Project_Creation.Controllers
{
    public class PagesController : Controller
    {
        [Authorize]
        public IActionResult Dashboard()
        {
            // Fix: Use the Controller's RouteData property instead of ViewContext.RouteData  
            RouteData.Values["controller"] = "Inventory1";
            return View("~/Views/Pages/Dashboard.cshtml");
        }
    }
}

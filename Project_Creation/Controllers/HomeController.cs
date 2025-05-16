using System.Diagnostics;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_Creation.Models;
using Microsoft.EntityFrameworkCore;
using Project_Creation.Models.Entities;
using Project_Creation.Data;
using Project_Creation.DTO;

namespace Project_Creation.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AuthDbContext _context;

        public HomeController(ILogger<HomeController> logger, AuthDbContext context)
        {
            _context = context;
            _logger = logger;
        }

        public IActionResult Index()
        {
            if (User.Identity.IsAuthenticated)
            {
                if(User.IsInRole("Admin"))
                {
                    return RedirectToAction("Index", "Admin");
                }
                else
                {
                    return RedirectToAction("Dashboard", "Pages");
                }
            }
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            _logger.LogInformation("User logged out.");
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> AdminCreateAccount()
        {
            // Check if any admin exists
            bool adminExists = await _context.Admin.AnyAsync();

            if (adminExists)
            {
                _logger.LogWarning("Attempt to access admin creation when admin already exists");
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminCreateAccount(AdminCreateDto model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Check if admin already exists
            if (await _context.Admin.AnyAsync())
            {
                ModelState.AddModelError("", "An admin account already exists");
                return View(model);
            }

            // Check if email already exists
            if (await _context.Admin.AnyAsync(a => a.Email == model.Email))
            {
                ModelState.AddModelError("Email", "This email is already registered");
                return View(model);
            }

            try
            {
                var admin = new Admin
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Email = model.Email,
                    Password = BCrypt.Net.BCrypt.HashPassword(model.Password),
                    Role = "Admin"
                };

                _context.Admin.Add(admin);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Admin account created successfully for {Email}", model.Email);

                // Optionally sign in the admin immediately
                // await HttpContext.SignInAsync(...);

                TempData["SuccessMessage"] = "Admin account created successfully!";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating admin account");
                ModelState.AddModelError("", "An error occurred while creating the admin account");
                return View(model);
            }
        }
    }
}

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
        private readonly IEmailService _emailService;

        public HomeController(ILogger<HomeController> logger, AuthDbContext context, IEmailService emailService)
        {
            _context = context;
            _logger = logger;
            _emailService = emailService;
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

        public IActionResult Contact()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contact(ContactMessage model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Save the contact message to the database
                    model.CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));
                    _context.ContactMessages.Add(model);
                    await _context.SaveChangesAsync();

                    // Get admin email for notification
                    var admin = await _context.Admin.FirstOrDefaultAsync();
                    if (admin != null)
                    {
                        // Create email body
                        string emailBody = $@"
                            <h2>New Contact Form Submission</h2>
                            <p><strong>From:</strong> {model.Name}</p>
                            <p><strong>Email:</strong> {model.Email}</p>
                            <p><strong>Subject:</strong> {model.Subject}</p>
                            <p><strong>Message:</strong></p>
                            <p>{model.Message}</p>
                            <hr>
                            <p>You can view this message in your admin dashboard.</p>
                        ";

                        // Send email notification to admin
                        await _emailService.SendEmail(admin.Email, "New Contact Form Submission - GrowTrack", emailBody, true);
                    }

                    TempData["SuccessMessage"] = "Your message has been sent successfully. We'll get back to you soon!";
                    return RedirectToAction(nameof(Contact));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing contact form");
                    ModelState.AddModelError("", "An error occurred while sending your message. Please try again later.");
                }
            }

            return View(model);
        }

        public IActionResult Features()
        {
            return View();
        }

        public IActionResult About()
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
                    Role = "Admin",
                    AllowEmailNotifications = true
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

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Creation.Data;
using Project_Creation.DTO;
using Project_Creation.Models.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using static Project_Creation.Models.Entities.Users;

namespace Project_Creation.Controllers
{
    public class RegisterController : Controller
    {
        private readonly AuthDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<RegisterController> _logger;
        private readonly IEmailService _emailService;

        public RegisterController(
            AuthDbContext context,
            IWebHostEnvironment environment,
            ILogger<RegisterController> logger,
            IEmailService emailService)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
            _emailService = emailService;
        }

        [HttpGet]
        public IActionResult Register()
        {
            // Initialize with empty values for required properties
            var model = new UsersDto
            {
                FirstName = string.Empty,
                LastName = string.Empty,
                Email = string.Empty,
                PhoneNumber = string.Empty,
                BusinessName = string.Empty,
                BusinessAddress = string.Empty,
                DTIReqistrationNumber = string.Empty,
                NumberOfBusinessYearsOperation = string.Empty,
                CategoryOfBusiness = string.Empty,
                CompanyBackground = string.Empty
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> CheckEmailExists(string email)
        {
            // Check in both Users and Admin tables
            var existsInUsers = await _context.Users.AnyAsync(u => u.Email == email);
            if(existsInUsers) return Json(new { exists = existsInUsers });
            var existsInAdmin = await _context.Admin.AnyAsync(u => u.Email == email);
            if(existsInAdmin) return Json(new { exists = existsInAdmin });
            return Json(new { exists = false });
            // Return true if email exists in either table
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(UsersDto model, IFormFile businessPermitFile, IFormFile? logoFile)
        {
            // Debugging: Log model state errors
            if (!ModelState.IsValid)
            {
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    _logger.LogError($"Validation error: {error.ErrorMessage}"); // Use _logger instance
                }
                return View(model);
            }

            // Validate required file
            if (businessPermitFile == null || businessPermitFile.Length == 0)
            {
                ModelState.AddModelError("businessPermitFile", "Business permit is required");
                return View(model);
            }

            try
            {
                // Map DTO to Entity
                var register = new Users
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Email = model.Email,
                    PhoneNumber = model.PhoneNumber,
                    BusinessName = model.BusinessName,
                    BusinessAddress = model.BusinessAddress,
                    DTIReqistrationNumber = model.DTIReqistrationNumber,
                    NumberOfBusinessYearsOperation = model.NumberOfBusinessYearsOperation,
                    CategoryOfBusiness = model.CategoryOfBusiness,
                    CompanyBackground = model.CompanyBackground,
                    RegistrationDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore")),
                    BusinessPermitPath = string.Empty,
                    IsAllowEditBusinessPermitPath = false,
                    LogoPath = null,
                    IsVerified = false,
                    MarkerPlaceStatus = MarketplaceStatus.Pending
                };

                // First save to get ID
                _context.Users.Add(register);
                await _context.SaveChangesAsync();

                // Process files
                register.BusinessPermitPath = await SaveFile(register.Id.ToString(), businessPermitFile, "BusinessPermits");

                if (logoFile != null && logoFile.Length > 0)
                {
                    register.LogoPath = await SaveFile(register.Id.ToString(), logoFile, "Logos");
                }

                // Update with file paths
                _context.Users.Update(register);
                await _context.SaveChangesAsync();
                // After successful registration
                if (register.UserRole == "Admin")
                {
                    TempData["SuccessMessage"] = "Registration successful! You Are the Admin";
                }
                else
                {
                    try
                    {
                        // Convert UTC to Philippine Time (UTC+8)
                        DateTime phTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));

                        // Send email
                        await _emailService.SendEmail(
                            "likedjans98@gmail.com",
                            "🌟 New User Registration - Action Required 🌟",  // More attention-grabbing subject
                            $"<h2>Dear Admin,</h2>" +
                            $"<p>A new user has just registered on the system. Below are the registration details:</p>" +

                            $"<h3>🔹 User Information</h3>" +
                            $"<ul>" +
                            $"<li><strong>Name:</strong> {register.FirstName} {register.LastName}</li>" +
                            $"<li><strong>Email:</strong> {register.Email}</li>" +
                            $"<li><strong>Registration Date:</strong> <span style='color: #e74c3c;'>{phTime.ToString("yyyy-MM-dd hh:mm tt")} (Philippine Time)</span></li>" +
                            $"</ul>" +

                            $"<h3>🔹 Business Details</h3>" +
                            $"<ul>" +
                            $"<li><strong>Business Name:</strong> {register.BusinessName}</li>" +
                            $"<li><strong>Business Type:</strong> {register.CategoryOfBusiness}</li>" +
                            $"</ul>" +

                            $"<div style='background-color: #f8f9fa; padding: 15px; border-left: 4px solid #3498db; margin: 20px 0;'>" +
                            $"<h3>⚠️ Action Required</h3>" +
                            $"<p>Please review this registration in the admin panel and verify the user account when appropriate.</p>" +
                            $"<a href='https://youradminportal.com/users/{register.Id}' style='background-color: #3498db; color: white; padding: 10px 15px; text-decoration: none; border-radius: 4px;'>Review User</a>" +
                            $"</div>" +

                            $"<p>Best regards,<br>GrowTrack CRM System</p>" +

                            $"<div style='margin-top: 30px; font-size: 12px; color: #7f8c8d;'>" +
                            $"<p>This is an automated message. Please do not reply directly to this email.</p>" +
                            $"</div>",
                            isBodyHtml: true);  // Don't forget to set this to true for HTML formatting

                        TempData["SuccessMessage"] = "Registration successful! Please wait for Admin to verify your account. We will send the link to your email to create your own password";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending registration email");
                        TempData["SuccessMessage"] = "Registration successful but failed to send email. Please contact support.";
                    }
                }

                return RedirectToAction("Login", "Login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registration failed"); // Use _logger instance
                ModelState.AddModelError("", "An error occurred during registration. Please try again.");
                return View(model);
            }
        }

        private async Task<string> SaveFile(string userId, IFormFile file, string subFolder)
        {
            if (file == null || file.Length == 0)
                return string.Empty;

            // Validate file extension
            var permittedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (string.IsNullOrEmpty(extension) || !permittedExtensions.Contains(extension))
            {
                throw new InvalidOperationException("Invalid file type. Only PDF, JPG, and PNG are allowed.");
            }

            // Validate file size (5MB limit)
            if (file.Length > 5 * 1024 * 1024)
            {
                throw new InvalidOperationException("File size exceeds 5MB limit.");
            }

            var userUploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", userId, subFolder);
            Directory.CreateDirectory(userUploadsFolder);

            var fileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}_{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(userUploadsFolder, fileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return $"/uploads/{userId}/{subFolder}/{fileName}";
        }

        [HttpGet]
        public async Task<IActionResult> SecondRegistration(string userId)
        {
            if (!int.TryParse(userId, out int parsedUserId))
            {
                TempData["ErrorMessage"] = "Invalid user ID.";
                return RedirectToAction("Login", "Login");
            }

            var existingUser = await _context.Users.FindAsync(parsedUserId);
            if (existingUser == null)
            {
                _logger.LogError("User not found");
                TempData["ErrorMessage"] = "User Not Found!";
                return RedirectToAction("Login", "Login");
            }

            if (existingUser.IsSetPassword)
            {
                _logger.LogError("You already set the password!");
                TempData["ErrorMessage"] = "You already set the password!";
                return RedirectToAction("Login", "Login");
            }

            // Initialize with empty values for required properties
            var model = new PasswordDto
            {
                UserId = int.Parse(userId),
                Password = string.Empty,
                ConfirmPassword = string.Empty
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SecondRegistration([FromForm] PasswordDto model)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogError("Model state is invalid");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    _logger.LogError($"Validation error: {error.ErrorMessage}");
                }
                return View("SecondRegistration", model);
            }

            if (model.Password != model.ConfirmPassword)
            {
                _logger.LogError("Password not match");
                ModelState.AddModelError("", "Passwords do not match.");
                return View("SecondRegistration", model);
            }

            try
            {
                var existingUser = await _context.Users.FindAsync(model.UserId);
                if (existingUser == null)
                {
                    _logger.LogError("User not found");
                    TempData["ErrorMessage"] = "User Not Found!";
                    return RedirectToAction("Login", "Login");
                }
                existingUser.Password = BCrypt.Net.BCrypt.HashPassword(model.Password);
                existingUser.IsSetPassword = true;
                _context.Users.Update(existingUser);
                await _context.SaveChangesAsync(); // This was missing!

                TempData["SuccessMessage"] = "Registration completed successfully!";
                return RedirectToAction("Login", "Login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during second registration step");
                ModelState.AddModelError("", "An error occurred while processing your registration. Please try again.");
                return View("SecondRegistration", model);
            }
        }
    }
}
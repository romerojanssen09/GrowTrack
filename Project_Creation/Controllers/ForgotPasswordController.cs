using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Project_Creation.Data;
using Project_Creation.DTO;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static NHibernate.Engine.Query.CallableParser;

namespace Project_Creation.Controllers
{
    //[Route("Account/[controller]")]
    public class ForgotPasswordController : Controller
    {
        private readonly AuthDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ForgotPasswordController> _logger;
        private readonly IEmailService _emailService;

        public ForgotPasswordController(
            AuthDbContext context,
            IWebHostEnvironment environment,
            ILogger<ForgotPasswordController> logger,
            IEmailService emailService)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
            _emailService = emailService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var password = new ForgotPasswordDto 
            { 
                UserId = 0,
                Email = string.Empty,
                Password = string.Empty
            };
            return View(password);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword([FromForm] ForgotPasswordDto model)
        {
            // Detailed request logging
            _logger.LogInformation("Received ForgotPassword request");
            _logger.LogInformation($"Form data: UserId={model?.UserId}, Email={model?.Email}");
            _logger.LogInformation($"Headers: {Request.Headers}");

            foreach (var key in Request.Form.Keys)
            {
                _logger.LogError($"Form key: {key}, Value: {Request.Form[key]}");
            }

            if (!ModelState.IsValid)
            {
                // Show validation errors in the view
                return View("Index", model);
            }

            var user2 = await _context.Users.FindAsync(model.UserId);
            if (user2 == null)
            {
                ModelState.AddModelError("", "User not found.");
                return View("Index", model);
            }

            if (user2.Email != model.Email)
            {
                ModelState.AddModelError("", "Email does not match user record.");
                return View("Index", model);
            }

            _logger.LogInformation("ForgotPassword action started");
            _logger.LogInformation($"Received data - UserId: {model.UserId}, Email: {model.Email}");

            if (ModelState.IsValid)
            {
                _logger.LogInformation("Model state is valid");

                var user = await _context.Users.FindAsync(model.UserId);
                if (user != null)
                {
                    _logger.LogInformation($"Found user with ID: {user.Id}");

                    var newHashedPassword = BCrypt.Net.BCrypt.HashPassword(model.Password);
                    _logger.LogInformation($"Old hash: {user.Password}, New hash: {newHashedPassword}");

                    user.Password = newHashedPassword;
                    user.ResetCode = null;
                    user.ResetCodeExpiry = null;

                    try
                    {
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Password updated successfully");

                        await _emailService.SendEmail(model.Email, "Change Password Success",
                    $"Your password has been changed successfully.");

                        TempData["SuccessMessage"] = "Your password has been changed successfully.";
                        return RedirectToAction("Login", "Login");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to save changes to database");
                        ModelState.AddModelError("", "Failed to update password. Please try again.");
                    }
                }
                else
                {
                    _logger.LogWarning($"User not found with ID: {model.UserId}");
                    ModelState.AddModelError("", "User not found.");
                }
            }
            else
            {
                _logger.LogWarning("Model state is invalid");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    _logger.LogWarning(error.ErrorMessage);
                }
            }

            // If we reach here, something failed, redisplay the form with any errors
            return View("Index", model);
        }

        [HttpGet]
        public async Task<IActionResult> SendCode(string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                    return BadRequest(new { success = false, message = "Email is required" });

                _logger.LogInformation($"Looking for user with email: {email}");
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

                if (user == null)
                    return Json(new { success = false, message = "Email not found in our system" });

                // Calculate Singapore time first
                var singaporeTime = TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow,
                    TimeZoneInfo.FindSystemTimeZoneById("Singapore"));

                // Get expired users (evaluated client-side)
                var allUsers = await _context.Users.ToListAsync();
                var expiredUsers = allUsers
                    .Where(u => u.ResetCodeExpiry < singaporeTime)
                    .ToList();

                foreach (var expiredUser in expiredUsers)
                {
                    expiredUser.ResetCode = null;
                    expiredUser.ResetCodeExpiry = null;
                }

                var code = GenerateVerificationCode();
                var expiry = singaporeTime.AddMinutes(15);

                user.ResetCode = code;
                user.ResetCodeExpiry = expiry;
                await _context.SaveChangesAsync();

                await _emailService.SendEmail(email, "Your verification code",
                    $"Your password reset code is: {code}\n\nThis code will expire in 15 minutes.");

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending verification code");
                return StatusCode(500, new { success = false, message = "An internal server error occurred" });
            }
        }

        [HttpGet]
        [Produces("application/json")]
        public async Task<IActionResult> VerifyCode(string code)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(code) || code.Length != 6 || !int.TryParse(code, out int codeValue))
                    return Json(new { success = false, message = "Invalid code format" });

                // Calculate Singapore time first
                var singaporeTime = TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow,
                    TimeZoneInfo.FindSystemTimeZoneById("Singapore"));

                // Get all users first, then filter in memory
                var allUsers = await _context.Users.ToListAsync();
                var user = allUsers.FirstOrDefault(u =>
                    u.ResetCode == codeValue &&
                    u.ResetCodeExpiry > singaporeTime);

                if (user == null)
                    return Json(new { success = false, message = "Invalid or expired code" });

                return Json(new
                {
                    success = true,
                    userId = user.Id,
                    email = user.Email
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying code");
                return Json(new { success = false, message = "An error occurred during verification" });
            }
        }

        private int GenerateVerificationCode()
        {
            var random = new Random(); // Remove the seed (6)
            return random.Next(100000, 999999);
        }
    }
}

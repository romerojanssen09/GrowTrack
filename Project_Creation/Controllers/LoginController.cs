using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Project_Creation.Data;
using Project_Creation.DTO;
using Project_Creation.Models.Entities;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace Project_Creation.Controllers
{
    public class LoginController : Controller
    {
        private readonly AuthDbContext _context;
        private readonly ILogger<LoginController> _logger;

        public LoginController(AuthDbContext context, ILogger<LoginController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string returnUrl = null)
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToDashboard();
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginDto model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                // Check both Users and Admin tables
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == model.Email);

                var admin = await _context.Admin
                    .FirstOrDefaultAsync(a => a.Email == model.Email);
                
                var staff = await _context.Staffs
                    .FirstOrDefaultAsync(a => a.StaffSEmail == model.Email);

                // Determine which account type we're dealing with
                if (admin != null && BCrypt.Net.BCrypt.Verify(model.Password, admin.Password))
                {
                    await SignInAdmin(admin, model.RememberMe);
                    _logger.LogInformation("Admin login successful: {Email}", admin.Email);
                    // Always redirect to admin dashboard after admin login
                    return RedirectToAction("Index", "Admin");
                }
                else if (user != null && BCrypt.Net.BCrypt.Verify(model.Password, user.Password))
                {
                    if (!user.IsVerified)
                    {
                        TempData["ErrorMessage"] = "Account pending verification. Please contact admin.";
                        return View(model);
                    }

                    user.IsOnline = Users.OnlineStatus.Online;
                    user.LastLoginDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));

                    await SignInUser(user, model.RememberMe);
                    _logger.LogInformation("User login successful: {Email}", user.Email);
                    return RedirectToLocal(returnUrl) ?? RedirectToAction("Dashboard", "Pages");
                }else if (staff != null && BCrypt.Net.BCrypt.Verify(model.Password, staff.Password))
                {
                    if (staff.IsActive == AccountStatus.Active)
                    {
                        TempData["ErrorMessage"] = "Account pending verification. Please contact admin.";
                        return View(model);
                    }

                    staff.IsOnline = Staff.OnlineStatus.Online;
                    staff.LastLoginDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));

                    await SignInStaff(staff, model.RememberMe);
                    _logger.LogInformation("staff login successful: {Email}", staff.StaffSEmail);
                    return RedirectToLocal(returnUrl) ?? RedirectToAction("Dashboard", "Pages");
                }

                    // Generic error message for security
                    _logger.LogWarning("Failed login attempt for email: {Email}", model.Email);
                ModelState.AddModelError(string.Empty, "Invalid email or password");
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error for email: {Email}", model.Email);
                ModelState.AddModelError(string.Empty, "An error occurred during login");
                return View(model);
            }
        }

        private async Task SignInAdmin(Admin admin, bool rememberMe)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()),
                new Claim(ClaimTypes.Email, admin.Email),
                new Claim(ClaimTypes.Name, $"{admin.FirstName} {admin.LastName}"),
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim("AccountType", "Admin")
            };

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : null,
                AllowRefresh = rememberMe
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
                authProperties);
        }

        private async Task SignInUser(Users user, bool rememberMe)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
                new Claim(ClaimTypes.Role, user.UserRole),
                new Claim("BusinessName", user.BusinessName),
                new Claim("AccountType", "User"),
                new Claim("IsVerified", user.IsVerified.ToString())
            };

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : null,
                AllowRefresh = rememberMe
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
                authProperties);
        }

        private async Task SignInStaff(Staff staff, bool rememberMe)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, staff.Id.ToString()),
                new Claim(ClaimTypes.Email, staff.StaffSEmail),
                new Claim(ClaimTypes.Name, $"{staff.StaffName}"),
                new Claim(ClaimTypes.Role, staff.Role),
                new Claim("AccessLevel", staff.StaffAccessLevel.ToString()),
                new Claim("AccountType", "User"),
                new Claim("IsVerified", staff.IsActive.ToString())
            };

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : null,
                AllowRefresh = rememberMe
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
                authProperties);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            _logger.LogInformation("User logged out.");
            return RedirectToAction("Index", "Home");
        }

        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return null;
        }

        private IActionResult RedirectToDashboard()
        {
            if (User.IsInRole("Admin"))
            {
                return RedirectToAction("Index", "Admin");
            }
            return RedirectToAction("Dashboard", "Pages");
        }

        //public IActionResult ForgotPassword()
        //{
        //    return View();
        //}

    }
}
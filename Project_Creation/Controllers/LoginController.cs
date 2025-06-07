using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Project_Creation.Data;
using Project_Creation.DTO;
using Project_Creation.Models.Entities;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Users = Project_Creation.Models.Entities.Users;
//using Project_Creation.Models;

namespace Project_Creation.Controllers
{
    public class LoginController : Controller
    {
        private readonly AuthDbContext _context;
        private readonly ILogger<LoginController> _logger;
        private readonly IEmailService _emailService;

        public LoginController(AuthDbContext context, IEmailService emailService, ILogger<LoginController> logger)
        {
            _context = context;
            _logger = logger;
            _emailService = emailService;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
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
                _logger.LogInformation("Starting login process for {Email}", model.Email);
                
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
                var admin = await _context.Admin.FirstOrDefaultAsync(a => a.Email == model.Email);
                var staff = await _context.Staffs.FirstOrDefaultAsync(s => s.StaffSEmail == model.Email);

                _logger.LogInformation("Login attempt for {Email}", model.Email);

                if (admin != null && BCrypt.Net.BCrypt.Verify(model.Password, admin.Password))
                {
                    _logger.LogInformation("Admin credentials verified for {Email}", model.Email);
                    
                    // Don't sign in yet if 2FA is enabled
                    if (admin.TwoFactorAuthentication)
                    {
                        await TrackUserDevice(admin.Id, HttpContext);
                        return await TwoFactorAuthentication(true, admin.Email);
                    }
                    
                    await SignInAdmin(admin, model.RememberMe);
                    _logger.LogInformation("Admin login successful: {Email}", admin.Email);
                    await TrackUserDevice(admin.Id, HttpContext);
                    return RedirectToAction("Index", "Admin");
                }
                else if (user != null && BCrypt.Net.BCrypt.Verify(model.Password, user.Password))
                {
                    _logger.LogInformation("User credentials verified for {Email}", model.Email);
                    
                    if (!user.IsVerified)
                    {
                        TempData["ErrorMessage"] = "Account pending verification. Please contact admin.";
                        return View(model);
                    }
                    
                    // Check account status
                    if (user.AccountStatus == Users.AccountStatuss.Suspended)
                    {
                        TempData["ErrorMessage"] = "Your account has been suspended. Please contact admin for assistance.";
                        _logger.LogWarning("Login attempt for suspended account: {Email}", model.Email);
                        return View(model);
                    }
                    
                    if (user.AccountStatus == Users.AccountStatuss.Deactivated)
                    {
                        TempData["ErrorMessage"] = "Your account has been deactivated. Please contact admin for assistance.";
                        _logger.LogWarning("Login attempt for deactivated account: {Email}", model.Email);
                        return View(model);
                    }

                    // Don't sign in yet if 2FA is enabled
                    if (user.TwoFactorAuthentication)
                    {
                        await TrackUserDevice(user.Id, HttpContext);
                        return await TwoFactorAuthentication(true, user.Email);
                    }

                    await SignInUser(user, model.RememberMe);
                    _logger.LogInformation("User login successful: {Email}", user.Email);
                    await TrackUserDevice(user.Id, HttpContext);
                    
                    // Ensure cookies are properly set before redirect
                    if (!HttpContext.Response.Headers.ContainsKey("Set-Cookie"))
                    {
                        _logger.LogWarning("No cookies were set during login for {Email}", model.Email);
                    }
                    
                    return RedirectToAction("Dashboard", "Pages");
                }
                else if (staff != null && BCrypt.Net.BCrypt.Verify(model.Password, staff.Password))
                {
                    _logger.LogInformation("Staff credentials verified for {Email}", model.Email);

                    var BOId = staff.BOId;
                    var staffBO = await _context.Users.FirstOrDefaultAsync(u => u.Id == BOId);
                    if (staffBO == null)
                    {
                        TempData["ErrorMessage"] = "Business owner not found. Please contact admin.";
                        return View(model);
                    }
                    if (staffBO.AccountStatus == Users.AccountStatuss.Suspended)
                    {
                        TempData["ErrorMessage"] = "Your Business Owner account has been suspended. Please contact admin for assistance.";
                        _logger.LogWarning("Login attempt for suspended account: {Email}", model.Email);
                        return View(model);
                    }
                    if (staffBO.AccountStatus == Users.AccountStatuss.Deactivated)
                    {
                        TempData["ErrorMessage"] = "Your Business Owner account has been deactivated. Please contact admin for assistance.";
                        _logger.LogWarning("Login attempt for deactivated account: {Email}", model.Email);
                        return View(model);
                    }

                    if (staff.IsActive == AccountStatus.Pending)
                    {
                        TempData["ErrorMessage"] = "Account is pending Go to your email to set a passowrd!";
                        return View(model);
                    }

                    if (staff.IsActive == AccountStatus.Suspended)
                    {
                        TempData["ErrorMessage"] = "Account is Suspended Contact your business owner!";
                        return View(model);
                    }

                    // Don't sign in yet if 2FA is enabled
                    if (staff.TwoFactorAuthentication)
                    {
                        await TrackUserDevice(staff.Id, HttpContext);
                        return await TwoFactorAuthentication(true, staff.StaffSEmail);
                    }

                    await SignInStaff(staff, model.RememberMe);
                    _logger.LogInformation("Staff login successful: {Email}", staff.StaffSEmail);
                    await TrackUserDevice(staff.Id, HttpContext);
                    return RedirectToAction("Dashboard", "Pages");
                }

                _logger.LogWarning("Failed login attempt for email: {Email}", model.Email);
                ModelState.AddModelError(string.Empty, "Invalid email or password");
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error for email {Email}: {ErrorMessage}", model.Email, ex.Message);
                
                // Add more detailed exception logging for hosted environment
                if (ex.InnerException != null)
                {
                    _logger.LogError("Inner exception: {Message}", ex.InnerException.Message);
                }
                
                ModelState.AddModelError(string.Empty, "An error occurred during login");
                return View(model);
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult TwoFactorAuthentication()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return View();
            }
            return RedirectToDashboard();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyTwoFactorAuthentication(Models.Entities.TwoFactorAuthentication model)
        {
            if (!ModelState.IsValid)
            {
                return View("TwoFactorAuthentication", model);
            }

            try
            {
                var email = TempData["TwoFactorEmail"]?.ToString();
                if (string.IsNullOrEmpty(email))
                {
                    return RedirectToAction("Login");
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                var admin = await _context.Admin.FirstOrDefaultAsync(a => a.Email == email);
                var staff = await _context.Staffs.FirstOrDefaultAsync(s => s.StaffSEmail == email);
                var modelCode = int.TryParse(model.code, out var userId) ? userId : 0;

                _logger.LogDebug($"modelCode: {modelCode}");

                if (user != null && user.TwoFactorAuthenticationCode == userId)
                {
                    user.TwoFactorAuthenticationCode = null;
                    await _context.SaveChangesAsync();
                    await SignInUser(user, false); // Sign in after successful 2FA
                    return RedirectToAction("Dashboard", "Pages");
                }
                else if (admin != null && admin.TwoFactorAuthenticationCode == userId)
                {
                    admin.TwoFactorAuthenticationCode = null;
                    await _context.SaveChangesAsync();
                    await SignInAdmin(admin, false); // Sign in after successful 2FA
                    return RedirectToAction("Index", "Admin");
                }
                else if (staff != null && staff.TwoFactorAuthenticationCode == userId)
                {
                    staff.TwoFactorAuthenticationCode = null;
                    await _context.SaveChangesAsync();
                    await SignInStaff(staff, false); // Sign in after successful 2FA
                    return RedirectToAction("Dashboard", "Pages");
                }

                ModelState.AddModelError("code", "Invalid verification code");
                return View("TwoFactorAuthentication", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying two-factor authentication code");
                ModelState.AddModelError(string.Empty, "An error occurred during verification");
                return View("TwoFactorAuthentication", model);
            }
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendTwoFactorCode()
        {
            try
            {
                var email = TempData["TwoFactorEmail"]?.ToString();
                if (string.IsNullOrEmpty(email))
                {
                    return Json(new { success = false, message = "Session expired. Please login again." });
                }

                var code = new Random().Next(100000, 999999);
                await _emailService.SendEmail(email, "Two-Factor Authentication Code", $"Your verification code is: {code}");

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                var admin = await _context.Admin.FirstOrDefaultAsync(a => a.Email == email);
                var staff = await _context.Staffs.FirstOrDefaultAsync(s => s.StaffSEmail == email);

                if (user != null)
                {
                    user.TwoFactorAuthenticationCode = code;
                }
                else if (admin != null)
                {
                    admin.TwoFactorAuthenticationCode = code;
                }
                else if (staff != null)
                {
                    staff.TwoFactorAuthenticationCode = code;
                }

                await _context.SaveChangesAsync();
                TempData["TwoFactorEmail"] = email;
                TempData["ErrorMessage"] = "A new verification code has been sent to your email.";

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resending two-factor authentication code");
                return Json(new { success = false, message = "Failed to resend code. Please try again." });
            }
        }

        private async Task<IActionResult> TwoFactorAuthentication(bool twoFactorAuthentication, string email)
        {
            _logger.LogDebug($"twoFactorAuthentication {twoFactorAuthentication}");
            if (twoFactorAuthentication)
            {
                var code = new Random().Next(100000, 999999);
                await _emailService.SendEmail(email, "Two-Factor Authentication Code", $"Your verification code is: {code}");

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                var admin = await _context.Admin.FirstOrDefaultAsync(a => a.Email == email);
                var staff = await _context.Staffs.FirstOrDefaultAsync(s => s.StaffSEmail == email);

                if (user != null)
                {
                    user.TwoFactorAuthenticationCode = code;
                }
                else if (admin != null)
                {
                    admin.TwoFactorAuthenticationCode = code;
                }
                else if (staff != null)
                {
                    staff.TwoFactorAuthenticationCode = code;
                }

                await _context.SaveChangesAsync();
                TempData["TwoFactorEmail"] = email;
                TempData["WarningMessage"] = "Please check your email for the verification code.";

                // Sign out any existing authentication
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                
                // Redirect to the TwoFactorAuthentication view
                return View("TwoFactorAuthentication");
            }

            return RedirectToDashboard();
        }

        private async Task SignInAdmin(Admin admin, bool rememberMe)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, admin.Id.ToString()),
                new(ClaimTypes.Email, admin.Email),
                new(ClaimTypes.Name, $"{admin.FirstName} {admin.LastName}"),
                new(ClaimTypes.Role, "Admin"),
                new("AccountType", "Admin")
            };

            await CreateAuthenticationTicket(claims, rememberMe);

            //admin.LastLoginDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        private async Task SignInUser(Users user, bool rememberMe)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
                new(ClaimTypes.Role, user.UserRole),
                new("AccessLevel", "BusinessOwner"),
                new("BusinessName", user.BusinessName),
                new("CategoryOfBusiness", user.CategoryOfBusiness),
                new("AccountType", "User"),
                new("IsVerified", user.IsVerified.ToString()),
                new("AccountStatus", user.AccountStatus.ToString())
            };

            await CreateAuthenticationTicket(claims, rememberMe);

            user.IsOnline = Users.OnlineStatus.Online;
            user.LastLoginDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        private async Task SignInStaff(Staff staff, bool rememberMe)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, staff.Id.ToString()),
                new(ClaimTypes.Email, staff.StaffSEmail),
                new(ClaimTypes.Name, staff.StaffName),
                new(ClaimTypes.Role, staff.Role),
                new("AccessLevel", staff.StaffAccessLevel.ToString()),
                new("AccountType", "Staff"),
                new("BOId", staff.BOId.ToString()),
                new("IsVerified", staff.IsActive.ToString())
            };

            await CreateAuthenticationTicket(claims, rememberMe);

            staff.IsOnline = Staff.OnlineStatus.Online;
            staff.LastLoginDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        private async Task CreateAuthenticationTicket(List<Claim> claims, bool rememberMe)
        {
            try
            {
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = rememberMe,
                    ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(2),
                    AllowRefresh = true,
                    // Ensure the redirect URI is valid for cross-domain scenarios
                    RedirectUri = "/"
                };

                var claimsIdentity = new ClaimsIdentity(
                    claims, CookieAuthenticationDefaults.AuthenticationScheme);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);
                
                // Log cookie information for debugging
                _logger.LogInformation("Authentication cookies created successfully");
                
                if (HttpContext.Response.Headers.TryGetValue("Set-Cookie", out var cookies))
                {
                    _logger.LogInformation("Set-Cookie headers found: {Count}", cookies.Count);
                    foreach (var cookie in cookies)
                    {
                        // Add to response headers manually to ensure proper delivery
                        HttpContext.Response.Headers.Append("Set-Cookie", cookie);
                    }
                }
                else
                {
                    _logger.LogWarning("No Set-Cookie headers found after authentication");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating authentication ticket: {Message}", ex.Message);
                throw;
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();

            foreach (var cookie in Request.Cookies.Keys)
            {
                Response.Cookies.Delete(cookie);
            }

            _logger.LogInformation("User logged out.");
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> CheckAccountStatus()
        {
            try
            {
                // Get the user ID from claims
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                {
                    return Json(new { valid = true });  // No user ID found, assume valid
                }
                
                // Get the account type from claims
                var accountType = User.FindFirst("AccountType")?.Value;
                if (accountType == "User")
                {
                    // Check business owner account status
                    var user = await _context.Users.FindAsync(userId);
                    if (user == null)
                    {
                        return Json(new { valid = false, reason = "Account not found" });
                    }
                    
                    if (user.AccountStatus == Users.AccountStatuss.Suspended)
                    {
                        return Json(new { valid = false, reason = "Your account has been suspended. Please contact admin for assistance." });
                    }
                    
                    if (user.AccountStatus == Users.AccountStatuss.Deactivated)
                    {
                        return Json(new { valid = false, reason = "Your account has been deactivated. Please contact admin for assistance." });
                    }
                }
                
                return Json(new { valid = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking account status");
                return Json(new { valid = true });  // On error, assume valid to prevent false lockouts
            }
        }

        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }

        private IActionResult RedirectToDashboard()
        {
            try
            {
                // Check if the request comes from the somee.com hosting environment
                var isSomeeHosting = HttpContext.Request.Host.Value.Contains("somee.com") ||
                                    HttpContext.Request.Headers["X-Forwarded-Host"].ToString().Contains("somee.com");
                
                _logger.LogInformation("Redirecting to dashboard. IsSomeeHosting: {IsSomeeHosting}, User Role: {Role}", 
                    isSomeeHosting, User.IsInRole("Admin") ? "Admin" : "User/Staff");
                
                // Set a TempData flag for handling redirect issues
                TempData["RedirectFromLogin"] = "true";
                
                if (User.IsInRole("Admin"))
                {
                    var url = Url.Action("Index", "Admin");
                    _logger.LogInformation("Redirecting admin to URL: {Url}", url);
                    return Redirect(url);
                }
                
                var dashboardUrl = Url.Action("Dashboard", "Pages");
                _logger.LogInformation("Redirecting user to URL: {Url}", dashboardUrl);
                return Redirect(dashboardUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RedirectToDashboard: {Message}", ex.Message);
                // Fallback to simple redirect if there's an error
                return User.IsInRole("Admin") 
                    ? RedirectToAction("Index", "Admin") 
                    : RedirectToAction("Dashboard", "Pages");
            }
        }

        private async Task TrackUserDevice(int userId, HttpContext httpContext)
        {
            try
            {
                var userAgent = httpContext.Request.Headers["User-Agent"].ToString();
                var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
                
                // Generate a unique device ID based on user agent and IP
                var deviceId = GenerateDeviceId(userAgent, ipAddress);
                
                // Parse user agent to get browser and OS info
                var browserInfo = ParseUserAgent(userAgent);
                
                // Check if this is a new device
                var existingDevice = await _context.UserDevices
                    .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceId == deviceId);

                if (existingDevice == null)
                {
                    // This is a new device
                    var newDevice = new UserDevice
                    {
                        UserId = userId,
                        DeviceId = deviceId,
                        DeviceName = $"{browserInfo.BrowserName} on {browserInfo.OperatingSystem}",
                        BrowserName = browserInfo.BrowserName,
                        BrowserVersion = browserInfo.BrowserVersion,
                        OperatingSystem = browserInfo.OperatingSystem,
                        IpAddress = ipAddress,
                        LastLogin = DateTime.UtcNow,
                        IsTrusted = false
                    };

                    _context.UserDevices.Add(newDevice);
                    await _context.SaveChangesAsync();

                    // Check if user has login alerts enabled
                    var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                    if (user?.AllowLoginAlerts == true)
                    {
                        // Send email notification about new device
                        var emailBody = $@"
                            <h3>New Device Login Alert</h3>
                            <p>Your account was accessed from a new device:</p>
                            <ul>
                                <li>Browser: {browserInfo.BrowserName} {browserInfo.BrowserVersion}</li>
                                <li>Operating System: {browserInfo.OperatingSystem}</li>
                                <li>IP Address: {ipAddress}</li>
                                <li>Time: {TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"))}</li>
                            </ul>
                            <p>If this wasn't you, please secure your account immediately.</p>";

                        await _emailService.SendEmail(
                            user.Email,
                            "New Device Login Alert",
                            emailBody,
                            true
                        );
                    }
                }
                else
                {
                    // Update last login time for existing device
                    existingDevice.LastLogin = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking user device for user {UserId}", userId);
            }
        }

        private string GenerateDeviceId(string userAgent, string ipAddress)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var inputBytes = System.Text.Encoding.UTF8.GetBytes($"{userAgent}{ipAddress}");
                var hashBytes = sha256.ComputeHash(inputBytes);
                return Convert.ToBase64String(hashBytes);
            }
        }

        private (string BrowserName, string BrowserVersion, string OperatingSystem) ParseUserAgent(string userAgent)
        {
            // Simple parsing for common browsers and OS
            string browserName = "Unknown";
            string browserVersion = "Unknown";
            string operatingSystem = "Unknown";

            if (userAgent.Contains("Chrome"))
            {
                browserName = "Chrome";
                var versionIndex = userAgent.IndexOf("Chrome/") + 7;
                browserVersion = userAgent.Substring(versionIndex, userAgent.IndexOf(" ", versionIndex) - versionIndex);
            }
            else if (userAgent.Contains("Firefox"))
            {
                browserName = "Firefox";
                var versionIndex = userAgent.IndexOf("Firefox/") + 8;
                browserVersion = userAgent.Substring(versionIndex, userAgent.IndexOf(" ", versionIndex) - versionIndex);
            }
            else if (userAgent.Contains("Safari"))
            {
                browserName = "Safari";
                var versionIndex = userAgent.IndexOf("Version/") + 8;
                browserVersion = userAgent.Substring(versionIndex, userAgent.IndexOf(" ", versionIndex) - versionIndex);
            }
            else if (userAgent.Contains("Edge"))
            {
                browserName = "Edge";
                var versionIndex = userAgent.IndexOf("Edge/") + 5;
                browserVersion = userAgent.Substring(versionIndex, userAgent.IndexOf(" ", versionIndex) - versionIndex);
            }

            if (userAgent.Contains("Windows"))
                operatingSystem = "Windows";
            else if (userAgent.Contains("Mac OS"))
                operatingSystem = "Mac OS";
            else if (userAgent.Contains("Linux"))
                operatingSystem = "Linux";
            else if (userAgent.Contains("Android"))
                operatingSystem = "Android";
            else if (userAgent.Contains("iOS"))
                operatingSystem = "iOS";

            return (browserName, browserVersion, operatingSystem);
        }
    }
}
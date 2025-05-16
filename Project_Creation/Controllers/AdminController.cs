using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Creation.Data;
using Project_Creation.DTO;
using Project_Creation.Models.Entities;
using static Project_Creation.Models.Entities.Users;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly AuthDbContext _context;
    private readonly ILogger<AdminController> _logger;
    private readonly IEmailService _emailService;

    public AdminController(AuthDbContext context, IEmailService emailService, ILogger<AdminController> logger)
    {
        _context = context;
        _logger = logger;
        _emailService = emailService;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var users = await _context.Users
                .Where(u => u.UserRole != "Admin")
                .OrderBy(u => u.RegistrationDate)
                .ToListAsync();

            var distinctCategories = await _context.Users
                .Where(u => !string.IsNullOrEmpty(u.CategoryOfBusiness))
                .Where(u => u.IsVerified == true)
                .Select(u => u.CategoryOfBusiness)
                .Distinct()
                .ToListAsync();

            var viewModel = new AdminDashboardViewModel
            {
                Step1_Applicants = users.Where(u => !u.IsVerified).ToList(),
                Step2_Applicants = users.Where(u => u.MarkerPlaceStatus == MarketplaceStatus.Requesting).ToList(),
                AllUsers = users.ToList(),
                BusinessCategories = distinctCategories.ToList()
            };

            return View("~/Views/Admin_Pages/dashboard.cshtml", viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading admin dashboard");
            return StatusCode(500);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyUser(int id, string customMessage, string verificationLink)
    {
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found" });
            }

            user.IsVerified = true;
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                user = new
                {
                    id = user.Id,
                    name = $"{user.FirstName} {user.LastName}",
                    email = user.Email,
                    businessName = user.BusinessName,
                    verifiedDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore")).ToString("d"),
                    customMessage = customMessage,
                    verificationLink = verificationLink,
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying user");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetUserDetails(int id)
    {
        var user = await _context.Users
            .Include(u => u.AdditionalInfo)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return NotFound();
        }
        var viewModel = new UserDetailsViewModel
        {
            User = user,
            AdditionInfo = user.AdditionalInfo
        };
        return PartialView("~/Views/Admin_Pages/_UserDetailsPartial.cshtml", viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnverifyUser(int id)
    {
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found" });
            }

            user.IsVerified = false;
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                user = new
                {
                    id = user.Id,
                    name = $"{user.FirstName} {user.LastName}",
                    email = user.Email,
                    businessName = user.BusinessName
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unverifying user");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    [HttpPost]
    [Route("Admin/ToggleUserStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUserStatus(int id, bool isVerified)
    {
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found" });
            }

            var previousStatus = user.IsVerified;

            user.IsVerified = isVerified;
            await _context.SaveChangesAsync();

            string subject = "Your Account Status Has Changed";
            string message = $"<p>Dear {user.FirstName},</p><br>" +
                "<p>Your Account access status has changed:<br>" +
                $"Previous status: <bold>{previousStatus}</bold><br>" +
                $"New status: <bold>{user.IsVerified}</bold></p>" +
                "<p>If you have any questions, please contact our support team.</p><br>" +
                "<p>Best regards,<br>The Marketplace Team</p>";

            await _emailService.SendEmailToBO(user, subject, message, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unverifying user");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
        return Json(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AllowMarketplace(int id)
    {
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found" });
            }

            user.MarkerPlaceStatus = MarketplaceStatus.Approved;
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                user = new
                {
                    id = user.Id,
                    name = $"{user.FirstName} {user.LastName}",
                    email = user.Email,
                    businessName = user.BusinessName
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error allowing marketplace access");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisallowMarketplace(int id)
    {
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found" });
            }

            user.MarkerPlaceStatus = MarketplaceStatus.Rejected;
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                user = new
                {
                    id = user.Id,
                    name = $"{user.FirstName} {user.LastName}",
                    email = user.Email,
                    businessName = user.BusinessName
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disallowing marketplace access");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("Admin/ChangeUserMarketplaceStatus")]
    public async Task<IActionResult> ChangeUserMarketPlaceStatus(string id, string MarkerPlaceStatus2)
    {
        try
        {
            if (!int.TryParse(id, out var userId))
            {
                _logger.LogWarning("Invalid user ID format in ChangeUserMarketPlaceStatus: {Id}", id);
                return Json(new { success = false, message = "Invalid user ID" });
            }

            if (string.IsNullOrEmpty(MarkerPlaceStatus2))
            {
                _logger.LogWarning("Marketplace status is null or empty");
                return Json(new { success = false, message = "Marketplace status is required" });
            }

            _logger.LogDebug("Parsed user ID: {UserId}", userId);
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User with ID {Id} not found", userId);
                return Json(new { success = false, message = "User not found" });
            }

            var previousStatus = user.MarkerPlaceStatus;

            // Try parsing as enum name first
            if (!Enum.TryParse<MarketplaceStatus>(MarkerPlaceStatus2, true, out var parsedStatus))
            {
                // If that fails, try parsing as numeric value
                if (int.TryParse(MarkerPlaceStatus2, out var numericStatus) &&
                    Enum.IsDefined(typeof(MarketplaceStatus), numericStatus))
                {
                    parsedStatus = (MarketplaceStatus)numericStatus;
                }
                else
                {
                    _logger.LogWarning("Invalid MarketplaceStatus value: {MarkerPlaceStatus}", MarkerPlaceStatus2);
                    return Json(new
                    {
                        success = false,
                        message = "Invalid marketplace status value. Valid values are: Pending (0), Requesting (1), Approved (2), Rejected (3)"
                    });
                }
            }

            user.MarkerPlaceStatus = parsedStatus;
            await _context.SaveChangesAsync();

            if (previousStatus != parsedStatus)
            {
                string subject = "Your Marketplace Status Has Changed";
                string message = $"<p>Dear {user.FirstName},</p><br>" +
                    "<p>Your marketplace access status has changed:<br>" +
                    $"Previous status: <bold>{previousStatus}</bold><br>" +
                    $"New status: <bold>{parsedStatus}</bold></p>" +
                    "<p>If you have any questions, please contact our support team.</p><br>" +
                    "<p>Best regards,<br>The Marketplace Team</p>";

                await _emailService.SendEmailToBO(user, subject, message, true);
            }

            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing user status");
            return Json(new { success = false, message = "Internal server error" });
        }
    }

    [HttpPost]
    [Route("Admin/ChangeUserStatus")]  // Explicit route
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeUserStatus(int id, bool isVerified, int MarkerPlaceStatus2)
    {
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                _logger.LogWarning("User with ID {Id} not found in ChangeUserStatus:", id);
                return Json(new { success = false, message = "User not found" });
            }

            _logger.LogDebug("User found. Current MarkerPlaceStatus: {Status}, IsVerified: {IsVerified}",
                user.MarkerPlaceStatus, user.IsVerified);

            var previousStatus = user.MarkerPlaceStatus;
            var previousVerification = user.IsVerified;

            user.IsVerified = isVerified;
            user.MarkerPlaceStatus = (MarketplaceStatus)MarkerPlaceStatus2;

            _logger.LogDebug("User status updated. Saving changes to database.");
            await _context.SaveChangesAsync();
            _logger.LogDebug("Changes saved successfully.");

            if (previousVerification != isVerified)
            {
                _logger.LogDebug("Status or verification changed. Sending notification email.");
                await SendStatusChangeEmail(user, previousStatus.ToString(), previousVerification);
            }

            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing user status");
            return Json(new { success = false, message = "Internal server error" });
        }
    }

    private async Task SendStatusChangeEmail(Users user, string previousStatus, bool previousVerification)
    {
        try
        {
            string subject = "Your Marketplace Status Has Changed";
            string message = $"Dear {user.FirstName},\n\n";

            if (previousVerification != user.IsVerified)
            {
                message += user.IsVerified
                    ? "Your account has been verified.\n"
                    : "Your account verification has been revoked.\n";
            }

            if (previousStatus != user.MarkerPlaceStatus.ToString())
            {
                message += "Your marketplace access status has changed:\n";
                message += $"Previous status: {previousStatus}\n";
                message += $"New status: {user.MarkerPlaceStatus}\n\n";
            }

            message += "If you have any questions, please contact our support team.\n\n";
            message += "Best regards,\nThe Marketplace Team";

            await _emailService.SendEmailToBO(user, subject, message, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send status change email");
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetUserStatusJson(int id)
    {
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found" });
            }
            return Json(new
            {
                success = true,
                id = user.Id,
                isVerified = user.IsVerified,
                marketplaceStatus = user.MarkerPlaceStatus.ToString(),
                name = user.FirstName + " " + user.LastName,
                email = user.Email
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user status");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectMarketplace(int id)
    {
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found" });
            }

            // Get the rejection reason from form data
            var form = await HttpContext.Request.ReadFormAsync();
            var rejectionReason = form["rejectionReason"].ToString();

            user.MarkerPlaceStatus = MarketplaceStatus.Rejected;
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                user = new
                {
                    id = user.Id,
                    name = $"{user.FirstName} {user.LastName}",
                    email = user.Email,
                    businessName = user.BusinessName,
                    rejectionReason = rejectionReason
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting marketplace access");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectUser(int id)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var user = await _context.Users
                .Include(u => u.AdditionalInfo)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found" });
            }

            // Get the rejection reason from form data
            var form = await HttpContext.Request.ReadFormAsync();
            var rejectionReason = form["rejectionReason"].ToString();

            // Remove related AdditionalInfo first if it exists
            if (user.AdditionalInfo != null)
            {
                _context.UsersAdditionInfo.Remove(user.AdditionalInfo);
                await _context.SaveChangesAsync();
            }

            // Then remove the user
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return Json(new
            {
                success = true,
                user = new
                {
                    id = user.Id,
                    name = $"{user.FirstName} {user.LastName}",
                    email = user.Email,
                    businessName = user.BusinessName,
                    rejectionReason = rejectionReason
                }
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error rejecting user");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }
}
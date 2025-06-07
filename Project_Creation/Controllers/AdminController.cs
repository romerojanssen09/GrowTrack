using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Creation.Controllers;
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
                Step2_Applicants = users.Where(u => u.MarkerPlaceStatus == MarketplaceStatus.AwaitingApproval).ToList(),
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

            // Send verification email notification to business owner
            try
            {
                // Create the registration link with the user's ID as a query parameter
                string registrationLink = $"{Request.Scheme}://{Request.Host}/Register/SecondRegistration?userId={id}";

                string subject = "Your Account Has Been Verified";
                string message = $"<p>Dear {user.FirstName},</p>" +
                    "<p>Congratulations! Your business owner account has been verified and approved.</p>" +
                    "<p>You can now log in and access the platform with all the features available to your account.</p>" +
                    $"<p>To complete your registration process, please click the button below:</p>" +
                    $"<p style='text-align: center; margin: 30px 0;'>" +
                    $"<a href='{registrationLink}' style='background-color: #4CAF50; color: white; padding: 12px 20px; text-decoration: none; border-radius: 4px; font-weight: bold; display: inline-block;'>Complete Registration</a>" +
                    $"</p>" +
                    "<p>If you have any questions, please contact our support team.</p>" +
                    "<p>Best regards,<br>The GrowTrack Team</p>";

                await _emailService.SendEmailToBO(user, subject, message, true);
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Failed to send verification email to user {UserId}", id);
                // Continue execution even if email fails
            }

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

            // Get the status change reason from form data
            var form = await HttpContext.Request.ReadFormAsync();
            var statusReason = form["statusReason"].ToString();
            if (string.IsNullOrWhiteSpace(statusReason))
            {
                statusReason = "No specific reason provided";
            }

            user.IsVerified = isVerified;
            await _context.SaveChangesAsync();

            string subject = "Your Account Status Has Changed";
            string message = $"<p>Dear {user.FirstName},</p>" +
                "<p>Your Account access status has changed:<br>" +
                $"<strong>Previous status:</strong> {(previousStatus ? "Verified" : "Unverified")}<br>" +
                $"<strong>New status:</strong> {(user.IsVerified ? "Verified" : "Unverified")}</p>" +
                $"<p><strong>Reason:</strong> {statusReason}</p>";
                
            if (user.IsVerified)
            {
                message += "<p>You now have full access to your account features.</p>";
                
                // Create notification for verification
                await NotificationsController.CreateNotification(
                    _context,
                    id,
                    "Account Verified",
                    "Your account has been verified. You now have full access to the platform.",
                    NotificationType.System,
                    "/Profile/Index",
                    isForBusinessOwner: true
                );
            }
            else
            {
                message += "<p>Your account access has been restricted. Some features may not be available.</p>";
                
                // Create notification for unverification
                await NotificationsController.CreateNotification(
                    _context,
                    id,
                    "Account Verification Revoked",
                    $"Your account verification has been revoked. Reason: {statusReason}",
                    NotificationType.System,
                    "/Profile/Index",
                    isForBusinessOwner: true
                );
            }
            
            message += "<p>If you have any questions, please contact our support team.</p>" +
                "<p>Best regards,<br>The GrowTrack Team</p>";

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

            user.MarkerPlaceStatus = MarketplaceStatus.Authorized;
            await _context.SaveChangesAsync();
            
            // Create notification for the user
            await NotificationsController.CreateNotification(
                _context,
                id,
                "Marketplace Access Approved",
                "Your request for marketplace access has been approved. You can now publish your products.",
                NotificationType.System,
                "/Marketplace/Index",
                isForBusinessOwner: true
            );
            
            // Send email notification to the user
            try
            {
                string subject = "Your Marketplace Access Request Has Been Approved";
                string message = $"<p>Dear {user.FirstName},</p>" +
                    "<p>Congratulations! Your request for marketplace access has been approved.</p>" +
                    "<p>You can now publish your products in our marketplace and start selling to customers.</p>" +
                    "<p>To get started with publishing your products, please visit your marketplace dashboard.</p>" +
                    "<p>If you have any questions, please contact our support team.</p>" +
                    "<p>Best regards,<br>The GrowTrack Team</p>";

                await _emailService.SendEmailToBO(user, subject, message, true);
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Failed to send marketplace approval email to user {UserId}", id);
                // Continue execution even if email fails
            }

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
            
            // Get the rejection reason from form data
            var form = await HttpContext.Request.ReadFormAsync();
            var rejectionReason = form["rejectionReason"].ToString();
            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                rejectionReason = "No specific reason provided";
            }

            user.MarkerPlaceStatus = MarketplaceStatus.Rejected;
            await _context.SaveChangesAsync();
            
            // Create notification for the user
            await NotificationsController.CreateNotification(
                _context,
                id,
                "Marketplace Access Rejected",
                $"Your marketplace access has been revoked. Reason: {rejectionReason}",
                NotificationType.System,
                "/Profile/Index",
                isForBusinessOwner: true
            );
            
            // Send email notification to the user
            try
            {
                string subject = "Your Marketplace Access Has Been Revoked";
                string message = $"<p>Dear {user.FirstName},</p>" +
                    "<p>We regret to inform you that your marketplace access has been revoked.</p>" +
                    $"<p><strong>Reason:</strong> {rejectionReason}</p>" +
                    "<p>If you have any questions or would like to address the issues mentioned, " +
                    "please contact our support team.</p>" +
                    "<p>Best regards,<br>The GrowTrack Team</p>";

                await _emailService.SendEmailToBO(user, subject, message, true);
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Failed to send marketplace revocation email to user {UserId}", id);
                // Continue execution even if email fails
            }

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

            // Get the rejection reason if status is being set to Rejected
            string statusReason = "";
            var form = await HttpContext.Request.ReadFormAsync();
            statusReason = form["statusReason"].ToString();
            
            if (string.IsNullOrWhiteSpace(statusReason))
            {
                statusReason = "No specific reason provided";
            }

            user.MarkerPlaceStatus = parsedStatus;
            await _context.SaveChangesAsync();

            if (previousStatus != parsedStatus)
            {
                string subject = "Your Marketplace Status Has Changed";
                string message = $"<p>Dear {user.FirstName},</p>" +
                    "<p>Your marketplace access status has changed:</p>" +
                    $"<p><strong>Previous status:</strong> {previousStatus}<br>" +
                    $"<strong>New status:</strong> {parsedStatus}</p>" +
                    $"<p><strong>Reason:</strong> {statusReason}</p>";
                
                // Add specific information based on new status
                if (parsedStatus == MarketplaceStatus.Authorized)
                {
                    message += "<p>Congratulations! You now have access to publish your products in our marketplace.</p>";
                    
                    // Create notification for approval
                    await NotificationsController.CreateNotification(
                        _context,
                        userId,
                        "Marketplace Access Approved",
                        "Your request for marketplace access has been approved. You can now publish your products.",
                        NotificationType.System,
                        "/Marketplace/Index",
                        isForBusinessOwner: true
                    );
                }
                else if (parsedStatus == MarketplaceStatus.Rejected)
                {
                    message += "<p>Unfortunately, your marketplace access request has been rejected.</p>";
                    
                    // Create notification for rejection
                    await NotificationsController.CreateNotification(
                        _context,
                        userId,
                        "Marketplace Access Rejected",
                        $"Your request for marketplace access has been rejected. Reason: {statusReason}",
                        NotificationType.System,
                        "/Profile/Index",
                        isForBusinessOwner: true
                    );
                }
                else if (parsedStatus == MarketplaceStatus.AwaitingApproval)
                {
                    message += "<p>Your marketplace access request is now pending approval.</p>";
                }
                
                message += "<p>If you have any questions, please contact our support team.</p>" +
                    "<p>Best regards,<br>The GrowTrack Team</p>";

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

            // Create notification for the user
            await NotificationsController.CreateNotification(
                _context,
                id,
                "Marketplace Access Rejected",
                $"Your request for marketplace access has been rejected. Reason: {rejectionReason}",
                NotificationType.System,
                "/Profile/Index",
                isForBusinessOwner: true
            );
            
            // Send email notification to the user
            try
            {
                string subject = "Your Marketplace Access Request Has Been Rejected";
                string message = $"<p>Dear {user.FirstName},</p>" +
                    "<p>We regret to inform you that your request for marketplace access has been rejected.</p>" +
                    $"<p><strong>Reason:</strong> {rejectionReason}</p>" +
                    "<p>If you have any questions or would like to address the issues mentioned, " +
                    "please contact our support team or make the necessary changes to your profile.</p>" +
                    "<p>Best regards,<br>The GrowTrack Team</p>";

                await _emailService.SendEmailToBO(user, subject, message, true);
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Failed to send marketplace rejection email to user {UserId}", id);
                // Continue execution even if email fails
            }

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
        try
        {
            // Get the rejection reason from form data - this must be outside the transaction
            var form = await HttpContext.Request.ReadFormAsync();
            var rejectionReason = form["rejectionReason"].ToString();

            // Create an execution strategy
            var strategy = _context.Database.CreateExecutionStrategy();

            // Execute all database operations within the strategy
            await strategy.ExecuteAsync(async () =>
            {
                // Begin transaction
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var user = await _context.Users
                        .Include(u => u.AdditionalInfo)
                        .FirstOrDefaultAsync(u => u.Id == id);

                    if (user == null)
                    {
                        throw new Exception("User not found");
                    }

                    // Send rejection email notification before removing the user
                    try
                    {
                        string subject = "Your Account Application Has Been Rejected";
                        string message = $"<p>Dear {user.FirstName},</p>" +
                            "<p>We regret to inform you that your business owner account application has been rejected.</p>" +
                            $"<p><strong>Reason:</strong> {rejectionReason}</p>" +
                            "<p>If you have any questions or would like to submit a new application with the necessary corrections, " +
                            "please contact our support team.</p>" +
                            "<p>Best regards,<br>The GrowTrack Team</p>";

                        await _emailService.SendEmailToBO(user, subject, message, true);
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError(emailEx, "Failed to send rejection email to user {UserId} with email {Email}", id, user.Email);
                        // Continue execution even if email fails
                    }

                    // Remove related AdditionalInfo first if it exists
                    if (user.AdditionalInfo != null)
                    {
                        _context.UsersAdditionInfo.Remove(user.AdditionalInfo);
                        await _context.SaveChangesAsync();
                    }

                    // Then remove the user
                    _context.Users.Remove(user);
                    await _context.SaveChangesAsync();

                    // Create notification for the user
                    await NotificationsController.CreateNotification(
                        _context,
                        id,
                        "Account Rejected",
                        $"Your account has been rejected by the administrator. Reason: {rejectionReason}",
                        NotificationType.System,
                        "/Profile/Index"
                    );

                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    // The transaction will be rolled back automatically if we don't commit
                    _logger.LogError(ex, "Error in transaction during user rejection process");
                    throw; // Rethrow to handle in outer catch block
                }
            });

            return Json(new
            {
                success = true,
                user = new
                {
                    id = id,
                    rejectionReason = rejectionReason
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting user");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> StaffLimits(int? highlight = null)
    {
        try
        {
            var businessOwners = await _context.Users
                .Where(u => u.UserRole == "BusinessOwner")
                .OrderBy(u => u.BusinessName)
                .Select(u => new BusinessOwnerViewModel
                {
                    Id = u.Id,
                    BusinessName = u.BusinessName,
                    OwnerName = $"{u.FirstName} {u.LastName}",
                    Email = u.Email,
                    StaffLimit = u.StaffLimit,
                    CurrentStaffCount = _context.Staffs.Count(s => s.BOId == u.Id)
                })
                .ToListAsync();

            if (highlight.HasValue)
            {
                ViewBag.HighlightUserId = highlight.Value;
            }

            return View("~/Views/Admin_Pages/StaffLimits.cshtml", businessOwners);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading staff limits page");
            return StatusCode(500);
        }
    }

    public async Task<IActionResult> AccountManagement()
    {
        try
        {
            var businessOwners = await _context.Users
                .Where(u => u.UserRole == "BusinessOwner")
                .OrderBy(u => u.BusinessName)
                .Select(u => new AccountManagementViewModel
                {
                    Id = u.Id,
                    BusinessName = u.BusinessName,
                    OwnerName = $"{u.FirstName} {u.LastName}",
                    Email = u.Email,
                    CategoryOfBusiness = u.CategoryOfBusiness,
                    IsVerified = u.IsVerified,
                    MarketplaceStatus = u.MarkerPlaceStatus,
                    AccountStatus = u.AccountStatus,
                    RegistrationDate = u.RegistrationDate,
                    LastLoginDate = u.LastLoginDate,
                    LastStatusChangeDate = u.LastStatusChangeDate,
                    StaffLimit = u.StaffLimit,
                    CurrentStaffCount = _context.Staffs.Count(s => s.BOId == u.Id)
                })
                .ToListAsync();

            return View("~/Views/Admin_Pages/AccountManagement.cshtml", businessOwners);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading account management page");
            return StatusCode(500);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAccountStatus(int userId, int accountStatus, string statusReason)
    {
        try
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found" });
            }

            // Validate the input
            if (!Enum.IsDefined(typeof(Users.AccountStatuss), accountStatus))
            {
                return BadRequest(new { success = false, message = "Invalid account status" });
            }

            if (string.IsNullOrWhiteSpace(statusReason))
            {
                return BadRequest(new { success = false, message = "Status change reason is required" });
            }

            // Store previous status for notification
            var previousStatus = user.AccountStatus;

            // Update the account status
            user.AccountStatus = (Users.AccountStatuss)accountStatus;
            user.LastStatusChangeDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));
            await _context.SaveChangesAsync();

            // Send notification email to business owner
            try
            {
                string subject = "Your Account Status Has Changed";
                string statusText = user.AccountStatus.ToString();
                string message = $"<p>Dear {user.FirstName},</p>" +
                    $"<p>Your account status has been changed to: <strong>{statusText}</strong>.</p>" +
                    $"<p><strong>Reason:</strong> {statusReason}</p>" +
                    $"<p><strong>What this means:</strong></p>" +
                    $"<ul>";

                switch (user.AccountStatus)
                {
                    case Users.AccountStatuss.Active:
                        message += "<li>You now have full access to the platform</li>" +
                                  "<li>You can log in and use all features available to your account</li>";
                        break;
                    case Users.AccountStatuss.Suspended:
                        message += "<li>Your account has been temporarily suspended</li>" +
                                  "<li>You cannot log in or access the platform until the suspension is lifted</li>" +
                                  "<li>Your account data is preserved</li>";
                        break;
                    case Users.AccountStatuss.Deactivated:
                        message += "<li>Your account has been deactivated</li>" +
                                  "<li>You cannot log in or access the platform</li>" +
                                  "<li>Your business is no longer visible in the marketplace</li>";
                        break;
                }

                message += "</ul>" +
                    "<p>If you have any questions, please contact our support team.</p>" +
                    "<p>Best regards,<br>The GrowTrack Team</p>";

                await _emailService.SendEmailToBO(user, subject, message, true);
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Failed to send account status update email to user {UserId}", userId);
                // Continue execution even if email fails
            }

            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating account status for user {UserId}", userId);
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStaffLimit(int userId, int staffLimit)
    {
        try
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found" });
            }

            // Validate the input
            if (staffLimit < 0)
            {
                return BadRequest(new { success = false, message = "Staff limit cannot be negative" });
            }

            var previousLimit = user.StaffLimit;

            // Update the staff limit
            user.StaffLimit = staffLimit;
            await _context.SaveChangesAsync();

            // Create notification for the user
            string notificationMessage;
            if (staffLimit > previousLimit)
            {
                notificationMessage = $"Your staff account limit has been increased from {previousLimit} to {staffLimit}.";
            }
            else if (staffLimit < previousLimit)
            {
                notificationMessage = $"Your staff account limit has been decreased from {previousLimit} to {staffLimit}.";
            }
            else
            {
                notificationMessage = $"Your staff account limit of {staffLimit} has been confirmed.";
            }

            await NotificationsController.CreateNotification(
                _context,
                userId,
                "Staff Limit Updated",
                notificationMessage,
                NotificationType.System,
                "/Staffs/Index"
            );

            // Send notification email to business owner
            try
            {
                string subject = "Staff Account Limit Updated";
                string message = $"<p>Dear {user.FirstName},</p>" +
                    $"<p>Your staff account limit has been updated to: <strong>{staffLimit}</strong> accounts.</p>" +
                    "<p>If you have any questions, please contact our support team.</p>" +
                    "<p>Best regards,<br>The GrowTrack Team</p>";

                await _emailService.SendEmailToBO(user, subject, message, true);
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Failed to send staff limit update email to user {UserId}", userId);
                // Continue execution even if email fails
            }

            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating staff limit for user {UserId}", userId);
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> BOApproval()
    {
        try
        {
            var pendingUsers = await _context.Users
                .Where(u => u.UserRole == "BusinessOwner" && !u.IsVerified)
                .OrderBy(u => u.RegistrationDate)
                .ToListAsync();

            return View("~/Views/Admin_Pages/BOApproval.cshtml", pendingUsers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading business owner approval page");
            return StatusCode(500);
        }
    }

    [HttpGet]
    public async Task<IActionResult> MarketplaceApproval()
    {
        try
        {
            var marketplaceRequests = await _context.Users
                .Include(u => u.AdditionalInfo)
                .Where(u => u.IsVerified && u.MarkerPlaceStatus == MarketplaceStatus.AwaitingApproval)
                .OrderBy(u => u.AdditionalInfo.SubmissionDate)
                .ToListAsync();

            return View("~/Views/Admin_Pages/MarketplaceApproval.cshtml", marketplaceRequests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading marketplace approval page");
            return StatusCode(500);
        }
    }

    [HttpGet]
    public async Task<IActionResult> BusinessOwners()
    {
        try
        {
            var businessOwners = await _context.Users
                .Where(u => u.UserRole == "BusinessOwner" && u.IsVerified)
                .OrderBy(u => u.BusinessName)
                .ToListAsync();

            return View("~/Views/Admin_Pages/BusinessOwners.cshtml", businessOwners);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading business owners page");
            return StatusCode(500);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkVerifyUsers([FromBody] List<int> userIds)
    {
        try
        {
            if (userIds == null || !userIds.Any())
            {
                return BadRequest(new { success = false, message = "No user IDs provided" });
            }

            var results = new List<object>();

            foreach (var userId in userIds)
            {
                try
                {
                    var user = await _context.Users.FindAsync(userId);
                    if (user == null)
                    {
                        results.Add(new { id = userId, success = false, message = "User not found" });
                        continue;
                    }

                    user.IsVerified = true;
                    await _context.SaveChangesAsync();

                    // Send verification email notification to business owner
                    try
                    {
                        // Create the registration link with the user's ID as a query parameter
                        string registrationLink = $"{Request.Scheme}://{Request.Host}/Register/SecondRegistration?userId={userId}";

                        string subject = "Your Account Has Been Verified";
                        string message = $"<p>Dear {user.FirstName},</p>" +
                            "<p>Congratulations! Your business owner account has been verified and approved.</p>" +
                            "<p>You can now log in and access the platform with all the features available to your account.</p>" +
                            $"<p>To complete your registration process, please click the button below:</p>" +
                            $"<p style='text-align: center; margin: 30px 0;'>" +
                            $"<a href='{registrationLink}' style='background-color: #4CAF50; color: white; padding: 12px 20px; text-decoration: none; border-radius: 4px; font-weight: bold; display: inline-block;'>Complete Registration</a>" +
                            $"</p>" +
                            "<p>If you have any questions, please contact our support team.</p>" +
                            "<p>Best regards,<br>The GrowTrack Team</p>";

                        await _emailService.SendEmailToBO(user, subject, message, true);

                        results.Add(new
                        {
                            id = userId,
                            success = true,
                            name = $"{user.FirstName} {user.LastName}",
                            email = user.Email
                        });
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError(emailEx, "Failed to send verification email to user {UserId}", userId);
                        results.Add(new { id = userId, success = false, message = "Email sending failed" });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error verifying user {UserId}", userId);
                    results.Add(new { id = userId, success = false, message = "Processing error" });
                }
            }

            return Json(new
            {
                success = true,
                processedCount = userIds.Count,
                successCount = results.Count(r => ((dynamic)r).success),
                results = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in bulk user verification");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkRejectUsers([FromBody] BulkRejectModel model)
    {
        try
        {
            if (model == null || model.UserIds == null || !model.UserIds.Any())
            {
                return BadRequest(new { success = false, message = "No user IDs provided" });
            }

            if (string.IsNullOrWhiteSpace(model.RejectionReason))
            {
                return BadRequest(new { success = false, message = "Rejection reason is required" });
            }

            var results = new List<object>();

            foreach (var userId in model.UserIds)
            {
                try
                {
                    // Create an execution strategy for this specific user
                    var strategy = _context.Database.CreateExecutionStrategy();

                    await strategy.ExecuteAsync(async () =>
                    {
                        // Begin transaction
                        using var transaction = await _context.Database.BeginTransactionAsync();
                        try
                        {
                            var user = await _context.Users
                                .Include(u => u.AdditionalInfo)
                                .FirstOrDefaultAsync(u => u.Id == userId);

                            if (user == null)
                            {
                                results.Add(new { id = userId, success = false, message = "User not found" });
                                return;
                            }

                            // Send rejection email notification before removing the user
                            try
                            {
                                string subject = "Your Account Application Has Been Rejected";
                                string message = $"<p>Dear {user.FirstName},</p>" +
                                    "<p>We regret to inform you that your business owner account application has been rejected.</p>" +
                                    $"<p><strong>Reason:</strong> {model.RejectionReason}</p>" +
                                    "<p>If you have any questions or would like to submit a new application with the necessary corrections, " +
                                    "please contact our support team.</p>" +
                                    "<p>Best regards,<br>The GrowTrack Team</p>";

                                await _emailService.SendEmailToBO(user, subject, message, true);
                            }
                            catch (Exception emailEx)
                            {
                                _logger.LogError(emailEx, "Failed to send rejection email to user {UserId}", userId);
                                // Continue execution even if email fails
                            }

                            // Remove related AdditionalInfo first if it exists
                            if (user.AdditionalInfo != null)
                            {
                                _context.UsersAdditionInfo.Remove(user.AdditionalInfo);
                                await _context.SaveChangesAsync();
                            }

                            // Then remove the user
                            _context.Users.Remove(user);
                            await _context.SaveChangesAsync();

                            // Create notification for the user
                            await NotificationsController.CreateNotification(
                                _context,
                                userId,
                                "Account Rejected",
                                $"Your account has been rejected by the administrator. Reason: {model.RejectionReason}",
                                NotificationType.System,
                                "/Profile/Index"
                            );

                            await transaction.CommitAsync();

                            results.Add(new
                            {
                                id = userId,
                                success = true
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error in transaction during rejection for user {UserId}", userId);
                            results.Add(new { id = userId, success = false, message = ex.Message });
                            // The transaction will be rolled back automatically if we don't commit
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error rejecting user {UserId}", userId);
                    results.Add(new { id = userId, success = false, message = "Processing error" });
                }
            }

            return Json(new
            {
                success = true,
                processedCount = model.UserIds.Count,
                successCount = results.Count(r => ((dynamic)r).success),
                results = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in bulk user rejection");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    public class BulkRejectModel
    {
        public List<int> UserIds { get; set; }
        public string RejectionReason { get; set; }
    }

    public async Task<IActionResult> ContactMessages()
    {
        try
        {
            var messages = await _context.ContactMessages
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();

            return View("~/Views/Admin_Pages/ContactMessages.cshtml", messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading contact messages");
            TempData["ErrorMessage"] = "An error occurred while loading contact messages.";
            return RedirectToAction("Index");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkMessageAsRead(int id)
    {
        try
        {
            var message = await _context.ContactMessages.FindAsync(id);
            if (message == null)
            {
                return NotFound();
            }

            message.IsRead = true;
            message.ReadAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));
            await _context.SaveChangesAsync();

            return RedirectToAction("ContactMessages");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking message as read");
            TempData["ErrorMessage"] = "An error occurred while updating the message.";
            return RedirectToAction("ContactMessages");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMessage(int id)
    {
        try
        {
            var message = await _context.ContactMessages.FindAsync(id);
            if (message == null)
            {
                return NotFound();
            }

            _context.ContactMessages.Remove(message);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Message deleted successfully.";
            return RedirectToAction("ContactMessages");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting message");
            TempData["ErrorMessage"] = "An error occurred while deleting the message.";
            return RedirectToAction("ContactMessages");
        }
    }

    [HttpGet]
    public async Task<IActionResult> ViewMessage(int id)
    {
        try
        {
            var message = await _context.ContactMessages.FindAsync(id);
            if (message == null)
            {
                return NotFound();
            }

            // Mark as read if not already
            if (!message.IsRead)
            {
                message.IsRead = true;
                message.ReadAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));
                await _context.SaveChangesAsync();
            }

            return View("~/Views/Admin_Pages/ViewMessage.cshtml", message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error viewing message");
            TempData["ErrorMessage"] = "An error occurred while loading the message.";
            return RedirectToAction("ContactMessages");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplyToMessage(int id, string replyMessage)
    {
        try
        {
            var message = await _context.ContactMessages.FindAsync(id);
            if (message == null)
            {
                return NotFound();
            }

            if (string.IsNullOrEmpty(replyMessage))
            {
                TempData["ErrorMessage"] = "Reply message cannot be empty.";
                return RedirectToAction("ViewMessage", new { id });
            }

            // Get admin info for the reply
            var admin = await _context.Admin.FirstOrDefaultAsync();
            if (admin == null)
            {
                TempData["ErrorMessage"] = "Admin information not found.";
                return RedirectToAction("ViewMessage", new { id });
            }

            // Create email body
            string emailBody = $@"
                <h2>Response to Your Inquiry</h2>
                <p>Dear {message.Name},</p>
                <p>Thank you for contacting GrowTrack. Below is our response to your inquiry:</p>
                <div style='background-color: #f9f9f9; padding: 15px; border-left: 4px solid #304251; margin: 20px 0;'>
                    {replyMessage}
                </div>
                <p>Your original message:</p>
                <div style='background-color: #f9f9f9; padding: 15px; border-left: 4px solid #F3993E; margin: 20px 0;'>
                    <strong>Subject:</strong> {message.Subject}<br>
                    <strong>Message:</strong><br>
                    {message.Message}
                </div>
                <p>If you have any further questions, please don't hesitate to contact us.</p>
                <p>Best regards,<br>
                {admin.FirstName} {admin.LastName}<br>
                GrowTrack Support Team</p>
            ";

            // Send email
            await _emailService.SendEmail(message.Email, $"Re: {message.Subject} - GrowTrack Support", emailBody, true);

            TempData["SuccessMessage"] = "Reply sent successfully.";
            return RedirectToAction("ContactMessages");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error replying to message");
            TempData["ErrorMessage"] = "An error occurred while sending the reply.";
            return RedirectToAction("ViewMessage", new { id });
        }
    }
}
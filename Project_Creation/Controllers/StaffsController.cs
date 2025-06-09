using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Creation.Data;
using Project_Creation.Models.Entities;
using System.Text.Json;
using static Project_Creation.Models.Entities.Users;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using BCrypt.Net;

namespace Project_Creation.Controllers
{
    public class StaffsController : Controller
    {
        private readonly AuthDbContext _context;
        private readonly ILogger<StaffsController> _logger;
        private readonly IEmailService _emailService;
        private readonly IHubContext<RealTimeHub> _hubContext;

        public StaffsController(
            AuthDbContext context,
            ILogger<StaffsController> logger,
            IEmailService emailService,
            IHubContext<RealTimeHub> hubContext)
        {
            _emailService = emailService;
            _logger = logger;
            _context = context;
            _hubContext = hubContext;

            CheckAccess();
        }
        
        RedirectToActionResult CheckAccess()
        {
            // Check if user is authenticated first
            if (User?.Identity?.IsAuthenticated == true && User.FindFirstValue(ClaimTypes.Role) == "Staff")
            {
                return RedirectToAction("Dashboard", "Pages");
            }
            return RedirectToAction("Index");
        }


        // GET: Staffs
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            var isMarketplaceApproved = user != null && user.MarkerPlaceStatus == MarketplaceStatus.Authorized;
            ViewBag.MarketplaceStatus = isMarketplaceApproved;
            
            // Get current staff count and limit
            var currentStaffCount = await _context.Staff.CountAsync(s => s.BOId == userId);
            var staffLimit = user?.StaffLimit ?? 5;
            
            ViewBag.CurrentStaffCount = currentStaffCount;
            ViewBag.StaffLimit = staffLimit;
            ViewBag.LimitReached = currentStaffCount >= staffLimit;

            var staffList = await _context.Staff.Where(s => s.BOId == userId).ToListAsync();
            return View(staffList);
        }

        // GET: Staffs/Details/5
        [HttpGet]
        [Route("Staffs/Details/{id:int}")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var staff = await _context.Staff
                .FirstOrDefaultAsync(m => m.Id == id);
            if (staff == null)
            {
                return NotFound();
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_DetailsPartial", staff);
            }

            return View(staff);
        }

        // POST: Staffs/Create
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Staff staff)
        {
            try
            {
                _logger.LogInformation($"Received staff creation request: {JsonSerializer.Serialize(staff)}");

                // Check if the business owner has reached their staff limit
                var userId = GetCurrentUserId();
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                var currentStaffCount = await _context.Staff.CountAsync(s => s.BOId == userId);
                var staffLimit = user?.StaffLimit ?? 5;

                if (currentStaffCount >= staffLimit)
                {
                    _logger.LogWarning($"Business owner (ID: {userId}) has reached their staff limit ({staffLimit})");
                    return BadRequest(new { message = $"You have reached your staff account limit of {staffLimit}. Please contact the administrator if you need to increase your limit." });
                }

                // Remove User from ModelState since it's handled by the controller
                ModelState.Remove("User");

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning($"ModelState is invalid: {string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))}");
                    return BadRequest(ModelState);
                }

                // Check if the email already exists in the database
                var existingStaff = await _context.Staff
                    .FirstOrDefaultAsync(s => s.StaffSEmail == staff.StaffSEmail);
                if (existingStaff != null)
                {
                    _logger.LogWarning($"Email {staff.StaffSEmail} already exists in Staff table");
                    ModelState.AddModelError("StaffSEmail", "Email already exists.");
                    return BadRequest(ModelState);
                }

                var existingAdmin = await _context.Admin
                    .FirstOrDefaultAsync(s => s.Email == staff.StaffSEmail);
                if (existingAdmin != null)
                {
                    _logger.LogWarning($"Email {staff.StaffSEmail} already exists in Admin table");
                    ModelState.AddModelError("StaffSEmail", "Email already exists.");
                    return BadRequest(ModelState);
                }

                var existingUserStaff = await _context.Staff
                    .FirstOrDefaultAsync(s => s.StaffSEmail == staff.StaffSEmail);
                if (existingUserStaff != null)
                {
                    _logger.LogWarning($"Email {staff.StaffSEmail} already used");
                    ModelState.AddModelError("StaffSEmail", "Email already exists.");
                    return BadRequest(ModelState);
                }

                // Check if the email already exists in Users table
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(s => s.Email == staff.StaffSEmail);
                if (existingUser != null)
                {
                    _logger.LogWarning($"Email {staff.StaffSEmail} already used");
                    ModelState.AddModelError("StaffSEmail", "Email already exists.");
                    return BadRequest(ModelState);
                }

                // Set staff properties
                staff.IsActive = AccountStatus.Active; // Set to Active since password is already set
                staff.CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time"));
                staff.BOId = GetCurrentUserId(); // Set the business owner ID
                staff.Role = "Staff";
                staff.AllowEmailNotifications = true;
                staff.IsSetPassword = true; // Password is set by business owner

                // Hash the password using BCrypt
                if (!string.IsNullOrEmpty(staff.Password))
                {
                    staff.Password = BCrypt.Net.BCrypt.HashPassword(staff.Password);
                }

                // Add staff to database
                _context.Add(staff);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Creating staff with properties: Name={staff.StaffName}, Email={staff.StaffSEmail}, AccessLevel={staff.StaffAccessLevel}");

                // Get business owner details for email
                var businessOwner = await _context.Users.FirstOrDefaultAsync(u => u.Id == staff.BOId);
                string businessName = businessOwner?.BusinessName ?? "Your Business";
                string businessOwnerEmail = businessOwner?.Email ?? "noreply@growtrack.com";

                // Create email content with access level information
                var accessLevelNames = GetAccessLevelNames(staff.StaffAccessLevel);
                string accessLevelHtml = accessLevelNames.Count > 0
                    ? string.Join("", accessLevelNames.Select(level => $@"<div class=""access-item"">• {level}</div>"))
                    : @"<div class=""access-item"">• None</div>";

                string emailContent = $@"
                    <html>
                    <head>
                        <style>
                            body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                            .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                            h2 {{ color: #2c3e50; }}
                            .access-list {{ background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 15px 0; }}
                            .access-list h3 {{ margin-top: 0; color: #3498db; }}
                            .access-item {{ padding: 5px 0; }}
                            .login-info {{ background-color: #e8f4fd; padding: 15px; border-radius: 5px; margin: 15px 0; border-left: 4px solid #3498db; }}
                            .footer {{ margin-top: 30px; font-size: 12px; color: #7f8c8d; }}
                        </style>
                    </head>
                    <body>
                        <div class=""container"">
                            <h2>Welcome to {businessName}!</h2>
                            <p>Dear {staff.StaffName},</p>
                            <p>A supervisor account has been created for you by {businessName}. You can now log in to the system using the credentials below.</p>

                            <div class=""login-info"">
                                <h3>Your Login Information</h3>
                                <p><strong>Email:</strong> {staff.StaffSEmail}</p>
                                <p><strong>Password:</strong> An initial password has been set for your account. Please change it after your first login for security reasons.</p>
                                <p><strong>Login URL:</strong> <a href='{Request.Scheme}://{Request.Host}/Login/login'>{Request.Scheme}://{Request.Host}/Login/login</a></p>
                            </div>

                            <div class=""access-list"">
                                <h3>Your Access Permissions</h3>
                                <p>You have been granted access to the following features:</p>
                                {accessLevelHtml}
                            </div>

                            <p>If you have any questions about your account or need assistance, please contact your business owner.</p>

                            <div class=""footer"">
                                <p>This is an automated message. Please do not reply to this email.</p>
                            </div>
                        </div>
                    </body>
                    </html>";

                // Send email with account information
                await _emailService.SendEmail2(
                    businessOwnerEmail,
                    businessName,
                    staff.StaffSEmail,
                    "Your Supervisor Account Has Been Created",
                    emailContent,
                    true);

                return Ok(new { message = "Staff created successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating staff member");
                return StatusCode(500, new { message = "An error occurred while creating the staff member" });
            }
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = "0";
            if(User.FindFirstValue(ClaimTypes.Role) == "Staff")
            {
                userIdClaim = User.FindFirstValue("BOId");
            }
            else
            {
                userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            }
            
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return 0;
            }

            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }

            return 0;
        }

        private string GetUserDataById(int userId, string columnName)
        {
            return _context.Users
                .Where(user => user.Id == userId)
                .Select(user => EF.Property<string>(user, columnName))
                .FirstOrDefault()
                ?? "Unknown Data";
        }

        // GET: Staffs/Edit/5
        [HttpGet]
        [Route("Staffs/Edit/{id:int}")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var staff = await _context.Staff.FindAsync(id);
            if (staff == null)
            {
                return NotFound();
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_EditPartial", staff);
            }
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id.ToString() == User.FindFirstValue(ClaimTypes.NameIdentifier));
            var isMarketplaceApproved = user != null && user.MarkerPlaceStatus == MarketplaceStatus.Authorized;
            ViewBag.MarketplaceStatus = isMarketplaceApproved;

            return View(staff);
        }

        // POST: Staffs/Edit/5
        [HttpPost]
        [Route("Staffs/Edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [FromForm] Staff staff)
        {
            _logger.LogInformation($"Received staff update request for ID {id}");

            // Remove User from ModelState since it's handled by the controller
            ModelState.Remove("User");

            if (!ModelState.IsValid)
            {
                var errors = ModelState.SelectMany(x => x.Value.Errors.Select(z => z.ErrorMessage));
                _logger.LogWarning($"ModelState is invalid for staff update (ID: {id}). Errors: {string.Join("; ", errors)}");
                return BadRequest(ModelState);
            }

            try
            {
                // Get the existing staff record
                var existingStaff = await _context.Staff.FindAsync(id);
                if (existingStaff == null)
                {
                    return NotFound();
                }

                // Check if the email is being changed and if it already exists
                if (existingStaff.StaffSEmail != staff.StaffSEmail)
                {
                    var emailExists = await _context.Staff
                        .AnyAsync(s => s.StaffSEmail == staff.StaffSEmail && s.Id != id);
                    if (emailExists)
                    {
                        ModelState.AddModelError("StaffSEmail", "Email already exists.");
                        return BadRequest(ModelState);
                    }
                }

                // Get current and new access levels
                var currentAccessLevel = existingStaff.StaffAccessLevel;
                var newAccessLevel = staff.StaffAccessLevel;

                // Check if status has changed
                var statusChanged = existingStaff.IsActive != staff.IsActive;
                var previousStatus = existingStaff.IsActive;

                // Calculate added and removed access levels
                var addedAccessLevels = newAccessLevel & ~currentAccessLevel;
                var removedAccessLevels = currentAccessLevel & ~newAccessLevel;

                // Update only the allowed properties
                if (existingStaff.StaffName != staff.StaffName)
                {
                    existingStaff.StaffName = staff.StaffName;
                }
                if (existingStaff.StaffSEmail != staff.StaffSEmail)
                {
                    existingStaff.StaffSEmail = staff.StaffSEmail;
                }
                if (existingStaff.StaffPhone != staff.StaffPhone)
                {
                    existingStaff.StaffPhone = staff.StaffPhone;
                }
                if (existingStaff.StaffAccessLevel != staff.StaffAccessLevel)
                {
                    existingStaff.StaffAccessLevel = staff.StaffAccessLevel;
                }
                
                // Handle password update if provided
                string password = Request.Form["Password"].ToString();
                if (!string.IsNullOrWhiteSpace(password))
                {
                    existingStaff.Password = BCrypt.Net.BCrypt.HashPassword(password);
                    existingStaff.IsSetPassword = true;
                }
                
                existingStaff.IsActive = staff.IsActive;
                existingStaff.UpdatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time"));

                _context.Update(existingStaff);
                await _context.SaveChangesAsync();
                
                // If access level was changed, notify the staff member via SignalR and email
                if (addedAccessLevels != StaffAccessLevel.None || removedAccessLevels != StaffAccessLevel.None)
                {
                    _logger.LogInformation(
                        $"Access level changed for staff ID {id}, sending notifications"
                    );
                    
                    // Create a formatted list of previous and current access levels
                    var previousAccessLevels = GetAccessLevelNames(currentAccessLevel);
                    
                    // Identify added and removed access levels
                    var addedAccessLevelNames = GetAccessLevelNames(addedAccessLevels);
                    var removedAccessLevelNames = GetAccessLevelNames(removedAccessLevels);
                    
                    // Save the previous access level to session for this staff member
                    HttpContext.Session.SetString($"PreviousAccessLevel_{id}", currentAccessLevel.ToString());
                    
                    _logger.LogInformation($"Previous access levels: {string.Join(", ", previousAccessLevels)}");
                    _logger.LogInformation($"Added access levels: {string.Join(", ", addedAccessLevelNames)}");
                    _logger.LogInformation($"Removed access levels: {string.Join(", ", removedAccessLevelNames)}");
                    
                    try
                    {
                        // Try sending SignalR notification to specific group first
                        await _hubContext
                            .Clients.Group($"staff_{id}")
                            .SendAsync("AccessLevelChanged", id.ToString(), previousAccessLevels, addedAccessLevelNames, removedAccessLevelNames);
                        
                        _logger.LogInformation($"SignalR notification sent to staff_{id} group");

                        // Send email notification
                        var businessOwner = await _context.Users.FirstOrDefaultAsync(u => u.Id == existingStaff.BOId);
                        string businessName = businessOwner?.BusinessName ?? "Your Business";

                        // Precompute dynamic HTML blocks
                        string previousAccessHtml = "";
                        if (previousAccessLevels.Count > 0)
                        {
                            previousAccessHtml = string.Join("", previousAccessLevels.Select(level =>
                                removedAccessLevelNames.Contains(level)
                                    ? $@"<div class=""access-item"" style=""color: #e74c3c; text-decoration: line-through;"">• {level} (Removed)</div>"
                                    : $@"<div class=""access-item"">• {level}</div>"));
                        }
                        else
                        {
                            previousAccessHtml = @"<div class=""access-item"">• None</div>";
                        }

                        string removedBlock = removedAccessLevelNames.Count > 0
                            ? $@"
                            <div class=""access-list"" style=""background-color: #ffecec; border-left: 4px solid #e74c3c;"">
                                <h3 style=""color: #e74c3c;"">⚠️ Access Levels Removed</h3>
                                <p>The following access levels have been removed from your account:</p>
                                {string.Join("", removedAccessLevelNames.Select(level =>
                                                        $@"<div class=""access-item"" style=""color: #e74c3c;"">• {level}</div>"))}
                                <p>You will no longer have access to these features.</p>
                            </div>"
                            : "";

                        string addedBlock = addedAccessLevelNames.Count > 0
                            ? $@"
                            <div class=""access-list"" style=""background-color: #eaffea; border-left: 4px solid #27ae60;"">
                                <h3 style=""color: #27ae60;"">✅ New Access Levels Added</h3>
                                <p>You now have access to the following new features:</p>
                                {string.Join("", addedAccessLevelNames.Select(level =>
                                                        $@"<div class=""access-item"" style=""color: #27ae60;"">• {level}</div>"))}
                            </div>"
                            : "";

                        // Create a summary of all current access levels
                        var currentAccessLevelNames = GetAccessLevelNames(staff.StaffAccessLevel);
                        string currentAccessHtml = currentAccessLevelNames.Count > 0
                            ? string.Join("", currentAccessLevelNames.Select(level =>
                                addedAccessLevelNames.Contains(level)
                                    ? $@"<div class=""access-item"" style=""color: #27ae60; font-weight: bold;"">• {level} (New)</div>"
                                    : $@"<div class=""access-item"">• {level}</div>"))
                            : @"<div class=""access-item"">• None</div>";

                        string currentAccessBlock = $@"
                            <div class=""access-list"" style=""background-color: #f8f9fa; border-left: 4px solid #3498db;"">
                                <h3 style=""color: #3498db;"">🔑 Your Current Access Levels</h3>
                                <p>These are all the features you currently have access to:</p>
                                {currentAccessHtml}
                            </div>";

                        // Final HTML email content
                        string emailContent = $@"
                            <html>
                            <head>
                                <style>
                                    body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                                    .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                                    h2 {{ color: #2c3e50; }}
                                    .access-list {{ background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 15px 0; }}
                                    .access-list h3 {{ margin-top: 0; color: #3498db; }}
                                    .access-item {{ padding: 5px 0; }}
                                    .footer {{ margin-top: 30px; font-size: 12px; color: #7f8c8d; }}
                                </style>
                            </head>
                            <body>
                                <div class=""container"">
                                    <h2>Your Access Levels Have Been Updated</h2>
                                    <p>Dear {existingStaff.StaffName},</p>
                                    <p>Your access permissions in {businessName} have been updated by the business owner.</p>

                                    <div class=""access-list"">
                                        <h3>Previous Access Levels:</h3>
                                        {previousAccessHtml}
                                    </div>

                                    {removedBlock}
                                    {addedBlock}
                                    {currentAccessBlock}

                                    <p>Please log in to your account to see these changes reflected in your dashboard.</p>
                                    <p>If you have any questions about your access levels, please contact your business owner.</p>

                                    <div class=""footer"">
                                        <p>This is an automated message. Please do not reply to this email.</p>
                                    </div>
                                </div>
                            </body>
                            </html>";


                        // Send the email
                        await _emailService.SendEmail2(
                            businessOwner?.Email ?? "noreply@growtrack.com",
                            businessName,
                            existingStaff.StaffSEmail,
                            "Your Access Levels Have Been Updated",
                            emailContent,
                            true);
                        
                        _logger.LogInformation($"Email notification sent to {existingStaff.StaffSEmail}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error sending notifications to staff ID {id}");
                    }                
                }
                
                // Send password change notification if password was updated
                if (!string.IsNullOrWhiteSpace(password))
                {
                    try
                    {
                        var businessOwner = await _context.Users.FirstOrDefaultAsync(u => u.Id == existingStaff.BOId);
                        string businessName = businessOwner?.BusinessName ?? "Your Business";
                        
                        string passwordChangeEmailContent = $@"
                            <html>
                            <head>
                                <style>
                                    body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                                    .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                                    h2 {{ color: #2c3e50; }}
                                    .alert-box {{ background-color: #fff3cd; padding: 15px; border-radius: 5px; margin: 15px 0; border-left: 4px solid #ffc107; }}
                                    .footer {{ margin-top: 30px; font-size: 12px; color: #7f8c8d; }}
                                </style>
                            </head>
                            <body>
                                <div class=""container"">
                                    <h2>Your Password Has Been Changed</h2>
                                    <p>Dear {existingStaff.StaffName},</p>
                                    <p>Your account password for {businessName} has been updated by the business owner.</p>

                                    <div class=""alert-box"">
                                        <h3>⚠️ Important Security Information</h3>
                                        <p>Your password has been changed. You can use this new password to log in to your account.</p>
                                        <p>If you did not expect this change, please contact your business owner immediately.</p>
                                    </div>

                                    <p>You can log in at: <a href=""{Request.Scheme}://{Request.Host}/Login/login"">{Request.Scheme}://{Request.Host}/Login/login</a></p>
                                    <p>For security reasons, we recommend changing your password after logging in.</p>

                                    <div class=""footer"">
                                        <p>This is an automated message. Please do not reply to this email.</p>
                                    </div>
                                </div>
                            </body>
                            </html>";
                        
                        // Send the email
                        await _emailService.SendEmail2(
                            businessOwner?.Email ?? "noreply@growtrack.com",
                            businessName,
                            existingStaff.StaffSEmail,
                            "Your Account Password Has Been Changed",
                            passwordChangeEmailContent,
                            true);
                        
                        _logger.LogInformation($"Password change notification sent to {existingStaff.StaffSEmail}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error sending password change notification to staff ID {id}");
                    }
                }
                
                // Send status change notification if status was changed
                if (statusChanged)
                {
                    try
                    {
                        var businessOwner = await _context.Users.FirstOrDefaultAsync(u => u.Id == existingStaff.BOId);
                        string businessName = businessOwner?.BusinessName ?? "Your Business";
                        
                        // Determine status change message and styling
                        string statusTitle = staff.IsActive == AccountStatus.Active ? "Your Account Has Been Activated" : "Your Account Has Been Suspended";
                        string statusColor = staff.IsActive == AccountStatus.Active ? "#27ae60" : "#e74c3c";
                        string statusIcon = staff.IsActive == AccountStatus.Active ? "✅" : "⚠️";
                        string statusMessage = staff.IsActive == AccountStatus.Active 
                            ? "Your supervisor account has been activated. You now have full access to the system based on your assigned permissions."
                            : "Your supervisor account has been suspended. You will not be able to access the system until your account is reactivated.";
                        
                        string statusChangeEmailContent = $@"
                            <html>
                            <head>
                                <style>
                                    body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                                    .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                                    h2 {{ color: #2c3e50; }}
                                    .status-box {{ background-color: {(staff.IsActive == AccountStatus.Active ? "#eaffea" : "#ffecec")}; 
                                               padding: 15px; border-radius: 5px; margin: 15px 0; 
                                               border-left: 4px solid {statusColor}; }}
                                    .status-box h3 {{ margin-top: 0; color: {statusColor}; }}
                                    .footer {{ margin-top: 30px; font-size: 12px; color: #7f8c8d; }}
                                </style>
                            </head>
                            <body>
                                <div class=""container"">
                                    <h2>{statusTitle}</h2>
                                    <p>Dear {existingStaff.StaffName},</p>
                                    <p>Your account status in {businessName} has been changed by the business owner.</p>

                                    <div class=""status-box"">
                                        <h3>{statusIcon} Account Status: {staff.IsActive}</h3>
                                        <p>{statusMessage}</p>
                                    </div>

                                    {(staff.IsActive == AccountStatus.Active ? $@"
                                    <p>You can log in at: <a href=""{Request.Scheme}://{Request.Host}/Login/login"">{Request.Scheme}://{Request.Host}/Login/login</a></p>
                                    " : "")}

                                    <p>If you have any questions about your account status, please contact your business owner.</p>

                                    <div class=""footer"">
                                        <p>This is an automated message. Please do not reply to this email.</p>
                                    </div>
                                </div>
                            </body>
                            </html>";
                        
                        // Send the email
                        await _emailService.SendEmail2(
                            businessOwner?.Email ?? "noreply@growtrack.com",
                            businessName,
                            existingStaff.StaffSEmail,
                            statusTitle,
                            statusChangeEmailContent,
                            true);
                        
                        _logger.LogInformation($"Status change notification sent to {existingStaff.StaffSEmail}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error sending status change notification to staff ID {id}");
                    }
                }
                
                return Ok();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!StaffExists(staff.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
        }

        // POST: Staffs/Delete/5
        [HttpPost]
        [Route("Staffs/Delete/{id:int}")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var staff = await _context.Staff.FindAsync(id);
            if (staff != null)
            {
                _context.Staff.Remove(staff);
                await _context.SaveChangesAsync();
                return Ok();
            }
            return NotFound();
        }

        private bool StaffExists(int id)
        {
            return _context.Staff.Any(e => e.Id == id);
        }
        
        /// <summary>
        /// Gets access level details for the current staff member, including previous and current access levels
        /// </summary>
        /// <returns>JSON object with access level details</returns>
        [HttpGet]
        [Authorize(Roles = "Staff")]
        public async Task<IActionResult> GetAccessLevelDetails()
        {
            try
            {
                // Get the current staff ID
                var staffIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(staffIdClaim) || !int.TryParse(staffIdClaim, out int staffId))
                {
                    _logger.LogWarning("Invalid or missing staff ID in claims");
                    return BadRequest("Invalid staff ID");
                }
                
                // Get the staff from the database
                var staff = await _context.Staff.FindAsync(staffId);
                if (staff == null)
                {
                    _logger.LogWarning($"Staff with ID {staffId} not found in database");
                    return NotFound("Staff not found");
                }
                
                // Get the current access level from the database
                var currentAccessLevel = staff.StaffAccessLevel;
                
                // Get the previous access level from session if available
                StaffAccessLevel previousAccessLevel = StaffAccessLevel.None;
                var previousAccessLevelString = HttpContext.Session.GetString($"PreviousAccessLevel_{staffId}");
                
                if (!string.IsNullOrEmpty(previousAccessLevelString) && 
                    Enum.TryParse<StaffAccessLevel>(previousAccessLevelString, out StaffAccessLevel parsedLevel))
                {
                    previousAccessLevel = parsedLevel;
                }
                
                // Calculate added and removed access levels using bitwise operations
                var addedAccessLevelsEnum = currentAccessLevel & ~previousAccessLevel;
                var removedAccessLevelsEnum = previousAccessLevel & ~currentAccessLevel;

                // Convert enum values to names
                var currentAccessLevelNames = GetAccessLevelNames(currentAccessLevel);
                var previousAccessLevelNames = GetAccessLevelNames(previousAccessLevel);
                var addedAccessLevelNames = GetAccessLevelNames(addedAccessLevelsEnum);
                var removedAccessLevelNames = GetAccessLevelNames(removedAccessLevelsEnum);
                
                // Store the current access level as the previous for next time
                HttpContext.Session.SetString($"PreviousAccessLevel_{staffId}", currentAccessLevel.ToString());
                
                // Return the access level details
                return Json(new
                {
                    currentAccessLevels = currentAccessLevelNames,
                    previousAccessLevels = previousAccessLevelNames,
                    addedAccessLevels = addedAccessLevelNames,
                    removedAccessLevels = removedAccessLevelNames
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting access level details");
                return StatusCode(500, new { error = "Error getting access level details" });
            }
        }
        
        /// <summary>
        /// Converts a StaffAccessLevel enum value to a list of readable access level names
        /// </summary>
        /// <param name="accessLevel">The StaffAccessLevel enum value</param>
        /// <returns>List of readable access level names</returns>
        private List<string> GetAccessLevelNames(StaffAccessLevel accessLevel)
        {
            var accessLevelNames = new List<string>();
            
            // Check each flag and add the corresponding name to the list
            if ((accessLevel & StaffAccessLevel.PublishedProducts) == StaffAccessLevel.PublishedProducts)
                accessLevelNames.Add("Published Products");

            if ((accessLevel & StaffAccessLevel.Leads) == StaffAccessLevel.Leads)
                accessLevelNames.Add("Leads");

            if ((accessLevel & StaffAccessLevel.QuickSales) == StaffAccessLevel.QuickSales)
                accessLevelNames.Add("QuickSales");

            if ((accessLevel & StaffAccessLevel.Inventory) == StaffAccessLevel.Inventory)
                accessLevelNames.Add("Inventory");

            if ((accessLevel & StaffAccessLevel.Campaigns) == StaffAccessLevel.Campaigns)
                accessLevelNames.Add("Campaigns");
                
            if ((accessLevel & StaffAccessLevel.Reports) == StaffAccessLevel.Reports)
                accessLevelNames.Add("Reports");
                
            if ((accessLevel & StaffAccessLevel.Notifications) == StaffAccessLevel.Notifications)
                accessLevelNames.Add("Notifications");
                
            if ((accessLevel & StaffAccessLevel.Calendar) == StaffAccessLevel.Calendar)
                accessLevelNames.Add("Calendar");
                
            if ((accessLevel & StaffAccessLevel.Chat) == StaffAccessLevel.Chat)
                accessLevelNames.Add("Chat");
            
            return accessLevelNames;
        }
    }
}
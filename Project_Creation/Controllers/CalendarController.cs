using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Project_Creation.Data;
using Project_Creation.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Project_Creation.DTO;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Logging;
using System.Data;
using Users = Project_Creation.Models.Entities.Users;
using Microsoft.AspNetCore.SignalR;
using Project_Creation.Models.DTOs;

namespace Project_Creation.Controllers
{
    [Authorize]
    public class CalendarController : Controller
    {
        private readonly AuthDbContext _context;
        private readonly ILogger<CalendarController> _logger;
        private readonly IHubContext<RealTimeHub> _hubContext;
        private readonly IEmailService _emailService;

        public CalendarController(
            AuthDbContext context,
            ILogger<CalendarController> logger,
            IHubContext<RealTimeHub> hubContext,
            IEmailService emailService)
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
            _emailService = emailService;
            
            // Check if IsAdminSetAll column exists, add it if missing
            EnsureIsAdminSetAllColumnExists();
        }

        // Method to check if IsAdminSetAll column exists and add it if missing
        private void EnsureIsAdminSetAllColumnExists()
        {
            try
            {
                // Check if the IsAdminSetAll column exists using raw SQL
                var command = _context.Database.GetDbConnection().CreateCommand();
                command.CommandText = @"
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM sys.columns 
                        WHERE Name = 'IsAdminSetAll'
                        AND Object_ID = Object_ID('Calendar')
                    )
                    BEGIN
                        ALTER TABLE Calendar
                        ADD IsAdminSetAll BIT NOT NULL DEFAULT 0
                    END";
                
                // Open connection if it's closed
                if (command.Connection.State != System.Data.ConnectionState.Open)
                {
                    command.Connection.Open();
                }
                
                // Execute the command
                int result = command.ExecuteNonQuery();
                
                _logger.LogInformation("Database schema check completed, result: {result}", result);
            }
            catch (Exception ex)
            {
                // Log the error but continue, we'll handle missing column in the GetTasks method
                _logger.LogError(ex, "Error checking or updating database schema for IsAdminSetAll column");
            }
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = "0";
            if (User.FindFirstValue(ClaimTypes.Role) == "Staff")
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

        public async Task<IActionResult> Index()
        {
            try
            {
                var role = User.FindFirstValue(ClaimTypes.Role);
                var userId = GetCurrentUserId();
                var staffId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                // Get business categories
                var categories = await _context.Users
                    .Where(u => u.UserRole == "BusinessOwner")
                    .Select(u => u.CategoryOfBusiness)
                    .Distinct()
                    .ToListAsync();

                // Get business owners with their categories
                var owners = await _context.Users
                    .Where(u => u.UserRole == "BusinessOwner")
                    .OrderBy(u => u.BusinessName)
                    .Select(u => new {
                        Id = u.Id,
                        Name = u.BusinessName,
                        Category = u.CategoryOfBusiness,
                        FirstName = u.FirstName,
                        LastName = u.LastName
                    })
                    .ToListAsync();

                // Get staff members if user is a business owner
                var staffMembers = new List<object>();
                if (role == "BusinessOwner")
                {
                    staffMembers = await _context.Staff
                        .Where(s => s.BOId == userId)
                        .Select(s => new {
                            Id = s.Id,
                            Name = s.StaffName
                        })
                        .ToListAsync<object>();
                }
                else if (role == "Staff")
                {
                    // For staff users, get all staff members under the same business owner
                    var boId = await _context.Staff
                        .Where(s => s.Id == staffId)
                        .Select(s => s.BOId)
                        .FirstOrDefaultAsync();
                        
                    if (boId > 0)
                    {
                        staffMembers = await _context.Staff
                            .Where(s => s.BOId == boId)
                            .Select(s => new {
                                Id = s.Id,
                                Name = s.StaffName
                            })
                            .ToListAsync<object>();
                    }
                }
                else if (role == "Admin")
                {
                    // For admin, get all staff for reference
                    staffMembers = await _context.Staff
                        .Select(s => new {
                            Id = s.Id,
                            Name = s.StaffName
                        })
                        .ToListAsync<object>();
                }

                // Add data to ViewBag
                ViewBag.BusinessCategories = categories;
                ViewBag.BusinessOwners = owners;
                ViewBag.StaffMembers = staffMembers;

                _logger.LogDebug("BusinessCategories: "+ categories);
                _logger.LogDebug("BusinessOwners: " + owners);
                _logger.LogDebug("StaffMembers: " + staffMembers);


                // Get tasks for the current user
                //var tasks = await _context.Staff
                //        .Where(c => c.BOId == GetCurrentUserId())
                //        .ToListAsync();

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading calendar page data");
                return View(new List<Staff>());
            }
        }

        [HttpGet]
        public IActionResult GetTasks()
        {
            try
            {
                // Get the current user's ID and role
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                var roleClaim = User.FindFirst(ClaimTypes.Role);
                var categoryClaim = User.FindFirst("CategoryOfBusiness");
                var boIdClaim = User.FindFirst("BOId");

                if (userIdClaim == null || roleClaim == null)
                {
                    _logger.LogWarning("User claims not found - UserId: {UserId}, Role: {Role}", 
                        userIdClaim?.Value, roleClaim?.Value);
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                var userId = userIdClaim.Value;
                var role = roleClaim.Value;
                var category = categoryClaim?.Value;
                var boId = boIdClaim?.Value;

                _logger.LogInformation($"Retrieving tasks for user {userId} with role {role}");

                // Get tasks based on user role
                var tasks = new List<Calendar>();

                try
                {

                    if (role == "Admin")
                    {
                        // Admin can see:
                        // 1. Tasks they created 
                        // 2. Tasks created by other admins
                        // 3. Tasks set as visible to all BOs by other admins (IsAdminSetAll = true)
                        tasks = _context.Calendar
                            .Include(t => t.User)
                            .Where(t => t.WhoSetAppointment == WhoSetAppointments.Admin)
                            .OrderByDescending(t => t.CreatedAt)
                            .ToList();
                    }
                    else if (role == "BusinessOwner")
                    {
                        // Get the BO's ID
                        int businessOwnerId = int.Parse(userId);
                        
                        // Business owner can see:
                        // 1. Tasks they created
                        // 2. Tasks created by their staff 
                        // 3. Tasks specifically shared with their category by an admin
                        // 4. Tasks specifically shared with them by an admin
                        // 5. Tasks marked as visible to all business owners by admins (IsAdminSetAll = true)
                        tasks = _context.Calendar
                            .Include(t => t.User)
                            .Where(t => t.UserId == businessOwnerId || // Tasks the BO created
                                  t.IsAdminSetAll || // Tasks visible to all BOs set by admins
                                  (t.AdminViewers1 != null && t.AdminViewers1.Contains(category)) || // Tasks shared with BO's category
                                  (t.AdminViewers2 != null && t.AdminViewers2.Contains(userId)) || // Tasks shared specifically with this BO
                                  // Tasks created by staff under this BO - removed WhoSetAppointment filter
                                  (_context.Staff.Any(s => s.Id == t.UserId && s.BOId == businessOwnerId)))
                            .OrderByDescending(t => t.CreatedAt)
                            .ToList();
                    }
                    else if (role == "Staff")
                    {
                        // Get the staff's business owner ID
                        var staffBOId = _context.Staff
                            .Where(s => s.Id == int.Parse(userId))
                            .Select(s => s.BOId)
                            .FirstOrDefault();

                        // Check if staff has calendar access
                        bool hasCalendarAccess = User.FindFirstValue("AccessLevel")
                            ?.Split(',')
                            .Select(a => a.Trim())
                            .Contains(StaffAccessLevel.Calendar.ToString()) ?? false;

                        if (hasCalendarAccess)
                        {
                            // Staff with calendar access can see:
                            // 1. Tasks they created
                            // 2. All tasks created by their business owner
                            // 3. Tasks shared with them specifically
                            tasks = _context.Calendar
                                .Include(t => t.User)
                                .Where(t =>
                                      (t.UserId == int.Parse(userId)) || // Tasks created by this staff member
                                      (t.UserId == staffBOId) || // All tasks created by staff's BO
                                      (t.BOViewers != null && t.BOViewers.Contains(userId))) // Tasks shared specifically with this staff member
                                .OrderByDescending(t => t.CreatedAt)
                                .ToList();
                        }
                        else
                        {
                            // Staff without calendar access can only see:
                            // 1. Tasks they created
                            // 2. Tasks shared with them specifically by their business owner
                            // 3. Tasks marked as visible to all staff by their business owner
                            tasks = _context.Calendar
                                .Include(t => t.User)
                                .Where(t =>
                                      (t.UserId == int.Parse(userId)) || // Tasks created by this staff member
                                      (t.UserId == staffBOId && t.IsAll) || // Tasks created by BO and marked as visible to all staff
                                      (t.BOViewers != null && t.BOViewers.Contains(userId))) // Tasks shared specifically with this staff member
                                .OrderByDescending(t => t.CreatedAt)
                                .ToList();
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"Unknown role {role} for user {userId}");
                        return BadRequest(new { success = false, message = "Invalid user role" });
                    }

                    _logger.LogInformation($"Found {tasks.Count} tasks for user {userId}");
                    return Json(tasks);
                }
                catch (Exception ex)
                {
                    // If there's an error with the new query, use a fallback query
                    _logger.LogError(ex, "Error executing query, using fallback query");
                    
                    var fallbackTasks = new List<Calendar>();
                    
                    if (role == "Admin")
                    {
                        // Admin fallback: just show tasks they created
                        fallbackTasks = _context.Calendar
                            .Include(t => t.User)
                            .Where(t => t.UserId == int.Parse(userId)) // Only tasks created by this admin
                            .OrderByDescending(t => t.CreatedAt)
                            .ToList();
                    }
                    else if (role == "BusinessOwner")
                    {
                        // Business owner fallback query
                        fallbackTasks = _context.Calendar
                            .Include(t => t.User)
                            .Where(t => t.UserId == int.Parse(userId) || // Tasks the BO created
                                  (t.IsAll && t.User.UserRole == "Admin") || // All tasks visible to all set by admins
                                  (t.AdminViewers1 != null && t.AdminViewers1.Contains(category)) || // Tasks shared with BO's category
                                  (t.AdminViewers2 != null && t.AdminViewers2.Contains(userId))) // Tasks shared specifically with this BO
                            .OrderByDescending(t => t.CreatedAt)
                            .ToList();
                    }
                    else if (role == "Staff")
                    {
                        // Staff fallback query
                        var staffBOId = _context.Staff
                            .Where(s => s.Id == int.Parse(userId))
                            .Select(s => s.BOId)
                            .FirstOrDefault();
                             
                        // Check if staff has calendar access
                        bool hasCalendarAccess = User.FindFirstValue("AccessLevel")
                            ?.Split(',')
                            .Select(a => a.Trim())
                            .Contains(StaffAccessLevel.Calendar.ToString()) ?? false;

                        if (hasCalendarAccess)
                        {
                            // Staff with calendar access can see all tasks from their business owner
                            fallbackTasks = _context.Calendar
                                .Include(t => t.User)
                                .Where(t => t.UserId == int.Parse(userId) || // Tasks created by this staff
                                      (t.UserId == staffBOId) || // All tasks created by staff's BO
                                      (t.BOViewers != null && t.BOViewers.Contains(userId))) // Tasks shared specifically with this staff member
                                .OrderByDescending(t => t.CreatedAt)
                                .ToList();
                        }
                        else
                        {
                            // Staff without calendar access can only see tasks assigned to them
                            fallbackTasks = _context.Calendar
                                .Include(t => t.User)
                                .Where(t => t.UserId == int.Parse(userId) || // Tasks created by this staff
                                      (t.UserId == staffBOId && t.IsAll) || // Tasks created by staff's BO and marked as visible to all
                                      (t.BOViewers != null && t.BOViewers.Contains(userId))) // Tasks shared specifically with this staff member
                                .OrderByDescending(t => t.CreatedAt)
                                .ToList();
                        }
                    }
                    
                    _logger.LogInformation($"Fallback query found {fallbackTasks.Count} tasks for user {userId}");
                    return Json(fallbackTasks);
                }
                
                _logger.LogInformation($"Found {tasks.Count} tasks for user {userId}");
                return Json(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving tasks");
                return StatusCode(500, new { success = false, message = "An error occurred while retrieving tasks" });
            }
        }

        [HttpGet]
        public IActionResult GetBusinessCategories()
        {
            try
        {
            var categories = _context.Users
                .Where(u => u.UserRole == "BusinessOwner")
                .Select(u => u.CategoryOfBusiness)
                .Distinct()
                .ToList();

                if (!categories.Any())
                {
                    _logger.LogWarning("No business categories found in the database");
                    return Json(new List<string>());
                }

                _logger.LogInformation($"Retrieved {categories.Count} business categories");
            return Json(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving business categories");
                return StatusCode(500, new { success = false, message = "Error retrieving business categories" });
            }
        }

        [HttpGet]
        public IActionResult GetBusinessOwners(string categoryIds = null)
        {
            try
        {
            var query = _context.Users
                .Where(u => u.UserRole == "BusinessOwner");

                if (!string.IsNullOrEmpty(categoryIds))
                {
                    var categories = categoryIds.Split(',');
                    query = query.Where(u => categories.Contains(u.CategoryOfBusiness));
                    _logger.LogInformation($"Filtering business owners by categories: {string.Join(", ", categories)}");
                }

                var owners = query
                    .OrderBy(u => u.BusinessName)
                    .Select(u => new {
                        Id = u.Id,
                        Name = u.BusinessName,
                        Category = u.CategoryOfBusiness,
                        FirstName = u.FirstName,
                        LastName = u.LastName
                    })
                    .ToList();

                if (!owners.Any())
                {
                    _logger.LogWarning("No business owners found matching the criteria");
                    return Json(new List<object>());
                }

                _logger.LogInformation($"Retrieved {owners.Count} business owners");
                return Json(owners);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving business owners");
                return StatusCode(500, new { success = false, message = "Error retrieving business owners" });
            }
        }

        [HttpGet]
        public IActionResult GetStaff(int? boId = null)
        {
            if (!boId.HasValue && User.FindFirstValue(ClaimTypes.Role) == "BusinessOwner")
            {
                boId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            }

            var query = _context.Staff.AsQueryable();

            if (boId.HasValue)
            {
                query = query.Where(s => s.BOId == boId.Value);
            }

            var staff = query.Select(s => new {
                Id = s.Id,
                Name = s.StaffName
            }).ToList();

            return Json(staff);
        }

        private string GetUserDataById(int userId, string columnName)
        {
            return _context.Staff
                .Where(staff => staff.Id == userId)
                .Select(staff => EF.Property<string>(staff, columnName))
                .FirstOrDefault()
                ?? "Unknown Data";
        }

        [HttpGet]
        public IActionResult GetStaffName(int id)
        {
            try
            {
                var staff = _context.Staff.FirstOrDefault(s => s.Id == id);
                if (staff != null)
                {
                    return Json(new { name = staff.StaffName });
                }
                
                // If staff not found, check in Users table for business owners or admins
                var user = _context.Users.FirstOrDefault(u => u.Id == id);
                if (user != null)
                {
                    string name = user.UserRole == "BusinessOwner" ? user.BusinessName : user.FirstName;
                    return Json(new { name = name });
                }
                
                return Json(new { name = $"User {id}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving staff name for ID {id}");
                return Json(new { name = $"User {id}" });
            }
        }

        [HttpPost]
        [Route("Calendar/CreateTask")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTask([FromBody] CalendarEventDTO task)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0)
                {
                    return Unauthorized(new { success = false, message = "Unauthorized" });
                }

                // Parse datetime
                DateTime taskDate;
                if (!DateTime.TryParse(task.Date, out taskDate))
                {
                    return BadRequest(new { success = false, message = "Invalid date format" });
                }

                // Parse time if provided
                TimeOnly? timeOnly = null;
                if (!string.IsNullOrEmpty(task.Time))
                {
                    if (TimeOnly.TryParse(task.Time, out TimeOnly parsedTime))
                    {
                        timeOnly = parsedTime;
                }
                }
                
                // Get user role to determine appointment type
                string role = User.FindFirstValue(ClaimTypes.Role);
                var whoSetAppointment = WhoSetAppointments.BusinessOwner; // Default
                
                if (role == "Admin")
                {
                    whoSetAppointment = WhoSetAppointments.Admin;
                }
                else if (role == "Staff")
                            {
                    whoSetAppointment = WhoSetAppointments.Staff;
                }
                
                // Determine if IsAll was checked
                bool isAll = task.SendReminder; // Use this field to store IsAll value from JSON
                
                // Create new Calendar event
                var newEvent = new Calendar
                    {
                    Title = task.Title,
                    Date = DateOnly.FromDateTime(taskDate),
                    Time = timeOnly,
                    Priority = task.Priority, // Now directly using the enum from DTO
                    Notes = task.Description,
                    CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore")),
                        UserId = userId,
                    WhoSetAppointment = whoSetAppointment,
                    IsAll = isAll
                    };

                // Handle sharing options based on user role
                    if (role == "Admin")
                    {
                    newEvent.IsAdminSetAll = isAll;
                                    }
                
                _context.Calendar.Add(newEvent);
                    await _context.SaveChangesAsync();

                // Create a reminder notification for the event
                string reminderTitle = $"Calendar Event: {task.Title}";
                string reminderMessage = task.Description ?? "No description provided";

                // Create a notification
                await NotificationsController.CreateNotification(
                    _context,
                    userId,
                    reminderTitle,
                    reminderMessage,
                    NotificationType.Calendar,
                    $"/Calendar/Index?date={taskDate.ToString("yyyy-MM-dd")}"
                );

                // Send an email notification
                var user = await _context.Users.FindAsync(userId);
                if (user != null && !string.IsNullOrEmpty(user.Email))
                {
                    string emailSubject = $"New Calendar Event: {task.Title}";
                    string emailBody = $@"
                        <h2>Calendar Event Reminder</h2>
                        <p>You have a new event scheduled:</p>
                        <div style='padding: 15px; border-left: 4px solid #f3993e; margin: 20px 0;'>
                            <h3>{task.Title}</h3>
                            <p><strong>Date:</strong> {taskDate.ToString("dddd, MMMM d, yyyy")}</p>
                            <p><strong>Time:</strong> {(timeOnly.HasValue ? timeOnly.Value.ToString("h:mm tt") : "All day")}</p>
                            <p><strong>Priority:</strong> {task.Priority}</p>
                            <p><strong>Description:</strong> {reminderMessage}</p>
                        </div>
                        <p>You can view this event in your calendar by <a href='{Request.Scheme}://{Request.Host}/Calendar/Index?date={taskDate.ToString("yyyy-MM-dd")}'>clicking here</a>.</p>
                    ";

                    await _emailService.SendEmail(user.Email, emailSubject, emailBody, true);
                }

                // Send real-time notification via SignalR
                await _hubContext.Clients.User(userId.ToString()).SendAsync(
                    "ReceivePrivateNotification",
                    reminderTitle,
                    reminderMessage,
                    newEvent.Id.ToString(),
                    $"/Calendar/Index?date={taskDate.ToString("yyyy-MM-dd")}",
                    "Calendar"
                );

                // Convert to CalendarTaskDTO for consistent response format
                var responseTask = new CalendarTaskDTO
                {
                    Id = newEvent.Id,
                    Title = newEvent.Title,
                    Date = newEvent.Date.ToString("yyyy-MM-dd"),
                    Time = newEvent.Time?.ToString("HH:mm") ?? "",
                    Priority = newEvent.Priority.ToString(), // Convert enum to string for DTO
                    Notes = newEvent.Notes ?? "",
                    CreatedAt = newEvent.CreatedAt,
                    IsAll = newEvent.IsAll,
                    BOViewers = newEvent.BOViewers ?? "",
                    AdminViewers1 = newEvent.AdminViewers1 ?? "",
                    AdminViewers2 = newEvent.AdminViewers2 ?? ""
                };

                return Ok(new { success = true, task = responseTask });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating calendar task");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPut]
        public async Task<IActionResult> UpdateTask([FromForm] CalendarTaskDTO taskDTO)
        {
            try
            {
                // Ensure we have a valid userId
                if (!User.Identity.IsAuthenticated || !User.HasClaim(c => c.Type == ClaimTypes.NameIdentifier))
                {
                    return Unauthorized(new { success = false, message = "User not authorized or missing ID claim" });
                }

                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var role = User.FindFirstValue(ClaimTypes.Role);
                var canAccess = false;
                
                // Check if staff has calendar access
                if (role == "Staff")
                {
                    canAccess = User.FindFirstValue("AccessLevel")?
                        .Split(',')
                        .Select(a => a.Trim())
                        .Contains(StaffAccessLevel.Calendar.ToString()) ?? false;
                    
                    // Prevent staff without calendar access from editing tasks
                    if (!canAccess)
                    {
                        return Forbid();
                    }
                }

                // First check if the task exists at all
                var taskExists = await _context.Calendar.AnyAsync(c => c.Id == taskDTO.Id);
                if (!taskExists)
                {
                    return NotFound(new { success = false, message = $"Task with ID {taskDTO.Id} not found in database" });
                }
                
                // Get the task to check its creator
                var existingTask = await _context.Calendar
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.Id == taskDTO.Id);

                if (existingTask == null)
                {
                    return NotFound(new { success = false, message = $"Task with ID {taskDTO.Id} not found" });
                }
                
                // Check if this is an admin-created task and the current user is not an admin
                bool isAdminCreatedTask = existingTask.WhoSetAppointment == WhoSetAppointments.Admin;
                if (isAdminCreatedTask && role != "Admin")
                {
                    return Forbid(new { success = false, message = "Only admins can edit admin-created appointments" });
                }
                
                bool canEditTask = false;
                
                // Admin can edit any task
                if (role == "Admin")
                {
                    canEditTask = true;
                }
                // Business Owner can edit tasks they created
                else if (role == "BusinessOwner" && existingTask.UserId == userId)
                {
                    canEditTask = true;
                }
                // Staff with calendar access can edit tasks they created
                else if (role == "Staff" && canAccess && existingTask.StaffId == userId)
                {
                    canEditTask = true;
                }
                // Staff can also edit tasks that are shared with them
                else if (role == "Staff" && canAccess && existingTask.BOViewers != null)
                {
                    // Check if the task is shared with this staff member
                    var sharedStaffIds = existingTask.BOViewers.Split(',').Select(int.Parse).ToList();
                    if (sharedStaffIds.Contains(userId))
                    {
                        canEditTask = true;
                    }
                }
                // Staff can also edit tasks that are visible to all staff
                else if (role == "Staff" && canAccess && existingTask.IsAll)
                {
                    canEditTask = true;
                }
                
                if (!canEditTask)
                {
                    return Forbid(new { success = false, message = "You do not have permission to edit this appointment" });
                }

                // Parse priority enum
                if (!Enum.TryParse<Priority>(taskDTO.Priority, out var priority))
                {
                    return BadRequest(new { success = false, message = "Invalid priority value" });
                }

                // Parse date
                if (!DateOnly.TryParse(taskDTO.Date, out var date))
                {
                    return BadRequest(new { success = false, message = "Invalid date format" });
                }

                // Parse time if provided
                TimeOnly? time = null;
                if (!string.IsNullOrEmpty(taskDTO.Time))
                {
                    if (!TimeOnly.TryParse(taskDTO.Time, out var parsedTime))
                    {
                        return BadRequest(new { success = false, message = "Invalid time format" });
                    }
                    time = parsedTime;
                }

                // Store original values for comparison to determine if notifications are needed
                bool sharingChanged = existingTask.IsAll != taskDTO.IsAll || 
                                      (role == "Admin" && existingTask.IsAdminSetAll != (taskDTO.IsAll && role == "Admin"));
                
                // Store previous sharing settings to compare
                string previousAdminViewers1 = existingTask.AdminViewers1;
                string previousAdminViewers2 = existingTask.AdminViewers2;
                
                // List to collect business owners for notifications
                var notifyBusinessOwners = new List<Users>();

                existingTask.Title = taskDTO.Title;
                existingTask.Date = date;
                existingTask.Time = time;
                existingTask.Priority = priority;
                existingTask.Notes = taskDTO.Notes;
                
                // Always update the IsAll property
                existingTask.IsAll = taskDTO.IsAll;
                
                // Update IsAdminSetAll flag if the user is an Admin
                if (role == "Admin")
                {
                    existingTask.IsAdminSetAll = taskDTO.IsAll;
                    
                    if (taskDTO.IsAll)
                    {
                        // Get all business owners for notification
                        notifyBusinessOwners = await _context.Users
                            .Where(u => u.UserRole == "BusinessOwner")
                            .ToListAsync();
                        
                        existingTask.AdminViewers1 = null;
                        existingTask.AdminViewers2 = null;
                    }
                }
                else if (role == "BusinessOwner")
                {
                    // Business owners can't change IsAdminSetAll flag
                    existingTask.IsAdminSetAll = false;
                }
                
                // For BusinessOwner role, always update staff sharing settings
                if (role == "BusinessOwner")
                {
                    if (taskDTO.IsAll)
                    {
                        existingTask.BOViewers = null;
                    }
                    else if (taskDTO.SelectedStaff != null && taskDTO.SelectedStaff.Any())
                    {
                        existingTask.BOViewers = string.Join(",", taskDTO.SelectedStaff);
                    }
                }
                // For Staff with calendar access, allow updating sharing options similar to business owners
                else if (role == "Staff" && canAccess)
                {
                    if (taskDTO.IsAll)
                    {
                        existingTask.BOViewers = null;
                    }
                    else if (taskDTO.SelectedStaff != null && taskDTO.SelectedStaff.Any())
                    {
                        existingTask.BOViewers = string.Join(",", taskDTO.SelectedStaff);
                    }
                }
                // For Admin role, update sharing settings and prepare notifications
                else if (role == "Admin" && !taskDTO.IsAll)
                {
                    if (taskDTO.SelectedBusinessCategories != null && taskDTO.SelectedBusinessCategories.Any())
                    {
                        // Convert numeric IDs back to category names for storing
                        var selectedCategoryIndexes = taskDTO.SelectedBusinessCategories;
                        var selectedCategories = new List<string>();
                        
                        // Get actual category names
                        var allCategories = await _context.Users
                            .Where(u => u.UserRole == "BusinessOwner")
                            .Select(u => u.CategoryOfBusiness)
                            .Distinct()
                            .ToListAsync();
                            
                        foreach (var index in selectedCategoryIndexes)
                        {
                            if (index > 0 && index <= allCategories.Count)
                            {
                                selectedCategories.Add(allCategories[index - 1]);
                            }
                        }
                        
                        existingTask.AdminViewers1 = string.Join(",", selectedCategories);
                        
                        // Collect business owners from these categories for notification
                        var businessOwnersFromCategories = await _context.Users
                            .Where(u => u.UserRole == "BusinessOwner" && selectedCategories.Contains(u.CategoryOfBusiness))
                            .ToListAsync();
                            
                        notifyBusinessOwners.AddRange(businessOwnersFromCategories);
                    }
                    else
                    {
                        existingTask.AdminViewers1 = null;
                    }
                    
                    if (taskDTO.SelectedBusinessOwners != null && taskDTO.SelectedBusinessOwners.Any())
                    {
                        existingTask.AdminViewers2 = string.Join(",", taskDTO.SelectedBusinessOwners);
                        
                        // Collect specific business owners for notification
                        var specificBusinessOwners = await _context.Users
                            .Where(u => u.UserRole == "BusinessOwner" && taskDTO.SelectedBusinessOwners.Contains(u.Id))
                            .ToListAsync();
                            
                        // Add only the ones not already in the list
                        foreach(var owner in specificBusinessOwners)
                        {
                            if (!notifyBusinessOwners.Any(bo => bo.Id == owner.Id))
                            {
                                notifyBusinessOwners.Add(owner);
                            }
                        }
                    }
                    else
                    {
                        existingTask.AdminViewers2 = null;
                    }
                }

                await _context.SaveChangesAsync();

                // Update the DTO with latest data
                taskDTO.CreatedAt = existingTask.CreatedAt;
                taskDTO.BOViewers = existingTask.BOViewers;
                taskDTO.AdminViewers1 = existingTask.AdminViewers1;
                taskDTO.AdminViewers2 = existingTask.AdminViewers2;
                taskDTO.IsAdminSetAll = existingTask.IsAdminSetAll;

                // Send notifications to business owners if the task is updated by an admin
                if (role == "Admin" && notifyBusinessOwners.Any())
                {
                    // Get the admin name
                    var adminUser = await _context.Admin.FirstOrDefaultAsync(a => a.Id == userId);
                    var adminName = adminUser != null ? $"{adminUser.FirstName} {adminUser.LastName}" : "The Admin";
                    
                    foreach (var owner in notifyBusinessOwners)
                    {
                        try
                        {
                            // Prepare email content
                            string subject = "Appointment Updated";
                            string timeInfo = existingTask.Time.HasValue ? $" at {existingTask.Time.Value.ToString("hh:mm tt")}" : "";
                            string message = $"<p>Dear {owner.FirstName},</p>" + 
                                $"<p>An appointment has been updated by {adminName}:</p>" +
                                $"<p><strong>Title:</strong> {existingTask.Title}<br>" +
                                $"<strong>Date:</strong> {existingTask.Date.ToString("dddd, MMMM d, yyyy")}{timeInfo}<br>" +
                                $"<strong>Priority:</strong> {existingTask.Priority}</p>";
                                
                            if (!string.IsNullOrEmpty(existingTask.Notes))
                            {
                                message += $"<p><strong>Notes:</strong> {existingTask.Notes}</p>";
                            }
                                
                            message += "<p>You can view this appointment in your calendar.</p>" +
                                "<p>Best regards,<br>The System Admin</p>";
                                
                            // Send email using the email service
                            if (HttpContext.RequestServices.GetService(typeof(IEmailService)) is IEmailService emailService)
                            {
                                await emailService.SendEmailToBO(owner, subject, message, true);
                                _logger.LogInformation($"Appointment update notification email sent to {owner.Email}");
                            }
                            else
                            {
                                // Fallback to direct email sending if service not available
                                _logger.LogWarning("IEmailService not available, email notification not sent");
                            }
                        }
                        catch (Exception emailEx)
                        {
                            _logger.LogError(emailEx, $"Failed to send appointment update notification email to {owner.Email}");
                            // Continue with other notifications even if one fails
                        }
                    }
                }

                return Json(new { success = true, task = taskDTO });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating task");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        public class UpdateTaskStatusRequest
        {
            public bool IsCompleted { get; set; }
        }

        [HttpPut]
        public async Task<IActionResult> UpdateTaskStatus([FromQuery] int id, [FromBody] UpdateTaskStatusRequest request)
        {
            try
            {
                // For appointments, we'll remove this functionality, but keep the method
                // to avoid breaking existing code. Just return a message that this is not applicable.
                return Json(new { success = false, message = "Status updates are not applicable for appointments" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating task status");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteTask(int id)
        {
            try
            {
                if (!User.Identity.IsAuthenticated || !User.HasClaim(c => c.Type == ClaimTypes.NameIdentifier))
                {
                    return Json(new { success = false, message = "User not authorized" });
                }

                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var role = User.FindFirstValue(ClaimTypes.Role);
                var canAccess = false;
                
                // Check if staff has calendar access
                if (role == "Staff")
                {
                    canAccess = User.FindFirstValue("AccessLevel")?
                        .Split(',')
                        .Select(a => a.Trim())
                        .Contains(StaffAccessLevel.Calendar.ToString()) ?? false;
                    
                    // Prevent staff without calendar access from deleting tasks
                    if (!canAccess)
                    {
                        return Forbid();
                    }
                }

                // First check if the task exists at all
                var taskExists = await _context.Calendar.AnyAsync(c => c.Id == id);
                if (!taskExists)
                {
                    return Json(new { success = false, message = $"Task with ID {id} not found in database" });
                }

                // Get the task with its details
                var task = await _context.Calendar
                    .Include(t => t.User)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (task == null)
                {
                    return Json(new { success = false, message = "Task not found" });
                }
                
                // Check if this is an admin-created task and the current user is not an admin
                bool isAdminCreatedTask = task.WhoSetAppointment == WhoSetAppointments.Admin;
                if (isAdminCreatedTask && role != "Admin")
                {
                    return Forbid(new { success = false, message = "Only admins can delete admin-created appointments" });
                }
                
                bool canDeleteTask = false;
                
                // Admin can delete any task
                if (role == "Admin")
                {
                    canDeleteTask = true;
                }
                // Business Owner can delete tasks they created
                else if (role == "BusinessOwner" && task.UserId == userId)
                {
                    canDeleteTask = true;
                }
                // Staff with calendar access can delete tasks they created
                else if (role == "Staff" && canAccess && task.StaffId == userId)
                {
                    canDeleteTask = true;
                }
                // Staff can also delete tasks that are shared with them
                else if (role == "Staff" && canAccess && task.BOViewers != null)
                {
                    // Check if the task is shared with this staff member
                    var sharedStaffIds = task.BOViewers.Split(',').Select(int.Parse).ToList();
                    if (sharedStaffIds.Contains(userId))
                    {
                        canDeleteTask = true;
                    }
                }
                // Staff can also delete tasks that are visible to all staff
                else if (role == "Staff" && canAccess && task.IsAll)
                {
                    canDeleteTask = true;
                }
                
                if (!canDeleteTask)
                {
                    return Forbid(new { success = false, message = "You do not have permission to delete this appointment" });
                }

                _context.Calendar.Remove(task);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Appointment deleted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private IActionResult Forbid(object value)
        {
            throw new NotImplementedException();
        }

        // Add this method to send daily event notifications
        [HttpGet]
        [Route("Calendar/GetTodayEvents")]
        public async Task<IActionResult> GetTodayEvents()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0)
                {
                    return Unauthorized(new { success = false, message = "Unauthorized" });
                }

                // Get today's date in Singapore time
                var singaporeTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
                var todayInSingapore = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, singaporeTimeZone).Date;

                // Get all calendar tasks for today
                var todayEvents = await _context.CalendarTasks
                    .Where(t => t.UserId == userId && 
                           t.StartTime.Date == todayInSingapore)
                    .OrderBy(t => t.StartTime)
                    .ToListAsync();

                if (!todayEvents.Any())
                {
                    return Ok(new { success = true, message = "No events scheduled for today", events = new List<object>() });
                }

                // Format events for the response
                var formattedEvents = todayEvents.Select(e => new
                {
                    id = e.Id,
                    title = e.Title,
                    description = e.Description,
                    startTime = e.StartTime,
                    endTime = e.EndTime,
                    priority = e.Priority.ToString(),
                    isCompleted = e.IsCompleted
                }).ToList();

                // Get user email for sending notification
                var user = await _context.Users.FindAsync(userId);
                if (user != null && !string.IsNullOrEmpty(user.Email))
                {
                    // Create a summary email with all today's events
                    string emailSubject = $"Your Calendar Events for Today ({todayInSingapore:dddd, MMMM d, yyyy})";
                    
                    string eventsHtml = "";
                    foreach (var evt in todayEvents)
                    {
                        string priorityColor = "";
                        switch (evt.Priority)
                        {
                            case TaskPriority.Urgent:
                                priorityColor = "#dc3545"; // red
                                break;
                            case TaskPriority.High:
                                priorityColor = "#fd7e14"; // orange
                                break;
                            case TaskPriority.Medium:
                                priorityColor = "#ffc107"; // yellow
                                break;
                            case TaskPriority.Low:
                                priorityColor = "#28a745"; // green
                                break;
                        }

                        eventsHtml += $@"
                        <div style='margin-bottom: 20px; padding: 15px; border-left: 4px solid {priorityColor}; background-color: #f8f9fa;'>
                            <h3 style='margin-top: 0; color: #333;'>{evt.Title}</h3>
                            <p><strong>Time:</strong> {evt.StartTime.ToString("h:mm tt")} - {evt.EndTime.ToString("h:mm tt")}</p>
                            <p><strong>Priority:</strong> <span style='color: {priorityColor};'>{evt.Priority}</span></p>
                            <p><strong>Description:</strong> {evt.Description}</p>
                        </div>";
                    }
                    
                    string emailBody = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <style>
                            body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px; }}
                            .header {{ background-color: #4a6fdc; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
                            .content {{ background-color: #f9f9f9; padding: 20px; border-left: 1px solid #ddd; border-right: 1px solid #ddd; }}
                            .footer {{ background-color: #eee; padding: 15px; text-align: center; font-size: 12px; color: #666; border-radius: 0 0 5px 5px; }}
                            .calendar-icon {{ font-size: 40px; margin-bottom: 10px; }}
                            .view-all-btn {{ display: inline-block; background-color: #4a6fdc; color: white; text-decoration: none; padding: 10px 20px; border-radius: 5px; margin-top: 15px; }}
                        </style>
                    </head>
                    <body>
                        <div class='header'>
                            <div class='calendar-icon'>📅</div>
                            <h2>Your Calendar Events for Today</h2>
                            <p>{todayInSingapore:dddd, MMMM d, yyyy}</p>
                        </div>
                        <div class='content'>
                            <p>You have {todayEvents.Count} event{(todayEvents.Count > 1 ? "s" : "")} scheduled for today:</p>
                            {eventsHtml}
                            <a href='{Request.Scheme}://{Request.Host}/Calendar/Index?date={todayInSingapore:yyyy-MM-dd}' class='view-all-btn'>View in Calendar</a>
                        </div>
                        <div class='footer'>
                            <p>This is an automated message, please do not reply to this email.</p>
                        </div>
                    </body>
                    </html>";

                    // Send the email
                    await _emailService.SendEmail(user.Email, emailSubject, emailBody, true);

                    // Create a notification in the app
                    var notification = new Notification
                    {
                        UserId = userId,
                        Title = $"Today's Calendar Events ({todayInSingapore:MMM d})",
                        Message = $"You have {todayEvents.Count} event{(todayEvents.Count > 1 ? "s" : "")} scheduled for today",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow,
                        Url = $"/Calendar/Index?date={todayInSingapore:yyyy-MM-dd}",
                        Type = NotificationType.Calendar
                    };

                    _context.Notifications.Add(notification);
                    await _context.SaveChangesAsync();

                    // Send real-time notification
                    await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", 
                        notification.Title, 
                        notification.Message,
                        notification.Id.ToString(),
                        notification.Url,
                        notification.Type.ToString());
                }

                return Ok(new { success = true, events = formattedEvents });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving today's events");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Add a scheduled task method for daily event notifications
        [NonAction]
        public async Task SendDailyEventNotificationsAsync()
        {
            try
            {
                _logger.LogInformation("Starting daily calendar events notification task");
                
                // Get today's date in Singapore time
                var singaporeTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
                var todayInSingapore = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, singaporeTimeZone).Date;
                
                // Get all business owners
                var businessOwners = await _context.Users
                    .Where(u => u.UserRole == "BusinessOwner")
                    .ToListAsync();
                    
                int notificationsSent = 0;
                
                foreach (var owner in businessOwners)
                {
                    // Get today's events for this business owner
                    var todayEvents = await _context.CalendarTasks
                        .Where(t => t.UserId == owner.Id && 
                               t.StartTime.Date == todayInSingapore)
                        .OrderBy(t => t.StartTime)
                        .ToListAsync();
                        
                    if (todayEvents.Any() && !string.IsNullOrEmpty(owner.Email))
                    {
                        // Create summary email with all today's events
                        string emailSubject = $"Your Calendar Events for Today ({todayInSingapore:dddd, MMMM d, yyyy})";
                        
                        string eventsHtml = "";
                        foreach (var evt in todayEvents)
                        {
                            string priorityColor = "";
                            switch (evt.Priority)
                            {
                                case TaskPriority.Urgent:
                                    priorityColor = "#dc3545"; // red
                                    break;
                                case TaskPriority.High:
                                    priorityColor = "#fd7e14"; // orange
                                    break;
                                case TaskPriority.Medium:
                                    priorityColor = "#ffc107"; // yellow
                                    break;
                                case TaskPriority.Low:
                                    priorityColor = "#28a745"; // green
                                    break;
                            }

                            eventsHtml += $@"
                            <div style='margin-bottom: 20px; padding: 15px; border-left: 4px solid {priorityColor}; background-color: #f8f9fa;'>
                                <h3 style='margin-top: 0; color: #333;'>{evt.Title}</h3>
                                <p><strong>Time:</strong> {evt.StartTime.ToString("h:mm tt")} - {evt.EndTime.ToString("h:mm tt")}</p>
                                <p><strong>Priority:</strong> <span style='color: {priorityColor};'>{evt.Priority}</span></p>
                                <p><strong>Description:</strong> {evt.Description}</p>
                            </div>";
                        }
                        
                        string emailBody = $@"
                        <!DOCTYPE html>
                        <html>
                        <head>
                            <style>
                                body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px; }}
                                .header {{ background-color: #4a6fdc; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
                                .content {{ background-color: #f9f9f9; padding: 20px; border-left: 1px solid #ddd; border-right: 1px solid #ddd; }}
                                .footer {{ background-color: #eee; padding: 15px; text-align: center; font-size: 12px; color: #666; border-radius: 0 0 5px 5px; }}
                                .calendar-icon {{ font-size: 40px; margin-bottom: 10px; }}
                                .view-all-btn {{ display: inline-block; background-color: #4a6fdc; color: white; text-decoration: none; padding: 10px 20px; border-radius: 5px; margin-top: 15px; }}
                            </style>
                        </head>
                        <body>
                            <div class='header'>
                                <div class='calendar-icon'>📅</div>
                                <h2>Good Morning, {owner.FirstName}!</h2>
                                <p>Your Calendar Events for {todayInSingapore:dddd, MMMM d, yyyy}</p>
                            </div>
                            <div class='content'>
                                <p>You have {todayEvents.Count} event{(todayEvents.Count > 1 ? "s" : "")} scheduled for today:</p>
                                {eventsHtml}
                                <a href='https://yourwebsite.com/Calendar/Index?date={todayInSingapore:yyyy-MM-dd}' class='view-all-btn'>View in Calendar</a>
                            </div>
                            <div class='footer'>
                                <p>This is an automated daily summary. You can manage your notification preferences in your account settings.</p>
                            </div>
                        </body>
                        </html>";

                        // Send the email
                        await _emailService.SendEmail(owner.Email, emailSubject, emailBody, true);
                        
                        // Create a notification in the app
                        var notification = new Notification
                        {
                            UserId = owner.Id,
                            Title = $"Your Events for Today ({todayInSingapore:MMM d})",
                            Message = $"You have {todayEvents.Count} event{(todayEvents.Count > 1 ? "s" : "")} scheduled for today",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow,
                            Url = $"/Calendar/Index?date={todayInSingapore:yyyy-MM-dd}",
                            Type = NotificationType.Calendar
                        };

                        _context.Notifications.Add(notification);
                        notificationsSent++;
                    }
                }
                
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Daily calendar events notification task completed. Sent {notificationsSent} notifications.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in daily calendar events notification task");
            }
        }
    }
}
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Project_Creation.Data;
using Project_Creation.Models.Entities;

namespace Project_Creation.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly AuthDbContext _context;
        private readonly ILogger<NotificationsController> _logger;
        private readonly IHubContext<RealTimeHub> _hubContext;

        public NotificationsController(
            AuthDbContext context, 
            ILogger<NotificationsController> logger,
            IHubContext<RealTimeHub> hubContext)
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }
            return 0;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                int currentUserId = GetCurrentUserId();
                if (currentUserId == 0) return Unauthorized();

                // Get user role
                string userRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
                
                // Base query for user's own notifications
                var query = _context.Notifications.Where(n => n.UserId == currentUserId);
                
                // Check if staff has notification access level
                int? businessOwnerId = null;
                if (userRole == "Staff")
                {
                    var staff = await _context.Staffs
                        .FirstOrDefaultAsync(s => s.Id == currentUserId);
                    
                    if (staff == null)
                    {
                        return Unauthorized();
                    }
                    
                    // If staff has Notifications access level, include business owner's notifications
                    if (staff.StaffAccessLevel.HasFlag(StaffAccessLevel.Notifications))
                    {
                        businessOwnerId = staff.BOId;
                        
                        // Include business owner's notifications
                        query = query.Union(_context.Notifications
                            .Where(n => n.UserId == businessOwnerId && n.IsForBusinessOwner));
                    }
                }

                bool isAdmin = userRole == "Admin";
                bool isStaff = userRole == "Staff";
                bool isBusinessOwner = userRole == "BusinessOwner";

                // Apply role-based filtering if needed
                if (isAdmin)
                {
                    // Admins see notifications marked for admins or without specific targeting
                    query = query.Where(n => n.IsForAdmin || (!n.IsForAdmin && !n.IsForStaff && !n.IsForBusinessOwner));
                }
                else if (isStaff)
                {
                    // Staff see notifications marked for staff or without specific targeting
                    // (Business owner notifications are already included above if they have access)
                    query = query.Where(n => n.IsForStaff || (!n.IsForAdmin && !n.IsForStaff && !n.IsForBusinessOwner));
                }
                else if (isBusinessOwner)
                {
                    // Business owners see notifications marked for business owners or without specific targeting
                    query = query.Where(n => n.IsForBusinessOwner || (!n.IsForAdmin && !n.IsForStaff && !n.IsForBusinessOwner));
                }

                var notifications = await query
                    .OrderByDescending(n => n.Id)
                    .ToListAsync();

                // Pass business owner ID to the view for staff members
                ViewBag.BusinessOwnerId = businessOwnerId;

                return View(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notifications");
                return StatusCode(500);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUserNotifications()
        {
            try
            {
                int currentUserId = GetCurrentUserId();
                if (currentUserId == 0) return Unauthorized();

                // Get user role
                string userRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
                
                // Base query for user's own notifications
                var query = _context.Notifications.Where(n => n.UserId == currentUserId);
                
                // Check if staff has notification access level
                int? businessOwnerId = null;
                if (userRole == "Staff")
                {
                    var staff = await _context.Staffs
                        .FirstOrDefaultAsync(s => s.Id == currentUserId);
                    
                    if (staff != null && staff.StaffAccessLevel.HasFlag(StaffAccessLevel.Notifications))
                    {
                        businessOwnerId = staff.BOId;
                        
                        // Include business owner's notifications
                        query = query.Union(_context.Notifications
                            .Where(n => n.UserId == businessOwnerId && n.IsForBusinessOwner));
                    }
                }
                
                bool isAdmin = userRole == "Admin";
                bool isStaff = userRole == "Staff";
                bool isBusinessOwner = userRole == "BusinessOwner";

                // Apply role-based filtering if needed
                if (isAdmin)
                {
                    // Admins see notifications marked for admins or without specific targeting
                    query = query.Where(n => n.IsForAdmin || (!n.IsForAdmin && !n.IsForStaff && !n.IsForBusinessOwner));
                }
                else if (isStaff)
                {
                    // Staff see notifications marked for staff or without specific targeting
                    query = query.Where(n => n.IsForStaff || (!n.IsForAdmin && !n.IsForStaff && !n.IsForBusinessOwner));
                }
                else if (isBusinessOwner)
                {
                    // Business owners see notifications marked for business owners or without specific targeting
                    query = query.Where(n => n.IsForBusinessOwner || (!n.IsForAdmin && !n.IsForStaff && !n.IsForBusinessOwner));
                }

                var notifications = await query
                    .OrderByDescending(n => n.Id)
                    .Take(10)
                    .ToListAsync();

                var unreadCount = await query
                    .CountAsync(n => !n.IsRead);

                return Json(new { 
                    success = true, 
                    notifications = notifications,
                    unreadCount = unreadCount,
                    businessOwnerId = businessOwnerId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user notifications");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            try
            {
                int currentUserId = GetCurrentUserId();
                if (currentUserId == 0) return Unauthorized();

                // Get user role
                string userRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
                
                // Base query for user's own unread notifications
                var query = _context.Notifications.Where(n => n.UserId == currentUserId && !n.IsRead);
                
                // Check if staff has notification access level
                if (userRole == "Staff")
                {
                    var staff = await _context.Staffs
                        .FirstOrDefaultAsync(s => s.Id == currentUserId);
                    
                    if (staff != null && staff.StaffAccessLevel.HasFlag(StaffAccessLevel.Notifications))
                    {
                        int businessOwnerId = staff.BOId;
                        
                        // Include business owner's unread notifications
                        query = query.Union(_context.Notifications
                            .Where(n => n.UserId == businessOwnerId && n.IsForBusinessOwner && !n.IsRead));
                    }
                }
                
                bool isAdmin = userRole == "Admin";
                bool isStaff = userRole == "Staff";
                bool isBusinessOwner = userRole == "BusinessOwner";

                // Apply role-based filtering if needed
                if (isAdmin)
                {
                    // Admins see notifications marked for admins or without specific targeting
                    query = query.Where(n => n.IsForAdmin || (!n.IsForAdmin && !n.IsForStaff && !n.IsForBusinessOwner));
                }
                else if (isStaff)
                {
                    // Staff see notifications marked for staff or without specific targeting
                    query = query.Where(n => n.IsForStaff || (!n.IsForAdmin && !n.IsForStaff && !n.IsForBusinessOwner));
                }
                else if (isBusinessOwner)
                {
                    // Business owners see notifications marked for business owners or without specific targeting
                    query = query.Where(n => n.IsForBusinessOwner || (!n.IsForAdmin && !n.IsForStaff && !n.IsForBusinessOwner));
                }

                var unreadCount = await query.CountAsync();

                return Json(new { success = true, unreadCount = unreadCount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread notification count");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetRecentNotifications()
        {
            try
            {
                int currentUserId = GetCurrentUserId();
                if (currentUserId == 0) return Unauthorized();

                // Get user role
                string userRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
                
                // Base query for user's own notifications
                var query = _context.Notifications.Where(n => n.UserId == currentUserId);
                
                // Check if staff has notification access level
                if (userRole == "Staff")
                {
                    var staff = await _context.Staffs
                        .FirstOrDefaultAsync(s => s.Id == currentUserId);
                    
                    if (staff != null && staff.StaffAccessLevel.HasFlag(StaffAccessLevel.Notifications))
                    {
                        int businessOwnerId = staff.BOId;
                        
                        // Include business owner's notifications
                        query = query.Union(_context.Notifications
                            .Where(n => n.UserId == businessOwnerId && n.IsForBusinessOwner));
                    }
                }
                
                bool isAdmin = userRole == "Admin";
                bool isStaff = userRole == "Staff";
                bool isBusinessOwner = userRole == "BusinessOwner";

                // Apply role-based filtering if needed
                if (isAdmin)
                {
                    // Admins see notifications marked for admins or without specific targeting
                    query = query.Where(n => n.IsForAdmin || (!n.IsForAdmin && !n.IsForStaff && !n.IsForBusinessOwner));
                }
                else if (isStaff)
                {
                    // Staff see notifications marked for staff or without specific targeting
                    query = query.Where(n => n.IsForStaff || (!n.IsForAdmin && !n.IsForStaff && !n.IsForBusinessOwner));
                }
                else if (isBusinessOwner)
                {
                    // Business owners see notifications marked for business owners or without specific targeting
                    query = query.Where(n => n.IsForBusinessOwner || (!n.IsForAdmin && !n.IsForStaff && !n.IsForBusinessOwner));
                }

                // Get unread count
                var unreadCount = await query.CountAsync(n => !n.IsRead);

                // Get recent notifications (limit to 5)
                var notifications = await query
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(5)
                    .Select(n => new {
                        id = n.Id,
                        title = n.Title,
                        message = n.Message,
                        type = n.Type.ToString(),
                        isRead = n.IsRead,
                        url = n.Url,
                        timeAgo = GetTimeAgo(n.CreatedAt)
                    })
                    .ToListAsync();

                return Json(new { 
                    success = true, 
                    notifications = notifications,
                    unreadCount = unreadCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent notifications");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Make the GetTimeAgo method static to fix the projection error
        private static string GetTimeAgo(DateTime dateTime)
        {
            var span = DateTime.Now - dateTime;

            if (span.TotalDays > 365)
            {
                int years = (int)(span.TotalDays / 365);
                return $"{years} {(years == 1 ? "year" : "years")} ago";
            }
            if (span.TotalDays > 30)
            {
                int months = (int)(span.TotalDays / 30);
                return $"{months} {(months == 1 ? "month" : "months")} ago";
            }
            if (span.TotalDays > 1)
            {
                return $"{(int)span.TotalDays} {((int)span.TotalDays == 1 ? "day" : "days")} ago";
            }
            if (span.TotalHours > 1)
            {
                return $"{(int)span.TotalHours} {((int)span.TotalHours == 1 ? "hour" : "hours")} ago";
            }
            if (span.TotalMinutes > 1)
            {
                return $"{(int)span.TotalMinutes} {((int)span.TotalMinutes == 1 ? "minute" : "minutes")} ago";
            }
            
            return "Just now";
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            try
            {
                int currentUserId = GetCurrentUserId();
                if (currentUserId == 0) return Unauthorized();

                var notification = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.Id == id && n.UserId == currentUserId);

                if (notification == null)
                    return NotFound(new { success = false, message = "Notification not found" });

                notification.IsRead = true;
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification as read");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead()
        {
            try
            {
                int currentUserId = GetCurrentUserId();
                if (currentUserId == 0) return Unauthorized();

                var notifications = await _context.Notifications
                    .Where(n => n.UserId == currentUserId && !n.IsRead)
                    .ToListAsync();

                foreach (var notification in notifications)
                {
                    notification.IsRead = true;
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all notifications as read");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        [Route("Notifications/MarkAllAsRead")]
        public async Task<IActionResult> MarkAllAsReadGet()
        {
            try
            {
                int currentUserId = GetCurrentUserId();
                if (currentUserId == 0) return Unauthorized();

                // Get user role
                string userRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
                
                // Base query for user's own notifications
                var query = _context.Notifications.Where(n => n.UserId == currentUserId && !n.IsRead);
                
                // Check if staff has notification access level
                if (userRole == "Staff")
                {
                    var staff = await _context.Staffs
                        .FirstOrDefaultAsync(s => s.Id == currentUserId);
                    
                    if (staff != null && staff.StaffAccessLevel.HasFlag(StaffAccessLevel.Notifications))
                    {
                        int businessOwnerId = staff.BOId;
                        
                        // Include business owner's unread notifications
                        query = query.Union(_context.Notifications
                            .Where(n => n.UserId == businessOwnerId && n.IsForBusinessOwner && !n.IsRead));
                    }
                }
                
                var notifications = await query.ToListAsync();

                foreach (var notification in notifications)
                {
                    notification.IsRead = true;
                }

                await _context.SaveChangesAsync();

                // If it's an AJAX request, return JSON
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true });
                }

                // Otherwise redirect back to notifications
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all notifications as read");
                
                // If it's an AJAX request, return JSON
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = ex.Message });
                }

                // Otherwise redirect back to notifications with error
                TempData["ErrorMessage"] = "Failed to mark notifications as read.";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                int currentUserId = GetCurrentUserId();
                if (currentUserId == 0) return Unauthorized();

                var notification = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.Id == id && n.UserId == currentUserId);

                if (notification == null)
                    return NotFound(new { success = false, message = "Notification not found" });

                _context.Notifications.Remove(notification);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting notification");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAll()
        {
            try
            {
                int currentUserId = GetCurrentUserId();
                if (currentUserId == 0) return Unauthorized();

                var notifications = await _context.Notifications
                    .Where(n => n.UserId == currentUserId)
                    .ToListAsync();

                _context.Notifications.RemoveRange(notifications);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting all notifications");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Helper method to create a notification
        public static async Task CreateNotification(
            AuthDbContext context,
            int userId,
            string title,
            string message,
            NotificationType type = NotificationType.General,
            string? url = null,
            bool isForAdmin = false,
            bool isForStaff = false,
            bool isForBusinessOwner = false)
        {
            try
            {
                var notification = new Notification
                {
                    UserId = userId,
                    Title = title,
                    Message = message,
                    Type = type,
                    Url = url,
                    CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore")),
                    IsRead = false,
                    IsForAdmin = isForAdmin,
                    IsForStaff = isForStaff,
                    IsForBusinessOwner = isForBusinessOwner
                };

                context.Notifications.Add(notification);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log the error but don't throw - notifications should be non-critical
                Console.WriteLine($"Error creating notification: {ex.Message}");
            }
        }

        // Helper method to create a notification for a specific user type
        public static async Task CreateTypedNotification(
            AuthDbContext context,
            int userId,
            string title,
            string message,
            string userRole,
            NotificationType type = NotificationType.General,
            string? url = null)
        {
            bool isForAdmin = userRole == "Admin";
            bool isForStaff = userRole == "Staff";
            bool isForBusinessOwner = userRole == "BusinessOwner";
            
            await CreateNotification(
                context,
                userId,
                title, 
                message,
                type,
                url,
                isForAdmin,
                isForStaff,
                isForBusinessOwner);
        }
        
        // Helper method to create a notification for an admin
        public static async Task CreateAdminNotification(
            AuthDbContext context,
            int adminUserId,
            string title,
            string message,
            NotificationType type = NotificationType.General,
            string? url = null)
        {
            await CreateNotification(
                context,
                adminUserId,
                title, 
                message,
                type,
                url,
                isForAdmin: true);
        }
        
        // Helper method to create a notification for a business owner
        public static async Task CreateBusinessOwnerNotification(
            AuthDbContext context,
            int businessOwnerId,
            string title,
            string message,
            NotificationType type = NotificationType.General,
            string? url = null)
        {
            await CreateNotification(
                context,
                businessOwnerId,
                title, 
                message,
                type,
                url,
                isForBusinessOwner: true);
        }

        private DateTime GetSingaporeTime() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));

        // Add a new action method to handle calendar event notifications
        [HttpPost]
        [Route("Notifications/CreateCalendarNotification")]
        public async Task<IActionResult> CreateCalendarNotification(int userId, string title, string message, string url, DateTime reminderTime)
        {
            try
            {
                if (userId <= 0)
                {
                    return BadRequest(new { success = false, message = "Invalid user ID" });
                }

                // Create the notification
                var notification = new Notification
                {
                    UserId = userId,
                    Title = title,
                    Message = message,
                    IsRead = false,
                    CreatedAt = GetSingaporeTime(),
                    Url = url,
                    NotificationTypes = "calendar"
                };

                await _context.Notifications.AddAsync(notification);
                await _context.SaveChangesAsync();

                // Send real-time notification to the user
                await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", new
                {
                    id = notification.Id,
                    title = notification.Title,
                    message = notification.Message,
                    isRead = notification.IsRead,
                    createdAt = notification.CreatedAt,
                    url = notification.Url,
                    notificationType = notification.NotificationTypes
                });

                return Ok(new { success = true, notification });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating calendar notification");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Add a new action method to handle business message notifications
        [HttpPost]
        [Route("Notifications/CreateBusinessMessageNotification")]
        public async Task<IActionResult> CreateBusinessMessageNotification(int userId, string title, string message, string url)
        {
            try
            {
                if (userId <= 0)
                {
                    return BadRequest(new { success = false, message = "Invalid user ID" });
                }

                // Create the notification
                var notification = new Notification
                {
                    UserId = userId,
                    Title = title,
                    Message = message,
                    IsRead = false,
                    CreatedAt = GetSingaporeTime(),
                    Url = url,
                    NotificationTypes = "business_message"
                };

                await _context.Notifications.AddAsync(notification);
                await _context.SaveChangesAsync();

                // Send real-time notification to the user
                await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", new
                {
                    id = notification.Id,
                    title = notification.Title,
                    message = notification.Message,
                    isRead = notification.IsRead,
                    createdAt = notification.CreatedAt,
                    url = notification.Url,
                    notificationType = notification.NotificationTypes
                });

                return Ok(new { success = true, notification });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating business message notification");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
} 
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Project_Creation.Data;
using Project_Creation.Models.Entities;

namespace Project_Creation.Controllers
{
    public class StaffActivityLogsController : Controller
    {
        private readonly AuthDbContext _context;
        private readonly ILogger<StaffActivityLogsController> _logger;
        private readonly IEmailService _emailService;
        private readonly IHubContext<RealTimeHub> _hubContext;

        public StaffActivityLogsController(
            AuthDbContext context,
            ILogger<StaffActivityLogsController> logger,
            IEmailService emailService,
            IHubContext<RealTimeHub> hubContext)
        {
            _emailService = emailService;
            _logger = logger;
            _context = context;
            _hubContext = hubContext;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> CreateStaffActivityLog(ActivityType activityType, string description)
        {
            try
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var role = User.FindFirstValue(ClaimTypes.Role);
                int staffId = int.TryParse(userIdClaim, out var parsedId) ? parsedId : 0;

                if (role != "Staff")
                {
                    return Unauthorized("Only staff members can create activity logs.");
                }
                if (userIdClaim == null)
                {
                    return Unauthorized();
                }

                var staffActivityLog = new StaffActivityLogs
                {
                    StaffId = staffId,
                    Activity = activityType,
                    Description = description
                };
                _context.StaffActivityLogs.Add(staffActivityLog);
                await _context.SaveChangesAsync();
                // Notify clients via SignalR
                //await _hubContext.Clients.All.SendAsync("ReceiveStaffActivityLog", staffActivityLog);
                return Ok(new { message = "Staff activity log created successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating staff activity log.");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}

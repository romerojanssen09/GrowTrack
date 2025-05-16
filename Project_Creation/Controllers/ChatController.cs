using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Project_Creation.Data;
using Project_Creation.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Project_Creation.DTO;
using Microsoft.AspNetCore.SignalR;
using Project_Creation.Models.Entities;

namespace Project_Creation.Controllers
{
    public class ChatController : Controller
    {
        private readonly AuthDbContext _context;
        private readonly ILogger<ChatController> _logger;
        private readonly IHubContext<RealTimeHub> _hubContext;

        public ChatController(AuthDbContext context, ILogger<ChatController> logger, IHubContext<RealTimeHub> hubContext)
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
        public async Task<IActionResult> Index(int? userId)
        {
            try
            {
                int currentUserId = GetCurrentUserId();
                if (currentUserId == 0) return Unauthorized();

                // Get users you've already chatted with (without timezone conversion)
                var existingChatmates = await _context.Users
                    .Where(u => _context.Chats
                        .Any(c => (c.SenderId == currentUserId && c.ReceiverId == u.Id) ||
                                 (c.SenderId == u.Id && c.ReceiverId == currentUserId)))
                    .Select(u => new
                    {
                        Id = u.Id,
                        FirstName = u.FirstName,
                        LastName = u.LastName,
                        Email = u.Email,
                        LastMessage = _context.Chats
                            .Where(c => (c.SenderId == currentUserId && c.ReceiverId == u.Id) ||
                                       (c.SenderId == u.Id && c.ReceiverId == currentUserId))
                            .OrderByDescending(c => c.CreatedAt)
                            .Select(c => c.Message)
                            .FirstOrDefault(),
                        LastMessageTime = _context.Chats
                            .Where(c => (c.SenderId == currentUserId && c.ReceiverId == u.Id) ||
                                       (c.SenderId == u.Id && c.ReceiverId == currentUserId))
                            .OrderByDescending(c => c.CreatedAt)
                            .Select(c => c.CreatedAt)
                            .FirstOrDefault(),
                        IsCurrentUserSender = _context.Chats
                            .Where(c => (c.SenderId == currentUserId && c.ReceiverId == u.Id) ||
                                       (c.SenderId == u.Id && c.ReceiverId == currentUserId))
                            .OrderByDescending(c => c.CreatedAt)
                            .Select(c => c.SenderId == currentUserId)
                            .FirstOrDefault(),
                        UnreadCount = _context.Chats
                            .Count(c => c.ReceiverId == currentUserId &&
                                       c.SenderId == u.Id &&
                                       !c.IsRead)
                    })
                    .ToListAsync();

                // Convert to view models with timezone conversion
                var chatmateViewModels = existingChatmates.Select(u => new ChatmateViewModel
                {
                    Id = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    LastMessage = u.LastMessage ?? "Nigga",
                    LastMessageTime = ConvertToLocalTime(u.LastMessageTime),
                    IsCurrentUserSender = u.IsCurrentUserSender,
                    UnreadCount = u.UnreadCount,
                    CurrentUser = currentUserId
                }).ToList();

                // Handle new chatmate if specified
                if (userId > 0 && !chatmateViewModels.Any(c => c.Id == userId))
                {
                    var newChatmate = await _context.Users
                        .Where(u => u.Id == userId)
                        .Select(u => new ChatmateViewModel
                        {
                            Id = u.Id,
                            FirstName = u.FirstName,
                            LastName = u.LastName,
                            Email = u.Email,
                            LastMessage = null,
                            LastMessageTime = DateTime.Now, // Local time already
                            IsCurrentUserSender = false,
                            UnreadCount = 0,
                            CurrentUser = currentUserId
                        })
                        .FirstOrDefaultAsync();

                    if (newChatmate != null)
                    {
                        chatmateViewModels.Insert(0, newChatmate);
                    }
                }

                ViewBag.SelectedUserId = userId;
                return View(chatmateViewModels.OrderByDescending(u => u.Id).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading chat index");
                return StatusCode(500);
            }
        }

        // Helper method to convert UTC to local time
        private DateTime ConvertToLocalTime(DateTime? utcTime)
        {
            if (!utcTime.HasValue) return DateTime.MinValue;

            try
            {
                // Singapore Standard Time (UTC+8)
                var sgtZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
                return TimeZoneInfo.ConvertTimeFromUtc(utcTime.Value, sgtZone);
            }
            catch
            {
                // Fallback if timezone not found
                return utcTime.Value.AddHours(8);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetMessages(int otherUserId)
        {
            int currentUserId = GetCurrentUserId();
            if (currentUserId == 0) return Unauthorized();

            // Get messages without time conversion
            var messages = await _context.Chats
                .Where(c => (c.SenderId == currentUserId && c.ReceiverId == otherUserId) ||
                           (c.SenderId == otherUserId && c.ReceiverId == currentUserId))
                .OrderBy(c => c.CreatedAt)
                .Select(c => new
                {
                    Id = c.Id,
                    SenderId = c.SenderId,
                    ReceiverId = c.ReceiverId,
                    Message = c.Message,
                    CreatedAt = c.CreatedAt
                })
                .ToListAsync();

            // Convert to DTOs with local time
            var messageDtos = messages.Select(m => new ChatDto
            {
                Id = m.Id,
                SenderId = m.SenderId,
                ReceiverId = m.ReceiverId,
                message = m.Message,
                CreatedAt = ConvertToLocalTime(m.CreatedAt)
            }).ToList();

            // Mark messages as read
            var unreadMessages = await _context.Chats
                .Where(c => c.SenderId == otherUserId &&
                           c.ReceiverId == currentUserId &&
                           !c.IsRead)
                .ToListAsync();

            foreach (var msg in unreadMessages)
            {
                msg.IsRead = true;
            }

            if (unreadMessages.Any())
            {
                await _context.SaveChangesAsync();
            }

            return Ok(messageDtos);
        }

        [HttpPost("SendMessage")]
        [Route("/Chat/SendMessage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == 0) return Unauthorized();

                var message = new Chat
                {
                    SenderId = currentUserId,
                    ReceiverId = int.Parse(request.ReceiverId),
                    Message = request.Message,
                    CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore")),
                    IsRead = false
                };

                _context.Chats.Add(message);
                await _context.SaveChangesAsync();

                // Notify via SignalR
                await _hubContext.Clients
                    .Users(request.ReceiverId, currentUserId.ToString())
                    .SendAsync("ReceiveChatMessage", currentUserId, request.ReceiverId, request.Message, TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore")));

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        public class SendMessageRequest
        {
            public string ReceiverId { get; set; }
            public string Message { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] ChatDto messageDto)
        {
            int currentUserId = GetCurrentUserId();
            if (currentUserId == 0) return Unauthorized();
            if (messageDto == null || string.IsNullOrWhiteSpace(messageDto.message) || messageDto.ReceiverId == 0)
            {
                return BadRequest(new { success = false, error = "Invalid message." });
            }

            var chat = new Chat
            {
                SenderId = currentUserId,
                ReceiverId = messageDto.ReceiverId,
                Message = messageDto.message,
                CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore")),
                IsRead = false
            };

            _context.Chats.Add(chat);
            await _context.SaveChangesAsync();

            // Send real-time message to receiver via SignalR
            await _hubContext.Clients.User(messageDto.ReceiverId.ToString()).SendAsync(
                "ReceiveChatMessage",
                currentUserId.ToString(),
                messageDto.ReceiverId.ToString(),
                messageDto.message,
                chat.CreatedAt.ToString("o")
            );

            return Ok(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetUserInfo(int userId)
        {
            var user = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => new {
                    u.FirstName,
                    u.LastName,
                    u.LogoPath
                })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound();
            }

            return Ok(new
            {
                firstName = user.FirstName,
                lastName = user.LastName,
                imageUrl = user.LogoPath ?? "/default/default-profile.png"
            });
        }
    }
}
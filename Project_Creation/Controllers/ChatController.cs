using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Project_Creation.Data;
using Project_Creation.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Project_Creation.DTO;
using Microsoft.AspNetCore.SignalR;
using Project_Creation.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using static Project_Creation.Controllers.Inventory1Controller;

namespace Project_Creation.Controllers
{
    [Authorize]
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
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                _logger.LogWarning("Invalid or missing user ID claim");
                throw new InvalidOperationException("User is not authenticated");
            }

            var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
            int boId = int.TryParse(User.FindFirstValue("BOId"), out var tempBoId) ? tempBoId : 0;
            int who = currentUserRole == "Staff" ? boId : userId;
            return who;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? userId)
        {
            try
            {
                int currentUserId = GetCurrentUserId();
                if (currentUserId == 0) return Unauthorized();

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
                        IsEdited = _context.Chats
                            .Where(c => (c.SenderId == currentUserId && c.ReceiverId == u.Id) ||
                                       (c.SenderId == u.Id && c.ReceiverId == currentUserId))
                            .OrderByDescending(c => c.CreatedAt)
                            .Select(c => c.IsEdited)
                            .FirstOrDefault(),
                        UnreadCount = _context.Chats
                            .Count(c => c.SenderId == u.Id && c.ReceiverId == currentUserId && !c.IsRead)
                    })
                    .ToListAsync();

                var chatmateViewModels = existingChatmates.Select(u => new ChatmateViewModel
                {
                    Id = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    LastMessage = u.LastMessage ?? "No messages yet",
                    LastMessageTime = ConvertToLocalTime(u.LastMessageTime),
                    IsCurrentUserSender = u.IsCurrentUserSender,
                    UnreadCount = u.UnreadCount,
                    CurrentUser = currentUserId,
                    IsEdited = u.IsEdited
                }).ToList();

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
                            LastMessageTime = DateTime.Now,
                            IsCurrentUserSender = false,
                            UnreadCount = 0,
                            CurrentUser = currentUserId,
                            IsEdited = false
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

        [HttpGet]
        public async Task<IActionResult> GetMessages(int otherUserId)
        {
            int currentUserId = GetCurrentUserId();
            if (currentUserId == 0) return Unauthorized();

            var messages = await _context.Chats
                .Where(c => (c.SenderId == currentUserId && c.ReceiverId == otherUserId) ||
                           (c.SenderId == otherUserId && c.ReceiverId == currentUserId) &&
                           c.SenderDelete == false)
                .OrderBy(c => c.CreatedAt)
                .Select(c => new
                {
                    Id = c.Id,
                    SenderId = c.SenderId,
                    ReceiverId = c.ReceiverId,
                    Message = c.Message,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt,
                    Status = c.Status,
                    JSONString = c.JSONString,
                    IsRead = c.IsRead,
                    IsEdited = c.IsEdited
                })
                .ToListAsync();

            var messageDtos = messages.Select(m => new ChatDto
            {
                Id = m.Id,
                SenderId = m.SenderId,
                ReceiverId = m.ReceiverId,
                Message = m.Message,
                CreatedAt = ConvertToLocalTime(m.CreatedAt),
                UpdatedAt = m.UpdatedAt,
                Status = m.Status,
                JSONString = m.JSONString,
                IsRead = m.IsRead,
                IsEdited = m.IsEdited
            }).ToList();

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

        [HttpPost]
        [Route("Chat/RejectRequest")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectRequest([FromBody] SendMessageRequest request)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == 0) return Unauthorized();
                var msg = await _context.Chats.FirstOrDefaultAsync(m => m.Id == request.MessageId);
                if (msg == null) return NotFound(new { success = false, error = "Message not found" });

                var rejectedMessage = new Chat
                {
                    SenderId = currentUserId,
                    ReceiverId = msg.SenderId,
                    Message = request.Reason ?? "No reason provided",
                    JSONString = msg.JSONString,
                    CreatedAt = msg.CreatedAt,
                    UpdatedAt = GetSingaporeTime(),
                    IsRead = false,
                    Status = ChatStatus.Rejected
                };

                _context.Chats.Remove(msg);
                _context.Chats.Add(rejectedMessage);
                await _context.SaveChangesAsync();

                await _hubContext.Clients
                    .Users(msg.SenderId.ToString(), currentUserId.ToString())
                    .SendAsync("SendAndRemoveMessage",
                        currentUserId.ToString(),
                        msg.SenderId.ToString(),
                        msg.Id.ToString(),
                        "Rejected",
                        Newtonsoft.Json.JsonConvert.SerializeObject(rejectedMessage));

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting request");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [Route("/Chat/EditMessage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMessage([FromBody] SendMessageRequest request)
        {
            _logger.LogInformation($"EditMessage called with MessageId: {request.MessageId}, ReceiverId: {request.RecipientId}, Message: {request.Message}");
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == 0) return Unauthorized();
                var msg = await _context.Chats.FirstOrDefaultAsync(m => m.Id == request.MessageId);
                if (msg == null) return NotFound(new { success = false, error = "Message not found" });

                // Store the old message ID before we remove it
                var oldMessageId = msg.Id;

                var EditedMessage = new Chat
                {
                    SenderId = msg.SenderId,
                    ReceiverId = msg.ReceiverId,
                    Message = request.Message ?? "",
                    CreatedAt = msg.CreatedAt,
                    UpdatedAt = GetSingaporeTime(),
                    IsRead = false,
                    Status = ChatStatus.Null,
                    IsEdited = true,
                    JSONString = msg.JSONString
                };

                _context.Chats.Remove(msg);
                _context.Chats.Add(EditedMessage);
                await _context.SaveChangesAsync();
                
                // Send notification to both sender and receiver
                await _hubContext.Clients
                    .Users(msg.SenderId.ToString(), msg.ReceiverId.ToString())
                    .SendAsync("EditMessage",
                        msg.SenderId.ToString(),
                        msg.ReceiverId.ToString(),
                        EditedMessage.Id.ToString(),
                        "Edited",
                        Newtonsoft.Json.JsonConvert.SerializeObject(EditedMessage, Newtonsoft.Json.Formatting.None, 
                            new Newtonsoft.Json.JsonSerializerSettings { 
                                ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver()
                            }),
                        oldMessageId.ToString());

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing message");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        [Route("/Chat/ApprovedRequest2")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApprovedRequest2(int MessageId, int ProductId, int Quantity, int ReceiverId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == 0) return Unauthorized();

                var msg = await _context.Chats.FirstOrDefaultAsync(m => m.Id == MessageId);
                if (msg == null) return NotFound(new { success = false, error = "Message not found" });

                // Get product details for the order
                var product = await _context.Products2.FindAsync(ProductId);
                if (product == null) 
                    return NotFound(new { success = false, error = "Product not found" });
                
                // Validate product quantity
                if (product.QuantityInStock < Quantity)
                    return BadRequest(new { success = false, error = "Not enough inventory available" });

                // Parse the JSON string to get the request data
                Dictionary<string, object> requestData;
                try {
                    requestData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(msg.JSONString ?? "{}");
                }
                catch {
                    requestData = new Dictionary<string, object>();
                }
                
                string customerMessage = string.Empty;
                if (requestData != null && requestData.TryGetValue("message", out var messageObj))
                {
                    customerMessage = messageObj?.ToString() ?? string.Empty;
                }

                // Create a ProductOrder record
                var order = new ProductOrder
                {
                    BuyerId = msg.SenderId,
                    SellerId = currentUserId,
                    ProductId = ProductId,
                    ProductName = product.ProductName,
                    Quantity = Quantity,
                    UnitPrice = product.SellingPrice,
                    TotalPrice = product.SellingPrice * Quantity,
                    Message = customerMessage,
                    RelatedChatId = msg.Id,
                    Status = ProductOrderStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                
                _context.ProductOrders.Add(order);
                
                try {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Created order #{order.Id} for product {ProductId}, quantity {Quantity}");
                }
                catch (Exception ex) {
                    _logger.LogError(ex, $"Error saving order for product {ProductId}, quantity {Quantity}");
                    return StatusCode(500, new { success = false, message = "Failed to create order: " + ex.Message });
                }

                // Create an acceptance message in the chat
                var approvedMessage = new Chat
                {
                    SenderId = currentUserId,
                    ReceiverId = msg.SenderId,
                    Message = $"Order #{order.Id} created. Your order is pending approval.",
                    JSONString = msg.JSONString,
                    CreatedAt = msg.CreatedAt,
                    UpdatedAt = GetSingaporeTime(),
                    IsRead = false,
                    Status = ChatStatus.Accepted
                };

                _context.Chats.Remove(msg);
                _context.Chats.Add(approvedMessage);
                await _context.SaveChangesAsync();

                // Notify the buyer
                var notification = new Notification
                {
                    UserId = msg.SenderId,
                    Title = "Order Created",
                    Message = $"Your order for {product.ProductName} has been created! Order #{order.Id} is pending approval.",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false,
                    Url = $"/ProductOrders/Details/{order.Id}",
                    Type = NotificationType.Order
                };
                
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                // Send real-time notification about the order
                await _hubContext.Clients.User(msg.SenderId.ToString())
                    .SendAsync("ReceiveNotification", 
                        notification.Title, 
                        notification.Message, 
                        notification.Id.ToString(), 
                        notification.Url, 
                        "Order");

                // Send chat message update
                await _hubContext.Clients
                    .Users(msg.SenderId.ToString(), currentUserId.ToString())
                    .SendAsync("SendAndRemoveMessage",
                        currentUserId.ToString(),
                        msg.SenderId.ToString(),
                        msg.Id.ToString(),
                        "Accepted",
                        Newtonsoft.Json.JsonConvert.SerializeObject(approvedMessage));

                return Ok(new { success = true, orderId = order.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving request");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [Route("/Chat/CancelRequest")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelRequest(int MessageId, int ReceiverId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == 0) return Unauthorized();

                var msg = await _context.Chats.FirstOrDefaultAsync(m => m.Id == MessageId);
                if (msg == null) return NotFound(new { success = false, error = "Message not found" });

                _context.Chats.Remove(msg);
                await _context.SaveChangesAsync();

                await _hubContext.Clients
                    .Users(msg.ReceiverId.ToString(), currentUserId.ToString())
                    .SendAsync("RemoveChat", currentUserId.ToString(), msg.ReceiverId.ToString(), msg.Id.ToString());

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error canceling request");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [Route("Chat/DeleteMessage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMessage(int MessageId, int ReceiverId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == 0) return Unauthorized();

                var msg = await _context.Chats.FirstOrDefaultAsync(m => m.Id == MessageId);
                if (msg == null) return NotFound(new { success = false, error = "Message not found" });

                _context.Chats.Remove(msg);
                await _context.SaveChangesAsync();

                await _hubContext.Clients
                    .Users(msg.ReceiverId.ToString(), currentUserId.ToString())
                    .SendAsync("RemoveChat", currentUserId.ToString(), msg.ReceiverId.ToString(), msg.Id.ToString());

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error canceling request");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [Route("Chat/DeleteChatMate")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteChatMate(int ChatmateId, int ReceiverId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == 0) return Unauthorized();

                var msg = await _context.Chats.FirstOrDefaultAsync(m => m.Id == ChatmateId);
                if (msg == null) return NotFound(new { success = false, error = "Message not found" });

                msg.SenderDelete = true;

                //_context.Chats.Remove(msg);
                await _context.SaveChangesAsync();

                await _hubContext.Clients
                    .Users(msg.ReceiverId.ToString(), currentUserId.ToString())
                    .SendAsync("RemoveChatmate", currentUserId.ToString(), msg.ReceiverId.ToString());

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error canceling request");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [Route("Chat/SendMessage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == 0)
                {
                    return Unauthorized(new { success = false, message = "Unauthorized" });
                }

                var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == currentUserId);
                var recipient = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.RecipientId);

                if (recipient == null)
                {
                    return BadRequest(new { success = false, message = "Recipient not found" });
                }

                var chat = new Chat
                {
                    SenderId = currentUserId,
                    ReceiverId = request.RecipientId,
                    Message = request.Message,
                    CreatedAt = GetSingaporeTime(),
                    UpdatedAt = GetSingaporeTime(),
                    IsRead = false,
                    IsEdited = false
                };
                _context.Chats.Add(chat);
                await _context.SaveChangesAsync();

                // Create notification for the recipient
                string businessName = "";
                if (currentUser != null)
                {
                    if (currentUser.UserRole == "BusinessOwner")
                    {
                        var businessProfile = await _context.BOBusinessProfiles
                            .FirstOrDefaultAsync(b => b.UserId == currentUserId);
                        businessName = businessProfile?.ShopName ?? currentUser.FirstName + " " + currentUser.LastName;
                    }
                    else
                    {
                        businessName = currentUser.FirstName + " " + currentUser.LastName;
                    }
                }

                // Create a notification for the message recipient
                // await NotificationsController.CreateNotification(
                //     _context,
                //     request.RecipientId,
                //     $"New message from {businessName}",
                //     request.Message.Length > 50 ? request.Message.Substring(0, 50) + "..." : request.Message,
                //     NotificationType.Chat,
                //     $"/Chat/Index?userId={currentUserId}"
                // );

                // Send real-time notification using SignalR
                await _hubContext.Clients.User(request.RecipientId.ToString()).SendAsync(
                    "ReceivePrivateNotification",
                    $"New message from {businessName}",
                    request.Message.Length > 50 ? request.Message.Substring(0, 50) + "..." : request.Message,
                    chat.Id.ToString(),
                    $"/Chat/Index?userId={currentUserId}",
                    "Chat"
                );
                
                // Send the actual message to the chat window for real-time updates
                await _hubContext.Clients.User(request.RecipientId.ToString()).SendAsync(
                    "ReceiveChatMessage",
                    currentUserId.ToString(),
                    request.RecipientId.ToString(),
                    request.Message,
                    chat.CreatedAt.ToString("o"),
                    chat.Id.ToString()
                );
                
                // Update the unread chat count for the recipient
                var unreadChatCount = await _context.Chats
                    .Where(c => c.ReceiverId == request.RecipientId && !c.IsRead)
                    .Select(c => c.SenderId)
                    .Distinct()
                    .CountAsync();
                    
                await _hubContext.Clients.User(request.RecipientId.ToString()).SendAsync(
                    "UpdateChatCount",
                    unreadChatCount
                );

                return Ok(new { success = true, message = "Message sent successfully", chatId = chat.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUserInfo(int userId)
        {
            try
            {
                var user = await _context.Users
                    .Where(u => u.Id == userId)
                    .Select(u => new {
                        u.FirstName,
                        u.LastName,
                        ImageUrl = u.LogoPath ?? "/default/default-profile.png"
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return NotFound(new { success = false, error = "User not found" });
                }

                return Ok(new
                {
                    success = true,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    imageUrl = user.ImageUrl
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user info");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        private DateTime ConvertToLocalTime(DateTime? utcTime)
        {
            if (!utcTime.HasValue) return DateTime.MinValue;

            try
            {
                var sgtZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
                return TimeZoneInfo.ConvertTimeFromUtc(utcTime.Value, sgtZone);
            }
            catch
            {
                return utcTime.Value.AddHours(8);
            }
        }

        // This method is kept for backward compatibility but is no longer used directly
        private async Task<IActionResult> QuickSale(int receiverId, int quantity, int productId)
        {
            var userId = GetCurrentUserId();
            var receiverData = await _context.Users
                .Where(u => u.Id == receiverId)
                .Select(u => new { u.FirstName, u.LastName })
                .FirstOrDefaultAsync();

            var product = await _context.Products2
                .FirstOrDefaultAsync(p => p.Id == productId && p.QuantityInStock > 0);

            if (product == null || receiverData == null) return NotFound();

            var saleItem = new SaleItem
            {
                ProductId = product.Id,
                ProductName = product.ProductName,
                Quantity = quantity,
                UnitPrice = product.SellingPrice,
                TotalPrice = product.SellingPrice * quantity,
                Notes = $"QuickSale to {receiverData.FirstName} {receiverData.LastName}, transaction in chat"
            };

            var sale = new Sale
            {
                BOId = userId,
                CustomerName = $"{receiverData.FirstName} {receiverData.LastName}",
                SaleDate = DateTime.UtcNow,
                TotalAmount = saleItem.TotalPrice,
                SaleItems = new List<SaleItem> { saleItem }
            };

            var log = new InventoryLog
            {
                BOId = userId,
                ProductId = product.Id,
                ProductName = product.ProductName,
                QuantityBefore = product.QuantityInStock,
                QuantityAfter = product.QuantityInStock - quantity,
                MovementType = InventoryMovementTypes.Sale,
                ReferenceId = $"SALE-{sale.Id}",
                Notes = $"QuickSale to {sale.CustomerName}, transaction in chat",
                Timestamp = DateTime.UtcNow,
            };

            product.QuantityInStock -= quantity;

            _context.Sales.Add(sale);
            _context.InventoryLogs.Add(log);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Quick sale completed." });
        }

        private DateTime GetSingaporeTime() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));

        public class SendMessageRequest
        {
            public int RecipientId { get; set; }
            public string? Message { get; set; }
            public int? MessageId { get; set; }
            public string? Reason { get; set; }
            public int? ProductId { get; set; }
            public int? Quantity { get; set; }
        }

        public class SendMessageRequestLead
        {
            public int? ReceiverId { get; set; }
            public string? Message { get; set; }
            public int? MessageId { get; set; }
            public string? Reason { get; set; }
            public int? BOId { get; set; }
            public int? LeadId { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> GetUnreadChatCount()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0) return Unauthorized(new { success = false, message = "Unauthorized" });

                // Get unique unread chat senders
                var unreadChatCount = await _context.Chats
                    .Where(c => c.ReceiverId == userId && !c.IsRead)
                    .Select(c => c.SenderId)
                    .Distinct()
                    .CountAsync();

                return Ok(new { success = true, unreadCount = unreadChatCount });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [Route("Chat/MarkAsRead")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int otherUserId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == 0) return Unauthorized(new { success = false, message = "Unauthorized" });

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

                // Update the unread chat count for the current user
                var unreadChatCount = await _context.Chats
                    .Where(c => c.ReceiverId == currentUserId && !c.IsRead)
                    .Select(c => c.SenderId)
                    .Distinct()
                    .CountAsync();
                    
                await _hubContext.Clients.User(currentUserId.ToString()).SendAsync(
                    "UpdateChatCount",
                    unreadChatCount
                );

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}

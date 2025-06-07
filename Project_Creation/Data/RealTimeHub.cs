using Microsoft.AspNetCore.SignalR;
using Project_Creation.Models.Entities;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Project_Creation.Data
{
    // DTO to avoid circular references when sending inventory movements through SignalR
    public class InventoryLogDto
    {
        public int Id { get; set; }
        public int BOId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int QuantityBefore { get; set; }
        public int QuantityAfter { get; set; }
        public string MovementType { get; set; }
        public string ReferenceId { get; set; }
        public string Notes { get; set; }
        public DateTime Timestamp { get; set; }
        
        // Static method to convert from entity to DTO
        public static InventoryLogDto FromEntity(InventoryLog entity)
        {
            if (entity == null) return null;
            
            return new InventoryLogDto
            {
                Id = entity.Id,
                BOId = entity.BOId,
                ProductId = entity.ProductId,
                ProductName = entity.ProductName,
                QuantityBefore = entity.QuantityBefore,
                QuantityAfter = entity.QuantityAfter,
                MovementType = entity.MovementType,
                ReferenceId = entity.ReferenceId,
                Notes = entity.Notes,
                Timestamp = entity.Timestamp
            };
        }
    }

    public class RealTimeHub : Hub
    {
        private readonly IHubContext<RealTimeHub> _hubContext;

        public RealTimeHub(IHubContext<RealTimeHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task SendMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }

        public async Task SendNotification(string user, string notification)
        {
            await Clients.All.SendAsync("ReceiveNotification", user, notification);
        }

        public async Task SendPrivateNotification(string userId, string title, string message, string notificationId, string url = "", string type = "General")
        {
            await Clients.User(userId).SendAsync("ReceivePrivateNotification", title, message, notificationId, url, type);
        }

        public async Task UpdateNotificationCount(string userId, int count)
        {
            await Clients.User(userId).SendAsync("UpdateNotificationCount", count);
        }

        public async Task UpdateChatCount(string userId, int count)
        {
            await Clients.User(userId).SendAsync("UpdateChatCount", count);
        }

        public async Task SendChatMessage(string sender, string receiver, string message, string createdAt, string messageId)
        {
            await Clients.Users(receiver, sender).SendAsync("ReceiveChatMessage", sender, receiver, message, createdAt, messageId);
        }

        public async Task RemoveChat(string sender, string receiver, string messageId)
        {
            await Clients.Users(receiver, sender).SendAsync("RemoveChat", sender, receiver, messageId);
        }

        public async Task RemoveChatmate(string sender, string receiver)
        {
            await Clients.Users(receiver, sender).SendAsync("RemoveChatmate", sender, receiver);
        }

        public async Task SendProductRequest2(string sender, string receiver, string status, string chatJson, string messageId)
        {
            await Clients.Users(receiver, sender).SendAsync("SendProductRequest2", sender, receiver, status, chatJson, messageId);
        }

        // Add these methods to your hub
        public async Task SendLeadRequest(int senderId, int receiverId, string status, string jsonString, int messageId)
        {
            await Clients.Users(senderId.ToString(), receiverId.ToString())
                .SendAsync("ReceiveLeadRequest", senderId, receiverId, messageId, status, jsonString);
        }

        public async Task NotifyLeadRequest(int senderId, int receiverId, int messageId, string senderName, string leadName, int leadId, string createdAt, string status, string jsonString)
        {
            await Clients.Users(receiverId.ToString())
                .SendAsync("ReceiveLeadRequestNotification", senderId, receiverId, messageId, senderName, leadName, leadId, createdAt, status, jsonString);
        }

        public async Task SendAndRemoveMessage(string sender, string receiver, string messageId, string status, string chatJson)
        {
            await Clients.Users(receiver, sender).SendAsync("SendAndRemoveMessage", sender, receiver, messageId, status, chatJson);
        }

        public async Task EditMessage(string sender, string receiver, string messageId, string status, string chatJson, string oldMessageId)
        {
            await Clients.Users(receiver, sender)
                .SendAsync("EditMessage", sender, receiver, messageId, status, chatJson, oldMessageId);
        }

        public async Task SendProductRequest(string receiverId, string senderId, int messageId, int productId, string productName, int quantity, string customerName, string message, string createdAt)
        {
            await Clients.Users(receiverId, senderId).SendAsync("ReceiveProductRequest", messageId, productId, productName, quantity, customerName, message, createdAt);
        }

        public async Task ReceiveLeadRequest(string receiverId, string senderId, int messageId, string senderName, string leadName, int leadId, string createdAt, string status, string jsonString)
        {
            await Clients.Users(receiverId, senderId).SendAsync("ReceiveLeadRequest", senderId, receiverId, messageId, senderName, leadName, leadId, createdAt, status, jsonString);
        }

        // Notify users about inventory movements in real-time
        public async Task NotifyInventoryMovement(string userId, InventoryLog movement)
        {
            // Convert to DTO to avoid circular references
            var movementDto = InventoryLogDto.FromEntity(movement);
            
            // Send to the specific user who owns this inventory
            await Clients.User(userId).SendAsync("ReceiveInventoryMovement", movementDto);
            
            // Also send to any staff members in the business owner's group
            await Clients.Group($"business_{movement.BOId}").SendAsync("ReceiveInventoryMovement", movementDto);
        }

        // Join a business owner's inventory group to receive updates
        public async Task JoinInventoryGroup(string businessId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"business_{businessId}");
        }

        // Allow staff members to join a group with their ID for targeted updates
        public async Task JoinStaffGroup(string staffId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"staff_{staffId}");
        }

        // Notify a specific staff member about access level changes
        public async Task NotifyAccessLevelChanged(string staffId, List<string> previous, List<string> added, List<string> removed)
        {
            await Clients.Group($"staff_{staffId}").SendAsync("AccessLevelChanged", staffId, previous, added, removed);
        }

        // Add a new method to handle direct inventory log objects
        public async Task SendInventoryMovementUpdate(InventoryLog movement)
        {
            if (movement == null)
            {
                return;
            }
            
            try
            {
                // Convert to DTO to avoid circular references
                var movementDto = InventoryLogDto.FromEntity(movement);
                
                // Send to the business group
                await Clients.Group($"business_{movement.BOId}").SendAsync("ReceiveInventoryMovement", movementDto);
                
                // Also send to the specific user
                await Clients.User(movement.BOId.ToString()).SendAsync("ReceiveInventoryMovement", movementDto);
            }
            catch (Exception ex)
            {
                // Log the exception but don't throw it
                System.Diagnostics.Debug.WriteLine($"Error sending inventory movement: {ex.Message}");
            }
        }

        // New method to send multiple inventory movements in a batch (for QuickSale)
        public async Task SendBatchInventoryMovements(List<InventoryLog> movements, int businessOwnerId)
        {
            if (movements == null || !movements.Any())
            {
                return;
            }
            
            try
            {
                // Convert all movements to DTOs
                var movementDtos = movements.Select(m => InventoryLogDto.FromEntity(m)).ToList();
                
                // Send batch to the business group
                await Clients.Group($"business_{businessOwnerId}").SendAsync("ReceiveBatchInventoryMovements", movementDtos);
                
                // Also send to the specific user
                await Clients.User(businessOwnerId.ToString()).SendAsync("ReceiveBatchInventoryMovements", movementDtos);
            }
            catch (Exception ex)
            {
                // Log the exception but don't throw it
                System.Diagnostics.Debug.WriteLine($"Error sending batch inventory movements: {ex.Message}");
            }
        }
    }
}

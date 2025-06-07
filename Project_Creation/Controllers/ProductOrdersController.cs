using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Project_Creation.Data;
using Project_Creation.Models.Entities;
using Project_Creation.Models.ViewModels;
using static Project_Creation.Controllers.Inventory1Controller;

namespace Project_Creation.Controllers
{
    [Authorize]
    public class ProductOrdersController : Controller
    {
        private readonly AuthDbContext _context;
        private readonly ILogger<ProductOrdersController> _logger;
        private readonly IHubContext<RealTimeHub> _hubContext;

        public ProductOrdersController(
            AuthDbContext context,
            ILogger<ProductOrdersController> logger,
            IHubContext<RealTimeHub> hubContext)
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

            return userId;
        }

        // GET: ProductOrders
        public async Task<IActionResult> Index(string status = null, string search = null, int page = 1)
        {
            var userId = GetCurrentUserId();
            var pageSize = 10;
            
            // Base query - get orders where user is either buyer or seller
            var query = _context.ProductOrders
                .Include(o => o.Buyer)
                .Include(o => o.Seller)
                .Where(o => o.BuyerId == userId || o.SellerId == userId);
                
            // Apply filters
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<ProductOrderStatus>(status, out var statusEnum))
            {
                query = query.Where(o => o.Status == statusEnum);
            }
            
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(o => 
                    o.ProductName.Contains(search) ||
                    o.Buyer.FirstName.Contains(search) ||
                    o.Buyer.LastName.Contains(search) ||
                    o.Seller.FirstName.Contains(search) ||
                    o.Seller.LastName.Contains(search) ||
                    o.Seller.BusinessName.Contains(search)
                );
            }
            
            // Get total count for pagination
            var totalCount = await query.CountAsync();
            
            // Get status counts
            var allOrders = await _context.ProductOrders
                .Where(o => o.BuyerId == userId || o.SellerId == userId)
                .ToListAsync();
                
            var pendingCount = allOrders.Count(o => o.Status == ProductOrderStatus.Pending);
            var acceptedCount = allOrders.Count(o => o.Status == ProductOrderStatus.Accepted);
            var preparingCount = allOrders.Count(o => o.Status == ProductOrderStatus.Preparing);
            var shippingCount = allOrders.Count(o => o.Status == ProductOrderStatus.Shipping);
            var deliveredCount = allOrders.Count(o => o.Status == ProductOrderStatus.Delivered);
            var receivedCount = allOrders.Count(o => o.Status == ProductOrderStatus.Received);
            var cancelledCount = allOrders.Count(o => o.Status == ProductOrderStatus.Cancelled);
            var rejectedCount = allOrders.Count(o => o.Status == ProductOrderStatus.Rejected);
            
            // Get paginated data
            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
                
            // Map to view models
            var viewModels = orders.Select(o => new ProductOrderViewModel
            {
                Id = o.Id,
                BuyerId = o.BuyerId,
                BuyerName = $"{o.Buyer.FirstName} {o.Buyer.LastName}",
                SellerId = o.SellerId,
                SellerName = $"{o.Seller.FirstName} {o.Seller.LastName}",
                BusinessName = o.Seller.BusinessName,
                ProductId = o.ProductId,
                ProductName = o.ProductName,
                ProductImageUrl = GetProductMainImage(o.ProductId),
                Quantity = o.Quantity,
                UnitPrice = o.UnitPrice,
                TotalPrice = o.TotalPrice,
                Message = o.Message,
                Status = o.Status,
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt,
                PreparedAt = o.PreparedAt,
                ShippedAt = o.ShippedAt,
                DeliveredAt = o.DeliveredAt,
                ReceivedAt = o.ReceivedAt
            }).ToList();
            
            var viewModel = new ProductOrderListViewModel
            {
                Orders = viewModels,
                TotalOrders = totalCount,
                CurrentPage = page,
                PageSize = pageSize,
                StatusFilter = status,
                SearchQuery = search,
                IsSellerView = false, // We'll determine this in the view based on the order
                PendingCount = pendingCount,
                AcceptedCount = acceptedCount,
                PreparingCount = preparingCount,
                ShippingCount = shippingCount,
                DeliveredCount = deliveredCount,
                ReceivedCount = receivedCount,
                CancelledCount = cancelledCount,
                RejectedCount = rejectedCount
            };
            
            return View(viewModel);
        }
        
        // GET: ProductOrders/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var userId = GetCurrentUserId();
            
            var order = await _context.ProductOrders
                .Include(o => o.Buyer)
                .Include(o => o.Seller)
                .Include(o => o.Product)
                .FirstOrDefaultAsync(o => o.Id == id && (o.BuyerId == userId || o.SellerId == userId));
                
            if (order == null)
            {
                return NotFound();
            }
            
            var viewModel = new ProductOrderViewModel
            {
                Id = order.Id,
                BuyerId = order.BuyerId,
                BuyerName = $"{order.Buyer.FirstName} {order.Buyer.LastName}",
                SellerId = order.SellerId,
                SellerName = $"{order.Seller.FirstName} {order.Seller.LastName}",
                BusinessName = order.Seller.BusinessName,
                ProductId = order.ProductId,
                ProductName = order.ProductName,
                ProductImageUrl = GetProductMainImage(order.ProductId),
                Quantity = order.Quantity,
                UnitPrice = order.UnitPrice,
                TotalPrice = order.TotalPrice,
                Message = order.Message,
                Status = order.Status,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt,
                PreparedAt = order.PreparedAt,
                ShippedAt = order.ShippedAt,
                DeliveredAt = order.DeliveredAt,
                ReceivedAt = order.ReceivedAt
            };
            
            return View(viewModel);
        }
        
        // POST: ProductOrders/UpdateStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, ProductOrderStatus status)
        {
            var userId = GetCurrentUserId();
            
            var order = await _context.ProductOrders
                .Include(o => o.Buyer)
                .Include(o => o.Seller)
                .FirstOrDefaultAsync(o => o.Id == id);
                
            if (order == null)
            {
                return NotFound();
            }
            
            // Only seller can update status (except for Received, which only buyer can set)
            if (status == ProductOrderStatus.Received)
            {
                if (order.BuyerId != userId)
                {
                    return Forbid();
                }
            }
            else
            {
                if (order.SellerId != userId)
                {
                    return Forbid();
                }
            }
            
            // Validate status transition
            if (!IsValidStatusTransition(order.Status, status))
            {
                TempData["ErrorMessage"] = "Invalid status transition";
                return RedirectToAction(nameof(Details), new { id = order.Id });
            }
            
            // Update status and timestamps
            order.Status = status;
            order.UpdatedAt = DateTime.UtcNow;
            
            switch (status)
            {
                case ProductOrderStatus.Preparing:
                    order.PreparedAt = DateTime.UtcNow;
                    break;
                case ProductOrderStatus.Shipping:
                    order.ShippedAt = DateTime.UtcNow;
                    break;
                case ProductOrderStatus.Delivered:
                    order.DeliveredAt = DateTime.UtcNow;
                    break;
                case ProductOrderStatus.Received:
                    order.ReceivedAt = DateTime.UtcNow;
                    // Now reduce inventory
                    await ReduceInventory(order);
                    break;
            }
            
            _context.Update(order);
            await _context.SaveChangesAsync();
            
            // Notify the other party
            var otherUserId = order.SellerId == userId ? order.BuyerId : order.SellerId;
            await SendStatusUpdateNotification(order, otherUserId);
            
            TempData["SuccessMessage"] = $"Order status updated to {status}";
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }
        
        // POST: ProductOrders/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var userId = GetCurrentUserId();
            
            var order = await _context.ProductOrders
                .Include(o => o.Buyer)
                .Include(o => o.Seller)
                .FirstOrDefaultAsync(o => o.Id == id);
                
            if (order == null)
            {
                return NotFound();
            }
            
            // Both buyer and seller can cancel if the order is not past preparing
            if (order.Status > ProductOrderStatus.Preparing && order.SellerId != userId)
            {
                return Forbid();
            }
            
            order.Status = ProductOrderStatus.Cancelled;
            order.UpdatedAt = DateTime.UtcNow;
            
            _context.Update(order);
            await _context.SaveChangesAsync();
            
            // Notify the other party
            var otherUserId = order.SellerId == userId ? order.BuyerId : order.SellerId;
            await SendStatusUpdateNotification(order, otherUserId);
            
            TempData["SuccessMessage"] = "Order cancelled successfully";
            return RedirectToAction(nameof(Index));
        }
        
        private bool IsValidStatusTransition(ProductOrderStatus currentStatus, ProductOrderStatus newStatus)
        {
            return (currentStatus, newStatus) switch
            {
                (ProductOrderStatus.Pending, ProductOrderStatus.Accepted) => true,
                (ProductOrderStatus.Accepted, ProductOrderStatus.Preparing) => true,
                (ProductOrderStatus.Preparing, ProductOrderStatus.Shipping) => true,
                (ProductOrderStatus.Shipping, ProductOrderStatus.Delivered) => true,
                (ProductOrderStatus.Delivered, ProductOrderStatus.Received) => true,
                (ProductOrderStatus.Pending, ProductOrderStatus.Cancelled) => true,
                (ProductOrderStatus.Pending, ProductOrderStatus.Rejected) => true,
                (ProductOrderStatus.Accepted, ProductOrderStatus.Cancelled) => true,
                (ProductOrderStatus.Preparing, ProductOrderStatus.Cancelled) => true,
                _ => false
            };
        }
        
        private async Task ReduceInventory(ProductOrder order)
        {
            try
            {
                var product = await _context.Products2.FindAsync(order.ProductId);
                if (product == null)
                {
                    _logger.LogError("Product not found when reducing inventory for order {OrderId}", order.Id);
                    return;
                }
                
                // Store old quantity for logging
                var oldQuantity = product.QuantityInStock;
                
                // Reduce inventory
                product.QuantityInStock -= order.Quantity;
                if (product.QuantityInStock < 0)
                {
                    product.QuantityInStock = 0;
                }
                
                // Create sale record
                var saleItem = new SaleItem
                {
                    ProductId = product.Id,
                    ProductName = product.ProductName,
                    Quantity = order.Quantity,
                    UnitPrice = order.UnitPrice,
                    TotalPrice = order.TotalPrice,
                    Notes = $"Sale from order #{order.Id}"
                };
                
                var sale = new Sale
                {
                    BOId = order.SellerId,
                    CustomerName = $"{order.Buyer.FirstName} {order.Buyer.LastName}",
                    SaleDate = DateTime.UtcNow,
                    TotalAmount = order.TotalPrice,
                    SaleItems = new List<SaleItem> { saleItem }
                };
                
                // Create inventory log
                var log = new InventoryLog
                {
                    BOId = order.SellerId,
                    ProductId = product.Id,
                    ProductName = product.ProductName,
                    QuantityBefore = oldQuantity,
                    QuantityAfter = product.QuantityInStock,
                    MovementType = InventoryMovementTypes.Sale,
                    ReferenceId = $"ORDER-{order.Id}",
                    Notes = $"Sale to {order.Buyer.FirstName} {order.Buyer.LastName} from order #{order.Id}",
                    Timestamp = DateTime.UtcNow
                };
                
                _context.Sales.Add(sale);
                _context.InventoryLogs.Add(log);
                _context.Update(product);
                await _context.SaveChangesAsync();
                
                // Send real-time notification through SignalR
                try
                {
                    // Create a simple anonymous object with only the necessary properties to avoid circular references
                    var logDto = new
                    {
                        Id = log.Id,
                        BOId = log.BOId,
                        ProductId = log.ProductId,
                        ProductName = log.ProductName,
                        QuantityBefore = log.QuantityBefore,
                        QuantityAfter = log.QuantityAfter,
                        MovementType = log.MovementType,
                        ReferenceId = log.ReferenceId,
                        Notes = log.Notes,
                        Timestamp = log.Timestamp
                    };
                    
                    // Send to the business group
                    await _hubContext.Clients.Group($"business_{order.SellerId}").SendAsync("ReceiveInventoryMovement", logDto);
                    
                    // Also send to the specific user in case they're not in the group
                    await _hubContext.Clients.User(order.SellerId.ToString()).SendAsync("ReceiveInventoryMovement", logDto);
                    
                    _logger.LogInformation("Successfully sent real-time notification for inventory movement");
                }
                catch (Exception ex)
                {
                    // Log the error but continue - don't fail the entire operation just because of SignalR
                    _logger.LogError(ex, "Error sending real-time notification for inventory movement");
                }
                
                _logger.LogInformation("Inventory reduced for order {OrderId}, product {ProductId}, quantity {Quantity}", 
                    order.Id, order.ProductId, order.Quantity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reducing inventory for order {OrderId}", order.Id);
            }
        }
        
        private async Task SendStatusUpdateNotification(ProductOrder order, int recipientUserId)
        {
            try
            {
                var statusText = order.Status.ToString();
                var title = $"Order #{order.Id} - {statusText}";
                var message = $"Your order for {order.ProductName} has been updated to: {statusText}";
                
                // Create notification in the database
                var notification = new Notification
                {
                    UserId = recipientUserId,
                    Title = title,
                    Message = message,
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false,
                    Url = $"/ProductOrders/Details/{order.Id}",
                    Type = NotificationType.Order
                };
                
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
                
                // Send real-time notification
                await _hubContext.Clients.User(recipientUserId.ToString())
                    .SendAsync("ReceiveNotification", 
                        title, 
                        message, 
                        notification.Id.ToString(), 
                        notification.Url,
                        "Order");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending status update notification for order {OrderId}", order.Id);
            }
        }
        
        private string GetProductMainImage(int productId)
        {
            var mainImage = _context.ProductImages
                .Where(pi => pi.ProductId == productId && pi.IsMainImage)
                .Select(pi => pi.ImagePath)
                .FirstOrDefault();
                
            if (!string.IsNullOrEmpty(mainImage))
            {
                return mainImage;
            }
            
            // Try to get any image if no main image
            var anyImage = _context.ProductImages
                .Where(pi => pi.ProductId == productId)
                .Select(pi => pi.ImagePath)
                .FirstOrDefault();
                
            return anyImage ?? "/images/no-image.png";
        }
    }
} 
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Creation.Data;
using System.Security.Claims;
using Project_Creation.Models.ViewModels;
using Project_Creation.DTO;
using static Project_Creation.Controllers.Inventory1Controller;

namespace Project_Creation.Controllers
{
    public class DashboardController : Controller
    {
        private readonly AuthDbContext _context;
        private readonly ILogger<DashboardController> _logger;
        private readonly IEmailService _emailService;

        public DashboardController(AuthDbContext context, IEmailService emailService, ILogger<DashboardController> logger)
        {
            _context = context;
            _logger = logger;
            _emailService = emailService;
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

        [HttpGet]
        public async Task<IActionResult> GetQuantity(int id)
        {
            var product = await _context.Products2.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            return Json(new { quantity = product.QuantityInStock });
        }

        [HttpGet]
        public async Task<IActionResult> GetBusinessMetrics()
        {
            try
            {
                var userId = GetCurrentUserId();
                //var threshold = await GetUserLowStockThreshold(userId);

                // Calculate inventory value (sum of purchase price * quantity)
                var inventoryValue = await _context.Products2
                    .Where(p => p.BOId == userId)
                    .SumAsync(p => p.PurchasePrice * p.QuantityInStock);

                // Count low stock items
                var lowStockCount = await _context.Products2
                    .Where(p => p.BOId == userId && p.QuantityInStock <= p.ReorderLevel)
                    .CountAsync();

                // Calculate today's sales (handle case where Sales table doesn't exist)
                decimal todaysSales = 0;
                try
                {
                    todaysSales = await _context.Sales
                        .Where(s => s.BOId == userId && s.SaleDate.Date == DateTime.Today)
                        .SumAsync(s => s.TotalAmount);
                }
                catch { /* Table might not exist yet */ }

                // Count total products
                var totalProducts = await _context.Products2
                    .Where(p => p.BOId == userId)
                    .CountAsync();

                return Json(new
                {
                    success = true,
                    inventoryValue,
                    lowStockCount,
                    todaysSales,
                    totalProducts
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message,
                    inventoryValue = 0,
                    lowStockCount = 0,
                    todaysSales = 0,
                    totalProducts = 0
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetInventoryOverview(int limit = 10)
        {
            try
            {
                var userId = GetCurrentUserId();

                var products = await _context.Products2
                    .Where(p => p.BOId == userId)
                    .OrderByDescending(p => p.QuantityInStock)
                    .Take(limit)
                    .Select(p => new {
                        name = p.ProductName,
                        category = p.Category,
                        quantity = p.QuantityInStock,
                        value = p.PurchasePrice * p.QuantityInStock,
                        reorderLevel = p.ReorderLevel
                    })
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    products
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSupplierData(int id)
        {
            try
            {
                var supplier = await _context.Supplier2
                    .Where(s => s.SupplierID == id)
                    .Select(s => new {
                        id = s.SupplierID,
                        name = s.SupplierName,
                        email = s.Email,
                        phone = s.Phone
                    })
                    .FirstOrDefaultAsync();

                if (supplier == null)
                {
                    return Json(new { success = false, message = "Supplier not found" });
                }

                return Json(new { success = true, data = supplier });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting supplier data");
                return Json(new { success = false, message = "Error retrieving supplier data" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendMessageToSupplier([FromBody] SupplierMessageDto messageDto)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(messageDto.Subject) ||
                    string.IsNullOrWhiteSpace(messageDto.Message))
                {
                    return Json(new { success = false, message = "Subject and message are required" });
                }

                // Get supplier details
                var supplier = await _context.Supplier2
                    .FirstOrDefaultAsync(s => s.SupplierID == messageDto.SupplierId);

                if (supplier == null)
                {
                    return Json(new { success = false, message = "Supplier not found" });
                }

                if (string.IsNullOrWhiteSpace(supplier.Email))
                {
                    return Json(new { success = false, message = "Supplier email is not available" });
                }

                // Get current user (sender) details
                var currentUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == GetCurrentUserId());

                // Send email
                var emailSubject = $"Message from {currentUser?.FirstName} {currentUser?.LastName}: {messageDto.Subject}";
                var emailBody = $@"
                    <h3>Message from {currentUser?.FirstName} {currentUser?.LastName}</h3>
                    <p><strong>Subject:</strong> {messageDto.Subject}</p>
                    <p><strong>Message:</strong></p>
                    <p>{messageDto.Message}</p>
                    <p>You can reply to this email directly to contact the sender.</p>
                ";

                await _emailService.SendEmail2(
                    User.FindFirstValue(ClaimTypes.Email),
                    User.FindFirstValue(ClaimTypes.Name),
                    supplier.Email,
                    emailSubject,
                    emailBody,
                    true);

                return Json(new { success = true, message = "Message sent successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to supplier");
                return Json(new { success = false, message = "Error sending message" });
            }
        }

        //[HttpPost]
        //public async Task<IActionResult> SendMessageToSupplier([FromBody] SupplierMessageDto dto)
        //{
        //    var supplier = await _context.Supplier2.FindAsync(dto.SupplierId);
        //    if (supplier == null) return NotFound(new { success = false, message = "Supplier not found." });
        //    if (supplier.Email == null) return BadRequest(new { success = false, message = "Supplier email is not Set." });

        //    await _emailService.SendEmail(supplier.Email, $"Your Customer: {dto.Subject}", dto.Message);

        //    return Ok(new { success = true, message = "Message sent successfully." });
        //}

        [HttpGet]
        public async Task<IActionResult> GetLowStockAlerts(int limit = 5)
        {
            try
            {
                var userId = GetCurrentUserId();
                //var threshold = await GetUserLowStockThreshold(userId);

                var alerts = await _context.Products2
                    .Where(p => p.BOId == userId && p.QuantityInStock <= p.ReorderLevel)
                    .OrderBy(p => p.QuantityInStock)
                    .Take(limit)
                    .Select(p => new {
                        id = p.Id,
                        supplierId = p.SupplierId,
                        productName = p.ProductName,
                        currentStock = p.QuantityInStock,
                        reorderLevel = p.ReorderLevel
                    })
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    alerts,
                    totalAlerts = alerts.Count
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSalesTrend(string type = "days", int days = 7, int weeks = 0, int month = 0, int year = 0)
        {
            try
            {
                var userId = GetCurrentUserId();
                DateTime startDate;
                DateTime endDate = DateTime.Today;
                List<DateTime> dateRange;
                string dateFormat = "yyyy-MM-dd";
                string labelFormat = "MMM dd";

                switch (type.ToLower())
                {
                    case "days":
                        // Handle days filter
                        startDate = endDate.AddDays(-days + 1); // Include today
                        dateRange = Enumerable.Range(0, days)
                            .Select(offset => startDate.AddDays(offset))
                            .ToList();
                        labelFormat = "MMM dd";
                        break;

                    case "weeks":
                        // Handle weeks filter
                        if (weeks <= 0) weeks = 1; // Default to 1 week if invalid
                        startDate = endDate.AddDays(-(weeks * 7) + 1); // Include today
                        dateRange = Enumerable.Range(0, weeks * 7)
                            .Select(offset => startDate.AddDays(offset))
                            .ToList();
                        labelFormat = "MMM dd";
                        break;

                    case "months":
                        // Handle month/year filter
                        if (month <= 0 || month > 12) month = DateTime.Today.Month;
                        if (year <= 0) year = DateTime.Today.Year;

                        // Get the first and last day of the selected month
                        startDate = new DateTime(year, month, 1);
                        endDate = startDate.AddMonths(1).AddDays(-1);

                        // Generate all days in the month
                        int daysInMonth = DateTime.DaysInMonth(year, month);
                        dateRange = Enumerable.Range(1, daysInMonth)
                            .Select(day => new DateTime(year, month, day))
                            .ToList();
                        labelFormat = "MMM dd";
                        break;

                    case "years":
                        // Handle year filter
                        if (year <= 0) year = DateTime.Today.Year;

                        // Get the first and last day of the selected year
                        startDate = new DateTime(year, 1, 1);
                        endDate = new DateTime(year, 12, 31);

                        // Generate all months in the year
                        dateRange = Enumerable.Range(1, 12)
                            .Select(month => new DateTime(year, month, 1))
                            .ToList();
                        dateFormat = "yyyy-MM-01"; // First day of each month
                        labelFormat = "MMM"; // Just show month name for year view
                        break;

                    default:
                        // Default to days filter
                        startDate = endDate.AddDays(-7 + 1); // Default to 7 days
                        dateRange = Enumerable.Range(0, 7)
                            .Select(offset => startDate.AddDays(offset))
                            .ToList();
                        labelFormat = "MMM dd";
                        break;
                }

                // Get sales data grouped by date
                var query = _context.Sales
                    .Where(s => s.BOId == userId && s.SaleDate.Date >= startDate && s.SaleDate.Date <= endDate);

                // For year view, group by month instead of day
                var salesData = type.ToLower() == "years"
                    ? await query
                        .GroupBy(s => new { s.SaleDate.Year, s.SaleDate.Month })
                        .Select(g => new
                        {
                            Date = new DateTime(g.Key.Year, g.Key.Month, 1),
                            TotalAmount = g.Sum(s => s.TotalAmount)
                        })
                        .ToListAsync()
                    : await query
                        .GroupBy(s => s.SaleDate.Date)
                        .Select(g => new
                        {
                            Date = g.Key,
                            TotalAmount = g.Sum(s => s.TotalAmount)
                        })
                        .ToListAsync();

                // Create complete data set with zero values for days/months without sales
                var result = dateRange.Select(date => new
                {
                    Date = date.ToString(dateFormat),
                    FormattedDate = date.ToString(labelFormat),
                    Amount = salesData.FirstOrDefault(s =>
                        type.ToLower() == "years"
                            ? s.Date.Year == date.Year && s.Date.Month == date.Month
                            : s.Date.Date == date.Date
                    )?.TotalAmount ?? 0
                }).ToList();

                return Json(new
                {
                    success = true,
                    labels = result.Select(r => r.FormattedDate).ToList(),
                    values = result.Select(r => r.Amount).ToList(),
                    rawDates = result.Select(r => r.Date).ToList() // Include raw dates for additional processing if needed
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sales trend data");

                // Fallback to sample data if there's an error
                var random = new Random();
                var endDate = DateTime.Today;
                var startDate = endDate.AddDays(-7);

                var labels = Enumerable.Range(0, 7)
                    .Select(i => startDate.AddDays(i).ToString("MMM dd"))
                    .ToList();

                var values = Enumerable.Range(0, 7)
                    .Select(i => (decimal)random.Next(1000, 5000))
                    .ToList();

                return Json(new
                {
                    success = false,
                    message = ex.Message,
                    labels,
                    values
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetStockDistribution()
        {
            try
            {
                var userId = GetCurrentUserId();
                //var threshold = await GetUserLowStockThreshold(userId);

                var totalProducts = await _context.Products2
                    .Where(p => p.BOId == userId)
                    .CountAsync();

                var healthy = await _context.Products2
                    .Where(p => p.BOId == userId && p.QuantityInStock > p.ReorderLevel)
                    .CountAsync();

                var low = await _context.Products2
                    .Where(p => p.BOId == userId && p.QuantityInStock <= p.ReorderLevel && p.QuantityInStock > 0)
                    .CountAsync();

                var outOfStock = await _context.Products2
                    .Where(p => p.BOId == userId && p.QuantityInStock <= 0)
                    .CountAsync();

                return Json(new
                {
                    success = true,
                    healthy,
                    low,
                    outOfStock
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetRecentActivity(int limit = 10)
        {
            try
            {
                var userId = GetCurrentUserId();
                var activities = new List<object>();

                // Check if Sales table exists and has records
                try
                {
                    // Get sales with their items
                    var sales = await _context.Sales
                        .Where(s => s.BOId == userId)
                        .Include(s => s.SaleItems)
                        .OrderByDescending(s => s.SaleDate)
                        .Take(limit)
                        .ToListAsync();

                    foreach (var sale in sales)
                    {
                        // Group sale items by product name for a cleaner display
                        var groupedItems = sale.SaleItems
                            .GroupBy(si => si.ProductName)
                            .Select(g => new {
                                ProductName = g.Key,
                                Quantity = g.Sum(si => si.Quantity)
                            })
                            .ToList();

                        // Create a comma-separated list of products (e.g., "Product1 (2), Product2 (1)")
                        var productsString = string.Join(", ",
                            groupedItems.Select(g => $"{g.ProductName} ({g.Quantity})"));

                        // Add to activities
                        activities.Add(new
                        {
                            type = "Sale",
                            description = $"Sale to {sale.CustomerName}",
                            details = $"Products: - {productsString}",
                            amount = $"Total: {sale.TotalAmount:C}",
                            time = sale.SaleDate.ToString("MM/dd/yyyy hh:mm tt"),
                            sortTime = sale.SaleDate // For sorting
                        });
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue
                    _logger.LogError(ex, "Error accessing Sales table");
                }

                // Check if InventoryLogs table exists and has records
                try
                {
                    var inventoryActivities = await _context.InventoryLogs
                        .Where(l => l.BOId == userId)
                        .OrderByDescending(l => l.Timestamp)
                        .Take(limit)
                        .ToListAsync(); // First materialize the query

                    // Then apply the transformation in memory
                    var transformedActivities = inventoryActivities.Select(l => new {
                        type = l.MovementType,
                        description = GetMovementDescription(l.MovementType, l.ProductName, l.Notes),
                        details = $"Quantity: {Sign(l.MovementType)}{Math.Abs(l.QuantityChange)}",
                        amount = "",
                        time = l.Timestamp.ToString("MM/dd/yyyy hh:mm tt"),
                        sortTime = l.Timestamp // For sorting
                    }).ToList();

                    activities.AddRange(transformedActivities);
                }
                catch (Exception ex)
                {
                    // Log error but continue
                    _logger.LogError(ex, "Error accessing InventoryLogs table");
                }

                // Combine and order all activities by time
                var result = activities
                    .OrderByDescending(a => ((dynamic)a).sortTime)
                    .Take(limit)
                    .ToList();

                return Json(new
                {
                    success = true,
                    activities = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent activities");
                return Json(new
                {
                    success = false,
                    message = ex.Message,
                    activities = new List<object>()
                });
            }
        }

        private string Sign(string movementType)
        {
            return movementType switch
            {
                InventoryMovementTypes.Sale => $"- ",
                InventoryMovementTypes.Purchase => $"+ ",
                InventoryMovementTypes.Adjustment => $"",
                InventoryMovementTypes.StockIn => $"+ ",
                InventoryMovementTypes.NewProduct => $"+ ",
                InventoryMovementTypes.EditProduct => $"",
                InventoryMovementTypes.DeleteProduct => $"- ",
                _ => $""
            };
        }

        // Make this method static since it doesn't use any instance members
        private static string GetMovementDescription(string movementType, string productName, string notes)
        {
            return movementType switch
            {
                InventoryMovementTypes.Sale => $"Sold {productName}",
                InventoryMovementTypes.Purchase => $"Purchased {productName}",
                InventoryMovementTypes.Adjustment => $"Adjusted stock for {productName}: {notes}",
                InventoryMovementTypes.StockIn => $"Stock received for {productName}",
                InventoryMovementTypes.NewProduct => $"Added new product: {productName}",
                InventoryMovementTypes.EditProduct => $"Updated product: {productName}",
                InventoryMovementTypes.DeleteProduct => $"Removed product: {productName}",
                //InventoryMovementTypes.Return => $"Returned {productName}: {notes}",
                //InventoryMovementTypes.Damaged => $"Marked {productName} as damaged: {notes}",
                _ => $"Inventory movement for {productName}"
            };
        }
    }
}
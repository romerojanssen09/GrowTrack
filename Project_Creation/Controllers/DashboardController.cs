using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Creation.Data;
using System.Security.Claims;
using Project_Creation.Models.ViewModels;
using Project_Creation.DTO;
using static Project_Creation.Controllers.Inventory1Controller;
using Project_Creation.Models.Authorization;
using Project_Creation.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Project_Creation.Controllers
{
    [Authorize(Roles = "BusinessOwner,Staff")]
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

        [HttpGet]
        [StaffAccess(StaffAccessLevel.Inventory | StaffAccessLevel.PublishedProducts)]
        public async Task<IActionResult> GetQuantity(int id)
        {
            var userId = GetCurrentUserId();
            var product = await _context.Products2.FindAsync(userId);
            if (product == null)
            {
                return NotFound();
            }

            return Json(new { quantity = product.QuantityInStock });
        }

        [HttpGet]
        [StaffAccess(StaffAccessLevel.Inventory | StaffAccessLevel.PublishedProducts)]
        public async Task<IActionResult> GetBusinessMetrics()
        {
            try
            {
                var userId = GetCurrentUserId();
                decimal inventoryValue = await _context.Products2
                    .Where(p => p.BOId == userId)
                    .SumAsync(p => p.PurchasePrice * p.QuantityInStock);

                int lowStockCount = await _context.Products2
                    .Where(p => p.BOId == userId && p.QuantityInStock <= p.ReorderLevel)
                    .CountAsync();

                decimal todaysSales = await _context.Sales
                    .Where(s => s.BOId == userId && s.SaleDate.Date == DateTime.Today)
                    .SumAsync(s => s.TotalAmount);

                int totalProducts = await _context.Products2
                    .Where(p => p.BOId == userId)
                    .CountAsync();

                return Json(new {
                    success = true,
                    inventoryValue,
                    lowStockCount,
                    todaysSales,
                    totalProducts
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting business metrics");
                return Json(new
                {
                    success = false,
                    message = ex.Message,
                    inventoryValue = 0m,
                    lowStockCount = 0,
                    todaysSales = 0m,
                    totalProducts = 0
                });
            }
        }

        [HttpGet]
        [StaffAccess(StaffAccessLevel.Inventory | StaffAccessLevel.PublishedProducts)]
        public async Task<IActionResult> GetInventoryOverview(int limit = 10)
        {
            try
            {
                var userId = GetCurrentUserId();
                var products = await _context.Products2
                    .Where(p => p.BOId == userId && p.IsDeleted == false)
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
                _logger.LogError(ex, "Error getting inventory overview");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        [StaffAccess(StaffAccessLevel.Inventory | StaffAccessLevel.PublishedProducts)]
        public async Task<IActionResult> GetLowStockAlerts(int limit = 5)
        {
            try
            {
                var userId = GetCurrentUserId();
                var alerts = await _context.Products2
                    .Where(p => p.BOId == userId && p.QuantityInStock <= p.ReorderLevel && p.IsDeleted == false)
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
                _logger.LogError(ex, "Error getting low stock alerts");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        [StaffAccess(StaffAccessLevel.QuickSales)]
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
                        startDate = endDate.AddDays(-days + 1);
                        dateRange = Enumerable.Range(0, days)
                            .Select(offset => startDate.AddDays(offset))
                            .ToList();
                        labelFormat = "MMM dd";
                        break;

                    case "weeks":
                        if (weeks <= 0) weeks = 1;
                        startDate = endDate.AddDays(-(weeks * 7) + 1);
                        dateRange = Enumerable.Range(0, weeks * 7)
                            .Select(offset => startDate.AddDays(offset))
                            .ToList();
                        labelFormat = "MMM dd";
                        break;

                    case "months":
                        if (month <= 0 || month > 12) month = DateTime.Today.Month;
                        if (year <= 0) year = DateTime.Today.Year;

                        startDate = new DateTime(year, month, 1);
                        endDate = startDate.AddMonths(1).AddDays(-1);

                        int daysInMonth = DateTime.DaysInMonth(year, month);
                        dateRange = Enumerable.Range(1, daysInMonth)
                            .Select(day => new DateTime(year, month, day))
                            .ToList();
                        labelFormat = "MMM dd";
                        break;

                    case "years":
                        if (year <= 0) year = DateTime.Today.Year;

                        startDate = new DateTime(year, 1, 1);
                        endDate = new DateTime(year, 12, 31);

                        dateRange = Enumerable.Range(1, 12)
                            .Select(month => new DateTime(year, month, 1))
                            .ToList();
                        dateFormat = "yyyy-MM-01";
                        labelFormat = "MMM";
                        break;

                    default:
                        startDate = endDate.AddDays(-7 + 1);
                        dateRange = Enumerable.Range(0, 7)
                            .Select(offset => startDate.AddDays(offset))
                            .ToList();
                        labelFormat = "MMM dd";
                        break;
                }

                var query = _context.Sales
                    .Where(s => s.BOId == userId && s.SaleDate.Date >= startDate && s.SaleDate.Date <= endDate);

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
                    rawDates = result.Select(r => r.Date).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sales trend data");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        [StaffAccess(StaffAccessLevel.Inventory | StaffAccessLevel.PublishedProducts)]
        public async Task<IActionResult> GetStockDistribution()
        {
            try
            {
                var userId = GetCurrentUserId();
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
                _logger.LogError(ex, "Error getting stock distribution");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSupplierData(int id)
        {
            var supplier = await _context.Supplier2.FindAsync(id);

            if (supplier == null) return NotFound(new { success = false, message = "Supplier not found." });

            return Ok(new { success = true, data = supplier });
        }

        [HttpGet]
        [StaffAccess(StaffAccessLevel.Inventory | StaffAccessLevel.QuickSales | StaffAccessLevel.PublishedProducts | StaffAccessLevel.Leads)]
        public async Task<IActionResult> GetRecentActivity(int limit = 10)
        {
            try
            {
                var userId = GetCurrentUserId();
                var activities = new List<object>();

                // Get leads created or updated in the last 30 days
                if (User.IsInRole("BusinessOwner") || User.FindFirstValue("AccessLevel").Contains(StaffAccessLevel.Leads.ToString()))
                {
                    var recentLeads = await _context.Leads
                        .Where(l => l.CreatedById == userId && 
                            (l.CreatedAt >= DateTime.Now.AddDays(-30) || l.UpdatedAt >= DateTime.Now.AddDays(-30)))
                        .OrderByDescending(l => l.CreatedAt ?? DateTime.MinValue)
                        .Take(limit)
                        .ToListAsync();

                    foreach (var lead in recentLeads)
                    {
                        bool isNew = lead.CreatedAt >= DateTime.Now.AddDays(-7);
                        string description = isNew 
                            ? $"New lead: {lead.LeadName}" 
                            : $"Updated lead: {lead.LeadName}";

                        string details = $"Status: {lead.Status}";
                        //if (!string.IsNullOrEmpty(lead.SelectedProductIds))
                        //{
                        //    details += $", Interested in: {lead.InterestedIn}";
                        //}

                        activities.Add(new
                        {
                            type = "Lead",
                            description = description,
                            details = details,
                            amount = "",
                            time = (lead.CreatedAt ?? DateTime.Now).ToString("MM/dd/yyyy hh:mm tt"),
                            sortTime = lead.CreatedAt ?? DateTime.Now
                        });
                    }
                }

                var sales = await _context.Sales
                    .Where(s => s.BOId == userId)
                    .Include(s => s.SaleItems)
                    .OrderByDescending(s => s.SaleDate)
                    .Take(limit)
                    .ToListAsync();

                foreach (var sale in sales)
                {
                    var groupedItems = sale.SaleItems
                        .GroupBy(si => si.ProductName)
                        .Select(g => new {
                            ProductName = g.Key,
                            Quantity = g.Sum(si => si.Quantity)
                        })
                        .ToList();

                    var productsString = string.Join(", ",
                        groupedItems.Select(g => $"{g.ProductName} ({g.Quantity})"));

                    activities.Add(new
                    {
                        type = "Sale",
                        description = $"Sale to {sale.CustomerName}",
                        details = $"Products: - {productsString}",
                        amount = $"Total: {sale.TotalAmount:C}",
                        time = sale.SaleDate.ToString("MM/dd/yyyy hh:mm tt"),
                        sortTime = sale.SaleDate
                    });
                }

                var inventoryActivities = await _context.InventoryLogs
                    .Where(l => l.BOId == userId)
                    .OrderByDescending(l => l.Timestamp)
                    .Take(limit)
                    .ToListAsync();

                var transformedActivities = inventoryActivities.Select(l => new {
                    type = l.MovementType,
                    description = GetMovementDescription(l.MovementType, l.ProductName, l.Notes),
                    details = $"Quantity: {Sign(l.MovementType)}{Math.Abs(l.QuantityChange)}",
                    amount = "",
                    time = l.Timestamp.ToString("MM/dd/yyyy hh:mm tt"),
                    sortTime = l.Timestamp
                }).ToList();

                activities.AddRange(transformedActivities);

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
                _ => $"Inventory movement for {productName}"
            };
        }

        [HttpPost]
        [Authorize(Roles = "Staff")]
        public async Task<IActionResult> RefreshClaims()
        {
            try
            {
                _logger.LogInformation("Refreshing claims for staff user");
                
                // Get the current staff ID
                var staffIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(staffIdClaim) || !int.TryParse(staffIdClaim, out int staffId))
                {
                    _logger.LogWarning("Invalid or missing staff ID in claims");
                    return Json(new { success = false, message = "Invalid staff ID" });
                }
                
                // Get the staff from the database
                var staff = await _context.Staff.FindAsync(staffId);
                if (staff == null)
                {
                    _logger.LogWarning($"Staff with ID {staffId} not found in database");
                    return Json(new { success = false, message = "Staff not found" });
                }
                
                // Create new claims with updated access level
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, staff.Id.ToString()),
                    new(ClaimTypes.Email, staff.StaffSEmail),
                    new(ClaimTypes.Name, staff.StaffName),
                    new(ClaimTypes.Role, staff.Role),
                    new("AccessLevel", staff.StaffAccessLevel.ToString()),
                    new("AccountType", "Staff"),
                    new("BOId", staff.BOId.ToString()),
                    new("IsVerified", staff.IsActive.ToString())
                };

                // Create a new identity and principal
                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                // Sign in with the new claims
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
                
                // Get the access level names for the response
                var accessLevelNames = GetAccessLevelNames(staff.StaffAccessLevel);
                
                return Json(new { 
                    success = true, 
                    accessLevel = string.Join(", ", accessLevelNames)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing claims");
                return Json(new { success = false, message = "Error refreshing claims" });
            }
        }
        
        // Helper method to get access level names
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

        [HttpGet]
        [StaffAccess(StaffAccessLevel.Leads)]
        public async Task<IActionResult> GetLeadsSummary()
        {
            try
            {
                var userId = GetCurrentUserId();
                
                // Get total leads count
                int totalLeads = await _context.Leads
                    .Where(l => l.CreatedById == userId)
                    .CountAsync();
                    
                // Get new leads count (not contacted)
                int newLeads = await _context.Leads
                    .Where(l => l.CreatedById == userId && l.Status == Leads.LeadStatus.New)
                    .CountAsync();
                    
                // Get contacted leads count
                int contactedLeads = await _context.Leads
                    .Where(l => l.CreatedById == userId && l.Status == Leads.LeadStatus.Hot)
                    .CountAsync();
                    
                // Get recent leads (last 7 days)
                int recentLeads = await _context.Leads
                    .Where(l => l.CreatedById == userId && l.CreatedAt >= DateTime.Now.AddDays(-7))
                    .CountAsync();
                    
                return Json(new
                {
                    success = true,
                    totalLeads,
                    newLeads,
                    contactedLeads,
                    recentLeads
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting leads summary");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        [StaffAccess(StaffAccessLevel.Chat)]
        public async Task<IActionResult> GetProductRequests(int limit = 20)
        {
            try
            {
                var userId = GetCurrentUserId();

                // Fetch chat messages with Status = ProductRequest (Status == 1)
                var productRequests = await _context.Chats
                    .Where(c => c.ReceiverId == userId && c.Status == ChatStatus.Request)
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(limit)
                    .Select(c => new {
                        c.Id,
                        c.SenderId,
                        c.CreatedAt,
                        FullMessage = c.JSONString
                    })
                    .ToListAsync();

                _logger.LogInformation($"Found {productRequests.Count} product requests for user {userId}");

                var groupedRequests = new Dictionary<int, List<object>>();
                var customerNames = new Dictionary<int, string>();

                foreach (var request in productRequests)
                {
                    if (string.IsNullOrWhiteSpace(request.FullMessage))
                        continue;

                    try
                    {
                        // Deserialize JSON string safely
                        var requestData = JsonSerializer.Deserialize<Dictionary<string, object>>(request.FullMessage);

                        // Cache customer names to avoid repetitive DB calls
                        if (!customerNames.ContainsKey(request.SenderId))
                        {
                            var customer = await _context.Users.FindAsync(request.SenderId);
                            customerNames[request.SenderId] = customer != null
                                ? $"{customer.FirstName} {customer.LastName}"
                                : "Unknown Customer";
                        }

                        requestData.TryGetValue("productName", out var productNameObj);
                        requestData.TryGetValue("quantity", out var quantityObj);
                        requestData.TryGetValue("message", out var messageObj);

                        var requestInfo = new
                        {
                            messageId = request.Id,
                            productName = productNameObj?.ToString() ?? "",
                            quantity = quantityObj?.ToString() ?? "",
                            message = messageObj?.ToString() ?? "",
                            createdAt = request.CreatedAt,
                            customerId = request.SenderId,
                            customerName = customerNames[request.SenderId]
                        };

                        if (!groupedRequests.ContainsKey(request.SenderId))
                            groupedRequests[request.SenderId] = new List<object>();

                        groupedRequests[request.SenderId].Add(requestInfo);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error parsing product request JSON: {request.FullMessage}");
                    }
                }

                var response = groupedRequests.Select(gr => new {
                    customerId = gr.Key,
                    customerName = customerNames.ContainsKey(gr.Key) ? customerNames[gr.Key] : "Unknown Customer",
                    requests = gr.Value.OrderByDescending(r => ((DateTime)((dynamic)r).createdAt)).ToList()
                });

                return Json(new { success = true, groupedRequests = response });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product requests");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        [StaffAccess(StaffAccessLevel.Chat)]
        public async Task<IActionResult> GetLeadRequests(int limit = 20)
        {
            try
            {
                var userId = GetCurrentUserId();

                var leadRequests = await _context.Chats
                    .Where(c => c.ReceiverId == userId && c.Status == ChatStatus.LeadRequest)
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(limit)
                    .Select(c => new {
                        c.Id,
                        c.SenderId,
                        c.CreatedAt,
                        FullMessage = c.JSONString
                    })
                    .ToListAsync();

                var groupedRequests = new Dictionary<int, List<object>>();
                var customerNames = new Dictionary<int, string>();
                var leadNames = new Dictionary<int, string>();

                foreach (var request in leadRequests)
                {
                    if (string.IsNullOrWhiteSpace(request.FullMessage))
                        continue;

                    try
                    {
                        var requestData = JsonSerializer.Deserialize<Dictionary<string, object>>(request.FullMessage);

                        if (!customerNames.ContainsKey(request.SenderId))
                        {
                            var customer = await _context.Users.FindAsync(request.SenderId);
                            customerNames[request.SenderId] = customer != null
                                ? $"{customer.FirstName} {customer.LastName}"
                                : "Unknown Customer";
                        }

                        requestData.TryGetValue("LeadId", out var leadIdObj);
                        requestData.TryGetValue("leadName", out var leadNameObj);
                        requestData.TryGetValue("RequesterName", out var requesterNameObj);
                        requestData.TryGetValue("Message", out var messageObj);

                        var leadId = leadIdObj != null ? int.Parse(leadIdObj.ToString()) : 0;

                        if (leadId > 0 && !leadNames.ContainsKey(leadId))
                        {
                            var lead = await _context.Leads.FindAsync(leadId);
                            leadNames[leadId] = lead?.LeadName ?? "Unknown Lead";
                        }

                        var requestInfo = new
                        {
                            messageId = request.Id,
                            leadId,
                            leadName = leadId > 0 ? leadNames[leadId] : leadNameObj?.ToString() ?? "",
                            requesterName = requesterNameObj?.ToString() ?? customerNames[request.SenderId],
                            message = messageObj?.ToString() ?? "",
                            createdAt = request.CreatedAt,
                            customerId = request.SenderId,
                            customerName = customerNames[request.SenderId]
                        };

                        if (!groupedRequests.ContainsKey(request.SenderId))
                            groupedRequests[request.SenderId] = new List<object>();

                        groupedRequests[request.SenderId].Add(requestInfo);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error parsing lead request JSON: {request.FullMessage}");
                    }
                }

                var response = groupedRequests.Select(gr => new {
                    customerId = gr.Key,
                    customerName = customerNames.ContainsKey(gr.Key) ? customerNames[gr.Key] : "Unknown Customer",
                    requests = gr.Value.OrderByDescending(r => ((DateTime)((dynamic)r).createdAt)).ToList()
                });

                return Json(new { success = true, groupedRequests = response });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting lead requests");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [StaffAccess(StaffAccessLevel.Inventory)]
        public async Task<IActionResult> SendMessageToSupplier([FromBody] SupplierMessageRequest request)
        {
            try
            {
                var supplier = await _context.Supplier2.FindAsync(request.SupplierId);
                if (supplier == null)
                {
                    return Json(new { success = false, message = "Supplier not found." });
                }

                // Get the business owner or staff name
                var userName = User.Identity.Name;

                // Construct email body with proper formatting
                string emailBody = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif; line-height: 1.6;'>
                        <div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 5px;'>
                            <h2 style='color: #333;'>Message from {userName}</h2>
                            <p style='margin-bottom: 20px;'>You have received a message regarding your product supply:</p>
                            <div style='background-color: #f9f9f9; padding: 15px; border-left: 4px solid #007bff; margin-bottom: 20px;'>
                                {request.Message.Replace(Environment.NewLine, "<br />")}
                            </div>
                            <p style='color: #777; font-size: 14px;'>This message was sent from the business dashboard.</p>
                        </div>
                    </body>
                    </html>";

                try
                {
                    // Send the email
                    await _emailService.SendEmail(supplier.Email, request.Subject, emailBody, true);
                    
                    // Log the activity
                    _logger.LogInformation($"Message sent to supplier {supplier.SupplierName} (ID: {request.SupplierId}) by {userName}");
                    
                    return Json(new { 
                        success = true, 
                        message = $"Message sent to {supplier.SupplierName} successfully." 
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to send email to supplier {supplier.SupplierName}");
                    return Json(new { 
                        success = false, 
                        message = $"Failed to send message to {supplier.SupplierName}. Please try again." 
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to supplier");
                return Json(new { 
                    success = false, 
                    message = "An error occurred while sending the message. Please try again." 
                });
            }
        }

        // Class to hold supplier message request data
        public class SupplierMessageRequest
        {
            public int SupplierId { get; set; }
            public string Subject { get; set; }
            public string Message { get; set; }
        }
    }
}
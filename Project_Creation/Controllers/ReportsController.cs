using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Creation.Data;
using Project_Creation.Models.Entities;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using Project_Creation.DTO;
using Microsoft.Extensions.Logging;
using Project_Creation.Models;
using System.Diagnostics;
using System.Collections.Generic;
using static Project_Creation.Controllers.Inventory1Controller;
using System.IO;
using System.Text;
using System.Globalization;
using ClosedXML.Excel;

namespace Project_Creation.Controllers
{
    public class ReportsController : Controller
    {
        private readonly AuthDbContext _context;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(AuthDbContext context, ILogger<ReportsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Reports
        public IActionResult Index()
        {
            _logger.LogInformation("Accessed Reports Index page");
            return View();
        }

        // GET: Reports/Sales
        [HttpGet("Reports/Sales")]
        public async Task<IActionResult> Sales(DateTime? startDate, DateTime? endDate, string productName, 
            string customerName, string timePeriod = "custom", string viewType = "cards")
        {
            try
            {
                var userId = GetCurrentUserId();

                // Handle time period selection
                (startDate, endDate) = GetDateRangeFromTimePeriod(timePeriod, startDate, endDate);

                var salesQuery = _context.Sales
                    .Include(s => s.SaleItems)
                    .Where(s => s.BOId == userId);

                if (startDate.HasValue)
                {
                    salesQuery = salesQuery.Where(s => s.SaleDate >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    // Include the entire end date
                    var endDateWithTime = endDate.Value.AddDays(1).AddSeconds(-1);
                    salesQuery = salesQuery.Where(s => s.SaleDate <= endDateWithTime);
                }

                if (!string.IsNullOrWhiteSpace(productName))
                {
                    salesQuery = salesQuery.Where(s =>
                        s.SaleItems.Any(i => i.ProductName.Contains(productName)));
                }

                if (!string.IsNullOrWhiteSpace(customerName))
                {
                    salesQuery = salesQuery.Where(s => s.CustomerName == customerName);
                }

                var sales = await salesQuery
                    .OrderByDescending(s => s.SaleDate)
                    .ToListAsync();

                var salesDto = sales.Select(s => new SaleDto
                {
                    Id = s.Id,
                    BOId = s.BOId,
                    CustomerName = s.CustomerName,
                    TotalAmount = s.TotalAmount,
                    SaleDate = s.SaleDate,
                    IsQuickSale = s.SaleItems.Any(si => si.Notes?.Contains("QuickSale") ?? false),
                    SaleItems = s.SaleItems.Select(i => new SaleItemDto
                    {
                        Id = i.Id,
                        SaleId = i.SaleId,
                        ProductId = i.ProductId,
                        ProductName = i.ProductName,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        TotalPrice = i.TotalPrice,
                        Notes = i.Notes
                    }).ToList()
                }).ToList();

                // Get all unique customers for the dropdown
                var customers = await _context.Sales
                    .Where(s => s.BOId == userId && !string.IsNullOrEmpty(s.CustomerName))
                    .Select(s => s.CustomerName)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToListAsync();

                // Set values in ViewBag for filtering UI and view toggle
                ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
                ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
                ViewBag.ProductName = productName;
                ViewBag.CustomerName = customerName; 
                ViewBag.TimePeriod = timePeriod;
                ViewBag.ViewType = viewType;
                ViewBag.Customers = customers;

                return View(salesDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating sales report");
                return View("Error", new ErrorViewModel
                {
                    RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
                });
            }
        }

        // GET: Reports/ExportSales
        [HttpGet("Reports/ExportSales")]
        public async Task<IActionResult> ExportSales(DateTime? startDate, DateTime? endDate, string productName, 
            string customerName, string timePeriod = "custom", string format = "csv")
        {
            try
            {
                var userId = GetCurrentUserId();

                // Handle time period selection
                (startDate, endDate) = GetDateRangeFromTimePeriod(timePeriod, startDate, endDate);

                var salesQuery = _context.Sales
                    .Include(s => s.SaleItems)
                    .Where(s => s.BOId == userId);

                if (startDate.HasValue)
                {
                    salesQuery = salesQuery.Where(s => s.SaleDate >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    var endDateWithTime = endDate.Value.AddDays(1).AddSeconds(-1);
                    salesQuery = salesQuery.Where(s => s.SaleDate <= endDateWithTime);
                }

                if (!string.IsNullOrWhiteSpace(productName))
                {
                    salesQuery = salesQuery.Where(s =>
                        s.SaleItems.Any(i => i.ProductName.Contains(productName)));
                }

                if (!string.IsNullOrWhiteSpace(customerName))
                {
                    salesQuery = salesQuery.Where(s => s.CustomerName == customerName);
                }

                var sales = await salesQuery
                    .OrderByDescending(s => s.SaleDate)
                    .ToListAsync();

                var salesData = sales.Select(s => new
                {
                    SaleId = s.Id,
                    Date = s.SaleDate.ToString("MM/dd/yyyy HH:mm"),
                    Customer = s.CustomerName,
                    Type = s.SaleItems.Any(si => si.Notes?.Contains("QuickSale") ?? false) ? "QuickSale" : "Regular",
                    ItemCount = s.SaleItems.Count,
                    TotalAmount = s.TotalAmount,
                    Items = string.Join(", ", s.SaleItems.Select(i => $"{i.ProductName} ({i.Quantity})"))
                }).ToList();

                string fileName = $"Sales_Report_{DateTime.Now:yyyyMMdd_HHmmss}";

                if (format.ToLower() == "excel")
                {
                    return ExportToExcel(salesData, fileName);
                }
                else // Default to CSV
                {
                    return ExportToCsv(salesData, fileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting sales report");
                return BadRequest("Error generating export file. Please try again.");
            }
        }

        // GET: Reports/Inventory
        [HttpGet]
        public async Task<IActionResult> Inventory()
        {
            try
            {
                _logger.LogInformation("Starting Inventory Report generation");

                var userId = GetCurrentUserId();
                _logger.LogDebug("Current user ID: {UserId}", userId);

                _logger.LogDebug("Querying inventory data from database");
                var inventory = await _context.Products2
                    .Where(p => p.BOId == userId)
                    .OrderBy(p => p.ProductName)
                    .ToListAsync();

                _logger.LogInformation("Found {ProductCount} products in inventory", inventory.Count);
                return View(inventory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating inventory report");
                return View("Error", new ErrorViewModel
                {
                    RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
                });
            }
        }

        // GET: Reports/Movements
        public async Task<IActionResult> Movements(DateTime? startDate, DateTime? endDate, string productName, string movementType)
        {
            try
            {
                var userId = GetCurrentUserId();

                // Base query
                var query = _context.InventoryLogs
                    .Where(l => l.BOId == userId)
                    .AsQueryable();

                // Apply filters
                if (startDate.HasValue)
                    query = query.Where(l => l.Timestamp >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(l => l.Timestamp <= endDate.Value.AddDays(1));

                if (!string.IsNullOrEmpty(productName))
                    query = query.Where(l => l.ProductName.Contains(productName));

                if (!string.IsNullOrEmpty(movementType))
                    query = query.Where(l => l.MovementType == movementType);

                // Get filtered movements
                var movements = await query
                    .OrderByDescending(l => l.Timestamp)
                    .ToListAsync();

                // Populate filter dropdowns
                ViewBag.Products = await _context.InventoryLogs
                    .Where(l => l.BOId == userId)
                    .Select(l => l.ProductName)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToListAsync();

                ViewBag.MovementTypes = new List<string>
                {
                    InventoryMovementTypes.Sale,
                    InventoryMovementTypes.Purchase,
                    InventoryMovementTypes.Adjustment,
                    InventoryMovementTypes.StockIn,
                    InventoryMovementTypes.NewProduct,
                    InventoryMovementTypes.EditProduct,
                    InventoryMovementTypes.DeleteProduct
                };

                // Pass filter values back to view
                ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
                ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
                ViewBag.ProductName = productName;
                ViewBag.MovementType = movementType;

                return View(movements);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating inventory movements report");
                return View("Error");
            }
        }

        // GET: Reports/ExportMovements
        [HttpGet("Reports/ExportMovements")]
        public async Task<IActionResult> ExportMovements(
            DateTime? startDate,
            DateTime? endDate,
            string productName,
            string movementType,
            string searchTerm,
            string format = "csv")
        {
            try
            {
                var userId = GetCurrentUserId();
                var query = _context.InventoryLogs
                    .Where(l => l.BOId == userId)
                    .AsQueryable();

                // Apply filters (same as in Movements action)
                if (startDate.HasValue)
                    query = query.Where(l => l.Timestamp >= startDate.Value);
                if (endDate.HasValue)
                    query = query.Where(l => l.Timestamp <= endDate.Value.AddDays(1));
                if (!string.IsNullOrEmpty(productName))
                    query = query.Where(l => l.ProductName.Contains(productName));
                if (!string.IsNullOrEmpty(movementType))
                    query = query.Where(l => l.MovementType == movementType);
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(l =>
                        l.ProductName.Contains(searchTerm) ||
                        l.ReferenceId.Contains(searchTerm) ||
                        (l.Notes != null && l.Notes.Contains(searchTerm)));
                }

                var movements = await query
                    .OrderByDescending(l => l.Timestamp)
                    .ToListAsync();

                var movementData = movements.Select(m => new
                {
                    Date = m.Timestamp.ToString("yyyy-MM-dd HH:mm"),
                    Product = m.ProductName,
                    Type = m.MovementType,
                    Before = m.QuantityBefore,
                    Change = m.QuantityAfter - m.QuantityBefore,
                    After = m.QuantityAfter,
                    Reference = m.ReferenceId,
                    Notes = m.Notes
                }).ToList();

                string fileName = $"Inventory_Movements_{DateTime.Now:yyyyMMdd_HHmmss}";

                if (format.ToLower() == "excel")
                {
                    return ExportToExcel(movementData, fileName);
                }
                else // Default to CSV
                {
                    return ExportToCsv(movementData, fileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting movements report");
                return BadRequest("Error generating export file. Please try again.");
            }
        }

        // Helper class for grouping movements by product
        public class ProductMovementSummary
        {
            public string ProductName { get; set; }
            public int TotalIn { get; set; }
            public int TotalOut { get; set; }
            public int NetChange { get; set; }
            public int MovementCount { get; set; }
            public List<InventoryLog> Movements { get; set; }
        }

        private int GetCurrentUserId()
        {
            try
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    _logger.LogWarning("Invalid or missing user ID claim");
                    throw new InvalidOperationException("User is not authenticated");
                }
                return userId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user ID");
                throw;
            }
        }

        private (DateTime?, DateTime?) GetDateRangeFromTimePeriod(string timePeriod, DateTime? startDate, DateTime? endDate)
        {
            var today = DateTime.Today;
            
            switch (timePeriod)
            {
                case "today":
                    return (today, today);
                    
                case "yesterday":
                    return (today.AddDays(-1), today.AddDays(-1));
                    
                case "thisWeek":
                    var firstDayOfWeek = today.AddDays(-(int)today.DayOfWeek);
                    return (firstDayOfWeek, today);
                    
                case "lastWeek":
                    var lastWeekStart = today.AddDays(-(int)today.DayOfWeek - 7);
                    var lastWeekEnd = lastWeekStart.AddDays(6);
                    return (lastWeekStart, lastWeekEnd);
                    
                case "thisMonth":
                    var firstDayOfMonth = new DateTime(today.Year, today.Month, 1);
                    return (firstDayOfMonth, today);
                    
                case "lastMonth":
                    var firstDayOfLastMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
                    var lastDayOfLastMonth = new DateTime(today.Year, today.Month, 1).AddDays(-1);
                    return (firstDayOfLastMonth, lastDayOfLastMonth);
                    
                case "thisYear":
                    var firstDayOfYear = new DateTime(today.Year, 1, 1);
                    return (firstDayOfYear, today);
                    
                case "lastYear":
                    var firstDayOfLastYear = new DateTime(today.Year - 1, 1, 1);
                    var lastDayOfLastYear = new DateTime(today.Year - 1, 12, 31);
                    return (firstDayOfLastYear, lastDayOfLastYear);
                    
                case "custom":
                default:
                    return (startDate, endDate);
            }
        }

        private FileContentResult ExportToCsv<T>(IEnumerable<T> data, string fileName)
        {
            var sb = new StringBuilder();
            
            // Add headers
            var properties = typeof(T).GetProperties();
            sb.AppendLine(string.Join(",", properties.Select(p => $"\"{p.Name}\"")));
            
            // Add data rows
            foreach (var item in data)
            {
                var values = properties.Select(p => 
                {
                    var value = p.GetValue(item);
                    if (value == null)
                        return "\"\"";
                    
                    // Escape quotes and format the value
                    return $"\"{value.ToString().Replace("\"", "\"\"").Replace("\r\n", " ").Replace("\n", " ")}\"";
                });
                
                sb.AppendLine(string.Join(",", values));
            }
            
            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"{fileName}.csv");
        }

        private FileContentResult ExportToExcel<T>(IEnumerable<T> data, string fileName)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Sales Report");
                
                // Add headers
                var properties = typeof(T).GetProperties();
                for (int i = 0; i < properties.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = properties[i].Name;
                }
                
                // Add data
                int row = 2;
                foreach (var item in data)
                {
                    for (int i = 0; i < properties.Length; i++)
                    {
                        var value = properties[i].GetValue(item);
                        worksheet.Cell(row, i + 1).Value = value?.ToString() ?? "";
                    }
                    row++;
                }
                
                // Format headers
                var headerRow = worksheet.Row(1);
                headerRow.Style.Font.Bold = true;
                headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;
                
                // Auto-fit columns
                worksheet.Columns().AdjustToContents();
                
                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{fileName}.xlsx");
                }
            }
        }

        // GET: Reports/ExportInventory
        [HttpGet("Reports/ExportInventory")]
        public async Task<IActionResult> ExportInventory(string format = "csv")
        {
            try
            {
                var userId = GetCurrentUserId();

                var inventory = await _context.Products2
                    .Where(p => p.BOId == userId)
                    .OrderBy(p => p.ProductName)
                    .ToListAsync();

                var inventoryData = inventory.Select(p => new
                {
                    ProductName = p.ProductName,
                    Category = p.Category,
                    QuantityInStock = p.QuantityInStock,
                    PurchasePrice = p.PurchasePrice,
                    SellingPrice = p.SellingPrice,
                    TotalValue = (p.QuantityInStock * p.PurchasePrice)
                }).ToList();

                string fileName = $"Inventory_Report_{DateTime.Now:yyyyMMdd_HHmmss}";

                if (format.ToLower() == "excel")
                {
                    return ExportToExcel(inventoryData, fileName);
                }
                else // Default to CSV
                {
                    return ExportToCsv(inventoryData, fileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting inventory report");
                return BadRequest("Error generating export file. Please try again.");
            }
        }
    }
}

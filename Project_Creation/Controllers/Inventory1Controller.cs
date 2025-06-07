using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Creation.Data;
using Project_Creation.DTO;
using Project_Creation.Models.Entities;
using Project_Creation.Models.ViewModels;
using System.Security.Claims;
using Project_Creation.Models;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using static Project_Creation.Models.Entities.Users;
using Microsoft.AspNetCore.SignalR;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authorization;
using System.Data;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Text;
using System.Reflection;
using System.Globalization;

namespace Project_Creation.Controllers
{
    [Authorize]
    public class Inventory1Controller : Controller
    {
        private readonly AuthDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly ILogger<Inventory1Controller> _logger;
        private readonly IHubContext<RealTimeHub> _hubContext;

        public Inventory1Controller(
            AuthDbContext context,
            IWebHostEnvironment hostEnvironment,
            ILogger<Inventory1Controller> logger,
            IHubContext<RealTimeHub> hubContext)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
            _logger = logger;
            _hubContext = hubContext;
        }

        // GET: Inventory1
        public async Task<IActionResult> Index()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
            int boId = int.TryParse(User.FindFirstValue("BOId"), out var tempBoId) ? tempBoId : 0;
            int who = currentUserRole == "Staff" ? boId : GetCurrentUserId();
            
            List<Product> products;
            try
            {
                // Try to filter by IsDeleted property
                products = await _context.Products2
                    .Where(p => p.BOId == who && !p.IsDeleted)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                // If IsDeleted column doesn't exist yet, just filter by BOId
                _logger.LogWarning(ex, "Error filtering by IsDeleted, falling back to basic query");
                products = await _context.Products2
                    .Where(p => p.BOId == who)
                    .ToListAsync();
            }

            // Get all suppliers for the current user
            var suppliers = await _context.Supplier2
                .Where(s => s.BOId == who)
                .ToListAsync();

            var isAllowed = await _context.Users
                .AnyAsync(u => u.MarkerPlaceStatus == MarketplaceStatus.Authorized);

            ViewBag.Suppliers = suppliers;
            ViewBag.IsAllowedMarketplace = isAllowed;

            return View(products);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                return Json(new { success = false });
            }
            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }


        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var productDto = new ProductDto
            {
                BOId = GetCurrentUserId(),
                ProductName = "",
                PurchasePrice = 0,
                SKU = "AUTO-GENERATED",
                Barcode = "",
                QuantityInStock = 0,
                ReorderLevel = 10,
                SellingPrice = 0,
                Suppliers = await _context.Supplier2
                    .Where(s => s.BOId == GetCurrentUserId())
                    .ToListAsync(),
                Category = await _context.Categories
                    .Where(c => c.BOId == GetCurrentUserId())
                    .ToListAsync()
            };

            return View(productDto);
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

        private string GenerateSKU(string productName, string categoryName)
        {
            var namePart = productName?.Length >= 3
                ? productName.Substring(0, 3).ToUpper()
                : "PRD";

            var catPart = categoryName?.Length >= 3
                ? categoryName.Substring(0, 3).ToUpper()
                : "CAT";

            return $"{namePart}-{catPart}-{TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore")):MMddHHmm}";
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductDto productDto)
        {
            try
            {
                string effectiveCategory = null;

                if (!string.IsNullOrWhiteSpace(productDto.NewCategoryName))
                {
                    effectiveCategory = productDto.NewCategoryName;
                    _logger.LogDebug("Using new category: {CategoryName}", effectiveCategory);
                }
                else if (!string.IsNullOrEmpty(productDto.Category2))
                {
                    effectiveCategory = productDto.Category2;
                    _logger.LogDebug("Using existing category: {CategoryName}", effectiveCategory);
                }

                if (string.IsNullOrEmpty(effectiveCategory))
                {
                    ModelState.AddModelError("Category", "Please select or create a category");
                }

                if (!ModelState.IsValid)
                {
                    await PopulateDropdowns(productDto);
                    return View(productDto);
                }

                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
                int boId = int.TryParse(User.FindFirstValue("BOId"), out var tempBoId) ? tempBoId : 0;

                var product = new Product
                {
                    BOId = currentUserRole == "Staff" ? boId : GetCurrentUserId(),
                    ProductName = productDto.ProductName,
                    SupplierId = productDto.SupplierId?.ToString(),
                    Category = effectiveCategory ?? "categories not found",
                    PurchasePrice = productDto.PurchasePrice,
                    SKU = productDto.SKU,
                    QuantityInStock = productDto.QuantityInStock,
                    ReorderLevel = productDto.ReorderLevel,
                    Description = productDto.Description,
                    SellingPrice = productDto.SellingPrice,
                    CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"))
                };

                // Use the execution strategy instead of direct transaction
                var strategy = _context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        // Add category if new
                        if (!string.IsNullOrEmpty(productDto.NewCategoryName))
                        {
                            bool categoryExists = await _context.Categories.AnyAsync(c =>
                                c.CategoryName.ToLower() == productDto.NewCategoryName.ToLower()
                                && c.BOId == GetCurrentUserId());

                            if (!categoryExists)
                            {
                                var newCategory = new Category
                                {
                                    CategoryName = productDto.NewCategoryName,
                                    BOId = GetCurrentUserId()
                                };
                                _context.Categories.Add(newCategory);
                                await _context.SaveChangesAsync();
                            }
                        }

                        _context.Products2.Add(product);
                        await _context.SaveChangesAsync();

                        var log = new InventoryLog
                        {
                            BOId = GetCurrentUserId(),
                            ProductId = product.Id,
                            ProductName = product.ProductName,
                            QuantityBefore = 0,
                            QuantityAfter = product.QuantityInStock,
                            MovementType = InventoryMovementTypes.NewProduct,
                            ReferenceId = $"NEWPROD-{product.Id}",
                            Notes = $"New product created with initial stock: {product.QuantityInStock}",
                            Timestamp = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"))
                        };
                        _context.InventoryLogs.Add(log);
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
                            
                            // Use the hub context directly with the DTO object
                            await _hubContext.Clients.Group($"business_{GetCurrentUserId()}").SendAsync("ReceiveInventoryMovement", logDto);
                            
                            // Also notify the specific user in case they're not in the group
                            await _hubContext.Clients.User(GetCurrentUserId().ToString()).SendAsync("ReceiveInventoryMovement", logDto);
                            
                            _logger.LogInformation("Successfully sent real-time notification for new product inventory movement");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error sending SignalR notification for new product inventory movement");
                            // Continue processing - don't fail the operation if real-time notification fails
                        }

                        // Save barcode if provided
                        if (!string.IsNullOrEmpty(productDto.Barcode))
                        {
                            product.Barcode = await SaveBarcodeImage(productDto.Barcode, productDto.SKU);
                            _context.Products2.Update(product);
                            await _context.SaveChangesAsync();
                        }

                        await transaction.CommitAsync();
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during product creation");
                ModelState.AddModelError("", "Error saving product. Please try again.");
                await PopulateDropdowns(productDto);
                return View(productDto);
            }
        }

        private async Task PopulateDropdowns(ProductDto productDto)
        {
            productDto.Suppliers = await _context.Supplier2
                .Where(s => s.BOId == GetCurrentUserId()).ToListAsync();
            productDto.Category = await _context.Categories
                .Where(c => c.BOId == GetCurrentUserId()).ToListAsync();
        }


        // Keep the SaveBarcodeImage method as is
        private async Task<string> SaveBarcodeImage(string imageData, string sku)
        {
            try
            {
                var base64Data = imageData.Split(',')[1];
                var imageBytes = Convert.FromBase64String(base64Data);

                string userId = GetCurrentUserId().ToString();
                string uploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, "uploads", userId, "barcodes");

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                string fileName = $"barcode_{sku}_{TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore")):yyyyMMddHHmmss}.png";
                string filePath = Path.Combine(uploadsFolder, fileName);

                await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);

                return $"/uploads/{userId}/barcodes/{fileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving barcode image");
                return string.Empty;
            }
        }

        // GET: Inventory1/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products2
                .FirstOrDefaultAsync(m => m.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            // Get all suppliers for the current user
            var suppliers = await _context.Supplier2
                .Where(s => s.BOId == GetCurrentUserId())
                .ToListAsync();

            ViewBag.Suppliers = suppliers;

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_DetailsPartial", product);
            }

            return View(product);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products2
                .Include(p => p.Supplier2)  // Include the supplier
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            var productDto = new ProductDto
            {
                Id = product.Id,
                ProductName = product.ProductName,
                SupplierId = int.TryParse(product.SupplierId, out var supplierId) ? supplierId : null,
                Supplier2 = product.Supplier2,  // Set the supplier navigation property
                PurchasePrice = product.PurchasePrice,
                SKU = product.SKU,
                Barcode = product.Barcode,
                QuantityInStock = product.QuantityInStock,
                ReorderLevel = product.ReorderLevel,
                Description = product.Description,
                SellingPrice = product.SellingPrice,
                Suppliers = await _context.Supplier2.Where(u => u.BOId == GetCurrentUserId()).ToListAsync(),
                Category = await _context.Categories.Where(u => u.BOId == GetCurrentUserId()).ToListAsync(),
                NewCategoryName = product.Category
            };

            return View(productDto);
        }

        // POST: Inventory1/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProductDto productDto)
        {
            if (id != productDto.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                productDto.Suppliers = await _context.Supplier2.ToListAsync();
                productDto.Category = await _context.Categories.ToListAsync();
                return View(productDto);
            }

            try
            {
                var product = await _context.Products2.FindAsync(id);
                if (product == null)
                {
                    return NotFound();
                }

                product.ProductName = productDto.ProductName;
                product.SupplierId = productDto.SupplierId?.ToString();
                product.PurchasePrice = productDto.PurchasePrice;
                product.SKU = productDto.SKU;
                product.QuantityInStock = productDto.QuantityInStock;
                product.ReorderLevel = productDto.ReorderLevel;
                product.Description = productDto.Description;
                product.SellingPrice = productDto.SellingPrice;

                // Determine and update the effective category
                if (!string.IsNullOrWhiteSpace(productDto.NewCategoryName))
                {
                    // Check if category already exists
                    bool categoryExists = await _context.Categories
                        .AnyAsync(c => c.CategoryName.ToLower() == productDto.NewCategoryName.ToLower()
                                       && c.BOId == GetCurrentUserId());

                    if (!categoryExists)
                    {
                        var newCategory = new Category
                        {
                            CategoryName = productDto.NewCategoryName,
                            BOId = GetCurrentUserId()
                        };
                        _context.Categories.Add(newCategory);
                        await _context.SaveChangesAsync();
                    }

                    product.Category = productDto.NewCategoryName;
                }
                else if (!string.IsNullOrEmpty(productDto.Category2))
                {
                    product.Category = productDto.Category2;
                }

                // Handle new barcode image if provided
                if (!string.IsNullOrEmpty(productDto.Barcode) && productDto.Barcode.StartsWith("data:image"))
                {
                    product.Barcode = await SaveBarcodeImage(productDto.Barcode, productDto.SKU);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Product updated successfully: {ProductId}", product.Id);

                // Create inventory log for the edit
                var log = new InventoryLog
                {
                    BOId = GetCurrentUserId(),
                    ProductId = product.Id,
                    ProductName = product.ProductName,
                    QuantityBefore = product.QuantityInStock,
                    QuantityAfter = product.QuantityInStock,
                    MovementType = InventoryMovementTypes.EditProduct,
                    ReferenceId = $"EDIT-{product.Id}",
                    Notes = $"Product updated: {GetProductChanges(await _context.Products2.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id), product)}",
                    Timestamp = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"))
                };
                _context.InventoryLogs.Add(log);
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
                    
                    // Use the hub context directly with the DTO object
                    await _hubContext.Clients.Group($"business_{GetCurrentUserId()}").SendAsync("ReceiveInventoryMovement", logDto);
                    
                    // Also notify the specific user in case they're not in the group
                    await _hubContext.Clients.User(GetCurrentUserId().ToString()).SendAsync("ReceiveInventoryMovement", logDto);
                    
                    _logger.LogInformation("Successfully sent real-time notification for product edit inventory movement");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending SignalR notification for product edit inventory movement");
                    // Continue processing - don't fail the operation if real-time notification fails
                }

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductExists(productDto.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
        }

        // Add this helper method to the controller:
        private string GetProductChanges(Product original, Product updated)
        {
            var changes = new List<string>();

            if (original.ProductName != updated.ProductName)
                changes.Add($"Name: {original.ProductName} → {updated.ProductName}");

            if (original.QuantityInStock != updated.QuantityInStock)
                changes.Add($"Stock: {original.QuantityInStock} → {updated.QuantityInStock}");

            if (original.SellingPrice != updated.SellingPrice)
                changes.Add($"Price: {original.SellingPrice} → {updated.SellingPrice}");

            return changes.Any() ? string.Join(", ", changes) : "No quantity changes";
        }

        private bool ProductExists(int id)
        {
            return _context.Products2.Any(e => e.Id == id);
        }

        // POST: Inventory1/Delete/5
        [HttpPost]
        public async Task<IActionResult> Delete(int productId)
        {
            var product = await _context.Products2.FindAsync(productId);
            if (product == null)
            {
                return Json(new { success = false, message = "Product not found." });
            }

            try
            {
                // Create inventory log for the deleted product
                var log = new InventoryLog
                {
                    BOId = GetCurrentUserId(),
                    ProductId = product.Id,
                    ProductName = product.ProductName,
                    QuantityBefore = product.QuantityInStock,
                    QuantityAfter = 0,
                    MovementType = InventoryMovementTypes.DeleteProduct,
                    ReferenceId = $"DELETE-{product.Id}",
                    Notes = $"Product deleted. Final stock was {product.QuantityInStock}",
                    Timestamp = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"))
                };
                _context.InventoryLogs.Add(log);
                
                // Soft delete the product instead of removing it
                try
                {
                    product.IsDeleted = true;
                    product.QuantityInStock = 0; // Set quantity to 0 when deleted
                    product.IsPublished = false; // Unpublish from marketplace when deleted
                    _context.Products2.Update(product);
                }
                catch (Exception ex)
                {
                    // If IsDeleted property doesn't exist yet, fall back to hard delete
                    _logger.LogWarning(ex, "Error setting IsDeleted property for product {ProductId}, falling back to hard delete", productId);
                    _context.Products2.Remove(product);
                }
                
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
                    
                    // Use the hub context directly with the DTO object
                    await _hubContext.Clients.Group($"business_{GetCurrentUserId()}").SendAsync("ReceiveInventoryMovement", logDto);
                    
                    // Also notify the specific user in case they're not in the group
                    await _hubContext.Clients.User(GetCurrentUserId().ToString()).SendAsync("ReceiveInventoryMovement", logDto);
                    
                    _logger.LogInformation("Successfully sent real-time notification for product deletion inventory movement");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending SignalR notification for product deletion inventory movement");
                    // Continue processing - don't fail the operation if real-time notification fails
                }
                
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: Inventory1/QuickSale
        [HttpGet]
        public async Task<IActionResult> QuickSale()
        {
            try
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
                int boId = int.TryParse(User.FindFirstValue("BOId"), out var tempBoId) ? tempBoId : 0;
                int who = currentUserRole == "Staff" ? boId : GetCurrentUserId();
                _logger.LogInformation("Loading products for QuickSale");
                
                List<Product> products;
                try
                {
                    // Try to filter by IsDeleted property
                    products = await _context.Products2
                        .Where(p => p.BOId == who && p.QuantityInStock > 0 && !p.IsDeleted)
                        .OrderBy(p => p.ProductName)
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    // If IsDeleted column doesn't exist yet, just filter without it
                    _logger.LogWarning(ex, "Error filtering by IsDeleted, falling back to basic query");
                    products = await _context.Products2
                        .Where(p => p.BOId == who && p.QuantityInStock > 0)
                        .OrderBy(p => p.ProductName)
                        .ToListAsync();
                }

                // Set products in ViewBag
                ViewBag.Products = products;

                _logger.LogInformation("Loaded {ProductCount} products for QuickSale", products.Count);
                
                return View(new QuickSaleViewModel
                {
                    AvailableProducts = products,
                    SaleDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore")),
                    SelectedOptions = "new", // Default to new customer option
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading QuickSale page");
                return View("Error", new ErrorViewModel
                {
                    RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickSale(QuickSaleViewModel model)
        {
            if(model == null)
            {
                _logger.LogError("QuickSale model is null");
                return BadRequest("Invalid sale data");
            }

            // Log the selected option value to diagnose the issue
            _logger.LogInformation("SelectedOptions value: {SelectedOptions}", model.SelectedOptions ?? "null");

            // Check if a customer type is selected
            if (string.IsNullOrEmpty(model.SelectedOptions))
            {
                _logger.LogWarning("No customer type (SelectedOptions) selected");
                ModelState.AddModelError("SelectedOptions", "Please select a customer type (New Customer or Anonymous Buyer)");
                
                // Repopulate the model for the view
                var products = await _context.Products2
                    .Where(p => p.BOId == GetCurrentUserId() && p.QuantityInStock > 0)
                    .OrderBy(p => p.ProductName)
                    .ToListAsync();

                ViewBag.Products = products;
                model.AvailableProducts = products;
                
                return View(model);
            }

            // Validate that there are items in the sale
            if (model.Items == null || !model.Items.Any())
            {
                _logger.LogWarning("No items in the sale. Adding ModelState error.");
                ModelState.AddModelError("", "At least one item is required for the sale");
                
                // Repopulate the model for the view
                var products = await _context.Products2
                    .Where(p => p.BOId == GetCurrentUserId() && p.QuantityInStock > 0)
                    .OrderBy(p => p.ProductName)
                    .ToListAsync();

                ViewBag.Products = products;
                model.AvailableProducts = products;
                
                return View(model);
            }

            // Consolidate duplicate products by combining quantities
            var consolidatedItems = new List<SaleItemViewModel>();
            var processedProductIds = new Dictionary<int, int>(); // ProductId -> Index in consolidatedItems
            
            foreach (var item in model.Items)
            {
                if (processedProductIds.ContainsKey(item.ProductId))
                {
                    // If this product was already processed, add the quantity to the existing item
                    int index = processedProductIds[item.ProductId];
                    consolidatedItems[index].Quantity += item.Quantity;
                    _logger.LogInformation("Consolidated duplicate product {ProductId}: Adding {Quantity} to existing quantity {ExistingQuantity}", 
                        item.ProductId, item.Quantity, consolidatedItems[index].Quantity - item.Quantity);
                }
                else
                {
                    // First time seeing this product, add it to the consolidated list
                    consolidatedItems.Add(item);
                    processedProductIds.Add(item.ProductId, consolidatedItems.Count - 1);
                    _logger.LogInformation("Added new product {ProductId} with quantity {Quantity} to consolidated list", 
                        item.ProductId, item.Quantity);
                }
            }
            
            _logger.LogInformation("Consolidated {OriginalCount} items into {ConsolidatedCount} unique product items", 
                model.Items.Count, consolidatedItems.Count);
            
            // Replace the original items with consolidated items
            model.Items = consolidatedItems;

            // Variables to track lead points and transaction details
            int totalLeadPoints = 0;
            decimal totalTransactionAmount = 0;
            List<(int productId, int quantity, decimal price, int points, string productName)> productDetails = new List<(int, int, decimal, int, string)>();
            
            // Track products that will be added in this transaction to check for duplicates
            var productsBeingAdded = new HashSet<int>();
            
            // Process each item in the sale
            foreach (var item in model.Items)
            {
                try
                {
                    // Get the product
                    var product = await _context.Products2.FindAsync(item.ProductId);
                    if (product == null)
                    {
                        continue; // Already validated above
                    }

                    // Check if product is deleted
                    try
                    {
                        if (product.IsDeleted)
                        {
                            _logger.LogWarning("Product {ProductName} (ID: {ProductId}) has been deleted and cannot be sold", 
                                product.ProductName, item.ProductId);
                            ModelState.AddModelError("", $"Product {product.ProductName} has been deleted and cannot be sold");
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        // If IsDeleted property doesn't exist yet, just continue
                        _logger.LogWarning(ex, "Error checking IsDeleted property for product {ProductId}", item.ProductId);
                    }

                    // Check inventory
                    if (product.QuantityInStock < item.Quantity)
                    {
                        _logger.LogWarning("Not enough inventory for product {ProductName}. Requested: {Requested}, Available: {Available}",
                            product.ProductName, item.Quantity, product.QuantityInStock);
                        ModelState.AddModelError("", $"Not enough inventory for {product.ProductName}. Available: {product.QuantityInStock}");
                        continue;
                    }

                    // Add to total transaction amount
                    decimal itemTotal = item.Quantity * item.Price;
                    totalTransactionAmount += itemTotal;
                    
                    // Flag this product as being added in this transaction
                    productsBeingAdded.Add(item.ProductId);
                    
                    // Store product details for later
                    productDetails.Add((item.ProductId, item.Quantity, item.Price, 0, product.ProductName));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error validating sale item with ProductId={ProductId}", item.ProductId);
                    ModelState.AddModelError("", $"Error validating item {item.ProductId}: {ex.Message}");
                }
            }
            
            if (!ModelState.IsValid)
            {
                // If there were errors, reload the page with the model
                var products = await _context.Products2
                    .Where(p => p.BOId == GetCurrentUserId() && p.QuantityInStock > 0)
                    .OrderBy(p => p.ProductName)
                    .ToListAsync();

                ViewBag.Products = products;
                model.AvailableProducts = products;
                
                return View(model);
            }

            // Calculate spend-based points (1 point per ₱1,000 of total transaction)
            // Note: We'll calculate this but NOT store it in EarnedPoints
            int spendPoints = (int)(totalTransactionAmount / 1000);
            
            _logger.LogInformation("Calculated {Points} points for total spend of {TotalSpend:C} (will be shown in lead views only)", 
                spendPoints, totalTransactionAmount);
            
            // Handle lead creation/update if needed
            if (model.SelectedOptions == "new" && model.CreateLead)
            {
                // Check required fields for new lead
                if (string.IsNullOrWhiteSpace(model.CustomerName))
                {
                    _logger.LogWarning("Customer name is required for new lead");
                    ModelState.AddModelError("CustomerName", "Customer name is required for new lead");
                    
                    // Repopulate the model for the view
                    var products = await _context.Products2
                        .Where(p => p.BOId == GetCurrentUserId() && p.QuantityInStock > 0)
                        .OrderBy(p => p.ProductName)
                        .ToListAsync();

                    ViewBag.Products = products;
                    model.AvailableProducts = products;
                    
                    return View(model);
                }
                
                try
                {
                    // First, check if this might be an existing lead
                    var existingLead = await FindExistingLead(model.CustomerName, model.CustomerEmail, model.CustomerPhone);
                    
                    if (existingLead != null)
                    {
                        _logger.LogInformation("Found existing lead that matches the new customer: {LeadId}, {LeadName}", 
                            existingLead.Id, existingLead.LeadName);
                        
                        // Update the existing lead with any new information
                        bool leadUpdated = false;
                        
                        // Update email if it was empty and we now have one
                        if (string.IsNullOrWhiteSpace(existingLead.LeadEmail) && !string.IsNullOrWhiteSpace(model.CustomerEmail))
                        {
                            existingLead.LeadEmail = model.CustomerEmail;
                            leadUpdated = true;
                        }
                        
                        // Update phone if it was empty and we now have one
                        if (string.IsNullOrWhiteSpace(existingLead.LeadPhone) && !string.IsNullOrWhiteSpace(model.CustomerPhone))
                        {
                            existingLead.LeadPhone = model.CustomerPhone;
                            leadUpdated = true;
                        }
                        
                        model.LeadId = existingLead.Id;
                        model.CustomerName = existingLead.LeadName;
                        
                        // Add the items to the lead's points directly
                        await UpdateLeadPoints(existingLead.Id, model.Items);
                        
                        _logger.LogInformation("Updated existing lead {0} points with new purchases", existingLead.Id);
                        
                        // Set TempData to inform the user that we found an existing lead
                        TempData["LeadMatched"] = $"Found existing lead '{existingLead.LeadName}' that matches the customer information. The sale has been associated with this lead.";
                    }
                    else
                    {
                        // Create a new lead since no match was found
                        var newLead = new Leads
                        {
                            LeadName = model.CustomerName,
                            LeadEmail = model.CustomerEmail ?? "",
                            LeadPhone = model.CustomerPhone ?? "",
                            Notes = "Created from QuickSale",
                            CreatedAt = GetSingaporeTime(),
                            CreatedById = GetCurrentUserId(),
                            LeadPoints = 0, // Start with 0 points, will update after saving
                            LastPurchaseDate = GetSingaporeTime(),
                            IsAllowToCampaign = false // Set to false as requested
                        };

                        if (model.Items.Any())
                        {
                            newLead.LastPurchasedId = model.Items.Last().ProductId;
                        }

                        _context.Leads.Add(newLead);
                        await _context.SaveChangesAsync(); // Save to get the ID
                        
                        _logger.LogInformation("Created new lead: {LeadName} with ID: {LeadId}", 
                            newLead.LeadName, newLead.Id);
                        
                        // Set the LeadId for the sales record
                        model.LeadId = newLead.Id;
                        
                        // For a new lead, all products get 2 points as there's no purchase history
                        await UpdateLeadPoints(newLead.Id, model.Items);
                        
                        _logger.LogInformation("Added initial points to new lead {LeadId}", newLead.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating or finding lead: {Message}", ex.Message);
                    ModelState.AddModelError("", "Error creating or finding lead: " + ex.Message);
                    return View(model);
                }
            }
            else if (model.SelectedOptions == "new" && !model.CreateLead)
            {
                // When "Create a lead" is unchecked, we don't associate with any lead
                model.LeadId = null;
            }
            // Handle anonymous buyer option
            else if (model.SelectedOptions == "anonymous")
            {
                // For anonymous buyers, we don't create a lead or associate with an existing one
                // Just set the customer name for the sales record
                model.CustomerName = "Anonymous Buyer";
                model.LeadId = null; // Ensure no lead is associated
            }

            // Variables to track sale information for the success message
            decimal totalAmount = 0;
            int totalItems = 0;
            List<string> productNames = new List<string>();

            // Check if TransactionId column exists in the database
            bool transactionIdColumnExists = false;
            bool itemPointsColumnExists = false;
            try
            {
                // Try to query a sale with TransactionId to see if the column exists
                var testSale = await _context.Sales.FirstOrDefaultAsync();
                var transactionIdProperty = testSale?.GetType().GetProperty("TransactionId");
                transactionIdColumnExists = transactionIdProperty != null;
                _logger.LogInformation("TransactionId column exists: {exists}", transactionIdColumnExists);
                
                // Check if ItemPoints column exists in SaleItem
                var testSaleItem = await _context.SaleItems.FirstOrDefaultAsync();
                var itemPointsProperty = testSaleItem?.GetType().GetProperty("ItemPoints");
                itemPointsColumnExists = itemPointsProperty != null;
                _logger.LogInformation("ItemPoints column exists: {exists}", itemPointsColumnExists);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error checking for columns: {message}", ex.Message);
                transactionIdColumnExists = false;
                itemPointsColumnExists = false;
            }

            // After model validation but before creating sales
            if (model.LeadId.HasValue)
            {
                // Debug: Examine all products and their purchase history
                _logger.LogInformation("==== DEBUGGING POINTS CALCULATION ====");
                DateTime thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
                
                // Get purchase history for the lead in the last 30 days
                var recentPurchasesQuery = _context.Sales
                    .Where(s => s.LeadId == model.LeadId.Value && s.SaleDate >= thirtyDaysAgo);
                
                // Safely select fields without TransactionId
                var recentPurchases = await recentPurchasesQuery
                    .Select(s => new { s.ProductId, s.SaleDate, s.CustomerName })
                    .ToListAsync();
                    
                _logger.LogInformation("Lead {LeadId} has {Count} purchases in the last 30 days", 
                    model.LeadId.Value, recentPurchases.Count);
                
                // Log each historical purchase
                foreach (var purchase in recentPurchases)
                {
                    _logger.LogInformation("  Previous purchase: Product {ProductId} on {Date}", 
                        purchase.ProductId, purchase.SaleDate);
                }
                
                // Now check each product in the current transaction
                foreach (var item in model.Items)
                {
                    bool previouslyPurchased = recentPurchases.Any(p => p.ProductId == item.ProductId);
                    _logger.LogInformation("  Current purchase: Product {ProductId} - Previously purchased: {WasPurchased} - Should get {Points} points", 
                        item.ProductId, previouslyPurchased, previouslyPurchased ? "1" : "2");
                }
                _logger.LogInformation("======================================");
            }

            // Create a single Sale record for the entire transaction
            var saleDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));
            var transactionId = Guid.NewGuid().ToString().Substring(0, 8); // Generate a unique transaction ID
            
            // Calculate total amount for the entire transaction
            decimal totalTransactionAmount2 = model.Items.Sum(item => item.Quantity * item.Price);
            
            // Create the main Sale record for the entire transaction
            var mainSale = new Sale
            {
                BOId = GetCurrentUserId(),
                ProductId = model.Items.First().ProductId, // Use first product ID as reference
                CustomerName = model.CustomerName ?? "Walk-in Customer",
                TotalAmount = totalTransactionAmount2,
                SaleDate = saleDate,
                LeadId = model.LeadId,
                CustomerEmail = model.CustomerEmail,
                CustomerPhone = model.CustomerPhone,
                EarnedPoints = 0 // Will calculate total points later
            };
            
            // Only set TransactionId if the column exists in the database
            if (transactionIdColumnExists)
            {
                mainSale.TransactionId = transactionId;
            }
            
            _context.Sales.Add(mainSale);
            await _context.SaveChangesAsync(); // Save to get the main Sale ID
            
            _logger.LogInformation("Created main Sale record with ID {SaleId}", mainSale.Id);
            if (transactionIdColumnExists)
            {
                _logger.LogInformation("Transaction ID: {TransactionId}", transactionId);
            }
            
            // Track total points earned in this transaction
            int totalPointsForTransaction = 0;
            
            // Create a list to collect all inventory log entries for batch notification
            var inventoryLogs = new List<InventoryLog>();
            
            // Flag to track if we've already sent the batch notification
            bool batchNotificationSent = false;
            
            // Now process each item in the sale (update inventory, create SaleItems)
            foreach (var item in model.Items)
            {
                try
                {
                    // Get the product
                    var product = await _context.Products2.FindAsync(item.ProductId);
                    if (product == null)
                    {
                        continue; // Already validated above
                    }

                    // Check if product is deleted
                    try
                    {
                        if (product.IsDeleted)
                        {
                            _logger.LogWarning("Product {ProductName} (ID: {ProductId}) has been deleted and cannot be sold", 
                                product.ProductName, item.ProductId);
                            ModelState.AddModelError("", $"Product {product.ProductName} has been deleted and cannot be sold");
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        // If IsDeleted property doesn't exist yet, just continue
                        _logger.LogWarning(ex, "Error checking IsDeleted property for product {ProductId}", item.ProductId);
                    }

                    // Update product stock
                    int oldQuantity = product.QuantityInStock;
                    product.QuantityInStock -= item.Quantity;
                    _context.Products2.Update(product);

                    // Get item total and points
                    decimal itemTotal = item.Quantity * item.Price;
                    
                    // Get points directly for this specific product
                    int itemPoints = 0;
                    if (model.LeadId.HasValue)
                    {
                        // Use the direct method to check this product's purchase history
                        itemPoints = await GetPointsForProduct(model.LeadId.Value, item.ProductId);
                        _logger.LogInformation("Sale: Product {ProductId}: Assigned {Points} points (directly calculated)", 
                            item.ProductId, itemPoints);
                            
                        // Double-check the points again - critical fix for the points issue
                        try 
                        {
                            // Check one more time directly from the database before saving
                            DateTime thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
                            
                            // Get all purchases for this lead in the last 30 days, including the actual product IDs from SaleItems
                            // Use a query that doesn't depend on the TransactionId column
                            var recentSaleItems = await _context.Sales
                                .Where(s => s.LeadId == model.LeadId.Value && s.SaleDate >= thirtyDaysAgo)
                                .Join(_context.SaleItems,
                                    sale => sale.Id,
                                    item => item.SaleId,
                                    (sale, item) => new { sale.Id, item.ProductId, sale.SaleDate })
                                .ToListAsync();
                                
                            bool isPreviouslyPurchased = recentSaleItems.Any(s => s.ProductId == item.ProductId);
                            
                            // For debugging
                            string productIds = string.Join(", ", recentSaleItems.Select(s => s.ProductId));
                            _logger.LogInformation("Double-check - All previous product IDs: {ProductIds}", productIds);
                            
                            // Ensure the correct points value (1 for previously purchased, 2 for new)
                            int correctPoints;
                            if (isPreviouslyPurchased)
                            {
                                correctPoints = 1;
                                _logger.LogInformation("Double-check: Product {ProductId} was previously purchased, should be 1 point", item.ProductId);
                            }
                            else
                            {
                                correctPoints = 2;
                                _logger.LogInformation("Double-check: Product {ProductId} was NOT previously purchased, should be 2 points", item.ProductId);
                            }
                            
                            if (itemPoints != correctPoints)
                            {
                                _logger.LogWarning("Correcting points for Product {ProductId}: Was {OldPoints}, should be {CorrectPoints} (previously purchased: {WasPurchased})",
                                    item.ProductId, itemPoints, correctPoints, isPreviouslyPurchased);
                                    
                                itemPoints = correctPoints;
                            }
                            
                            // Add to total points for the transaction
                            totalPointsForTransaction += itemPoints;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error during points correction check for product {ProductId}", item.ProductId);
                        }
                    }
                    
                    // Create a SaleItem record linked to the main Sale
                    var saleItem = new SaleItem
                    {
                        SaleId = mainSale.Id, // Link to the main sale
                        ProductId = item.ProductId,
                        ProductName = product.ProductName,
                        Quantity = item.Quantity,
                        UnitPrice = item.Price,
                        TotalPrice = itemTotal,
                        Notes = $"QuickSale on {DateTime.Now.ToString("yyyy-MM-dd")}"
                    };
                    
                    // Only set ItemPoints if the column exists in the database
                    if (itemPointsColumnExists)
                    {
                        saleItem.ItemPoints = itemPoints; // Store individual item points
                    }
                    _context.SaleItems.Add(saleItem);

                    // Create inventory log
                    var log = new InventoryLog
                    {
                        BOId = GetCurrentUserId(),
                        ProductId = product.Id,
                        ProductName = product.ProductName,
                        QuantityBefore = oldQuantity,
                        QuantityAfter = product.QuantityInStock,
                        MovementType = InventoryMovementTypes.Sale,
                        ReferenceId = transactionIdColumnExists ? $"SALE-{mainSale.Id}-{transactionId}" : $"SALE-{mainSale.Id}", // Include transaction ID if it exists
                        Notes = $"Sale to {model.CustomerName ?? "Customer"} (Item: {item.Quantity} x {product.ProductName})",
                        Timestamp = saleDate
                    };
                    _context.InventoryLogs.Add(log);
                    
                    // Add the log to our collection for batch notification
                    inventoryLogs.Add(log);

                    // Update sale information for the success message
                    totalAmount += itemTotal;
                    totalItems += item.Quantity;
                    productNames.Add($"{item.Quantity} x {product.ProductName}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing sale item with ProductId={ProductId}", item.ProductId);
                    ModelState.AddModelError("", $"Error processing item {item.ProductId}: {ex.Message}");
                }
            }
            
            // Update the main sale record with the total points earned
            if (model.LeadId.HasValue)
            {
                mainSale.EarnedPoints = totalPointsForTransaction;
                _context.Sales.Update(mainSale);
                
                _logger.LogInformation("Updated main sale with total points: {Points}", totalPointsForTransaction);
            }
            
            // Save all changes
            await _context.SaveChangesAsync();
            
            // Send batch notification through SignalR for all inventory movements at once
            if (inventoryLogs.Count > 0 && !batchNotificationSent)
            {
                try
                {
                    var userId = GetCurrentUserId();
                    
                    // Log the inventory movements being sent
                    _logger.LogInformation("Sending batch notification for {Count} inventory movements:", inventoryLogs.Count);
                    foreach (var log in inventoryLogs)
                    {
                        _logger.LogInformation("  - Movement {Id}: Product {ProductName}, Change: {Before} → {After}, Ref: {Ref}",
                            log.Id, log.ProductName, log.QuantityBefore, log.QuantityAfter, log.ReferenceId);
                    }
                    
                    // Convert inventory logs to DTOs to avoid circular references
                    var logDtos = inventoryLogs.Select(log => new
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
                    }).ToList();
                    
                    // Make sure we have all logs in the DTO list
                    _logger.LogInformation("Created {Count} DTOs for batch notification", logDtos.Count);
                    
                    // Send to business group
                    await _hubContext.Clients.Group($"business_{userId}").SendAsync("ReceiveBatchInventoryMovements", logDtos);
                    
                    // Also send to the specific user
                    // await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveBatchInventoryMovements", logDtos);
                    
                    _logger.LogInformation("Successfully sent batch notification for {Count} inventory movements", inventoryLogs.Count);
                    
                    // Mark that we've sent the notification
                    batchNotificationSent = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending batch SignalR notification for inventory movements");
                    // Continue processing - don't fail the sale if real-time notification fails
                }
            }
            
            _logger.LogInformation("Successfully completed QuickSale transaction with {ItemCount} items, total amount: {Amount:C}", 
                model.Items.Count, totalAmount);

            if (!ModelState.IsValid)
            {
                // If there were errors, reload the page with the model
                var products = await _context.Products2
                    .Where(p => p.BOId == GetCurrentUserId() && p.QuantityInStock > 0)
                    .OrderBy(p => p.ProductName)
                    .ToListAsync();

                ViewBag.Products = products;
                model.AvailableProducts = products;
                
                return View(model);
            }

            // Build success message with points breakdown
            string pointsBreakdown = "";
            if (model.LeadId.HasValue)
            {
                int totalPointsEarned = totalLeadPoints + spendPoints;
                
                if (totalPointsEarned > 0)
                {
                    pointsBreakdown = $" <strong>(+{totalPointsEarned} points)</strong>";
                }
            }
            
            // Format currency values and build product summary
            string formattedAmount = string.Format("₱{0:N2}", totalAmount);
            
            // Get distinct product names without quantities
            var distinctProductNames = new List<string>();
            foreach (var fullProductName in productNames)
            {
                // Extract just the product name without quantity
                string[] parts = fullProductName.Split('x');
                if (parts.Length > 1)
                {
                    // Take the part after "x " to get just the product name
                    string productName = parts[1].Trim();
                    if (!distinctProductNames.Contains(productName))
                    {
                        distinctProductNames.Add(productName);
                    }
                }
                else
                {
                    // If format is different, just add the whole name
                    distinctProductNames.Add(fullProductName);
                }
            }
            
            // Format the product list
            string productSummary;
            if (distinctProductNames.Count <= 2)
            {
                productSummary = string.Join(", ", distinctProductNames);
            }
            else
            {
                productSummary = $"{distinctProductNames[0]}, {distinctProductNames[1]} and {distinctProductNames.Count - 2} more item{(distinctProductNames.Count - 2 > 1 ? "s" : "")}";
            }
            
            // Set SweetAlert success message
            string customerName = !string.IsNullOrEmpty(model.CustomerName) ? model.CustomerName : "Customer";
            TempData["SwalSuccess"] = $"Sale to <strong>{customerName}</strong> completed successfully!<br>{totalItems} items sold for <strong>{formattedAmount}</strong>.<br>Products: {productSummary}{pointsBreakdown}";

            return RedirectToAction("QuickSale");
        }

        // New method to calculate points for products based on purchase history
        private async Task CalculateProductPoints(int leadId, List<(int productId, int quantity, decimal price, int points, string productName)> productDetails)
        {
            try
            {
                _logger.LogInformation("Starting CalculateProductPoints for lead {LeadId} with {Count} products", 
                    leadId, productDetails.Count);
                    
                // Get purchase history for this lead in the last 30 days - direct query for each product
                DateTime thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
                
                var recentSaleItems = await _context.Sales
                    .Where(s => s.LeadId == leadId && s.SaleDate >= thirtyDaysAgo)
                    .Join(_context.SaleItems,
                        sale => sale.Id,
                        item => item.SaleId,
                        (sale, item) => new { sale.Id, item.ProductId, sale.SaleDate })
                    .ToListAsync();
                
                // Log all previously purchased products
                var previousProductIds = recentSaleItems.Select(s => s.ProductId).Distinct().ToList();
                _logger.LogInformation("Lead {LeadId} has previously purchased these products: {Products}", 
                    leadId, string.Join(", ", previousProductIds));
                
                // Calculate points for each product (no need to track duplicates since they're already consolidated)
                for (int i = 0; i < productDetails.Count; i++)
                {
                    var item = productDetails[i];
                    int productId = item.productId;
                    
                    // Check if this product appears in the purchase history
                    bool previouslyPurchased = previousProductIds.Contains(productId);
                    
                    // Assign points - 2 for new product, 1 for previously purchased
                    // Note: Each unique product earns points only once, regardless of quantity
                    int productPoints = previouslyPurchased ? 1 : 2;
                    
                    _logger.LogInformation("Product {ProductId} ({ProductName}): Quantity: {Quantity}, Previously purchased: {WasPurchased}, Assigned {Points} points",
                        productId, item.productName, item.quantity, previouslyPurchased, productPoints);
                    
                    // Update the points value in the tuple
                    productDetails[i] = (item.productId, item.quantity, item.price, productPoints, item.productName);
                }
                
                _logger.LogInformation("Completed points calculation for {Count} products", productDetails.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating product points for lead {LeadId}", leadId);
                
                // If there's an error, default to giving 2 points for all products
                for (int i = 0; i < productDetails.Count; i++)
                {
                    var item = productDetails[i];
                    productDetails[i] = (item.productId, item.quantity, item.price, 2, item.productName);
                }
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetProductDetails(int id)
        {
            try
            {
                _logger.LogInformation("Fetching product details for {ProductId}", id);
                var product = await _context.Products2.FindAsync(id);
                if (product == null)
                {
                    _logger.LogWarning("Product {ProductId} not found", id);
                    return NotFound();
                }

                _logger.LogInformation("Returning details for product {ProductId}: Price={Price}, Stock={Stock}",
                    id, product.SellingPrice, product.QuantityInStock);

                return Json(new
                {
                    price = product.SellingPrice,
                    availableStock = product.QuantityInStock
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching product details for {ProductId}", id);
                return StatusCode(500, "Error fetching product details");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetProductPrice(int id)
        {
            var product = await _context.Products2.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            return Json(product.SellingPrice);
        }

        // GET: Inventory1/StockIn
        [HttpGet]
        public async Task<IActionResult> StockIn(int? id)
        {
            try
            {
                _logger.LogInformation("Loading StockIn page for user {UserId}", GetCurrentUserId());

                List<Product> products;
                try
                {
                    // Try to filter by IsDeleted property
                    products = await _context.Products2
                        .Where(p => p.BOId == GetCurrentUserId() && !p.IsDeleted)
                        .OrderBy(p => p.ProductName)
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    // If IsDeleted column doesn't exist yet, just filter without it
                    _logger.LogWarning(ex, "Error filtering by IsDeleted, falling back to basic query");
                    products = await _context.Products2
                        .Where(p => p.BOId == GetCurrentUserId())
                        .OrderBy(p => p.ProductName)
                        .ToListAsync();
                }

                _logger.LogDebug("Loaded {ProductCount} products for StockIn", products.Count);

                return View(new StockInViewModel
                {
                    AvailableProducts = products,
                    ProductId = id ?? 0,
                    ReceivingDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore")),
                    Notes = "Stock In"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading StockIn page");

                return View("Error", new ErrorViewModel
                {
                    RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
                });
            }
        }

        // POST: Inventory1/StockIn
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StockIn(StockInViewModel model)
        {
            _logger.LogInformation("Starting StockIn process for product {ProductId} with quantity {Quantity}",
                model.ProductId, model.Quantity);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState invalid for StockIn. Errors: {Errors}",
                    string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

                try
                {
                    model.AvailableProducts = await _context.Products2
                        .Where(p => p.BOId == GetCurrentUserId())
                        .ToListAsync();
                    return View(model);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error repopulating products for invalid StockIn model");
                    throw; // or handle gracefully
                }
            }

            try
            {
                // Update product quantity
                var product = await _context.Products2.FindAsync(model.ProductId);
                if (product == null)
                {
                    _logger.LogError("Product {ProductId} not found during StockIn", model.ProductId);
                    ModelState.AddModelError("", "Product not found");
                    model.AvailableProducts = await _context.Products2
                        .Where(p => p.BOId == GetCurrentUserId())
                        .ToListAsync();
                    return View(model);
                }

                // Check if product is deleted
                try
                {
                    if (product.IsDeleted)
                    {
                        _logger.LogWarning("Product {ProductName} (ID: {ProductId}) has been deleted and cannot be restocked", 
                            product.ProductName, model.ProductId);
                        ModelState.AddModelError("", $"Product {product.ProductName} has been deleted and cannot be restocked");
                        model.AvailableProducts = await _context.Products2
                            .Where(p => p.BOId == GetCurrentUserId() && !p.IsDeleted)
                            .ToListAsync();
                        return View(model);
                    }
                }
                catch (Exception ex)
                {
                    // If IsDeleted property doesn't exist yet, just continue
                    _logger.LogWarning(ex, "Error checking IsDeleted property for product {ProductId}", model.ProductId);
                }

                _logger.LogDebug("Current stock for product {ProductId}: {CurrentStock}. Adding {Quantity}",
                    model.ProductId, product.QuantityInStock, model.Quantity);

                int oldQuantity = product.QuantityInStock;
                product.QuantityInStock += model.Quantity;
                _context.Products2.Update(product);

                // Create inventory log
                var userId = GetCurrentUserId();
                var log = new InventoryLog
                {
                    BOId = userId,
                    ProductId = product.Id,
                    ProductName = product.ProductName,
                    QuantityBefore = oldQuantity,
                    QuantityAfter = product.QuantityInStock,
                    MovementType = InventoryMovementTypes.StockIn,
                    ReferenceId = $"STOCKIN-{TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore")):yyyyMMddHHmmss}",
                    Notes = model.Notes ?? $"Stock In of {model.Quantity} items",
                    Timestamp = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"))
                };

                _context.InventoryLogs.Add(log);
                _logger.LogDebug("Created inventory log entry for StockIn");

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
                    await _hubContext.Clients.Group($"business_{userId}").SendAsync("ReceiveInventoryMovement", logDto);
                    
                    // Also send to the specific user in case they're not in the group
                    await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveInventoryMovement", logDto);
                    
                    _logger.LogInformation("Successfully sent real-time notification for StockIn");
                }
                catch (Exception ex)
                {
                    // Log the error but continue - don't fail the entire operation just because of SignalR
                    _logger.LogError(ex, "Error sending real-time notification for StockIn");
                }

                _logger.LogInformation("Successfully processed StockIn for product {ProductId}. New quantity: {NewQuantity}",
                    model.ProductId, product.QuantityInStock);
                
                // Add success message for SweetAlert
                TempData["SwalSuccess"] = $"Successfully added {model.Quantity} units to {product.ProductName}. New total: {product.QuantityInStock} units.";
                
                return RedirectToAction("StockIn", "Inventory1");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing StockIn for product {ProductId}", model.ProductId);

                ModelState.AddModelError("", $"Error processing stock in: {ex.Message}");
                model.AvailableProducts = await _context.Products2
                    .Where(p => p.BOId == GetCurrentUserId())
                    .ToListAsync();
                return View(model);
            }
        }

        public static class InventoryMovementTypes
        {
            public const string Sale = "Sale";
            public const string Purchase = "Purchase";
            public const string Adjustment = "Adjustment";
            public const string StockIn = "Stock In";
            public const string NewProduct = "New Product";
            public const string EditProduct = "Edit Product";
            public const string DeleteProduct = "Delete Product";
        }

        [HttpGet]
        public async Task<IActionResult> PublishToMarketplace(int id)
        {
            var product = await _context.Products2
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                TempData["ErrorMessage"] = "Product not found";
                return RedirectToAction("Index");
            }
            
            var viewModel = new PublishToMarketplaceViewModel
            {
                ProductId = id,
                ProductName = product.ProductName,
                Description = product.Description,
                Price = product.SellingPrice,
                IsPublished = product.IsPublished,
                MarketplaceDescription = product.Description,
                MarketplacePrice = product.SellingPrice,
                DisplayFeatured = product.DisplayFeatured,
                ExistingImages = product.Images.Select(img => new ProductImageViewModel
                {
                    Id = img.Id,
                    ImagePath = img.ImagePath,
                    IsMainImage = img.IsMainImage,
                    Title = img.Title ?? product.ProductName,
                    DisplayOrder = img.DisplayOrder
                }).ToList()
            };
            
            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> UnPublishToMarketplace(int id)
        {
            var product = await _context.Products2.FindAsync(id);
            if (product == null)
            {
                _logger.LogError("Product {id} not found in database", id);
                TempData["ErrorMessage"] = "Product not found.";
                return RedirectToAction("Index"); // or wherever your list is
            }

            product.IsPublished = false;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Product has been unpublished successfully.";
            return RedirectToAction("Index"); // or redirect back to the list or detail page
        }

        [HttpGet]
        public async Task<IActionResult> PublishToMarketplaceAgain(int id)
        {
            var product = await _context.Products2.FindAsync(id);
            if (product == null)
            {
                _logger.LogError("Product {id} not found in database", id);
                TempData["ErrorMessage"] = "Product not found.";
                return RedirectToAction("Index"); // or wherever your list is
            }

            product.IsPublished = true;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Product has been published successfully.";
            return RedirectToAction("Index"); // or redirect back to the list or detail page
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PublishToMarketplace(PublishToMarketplaceViewModel model)
        {
            _logger.LogInformation("Starting PublishToMarketplace for product {ProductId}", model.ProductId);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState invalid for product {ProductId}. Errors: {Errors}",
                    model.ProductId,
                    string.Join(", ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)));

                try
                {
                    model.ExistingImages = await _context.ProductImages
                        .Where(img => img.ProductId == model.ProductId)
                        .Select(img => new ProductImageViewModel
                        {
                            Id = img.Id,
                            ImagePath = img.ImagePath,
                            IsMainImage = img.IsMainImage,
                            Title = img.Title,
                            DisplayOrder = img.DisplayOrder
                        })
                        .ToListAsync();

                    return View(model);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error repopulating existing images for product {ProductId}", model.ProductId);
                    throw;
                }
            }

            try
            {
                // Look up the product
                _logger.LogDebug("Looking up product {ProductId}", model.ProductId);
                var product = await _context.Products2.FindAsync(model.ProductId);
                if (product == null)
                {
                    _logger.LogError("Product {ProductId} not found in database", model.ProductId);
                    ModelState.AddModelError("", "Product not found");
                    return View(model);
                }

                // Get existing image count
                var existingImageCount = await _context.ProductImages
                    .CountAsync(img => img.ProductId == model.ProductId);

                // Check if new uploads would exceed 8 images total
                var newImageCount = model.ImageFiles?.Count ?? 0;
                if (existingImageCount + newImageCount > 8)
                {
                    _logger.LogWarning("Image count exceeds limit: {ExistingCount} existing + {NewCount} new > 8 max", 
                        existingImageCount, newImageCount);
                    ModelState.AddModelError("ImageFiles", $"Total image count cannot exceed 8. You already have {existingImageCount} images.");
                    
                    model.ExistingImages = await _context.ProductImages
                        .Where(img => img.ProductId == model.ProductId)
                        .Select(img => new ProductImageViewModel
                        {
                            Id = img.Id,
                            ImagePath = img.ImagePath,
                            IsMainImage = img.IsMainImage,
                            Title = img.Title,
                            DisplayOrder = img.DisplayOrder
                        })
                        .ToListAsync();
                    
                    return View(model);
                }

                _logger.LogInformation("Updating product publishing status - Published: {IsPublished}, Featured: {IsFeatured}",
                    model.IsPublished, model.DisplayFeatured);

                // Update product
                product.IsPublished = model.IsPublished;
                product.IsAlreadyPublished = true;
                product.DisplayFeatured = model.DisplayFeatured;
                product.Description = model.MarketplaceDescription;
                product.SellingPrice = model.MarketplacePrice ?? product.SellingPrice;
                product.UpdatedAt = GetSingaporeTime();
                _context.Products2.Update(product);

                // Handle image uploads
                if (model.ImageFiles != null && model.ImageFiles.Count > 0)
                {
                    _logger.LogDebug("Processing {ImageCount} uploaded images", model.ImageFiles.Count);
                    
                    // Check if this product has any images already
                    bool hasNoExistingImages = existingImageCount == 0;

                    foreach (var file in model.ImageFiles)
                    {
                        if (file.Length > 0)
                        {
                            _logger.LogDebug("Processing image: {FileName} ({FileSize} bytes)",
                                file.FileName, file.Length);

                            try
                            {
                                var imagePath = await SaveFile(GetCurrentUserId().ToString(), file, "products");
                                _logger.LogDebug("Image saved to: {ImagePath}", imagePath);

                                var image = new ProductImage
                                {
                                    ProductId = model.ProductId,
                                    ImagePath = imagePath,
                                    IsMainImage = hasNoExistingImages && model.ImageFiles.IndexOf(file) == 0, // First image is main only if no existing images
                                    Title = model.ProductName,
                                    DisplayOrder = existingImageCount + model.ImageFiles.IndexOf(file) + 1,
                                    CreatedAt = GetSingaporeTime()
                                };

                                _context.ProductImages.Add(image);
                                _logger.LogInformation("Added new product image (Main: {IsMain})", image.IsMainImage);

                                hasNoExistingImages = false; // After first image is processed
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error saving image {FileName}", file.FileName);
                                ModelState.AddModelError("ImageFiles", $"Error saving image: {ex.Message}");
                                continue;
                            }
                        }
                    }
                }

                _logger.LogDebug("Saving changes to database");
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully updated product publishing settings");

                TempData["SuccessMessage"] = "Product publishing settings updated successfully";
                return RedirectToAction("Index", "Inventory1");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing product {ProductId} to marketplace", model.ProductId);

                // Log all model state errors if they exist
                if (!ModelState.IsValid)
                {
                    foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                    {
                        _logger.LogError("ModelState error: {ErrorMessage}", error.ErrorMessage);
                    }
                }

                ModelState.AddModelError("", "An error occurred while updating the product. Please try again.");

                try
                {
                    model.ExistingImages = await _context.ProductImages
                        .Where(img => img.ProductId == model.ProductId)
                        .Select(img => new ProductImageViewModel
                        {
                            Id = img.Id,
                            ImagePath = img.ImagePath,
                            IsMainImage = img.IsMainImage,
                            Title = img.Title,
                            DisplayOrder = img.DisplayOrder
                        })
                        .ToListAsync();
                }
                catch (Exception innerEx)
                {
                    _logger.LogError(innerEx, "Error repopulating existing images after failure");
                }

                return View(model);
            }
        }

        private async Task<string> SaveFile(string userId, IFormFile file, string subFolder)
        {
            if (file == null || file.Length == 0)
                return string.Empty;

            // Validate file extension
            var permittedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (string.IsNullOrEmpty(extension) || !permittedExtensions.Contains(extension))
            {
                throw new InvalidOperationException("Invalid file type. Only JPG, PNG, and GIF are allowed.");
            }

            // Validate file size (5MB limit)
            if (file.Length > 5 * 1024 * 1024)
            {
                throw new InvalidOperationException("File size exceeds 5MB limit.");
            }

            var userUploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, "uploads", userId, subFolder);
            Directory.CreateDirectory(userUploadsFolder);

            var fileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}_{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(userUploadsFolder, fileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return $"/uploads/{userId}/{subFolder}/{fileName}";
        }

        [HttpPost]
        public async Task<IActionResult> SetMainImage(int imageId, int productId)
        {
            try
            {
                // Reset all images for this product to not be main
                var productImages = await _context.ProductImages
                    .Where(img => img.ProductId == productId)
                    .ToListAsync();

                foreach (var img in productImages)
                {
                    img.IsMainImage = false;
                }

                // Set the selected image as main
                var mainImage = productImages.FirstOrDefault(img => img.Id == imageId);
                if (mainImage != null)
                {
                    mainImage.IsMainImage = true;
                    await _context.SaveChangesAsync();
                    return Json(new { success = true });
                }

                return Json(new { success = false, message = "Image not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting main image");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteImage(int imageId)
        {
            try
            {
                var image = await _context.ProductImages.FindAsync(imageId);
                if (image == null)
                {
                    return Json(new { success = false, message = "Image not found" });
                }

                // Delete physical file
                if (!string.IsNullOrEmpty(image.ImagePath))
                {
                    var filePath = Path.Combine(_hostEnvironment.WebRootPath, image.ImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }

                _context.ProductImages.Remove(image);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting image");
                return Json(new { success = false, message = ex.Message });
            }
        }

        private DateTime GetSingaporeTime() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));

        // Add QuickView action method to get product details for the modal
        [HttpGet]
        [Route("Inventory1/QuickView/{id}")]
        public async Task<IActionResult> QuickView(int id)
        {
            try
            {
                var product = await _context.Products2
                    .Include(p => p.Images)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (product == null)
                {
                    return NotFound();
                }

                var viewModel = new ProductQuickViewModel
                {
                    Id = product.Id,
                    ProductName = product.ProductName,
                    Description = product.Description,
                    SellingPrice = product.SellingPrice,
                    QuantityInStock = product.QuantityInStock,
                    Category = product.Category,
                    Images = product.Images?.ToList() ?? new List<ProductImage>(),
                    IsCurrentUserOwner = product.BOId == GetCurrentUserId(),
                    IsPublished = product.IsPublished
                };

                ViewBag.IsCurrentUser = viewModel.IsCurrentUserOwner;
                return PartialView("_ProductQuickView", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading product quick view for product {ProductId}", id);
                return Content($"<div class='alert alert-danger'>Error loading product details: {ex.Message}</div>");
            }
        }

        [HttpPost]
        [Route("Inventory1/SendProductRequest")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendProductRequest([FromBody] ProductRequestModel request)
        {
            try
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();

                if (!int.TryParse(userIdClaim, out int userId))
                    return Unauthorized();

                var product = await _context.Products2.FindAsync(request.ProductId);
                if (product == null) return NotFound("Product not found");

                // Create the product request data
                var requestData = new
                {
                    productId = request.ProductId,
                    productName = product.ProductName,
                    quantity = request.Quantity,
                    customerName = request.CustomerName,
                    message = request.Message,
                };

                string jsonData = System.Text.Json.JsonSerializer.Serialize(requestData);

                if (product.BOId <= 0)
                    return BadRequest(new { success = false, message = "Product owner not found." });

                int receiverId = product.BOId;

                var chat = new Chat
                {
                    SenderId = userId,
                    ReceiverId = receiverId,
                    Message = "Product Request",
                    JSONString = jsonData,
                    CreatedAt = GetSingaporeTime(),
                    IsRead = false,
                    Status = ChatStatus.Request
                };

                _context.Chats.Add(chat);
                await _context.SaveChangesAsync();

                await _hubContext.Clients
                    .Users(receiverId.ToString(), userId.ToString())
                    .SendAsync("SendProductRequest2",
                        userId.ToString(),
                        receiverId.ToString(),
                        chat.Status.ToString(),
                        chat.JSONString,
                        chat.Id);

                await _hubContext.Clients
                    .Users(receiverId.ToString(), userId.ToString())
                    .SendAsync("ReceiveProductRequest",
                        chat.Id,
                        product.Id,
                        product.ProductName,
                        requestData.quantity,
                        requestData.customerName,
                        requestData.message,
                        chat.CreatedAt
                        );

                return Ok(new
                {
                    success = true,
                    message = "Product request sent successfully",
                    receiverId = receiverId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending product request");
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while sending the product request"
                });
            }
        }

        public class ProductRequestModel
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
            public string CustomerName { get; set; }
            public string Message { get; set; }
        }

        // Method to calculate and update lead points
        private async Task UpdateLeadPoints(int leadId, List<SaleItemViewModel> items)
        {
            var lead = await _context.Leads.FindAsync(leadId);
            if (lead == null) return;

            // Start with current points or 0
            int totalPoints = lead.LeadPoints ?? 0;
            
            // Get all recent purchases in the past 30 days - join with SaleItems to get correct product IDs
            DateTime thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var recentSaleItems = await _context.Sales
                .Where(s => s.LeadId == leadId && s.SaleDate >= thirtyDaysAgo)
                .Join(_context.SaleItems,
                    sale => sale.Id,
                    item => item.SaleId,
                    (sale, item) => new { sale.Id, item.ProductId, sale.SaleDate })
                .ToListAsync();
                
            // Extract unique product IDs for logging and checking
            var recentProductIds = recentSaleItems.Select(s => s.ProductId).Distinct().ToList();
                
            _logger.LogInformation("RECENT PURCHASES: Lead {LeadId} has {Count} distinct products purchased in the last 30 days: {Products}", 
                leadId, recentProductIds.Count, string.Join(", ", recentProductIds));
            
            // Calculate points for each unique product (note: items should already be consolidated)
            int productPoints = 0;
            
            foreach (var item in items)
            {
                // Determine if this product was previously purchased
                bool wasPreviouslyPurchased = recentProductIds.Contains(item.ProductId);
                
                // Award points: 1 for previously purchased, 2 for new
                int points;
                if (wasPreviouslyPurchased)
                {
                    points = 1;
                    _logger.LogInformation("Product {ProductId} with quantity {Quantity} was previously purchased, assigning 1 point", 
                        item.ProductId, item.Quantity);
                }
                else
                {
                    points = 2;
                    _logger.LogInformation("Product {ProductId} with quantity {Quantity} was NOT previously purchased, assigning 2 points", 
                        item.ProductId, item.Quantity);
                }
                
                productPoints += points;
            }
            
            // Update total points
            totalPoints += productPoints;
            lead.LeadPoints = totalPoints;
            lead.LastPurchaseDate = GetSingaporeTime();
            
            // Set the last purchased product ID to the last product in the list
            if (items.Any())
            {
                lead.LastPurchasedId = items.Last().ProductId;
            }
            
            _context.Leads.Update(lead);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Updated lead {LeadId}: Added {NewPoints} new points, Total now {TotalPoints}", 
                leadId, productPoints, totalPoints);
        }
        
        // Method to calculate points for a single product
        private async Task<int> GetPointsForProduct(int leadId, int productId)
        {
            try
            {
                // Get all sales in the last 30 days for this lead first, then filter client-side
                DateTime thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
                
                // Query Sale and SaleItems tables together to get the correct product IDs
                var recentSaleItems = await _context.Sales
                    .Where(s => s.LeadId == leadId && s.SaleDate >= thirtyDaysAgo)
                    .Join(_context.SaleItems, 
                        sale => sale.Id, 
                        item => item.SaleId, 
                        (sale, item) => new { sale.Id, item.ProductId, sale.SaleDate })
                    .ToListAsync(); // Execute query here
                
                // Get distinct product IDs
                var previousProductIds = recentSaleItems.Select(s => s.ProductId).Distinct().ToList();
                
                // Now check if our product ID is in the list
                bool wasPreviouslyPurchased = previousProductIds.Contains(productId);
                
                // Print all product IDs for debugging
                string allProductIds = string.Join(", ", previousProductIds);
                _logger.LogInformation("All previous purchases (distinct products): {ProductIds}", allProductIds);
                _logger.LogInformation("Looking for product ID: {ProductId}, Found: {WasFound}", productId, wasPreviouslyPurchased);
                
                int points;
                if (wasPreviouslyPurchased)
                {
                    points = 1;
                    _logger.LogInformation("Product was previously purchased, assigning 1 point");
                }
                else
                {
                    points = 2;
                    _logger.LogInformation("Product was NOT previously purchased, assigning 2 points");
                }
                
                _logger.LogInformation("DIRECT CHECK: Product {ProductId} for Lead {LeadId}: Previously purchased = {WasPurchased}, Points = {Points}", 
                    productId, leadId, wasPreviouslyPurchased, points);
                    
                return points;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking purchase history for Product {ProductId}", productId);
                return 2; // Default to 2 points if there's an error
            }
        }
        
        private async Task<Leads?> FindExistingLead(string? name, string? email, string? phone)
        {
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone))
            {
                _logger.LogInformation("No lead info provided to search with");
                return null;
            }
            
            _logger.LogInformation("Searching for existing lead with Name: '{Name}', Email: '{Email}', Phone: '{Phone}'", 
                name ?? "(null)", email ?? "(null)", phone ?? "(null)");
            
            // Build base query - remove the BOId filter to find all leads regardless of creator
            var query = _context.Leads.Where(l => l.Status != Leads.LeadStatus.Deleted);
            
            // Store potential matches with priority
            List<(Leads lead, int priority)> potentialMatches = new List<(Leads, int)>();
            
            // --- Email + Phone match (highest priority: 3) ---
            if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(phone))
            {
                // Normalize phone number
                string normalizedInputPhone = new string(phone.Where(c => char.IsDigit(c)).ToArray());
                if (!string.IsNullOrWhiteSpace(normalizedInputPhone))
                {
                    var leads = await query.Where(l => 
                        !string.IsNullOrWhiteSpace(l.LeadEmail) && 
                        l.LeadEmail.ToLower() == email.ToLower())
                        .ToListAsync();
                        
                    foreach (var lead in leads)
                    {
                        if (!string.IsNullOrWhiteSpace(lead.LeadPhone))
                        {
                            string normalizedLeadPhone = new string(lead.LeadPhone.Where(c => char.IsDigit(c)).ToArray());
                            if (normalizedLeadPhone == normalizedInputPhone)
                            {
                                _logger.LogInformation("Found email+phone match: Lead {LeadId}, {LeadName} - Priority 3", lead.Id, lead.LeadName);
                                potentialMatches.Add((lead, 3));
                            }
                        }
                    }
                }
            }
            
            // --- Email + Name match (priority: 2) ---
            if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(name))
            {
                var leads = await query.Where(l => 
                    !string.IsNullOrWhiteSpace(l.LeadEmail) && 
                    l.LeadEmail.ToLower() == email.ToLower())
                    .ToListAsync();
                    
                foreach (var lead in leads)
                {
                    if (lead.LeadName.ToLower() == name.ToLower())
                    {
                        _logger.LogInformation("Found email+name match: Lead {LeadId}, {LeadName} - Priority 2", lead.Id, lead.LeadName);
                        potentialMatches.Add((lead, 2));
                    }
                }
            }
            
            // --- Phone + Name match (priority: 1) ---
            if (!string.IsNullOrWhiteSpace(phone) && !string.IsNullOrWhiteSpace(name))
            {
                // Normalize phone number
                string normalizedInputPhone = new string(phone.Where(c => char.IsDigit(c)).ToArray());
                if (!string.IsNullOrWhiteSpace(normalizedInputPhone))
                {
                    var leads = await query.ToListAsync();
                    
                    foreach (var lead in leads)
                    {
                        if (!string.IsNullOrWhiteSpace(lead.LeadPhone) && lead.LeadName.ToLower() == name.ToLower())
                        {
                            string normalizedLeadPhone = new string(lead.LeadPhone.Where(c => char.IsDigit(c)).ToArray());
                            if (normalizedLeadPhone == normalizedInputPhone)
                            {
                                _logger.LogInformation("Found phone+name match: Lead {LeadId}, {LeadName} - Priority 1", lead.Id, lead.LeadName);
                                potentialMatches.Add((lead, 1));
                            }
                        }
                    }
                }
            }
            
            // --- Individual field exact matches (if no combined matches found) ---
            if (!potentialMatches.Any())
            {
                // Email exact match
                if (!string.IsNullOrWhiteSpace(email))
                {
                    var lead = await query.FirstOrDefaultAsync(l => 
                        !string.IsNullOrWhiteSpace(l.LeadEmail) && 
                        l.LeadEmail.ToLower() == email.ToLower());
                        
                    if (lead != null)
                    {
                        _logger.LogInformation("Found email-only match: Lead {LeadId}, {LeadName} - Priority 0", lead.Id, lead.LeadName);
                        potentialMatches.Add((lead, 0));
                    }
                }
                
                // Phone exact match
                if (!string.IsNullOrWhiteSpace(phone) && !potentialMatches.Any())
                {
                    string normalizedInputPhone = new string(phone.Where(c => char.IsDigit(c)).ToArray());
                    if (!string.IsNullOrWhiteSpace(normalizedInputPhone))
                    {
                        var leads = await query.ToListAsync();
                        var lead = leads.FirstOrDefault(l => 
                            !string.IsNullOrWhiteSpace(l.LeadPhone) && 
                            new string(l.LeadPhone.Where(c => char.IsDigit(c)).ToArray()) == normalizedInputPhone);
                            
                        if (lead != null)
                        {
                            _logger.LogInformation("Found phone-only match: Lead {LeadId}, {LeadName} - Priority 0", lead.Id, lead.LeadName);
                            potentialMatches.Add((lead, 0));
                        }
                    }
                }
                
                // Name exact match (lowest priority)
                if (!string.IsNullOrWhiteSpace(name) && !potentialMatches.Any())
                {
                    var lead = await query.FirstOrDefaultAsync(l => l.LeadName.ToLower() == name.ToLower());
                    if (lead != null)
                    {
                        _logger.LogInformation("Found name-only match: Lead {LeadId}, {LeadName} - Priority 0", lead.Id, lead.LeadName);
                        potentialMatches.Add((lead, 0));
                    }
                }
            }
            
            // Return the lead with the highest priority
            if (potentialMatches.Any())
            {
                var bestMatch = potentialMatches.OrderByDescending(m => m.priority).First();
                _logger.LogInformation("Found existing lead by priority {Priority}: {LeadId}, {LeadName}", 
                    bestMatch.priority, bestMatch.lead.Id, bestMatch.lead.LeadName);
                return bestMatch.lead;
            }
            
            // No match found
            return null;
        }

        [HttpGet]
        [Route("Inventory1/IsProductPublished/{id}")]
        public async Task<IActionResult> IsProductPublished(int id)
        {
            try
            {
                var product = await _context.Products2.FindAsync(id);
                if (product == null)
                {
                    return NotFound();
                }
                
                return Json(product.IsPublished);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if product {ProductId} is published", id);
                return Json(false);
            }
        }
    }
}
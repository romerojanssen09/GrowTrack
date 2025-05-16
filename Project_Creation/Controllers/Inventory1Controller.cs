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

namespace Project_Creation.Controllers
{
    public class Inventory1Controller : Controller
    {
        private readonly AuthDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly ILogger<Inventory1Controller> _logger;

        public Inventory1Controller(
            AuthDbContext context,
            IWebHostEnvironment hostEnvironment,
            ILogger<Inventory1Controller> logger)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
            _logger = logger;
        }

        // GET: Inventory1
        public async Task<IActionResult> Index()
        {
            var products = await _context.Products2
                .Where(p => p.BOId == GetCurrentUserId())
                .ToListAsync();

            // Get all suppliers for the current user
            var suppliers = await _context.Supplier2
                .Where(s => s.BOId == GetCurrentUserId())
                .ToListAsync();

            var isAllowed = await _context.Users
                .AnyAsync(u => u.MarkerPlaceStatus == MarketplaceStatus.Approved);

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
            return userId;
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

                var product = new Product2
                {
                    BOId = GetCurrentUserId(),
                    ProductName = productDto.ProductName,
                    SupplierId = productDto.SupplierId?.ToString(),
                    Category = effectiveCategory,
                    PurchasePrice = productDto.PurchasePrice,
                    SKU = productDto.SKU,
                    QuantityInStock = productDto.QuantityInStock,
                    ReorderLevel = productDto.ReorderLevel,
                    Description = productDto.Description,
                    SellingPrice = productDto.SellingPrice,
                    CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"))
                };

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

                    // Save barcode if provided
                    if (!string.IsNullOrEmpty(productDto.Barcode))
                    {
                        product.Barcode = await SaveBarcodeImage(productDto.Barcode, productDto.SKU);
                        _context.Products2.Update(product);
                        await _context.SaveChangesAsync();
                    }

                    await transaction.CommitAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error during product creation");
                    ModelState.AddModelError("", "Error saving product. Please try again.");
                    await PopulateDropdowns(productDto);
                    return View(productDto);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error");
                ModelState.AddModelError("", "An unexpected error occurred. Please try again.");
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
        private string GetProductChanges(Product2 original, Product2 updated)
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
                // In the Delete method, before removing the product, add:
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
                _context.Products2.Remove(product);
                await _context.SaveChangesAsync();
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
                _logger.LogInformation("Loading products for QuickSale");
                var products = await _context.Products2
                    .Where(p => p.BOId == GetCurrentUserId() && p.QuantityInStock > 0)
                    .OrderBy(p => p.ProductName)
                    .ToListAsync();

                // Set products in ViewBag instead of model
                ViewBag.Products = products;

                _logger.LogInformation("Loaded {ProductCount} products for QuickSale", products.Count);
                return View(new QuickSaleViewModel
                {
                    AvailableProducts = products, // Keep this for backward compatibility
                    SaleDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"))
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
            _logger.LogInformation("QuickSale POST started. Customer: {CustomerName}, Items count: {ItemsCount}",
                model.CustomerName, model.Items?.Count ?? 0);

            if (model.Items == null || !model.Items.Any())
            {
                _logger.LogWarning("No items in the sale. Adding ModelState error.");
                ModelState.AddModelError("", "At least one item is required for the sale");
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState invalid: {Errors}",
                    string.Join(", ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)));

                // Repopulate products for the view
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
                // Get current user ID
                var userId = GetCurrentUserId();
                _logger.LogInformation("Processing sale for user {UserId} with {ItemCount} items",
                    userId, model.Items.Count);

                // Create a new sale record
                var sale = new Sale
                {
                    BOId = userId,
                    CustomerName = model.CustomerName ?? "Walk-in Customer",
                    SaleDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore")),
                    TotalAmount = 0, // Will calculate this based on items
                    SaleItems = new List<SaleItem>()
                };

                decimal totalAmount = 0;

                // Process each product in the sale
                foreach (var item in model.Items)
                {
                    _logger.LogDebug("Processing sale item: ProductId={ProductId}, Quantity={Quantity}",
                        item.ProductId, item.Quantity);

                    // Get product from database
                    var product = await _context.Products2.FindAsync(item.ProductId);
                    if (product == null)
                    {
                        _logger.LogWarning("Product with ID {ProductId} not found", item.ProductId);
                        ModelState.AddModelError("", $"Product with ID {item.ProductId} not found");

                        // Repopulate products for the view
                        var products = await _context.Products2
                            .Where(p => p.BOId == userId && p.QuantityInStock > 0)
                            .OrderBy(p => p.ProductName)
                            .ToListAsync();

                        ViewBag.Products = products;
                        model.AvailableProducts = products;
                        return View(model);
                    }

                    // Check inventory
                    if (product.QuantityInStock < item.Quantity)
                    {
                        _logger.LogWarning("Not enough inventory for product {ProductName}. Requested: {Requested}, Available: {Available}",
                            product.ProductName, item.Quantity, product.QuantityInStock);

                        ModelState.AddModelError("", $"Not enough inventory for {product.ProductName}. Available: {product.QuantityInStock}");

                        // Repopulate products for the view
                        var products = await _context.Products2
                            .Where(p => p.BOId == userId && p.QuantityInStock > 0)
                            .OrderBy(p => p.ProductName)
                            .ToListAsync();

                        ViewBag.Products = products;
                        model.AvailableProducts = products;
                        return View(model);
                    }

                    // Calculate item price
                    decimal itemTotal = product.SellingPrice * item.Quantity;
                    totalAmount += itemTotal;
                    _logger.LogDebug("Item total: {ItemTotal}, Running total: {TotalAmount}",
                        itemTotal, totalAmount);

                    // Create sale item
                    var saleItem = new SaleItem
                    {
                        ProductId = product.Id,
                        ProductName = product.ProductName,
                        Quantity = item.Quantity,
                        UnitPrice = product.SellingPrice,
                        TotalPrice = itemTotal,
                        Notes = $"QuickSale to {sale.CustomerName}"
                    };

                    // Add to sale
                    sale.SaleItems.Add(saleItem);

                    // Update inventory
                    product.QuantityInStock -= item.Quantity;
                    _logger.LogDebug("Updated inventory for {ProductName}. New quantity: {NewQuantity}",
                        product.ProductName, product.QuantityInStock);

                    // Log inventory movement
                    var log = new InventoryLog
                    {
                        BOId = userId,
                        ProductId = product.Id,
                        ProductName = product.ProductName,
                        QuantityBefore = product.QuantityInStock + item.Quantity,
                        QuantityAfter = product.QuantityInStock,
                        MovementType = InventoryMovementTypes.Sale,
                        ReferenceId = $"SALE-{sale.Id}",
                        Notes = $"QuickSale to {sale.CustomerName} (Item: {item.Quantity} x {product.ProductName})",
                        Timestamp = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore")),

                    };

                    _context.InventoryLogs.Add(log);
                }

                // Update the total amount
                sale.TotalAmount = totalAmount;

                // Save to database
                _logger.LogInformation("Saving sale with {ItemCount} items, total amount: {TotalAmount}",
                    sale.SaleItems.Count, totalAmount);

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                // Success message
                _logger.LogInformation("Sale completed successfully. Total: {TotalAmount}", totalAmount);
                TempData["SuccessMessage"] = $"Sale completed successfully. Total: {totalAmount:C}";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing quick sale. Exception details: {ExceptionMessage}", ex.Message);
                ModelState.AddModelError("", "An error occurred while processing the sale.");

                // Repopulate products for the view
                var products = await _context.Products2
                    .Where(p => p.BOId == GetCurrentUserId() && p.QuantityInStock > 0)
                    .OrderBy(p => p.ProductName)
                    .ToListAsync();

                ViewBag.Products = products;
                model.AvailableProducts = products;
                return View(model);
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

                var products = await _context.Products2
                    .Where(p => p.BOId == GetCurrentUserId())
                    .OrderBy(p => p.ProductName)
                    .ToListAsync();

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

            _logger.LogDebug("Starting database transaction for StockIn");
            using var transaction = await _context.Database.BeginTransactionAsync();

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

                _logger.LogDebug("Current stock for product {ProductId}: {CurrentStock}. Adding {Quantity}",
                    model.ProductId, product.QuantityInStock, model.Quantity);

                product.QuantityInStock += model.Quantity;
                _context.Products2.Update(product);

                // Create inventory log
                var userId = GetCurrentUserId();
                // In InventoryController's StockIn method, update the log:
                var log = new InventoryLog
                {
                    BOId = userId,
                    ProductId = product.Id,
                    ProductName = product.ProductName,
                    QuantityBefore = product.QuantityInStock - model.Quantity,
                    QuantityAfter = product.QuantityInStock,
                    MovementType = InventoryMovementTypes.StockIn,
                    ReferenceId = $"STOCKIN-{TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore")):yyyyMMddHHmmss}",
                    Notes = model.Notes ?? $"Stock In of {model.Quantity} items",
                    Timestamp = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"))
                };

                _context.InventoryLogs.Add(log);
                _logger.LogDebug("Created inventory log entry for StockIn");

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully processed StockIn for product {ProductId}. New quantity: {NewQuantity}",
                    model.ProductId, product.QuantityInStock);

                return RedirectToAction("Index", "Inventory1");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
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
        public IActionResult PublishToMarketplace(int id)
        {
            var product = _context.Products2.Find(id);
            if (product == null)
            {
                TempData["ErrorMessage"] = "BUNAKS";
                return View();
            }
            var viewModel = new PublishToMarketplaceViewModel
            {
                ProductId = id,
                ProductName = product.ProductName,
                Description = product.Description,
                Price = product.SellingPrice,
                IsPublished = false,
                MarketplaceDescription = product.Description,
                MarketplacePrice = product.SellingPrice,
                DisplayFeatured = product.DisplayFeatured
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
                if(model.ImageFiles == null)
                {
                    return View(model);
                }

                _logger.LogDebug("Looking up product {ProductId}", model.ProductId);
                var product = await _context.Products2.FindAsync(model.ProductId);
                if (product == null)
                {
                    _logger.LogError("Product {ProductId} not found in database", model.ProductId);
                    ModelState.AddModelError("", "Product not found");
                    return View(model);
                }

                _logger.LogInformation("Updating product publishing status - Published: {IsPublished}, Featured: {IsFeatured}",
                    model.IsPublished, model.DisplayFeatured);

                product.IsPublished = model.IsPublished;
                product.IsAlreadyPublished = true;
                product.DisplayFeatured = model.DisplayFeatured;
                _context.Products2.Update(product);

                // Handle image uploads
                if (model.ImageFiles != null && model.ImageFiles.Count > 0)
                {
                    _logger.LogDebug("Processing {ImageCount} uploaded images", model.ImageFiles.Count);
                    bool isFirstImage = !await _context.ProductImages.AnyAsync(img => img.ProductId == model.ProductId);

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
                                    IsMainImage = isFirstImage,
                                    Title = model.ProductName,
                                    DisplayOrder = 1,
                                    CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"))
                                };

                                _context.ProductImages.Add(image);
                                _logger.LogInformation("Added new product image (Main: {IsMain})", isFirstImage);

                                isFirstImage = false;
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
    }
}
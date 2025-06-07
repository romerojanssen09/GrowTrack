using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Creation.Data;
using Project_Creation.DTO;
using Project_Creation.Models.Entities;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Project_Creation.Models.ViewModels;
using System.Threading.Tasks;

namespace Project_Creation.Controllers
{
    public class BOBusinessProfileController : Controller
    {
        private readonly AuthDbContext _context;
        private readonly ILogger<AdminController> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public BOBusinessProfileController(AuthDbContext context, ILogger<AdminController> logger, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
        }

        public IActionResult Index()
        {
            return RedirectToAction("BusinessProfile");
        }

        [HttpGet("BusinessProfile/{id}/{category?}", Name = "ViewBusinessProfile")]
        public async Task<IActionResult> BusinessProfile(int id, string category = null)
        {
            var userData = await _context.Users
                .Where(user => user.Id == id)
                .FirstOrDefaultAsync();

            if (userData == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId != null)
            {
                var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == Convert.ToInt32(userId));
                if (currentUser != null && currentUser.MarkerPlaceStatus.ToString() == "Pending")
                {
                    TempData["WarningMessage"] = "Please fill the form to send a request to the admin!";
                    return RedirectToAction("Index", "Profile", new { userId = userId });
                }
                else if (currentUser != null && currentUser.MarkerPlaceStatus.ToString() == "Rejected")
                {
                    TempData["WarningMessage"] = "Please wait for the admin to approve your Marketplace access.";
                    return RedirectToAction("Dashboard", "Pages");
                }
                else if (currentUser != null && currentUser.MarkerPlaceStatus.ToString() == "AwaitingApproval")
                {
                    TempData["WarningMessage"] = "Please wait for the admin to approve your Marketplace access.";
                    return RedirectToAction("Dashboard", "Pages");
                }
            }

            // Check if business profile exists
            var existingProfile = await _context.BOBusinessProfiles
                    .Where(bp => bp.UserId == id)
                    .FirstOrDefaultAsync();

            // Get products for this business owner
            var productsQuery = _context.Products2
                .Include(p => p.Images)
                .Where(p => p.BOId == id && p.IsPublished);

            //get all the product first
            var allProducts = await _context.Products2
                    .Include(p => p.Images)
                    .Where(p => p.BOId == id && p.IsPublished)
                    .Select(p => new ProductDto
                    {
                        Id = p.Id,
                        ProductName = p.ProductName,
                        Images = p.Images.ToList(),
                        QuantityInStock = p.QuantityInStock,
                        SellingPrice = p.SellingPrice,
                        Category2 = p.Category
                    })
                    .ToListAsync();

            // Filter by category if specified
            if (!string.IsNullOrEmpty(category))
            {
                productsQuery = productsQuery.Where(p => p.Category == category);
            }

            var products = await productsQuery.ToListAsync();

            // Check if this is the current user's profile
            int currentUserId = GetCurrentUserId();
            bool isCurrentUser = currentUserId == id;

            var groupedProducts = allProducts
                .GroupBy(p => string.IsNullOrWhiteSpace(p.Category2) ? "uncategorized" : p.Category2.ToLower())
                .Select(g =>
                {
                    // Use the first non-empty category name with original casing if available
                    var displayName = g.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.Category2))?.Category2 ?? "Uncategorized";
                    return new ProductGroupViewModel
                    {
                        CategoryName = displayName,
                        Products = g.ToList()
                    };
                })
                .ToList();

            //var hotSalesProducts = await GetTopHotSellingProducts(id);

            var featuredProductsQuery = _context.Products2
                .Include(p => p.Images)
                .Where(p => p.BOId == id && p.IsPublished && p.DisplayFeatured);
            var featuredProductsList = await featuredProductsQuery
                .Select(p => new ProductDto
                {
                    Id = p.Id,
                    ProductName = p.ProductName,
                    Images = p.Images.ToList(),
                    QuantityInStock = p.QuantityInStock,
                    SellingPrice = p.SellingPrice
                })
                .ToListAsync();

            var links = await _context.UserSocialMediaLinks
                .FirstOrDefaultAsync(u => u.UserId == userData.Id);

            // Create view model regardless of whether profile exists
            var viewModel = new BusinessProfileViewModel
            {
                UserId = id,
                ShopName = existingProfile?.ShopName ?? userData.BusinessName ?? string.Empty,
                ShopDescription = existingProfile?.Description ?? string.Empty,
                BusinessBackgroundImgPath = existingProfile?.BusinessBackgroundImgPath ?? DefaultBackground(),
                LogoPath = userData.LogoPath ?? DefaultLogo(),
                BusinessAddress = userData.BusinessAddress ?? string.Empty,
                PhoneNumber = userData.PhoneNumber ?? "Not provided",
                BusinessName = userData.BusinessName ?? string.Empty,
                BusinessOwnerName = $"{userData.FirstName} {userData.LastName}",
                Email = userData.Email,
                IsCurrentUser = isCurrentUser,
                Categories = await _context.Categories
                    .Where(c => c.BOId == id)
                    .ToListAsync(),
                Recommended = products.Select(p => new ProductDto
                {
                    Id = p.Id,
                    ProductName = p.ProductName,
                    Images = p.Images?.ToList(),
                    QuantityInStock = p.QuantityInStock,
                    SellingPrice = p.SellingPrice
                }).ToList(),
                Products = await _context.Products2
                    .Include(p => p.Images)
                    .Where(p => p.BOId == id && p.IsPublished)
                    .Select(p => new ProductDto
                    {
                        Id = p.Id,
                        ProductName = p.ProductName,
                        Images = p.Images.ToList(),
                        QuantityInStock = p.QuantityInStock,
                        SellingPrice = p.SellingPrice,
                        Category2 = p.Category
                    })
                    .ToListAsync(),
                GroupedProductsByCategory = groupedProducts,
                FeaturedProductViewModel = featuredProductsList,
                UserLinks = links
            };

            // Pass any messages from TempData to ViewBag
            if (TempData["SuccessMessage"] != null)
            {
                ViewBag.SuccessMessage = TempData["SuccessMessage"];
            }
            if (TempData["ErrorMessage"] != null)
            {
                ViewBag.ErrorMessage = TempData["ErrorMessage"];
            }
            if (TempData["InfoMessage"] != null)
            {
                ViewBag.InfoMessage = TempData["InfoMessage"];
            }

            ViewBag.SelectedCategory = category;
            return View(viewModel);
        }

        private async Task<List<HotSellingProductViewModel>> GetTopHotSellingProducts(int boId, int top = 5)
        {
            var topSelling = await _context.InventoryLogs
                .Where(log => log.BOId == boId && log.MovementType == "Sale")
                .GroupBy(log => log.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    TotalSold = g.Sum(x => x.QuantityBefore - x.QuantityAfter)
                })
                .OrderByDescending(x => x.TotalSold)
                .Take(top)
                .ToListAsync();

            // Join with product details
            var productIds = topSelling.Select(x => x.ProductId).ToList();
            var products = await _context.Products2
                .Where(p => productIds.Contains(p.Id))
                .Where(p => p.IsPublished == true)
                .ToListAsync();

            // Merge data into final view model
            var result = (from s in topSelling
                          join p in products on s.ProductId equals p.Id
                          select new HotSellingProductViewModel
                          {
                              ProductId = p.Id,
                              ProductName = p.ProductName,
                              TotalSold = s.TotalSold,
                              Images = p.Images?.ToList(),
                              QuantityInStock = p.QuantityInStock,
                              SellingPrice = p.SellingPrice
                          }).ToList();

            return result;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(BOBusinessProfileDto model, IFormFile logo = null, IFormFile backgroundImage = null)
        {
            _logger.LogInformation("UpdateProfile action started");
            if (!ModelState.IsValid)
            {
                // Log specific validation errors
                var errors = ModelState
                    .Where(x => x.Value.Errors.Count > 0)
                    .Select(x => new {
                        Key = x.Key,
                        Errors = x.Value.Errors.Select(e => e.ErrorMessage)
                    })
                    .ToList();

                _logger.LogWarning("ModelState errors: {@Errors}", errors);

                return View("BusinessProfile", model);
            }

            foreach (var key in ModelState.Keys)
            {
                var state = ModelState[key];
                foreach (var error in state.Errors)
                {
                    _logger.LogWarning($"Field: {key}, Error: {error.ErrorMessage}");
                }
            }

            _logger.LogInformation("Updating profile for user {UserId}", model.UserId);

            // Check if the current user is the owner of the profile being edited
            int currentUserId = GetCurrentUserId();
            if (currentUserId != model.UserId && !User.IsInRole("BusinessOwner"))
            {
                _logger.LogWarning("User {CurrentUserId} attempted to edit profile for {TargetUserId} without permission",
                    currentUserId, model.UserId);
                TempData["ErrorMessage"] = "You don't have permission to edit this profile.";
                return RedirectToAction(nameof(BusinessProfile));
            }

            var user = await _context.Users.FindAsync(model.UserId);
            if (user == null)
            {
                return NotFound();
            }

            // Track if any changes were made
            bool changesDetected = false;

            // Handle logo upload only if a new logo was provided
            if (logo != null && logo.Length > 0)
            {
                // Create directory if it doesn't exist
                var logoDirectory = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", model.UserId.ToString(), "logos");
                if (!Directory.Exists(logoDirectory))
                {
                    Directory.CreateDirectory(logoDirectory);
                }

                var logoFileName = Guid.NewGuid().ToString() + Path.GetExtension(logo.FileName);
                var logoPath = Path.Combine(logoDirectory, logoFileName);

                using (var stream = new FileStream(logoPath, FileMode.Create))
                {
                    await logo.CopyToAsync(stream);
                }

                user.LogoPath = $"/uploads/{model.UserId}/logos/{logoFileName}";
                changesDetected = true;
            }

            // Update user information only if changed
            if (!string.IsNullOrEmpty(model.BusinessName) && user.BusinessName != model.BusinessName)
            {
                user.BusinessName = model.BusinessName;
                changesDetected = true;
            }

            if (!string.IsNullOrEmpty(model.BusinessAddress) && user.BusinessAddress != model.BusinessAddress)
            {
                user.BusinessAddress = model.BusinessAddress;
                changesDetected = true;
            }

            // Check if business profile exists
            BOBusinessProfile existingProfile = null;
            try
            {
                existingProfile = await _context.BOBusinessProfiles
                    .Where(bp => bp.UserId == model.UserId)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                // Log the error
                _logger.LogError(ex, "Error accessing BOBusinessProfiles table. It may not exist yet.");
                // We'll handle this by creating a new profile below
            }

            if (existingProfile == null)
            {
                // Create new profile
                var newProfile = new BOBusinessProfile
                {
                    UserId = model.UserId,
                    ShopName = model.ShopName ?? string.Empty,
                    Description = model.Description ?? string.Empty,
                    BusinessBackgroundImgPath = DefaultBackground()
                };
                _context.BOBusinessProfiles.Add(newProfile);
                existingProfile = newProfile;
            }
            else
            {
                // Ensure the existing profile is being tracked
                if (_context.Entry(existingProfile).State == EntityState.Detached)
                {
                    _context.BOBusinessProfiles.Attach(existingProfile);
                    _context.Entry(existingProfile).State = EntityState.Modified;
                }

                // Update properties and track changes
                if (existingProfile.ShopName != model.ShopName)
                {
                    existingProfile.ShopName = model.ShopName;
                    changesDetected = true;
                }
                if (existingProfile.Description != (model.Description ?? string.Empty))
                {
                    existingProfile.Description = model.Description ?? string.Empty;
                    changesDetected = true;
                }
            }

            // Handle background image upload only if a new image was provided
            if (backgroundImage != null && backgroundImage.Length > 0)
            {
                // Create directory if it doesn't exist
                var backgroundDirectory = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", model.UserId.ToString(), "backgrounds");
                if (!Directory.Exists(backgroundDirectory))
                {
                    Directory.CreateDirectory(backgroundDirectory);
                }

                string uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(backgroundImage.FileName);
                string filePath = Path.Combine(backgroundDirectory, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await backgroundImage.CopyToAsync(fileStream);
                }

                string backgroundPath = $"/uploads/{model.UserId}/backgrounds/{uniqueFileName}";
                existingProfile.BusinessBackgroundImgPath = backgroundPath;
                changesDetected = true;
            }

            // Only save changes if something was actually modified
            if (changesDetected)
            {
                try
                {
                    var changes = await _context.SaveChangesAsync();
                    _logger.LogInformation("Saved {Count} changes to database", changes);

                    if (changes == 0)
                    {
                        _logger.LogWarning("SaveChanges reported 0 changes despite changesDetected being true");
                    }

                    TempData["SuccessMessage"] = "Business profile updated successfully!";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving business profile changes");
                    TempData["ErrorMessage"] = "An error occurred while saving your changes.";
                }
            }

            return RedirectToAction("BusinessProfile", new { id = model.UserId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile2(BOBusinessProfileDto model, IFormFile logo, IFormFile backgroundImage)
        {
            _logger.LogInformation("UpdateProfile action started");

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState is invalid: {@ModelState}", ModelState);
                return View("BusinessProfile", model);
            }

            _logger.LogInformation("Updating profile for user {UserId}", model.UserId);

            // Check if the current user is the owner of the profile being edited
            int currentUserId = GetCurrentUserId();
            if (currentUserId != model.UserId && !User.IsInRole("BusinessOwner"))
            {
                _logger.LogWarning("User {CurrentUserId} attempted to edit profile for {TargetUserId} without permission",
                    currentUserId, model.UserId);
                TempData["ErrorMessage"] = "You don't have permission to edit this profile.";
                return RedirectToAction(nameof(BusinessProfile));
            }

            var user = await _context.Users.FindAsync(model.UserId);
            if (user == null)
            {
                return NotFound();
            }

            // Track if any changes were made
            bool changesDetected = false;

            // Handle logo upload only if a new logo was provided
            if (logo != null && logo.Length > 0)
            {
                // Create directory if it doesn't exist
                var logoDirectory = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", model.UserId.ToString(), "logos");
                if (!Directory.Exists(logoDirectory))
                {
                    Directory.CreateDirectory(logoDirectory);
                }

                var logoFileName = Guid.NewGuid().ToString() + Path.GetExtension(logo.FileName);
                var logoPath = Path.Combine(logoDirectory, logoFileName);

                using (var stream = new FileStream(logoPath, FileMode.Create))
                {
                    await logo.CopyToAsync(stream);
                }

                user.LogoPath = $"/uploads/{model.UserId}/logos/{logoFileName}";
                changesDetected = true;
            }

            // Update user information only if changed
            if (user.BusinessName != model.BusinessName)
            {
                user.BusinessName = model.BusinessName;
                changesDetected = true;
            }

            if (user.BusinessAddress != model.BusinessAddress)
            {
                user.BusinessAddress = model.BusinessAddress;
                changesDetected = true;
            }

            // Check if business profile exists
            BOBusinessProfile existingProfile = null;
            try
            {
                existingProfile = await _context.BOBusinessProfiles
                    .Where(bp => bp.UserId == model.UserId)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                // Log the error
                _logger.LogError(ex, "Error accessing BOBusinessProfiles table. It may not exist yet.");
                // We'll handle this by creating a new profile below
            }

            if (existingProfile == null)
            {
                // Create new profile
                var newProfile = new BOBusinessProfile
                {
                    UserId = model.UserId,
                    ShopName = model.ShopName ?? string.Empty,
                    Description = model.Description ?? string.Empty,
                    BusinessBackgroundImgPath = DefaultBackground()
                };
                _context.BOBusinessProfiles.Add(newProfile);
                existingProfile = newProfile;
            }
            else
            {
                // Ensure the existing profile is being tracked
                if (_context.Entry(existingProfile).State == EntityState.Detached)
                {
                    _context.BOBusinessProfiles.Attach(existingProfile);
                    _context.Entry(existingProfile).State = EntityState.Modified;
                }

                // Update properties and track changes
                if (existingProfile.ShopName != model.ShopName)
                {
                    existingProfile.ShopName = model.ShopName;
                    changesDetected = true;
                }
                if (existingProfile.Description != (model.Description ?? string.Empty))
                {
                    existingProfile.Description = model.Description ?? string.Empty;
                    changesDetected = true;
                }
            }

            // Handle background image upload only if a new image was provided
            if (backgroundImage != null && backgroundImage.Length > 0)
            {
                // Create directory if it doesn't exist
                var backgroundDirectory = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", model.UserId.ToString(), "backgrounds");
                if (!Directory.Exists(backgroundDirectory))
                {
                    Directory.CreateDirectory(backgroundDirectory);
                }

                string uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(backgroundImage.FileName);
                string filePath = Path.Combine(backgroundDirectory, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await backgroundImage.CopyToAsync(fileStream);
                }

                string backgroundPath = $"/uploads/{model.UserId}/backgrounds/{uniqueFileName}";
                existingProfile.BusinessBackgroundImgPath = backgroundPath;
                changesDetected = true;
            }

            // Only save changes if something was actually modified
            if (changesDetected)
            {
                try
                {
                    var changes = await _context.SaveChangesAsync();
                    _logger.LogInformation("Saved {Count} changes to database", changes);

                    if (changes == 0)
                    {
                        _logger.LogWarning("SaveChanges reported 0 changes despite changesDetected being true");
                    }

                    TempData["SuccessMessage"] = "Business profile updated successfully!";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving business profile changes");
                    TempData["ErrorMessage"] = "An error occurred while saving your changes.";
                }
            }

            return RedirectToAction("BusinessProfile", new { id = model.UserId });
        }

        //[HttpGet]
        //public async Task<IActionResult> GetShopFilters()
        //{
        //    try
        //    {
        //        // Get actual categories from database
        //        var categories = await _context.Categories
        //            .OrderBy(c => c.CategoryName)
        //            .Select(c => new { Id = c.Id, Name = c.CategoryName })
        //            .ToListAsync();

        //        // If no categories exist yet, return sample ones
        //        if (!categories.Any())
        //        {
        //            var sampleCategories = new[]
        //            {
        //                new { Id = 1, Name = "Clothing" },
        //                new { Id = 2, Name = "Electronics" },
        //                new { Id = 3, Name = "Home & Living" },
        //                new { Id = 4, Name = "Beauty" },
        //                new { Id = 5, Name = "Sports" },
        //                new { Id = 6, Name = "Toys" }
        //            };
        //            return Json(sampleCategories);
        //        }

        //        return Json(categories);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error getting shop filters");
        //        return Json(new object[0]);
        //    }
        //}

        [HttpGet]
        public async Task<IActionResult> GetBusinessProducts(int businessId, int? categoryId)
        {
            var query = _context.Products2
                .Where(p => p.BOId == businessId && p.IsPublished);

            if (categoryId.HasValue && categoryId.Value > 0)
                query = query.Where(p => p.Id == categoryId.Value);

            var products = await query
                .Select(p => new
                {
                    id = p.Id,
                    name = p.ProductName,
                    description = p.Description,
                    p.Images,
                    categoryName = p.Category,
                    price = p.SellingPrice
                })
                .ToListAsync();

            return Json(products);
        }

        private string DefaultLogo()
        {
            try
            {
                int userId = GetCurrentUserId();
                string firstName = GetUserDataById(userId, "FirstName") ?? string.Empty;
                string lastName = GetUserDataById(userId, "LastName") ?? string.Empty;

                string fullName = $"{firstName} {lastName}".Trim();
                if (string.IsNullOrEmpty(fullName))
                {
                    fullName = "User"; // Fallback if no name is available
                }

                return $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(fullName)}&background=fff&color=6366f1&rounded=true&size=256";
            }
            catch
            {
                // Fallback to a generic avatar if something goes wrong
                return "https://ui-avatars.com/api/?name=User&background=fff&color=6366f1&rounded=true&size=256";
            }
        }

        private string DefaultBackground()
        {
            try
            {
                int userId = GetCurrentUserId();
                string businessName = GetUserDataById(userId, "BusinessName") ?? string.Empty;

                if (string.IsNullOrEmpty(businessName))
                {
                    businessName = "Business"; // Fallback if no business name is available
                }

                return $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(businessName)}&background=fff&color=6366f1&rounded=true&size=256";
            }
            catch
            {
                // Fallback to a generic background if something goes wrong
                return "https://ui-avatars.com/api/?name=Business&background=fff&color=6366f1&rounded=true&size=256";
            }
        }

        // Helper method to get the main product image
        private string GetProductMainImage(int productId)
        {
            try
            {
                var images = _context.ProductImages
                    .Where(img => img.ProductId == productId)
                    .ToList();
                    
                if (!images.Any())
                    return "/default/product-placeholder.jpg";
                    
                // Try to get main image first, then any image
                var mainImage = images.FirstOrDefault(img => img.IsMainImage);
                return mainImage?.ImagePath ?? images.First().ImagePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting main image for product {productId}");
                return "/default/product-placeholder.jpg";
            }
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

        private string GetUserDataById(int userId, string columnName)
        {
            return _context.Users
                .Where(user => user.Id == userId)
                .Select(user => EF.Property<string>(user, columnName))
                .FirstOrDefault()
                ?? "Unknown Data";
        }

        //[HttpGet]
        //public async Task<IActionResult> GetProductById(int productId)
        //{
        //    try
        //    {
        //        var product = await _context.Products
        //            .Where(p => p.Id == productId)
        //            .Select(p => new
        //            {
        //                Id = p.Id,
        //                Name = p.Name,
        //                Description = p.MarketplaceDescription ?? p.Description,
        //                Price = p.MarketplacePrice ?? p.Price,
        //                QuantityInStock = p.QuantityInStock,
        //                CategoryId = p.CategoryId,
        //                CategoryName = p.Category.Name,
        //                IsFeatured = p.DisplayFeatured
        //            })
        //            .FirstOrDefaultAsync();

        //        if (product == null)
        //        {
        //            return Json(new { success = false, message = "Product not found" });
        //        }

        //        return Json(new { success = true, product = product });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, $"Error getting product {productId}");
        //        return Json(new { success = false, message = "Error retrieving product" });
        //    }
        //}

        [HttpGet]
        public async Task<IActionResult> GetProductImages(int productId)
        {
            try
            {
                var images = await _context.ProductImages
                    .Where(img => img.ProductId == productId)
                    .OrderBy(img => img.DisplayOrder)
                    .Select(img => new
                    {
                        Id = img.Id,
                        Path = img.ImagePath,
                        IsMain = img.IsMainImage,
                        Title = img.Title,
                        AltText = img.AltText,
                        DisplayOrder = img.DisplayOrder
                    })
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    images = images,
                    mainImageUrl = GetProductMainImage(productId)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting images for product {productId}");
                return Json(new
                {
                    success = false,
                    message = "Error retrieving product images"
                });
            }
        }

        [HttpPost]
        public IActionResult RecordProductClick(int productId)
        {
            try
            {
                int userId = GetCurrentUserId();
                if (userId == 0)
                {
                    // For non-logged in users, we'll just return success
                    // The client-side code will still store the click in localStorage
                    return Json(new { success = true });
                }

                // For now, we'll just log it
                _logger.LogInformation($"User {userId} clicked on product {productId}");

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error recording product click for product {productId}");
                return Json(new { success = false, message = "Error recording click" });
            }
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Creation.Data;
using Project_Creation.Models.Entities;
using Project_Creation.Models.ViewModels;
using System.Security.Claims; // for ClaimTypes.NameIdentifier
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project_Creation.Controllers
{
    public class MarketplaceController : Controller
    {
        private readonly AuthDbContext _context;
        private const int PageSize = 12;

        public MarketplaceController(AuthDbContext context)
        {
            _context = context;
        }

        // GET: Marketplace
        public async Task<IActionResult> Index(string category, string search, int page = 1)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId != null)
            {
                var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == Convert.ToInt32(userId));
                if (currentUser != null && currentUser.MarkerPlaceStatus.ToString() == "NotApplied")
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

            var categories = await _context.Categories
               .Select(c => c.CategoryName.ToLower())
               .Distinct()
               .Select(name => new { CategoryName = char.ToUpper(name[0]) + name.Substring(1) })
               .ToListAsync();

            ViewBag.Categories = categories;
            ViewBag.CurrentCategory = category;
            ViewBag.CurrentSearch = search;

            // Base query - only include actual navigation properties
            var query = _context.Products2
                .Include(p => p.Images)
                .Where(p => p.IsPublished && p.QuantityInStock > 0);

            // Base query
            //var query = _context.Products2
            //    .Include(p => p.Category)
            //    .Include(p => p.BOId)
            //    .Include(p => p.Images)
            //    .Where(p => p.IsPublished && p.QuantityInStock > 0);

            // Apply filters
            if (!string.IsNullOrEmpty(category))
            {
                // Compare with the correct property - assuming Category is a string property
                query = query.Where(p => p.Category == category);
            }

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p =>
                    p.ProductName.Contains(search) ||
                    (p.Description != null && p.Description.Contains(search)));
            }

            // Pagination
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalItems / PageSize);
            page = Math.Clamp(page, 1, totalPages > 0 ? totalPages : 1);

            // Get featured products
            var featuredProducts = await query
                .Where(p => p.DisplayFeatured)
                .OrderByDescending(p => p.Id)
                .Take(4)
                .ToListAsync();

            // Get paginated results
            var products = await query
                .OrderByDescending(p => p.Id)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            var viewModel = new MarketplaceViewModel
            {
                Products = MapToProductViewModels(products),
                FeaturedProducts = MapToProductViewModels(featuredProducts),
                CurrentPage = page,
                TotalPages = totalPages,
                CategoryFilter = category,
                SearchQuery = search
            };

            return View(viewModel);
        }

        // GET: Marketplace/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products2
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id && p.IsPublished);

            if (product == null)
            {
                return NotFound();
            }

            // Get related products - prioritize same category
            var relatedProducts = await _context.Products2
                .Include(p => p.Images)
                .Where(p => p.Id != product.Id &&
                           p.IsPublished &&
                           p.QuantityInStock > 0)
                .OrderByDescending(p => p.Category == product.Category) // Prioritize same category
                .ThenByDescending(p => p.UpdatedAt)                    // Then by newest
                .Take(4)
                .ToListAsync();

            var viewModel = new ProductDetailsViewModel
            {
                Id = product.Id,
                Name = product.ProductName,
                Description = product.Description ?? product.Description ?? string.Empty,
                Price = product.SellingPrice,
                CategoryName = product.Category,
                BusinessName = GetUserDataById(Convert.ToInt32(product.BOId), "BusinessName"),
                BusinessOwnerId = product.BOId, 
                InStock = product.QuantityInStock > 0,
                QuantityInStock = product.QuantityInStock,
                Images = product.Images?
                    .OrderBy(i => !i.IsMainImage)
                    .ThenBy(i => i.DisplayOrder)
                    .Select(i => i.ImagePath)
                    .ToList() ?? new List<string>(),
                RelatedProducts = MapToProductViewModels(relatedProducts)
            };
            return View(viewModel);
        }

        // GET: Marketplace/BusinessProducts/5
        public async Task<IActionResult> BusinessProducts(int id)
        {
            var business = await _context.Users.FindAsync(id);
            if (business == null)
            {
                return NotFound();
            }

            var products = await _context.Products2
                .Include(p => p.Category)
                .Include(p => p.Images)
                .Where(p => p.BOId == id && p.IsPublished && p.QuantityInStock > 0)
                .OrderByDescending(p => p.UpdatedAt)
                .ToListAsync();

            ViewBag.BusinessName = business.BusinessName;

            return View(MapToProductViewModels(products));
        }

        // GET: Marketplace/SearchBusinesses
        public async Task<IActionResult> SearchBusinesses(string search)
        {
            var businesses = await _context.Users
                .Where(u => u.BusinessName.Contains(search))
                .Select(u => new BusinessSearchResult
                {
                    Id = u.Id,
                    BusinessName = u.BusinessName,
                    ProductCount = _context.Products2.Count(p => p.BOId == u.Id && p.IsPublished)
                })
                .OrderByDescending(b => b.ProductCount)
                .ToListAsync();

            return View(businesses);
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                throw new InvalidOperationException("User is not authenticated");
            }
            return userId;
        }

        #region Helper Methods

        private List<MarketplaceProductViewModel> MapToProductViewModels(List<Product> products)
        {
            // First get all distinct business owner IDs from the products
            var businessOwnerIds = products
                .Where(p => p.BOId == GetCurrentUserId())
                .Select(p => p.BOId)
                .Distinct()
                .ToList();

            // Query all needed business names in one database call
            var businessNames = _context.Users
                .Where(u => businessOwnerIds.Contains(u.Id))
                .ToDictionary(u => u.Id, u => u.BusinessName);

            return products.Select(p => new MarketplaceProductViewModel
            {
                Id = p.Id,
                Name = p.ProductName,
                Description = p.Description ?? p.Description ?? string.Empty,
                Price = p.SellingPrice,
                CategoryName = p.Category,
                BusinessName = GetUserDataById(Convert.ToInt32(p.BOId), "BusinessName"),
                BusinessOwnerId = p.BOId,
                MainImageUrl = GetMainImageUrl(p.Images),
                InStock = p.QuantityInStock > 0
            }).ToList();
        }

        private string GetMainImageUrl(ICollection<ProductImage> images)
        {
            return images?.FirstOrDefault(i => i.IsMainImage)?.ImagePath
                ?? images?.FirstOrDefault()?.ImagePath
                ?? "/images/no-image.png";
        }

        #endregion

        // get userDataById
        private string GetUserDataById(int userId, string columnName)
        {
            return _context.Users
                .Where(user => user.Id == userId)
                .Select(user => EF.Property<string>(user, columnName))
                .FirstOrDefault()
                ?? "Unknown Data";
        }
    }

    public class BusinessSearchResult
    {
        public int Id { get; set; }
        public string BusinessName { get; set; }
        public int ProductCount { get; set; }
    }
}
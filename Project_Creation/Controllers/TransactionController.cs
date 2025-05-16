// TransactionController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Creation.Data;
using Project_Creation.DTO;
using Project_Creation.Models.Entities;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;

namespace Project_Creation.Controllers
{
    public class TransactionController : Controller
    {
        private readonly AuthDbContext _context;

        public TransactionController(AuthDbContext context)
        {
            _context = context;
        }

        // GET: Transaction
        public async Task<IActionResult> Index()
        {
            var transactions = await _context.InventoryTransactions
                .Include(t => t.Product)
                .Where(t => t.BOId == GetCurrentUserId())
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            return View(transactions);
        }

        // GET: Transaction/Create
        public async Task<IActionResult> Create()
        {
            var products = await _context.Products2
                .Where(p => p.BOId == GetCurrentUserId())
                .ToListAsync();

            ViewBag.Products = products;
            return View(new TransactionDto());
        }

        // POST: Transaction/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TransactionDto transactionDto)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Products = await _context.Products2
                    .Where(p => p.BOId == GetCurrentUserId())
                    .ToListAsync();
                return View(transactionDto);
            }

            var product = await _context.Products2.FindAsync(transactionDto.ProductId);
            if (product == null)
            {
                ModelState.AddModelError("ProductId", "Product not found");
                ViewBag.Products = await _context.Products2
                    .Where(p => p.BOId == GetCurrentUserId())
                    .ToListAsync();
                return View(transactionDto);
            }

            var transaction = new Transaction
            {
                BOId = GetCurrentUserId(),
                ProductId = transactionDto.ProductId,
                Type = transactionDto.Type,
                Quantity = transactionDto.Quantity,
                Notes = transactionDto.Notes,
                TransactionDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore")),
                ReferenceNumber = GenerateReferenceNumber()
            };

            // Handle different transaction types
            switch (transactionDto.Type)
            {
                case TransactionType.Adjustment:
                    transaction.PreviousQuantity = product.QuantityInStock;
                    transaction.NewQuantity = transactionDto.NewQuantity;

                    if (!transactionDto.NewQuantity.HasValue)
                    {
                        ModelState.AddModelError("NewQuantity", "New quantity is required for adjustments");
                        ViewBag.Products = await _context.Products2
                            .Where(p => p.BOId == GetCurrentUserId())
                            .ToListAsync();
                        return View(transactionDto);
                    }

                    product.QuantityInStock = transactionDto.NewQuantity.Value;
                    break;

                case TransactionType.Purchase:
                    product.QuantityInStock += transactionDto.Quantity;
                    break;

                case TransactionType.Sale:
                    if (product.QuantityInStock < transactionDto.Quantity)
                    {
                        ModelState.AddModelError("Quantity", "Insufficient stock");
                        ViewBag.Products = await _context.Products2
                            .Where(p => p.BOId == GetCurrentUserId())
                            .ToListAsync();
                        return View(transactionDto);
                    }
                    product.QuantityInStock -= transactionDto.Quantity;
                    break;

                case TransactionType.Return:
                    transaction.CustomerName = transactionDto.CustomerName;
                    transaction.Reason = transactionDto.Reason;
                    product.QuantityInStock += transactionDto.Quantity;
                    break;

                case TransactionType.Damage:
                    if (product.QuantityInStock < transactionDto.Quantity)
                    {
                        ModelState.AddModelError("Quantity", "Insufficient stock");
                        ViewBag.Products = await _context.Products2
                            .Where(p => p.BOId == GetCurrentUserId())
                            .ToListAsync();
                        return View(transactionDto);
                    }
                    product.QuantityInStock -= transactionDto.Quantity;
                    break;

                case TransactionType.Transfer:
                    // Implementation for transfer between locations would go here
                    break;
            }

            using (var dbContextTransaction = _context.Database.BeginTransaction())
            {
                try
                {
                    _context.Add(transaction);
                    _context.Update(product);
                    await _context.SaveChangesAsync();
                    await dbContextTransaction.CommitAsync();

                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    await dbContextTransaction.RollbackAsync();
                    ModelState.AddModelError("", $"Error saving transaction: {ex.Message}");
                    ViewBag.Products = await _context.Products2
                        .Where(p => p.BOId == GetCurrentUserId())
                        .ToListAsync();
                    return View(transactionDto);
                }
            }
        }

        private string GenerateReferenceNumber()
        {
            return $"TRX-{TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore")):yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }
            throw new InvalidOperationException("User ID not found");
        }
    }
}
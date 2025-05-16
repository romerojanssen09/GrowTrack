using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Creation.Data;
using Project_Creation.Models.Entities;
using System.Linq;
using System.Threading.Tasks;

namespace Project_Creation.Controllers
{
    public class SupplierController : Controller
    {
        private readonly AuthDbContext _context;

        public SupplierController(AuthDbContext context)
        {
            _context = context;
        }

        // GET: Supplier
        public async Task<IActionResult> Index()
        {
            return View(await _context.Supplier2.ToListAsync());
        }

        // GET: Supplier/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Supplier/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Address,PhoneNumber,Email,ContactPerson")] Supplier2 supplier)
        {
            if (ModelState.IsValid)
            {
                _context.Add(supplier);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(supplier);
        }

        // GET: Supplier/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var supplier = await _context.Supplier2.FindAsync(id);
            if (supplier == null)
            {
                return NotFound();
            }
            return View(supplier);
        }

        // POST: Supplier/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Address,PhoneNumber,Email,ContactPerson")] Supplier2 supplier)
        {
            if (id != supplier.SupplierID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(supplier);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SupplierExists(supplier.SupplierID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(supplier);
        }

        // GET: Supplier/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var supplier = await _context.Supplier2
                .FirstOrDefaultAsync(m => m.SupplierID == id);
            if (supplier == null)
            {
                return NotFound();
            }

            return View(supplier);
        }

        // POST: Supplier/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var supplier = await _context.Supplier2.FindAsync(id);
            
            // Check if there are products using this supplier
            bool hasProducts = await _context.Supplier2.AnyAsync(p => p.SupplierID == id);
            if (hasProducts)
            {
                ModelState.AddModelError("", "Cannot delete supplier that has products assigned to it.");
                return View(supplier);
            }
            
            if (supplier != null)
            {
                _context.Supplier2.Remove(supplier);
                await _context.SaveChangesAsync();
            }
            
            return RedirectToAction(nameof(Index));
        }

        private bool SupplierExists(int id)
        {
            return _context.Supplier2.Any(e => e.SupplierID == id);
        }
    }
} 
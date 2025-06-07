using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Project_Creation.Data;
using Project_Creation.Models.Entities;
using Project_Creation.DTO;
using System.Security.Claims;

namespace Project_Creation.Controllers
{
    public class Supplier2Controller : Controller
    {
        private readonly AuthDbContext _context;

        public Supplier2Controller(AuthDbContext context)
        {
            _context = context;
        }

        // GET: Supplier2
        public async Task<IActionResult> Index()
        {
            var suppliers = await _context.Supplier2
                .Where(u => u.BOId == GetCurrentUserId()) // get the supplier by current user id
                .Select(s => new Supplier2Dto
                {
                    SupplierID = s.SupplierID,
                    BOId = s.BOId,
                    SupplierName = s.SupplierName,
                    ContactPerson = s.ContactPerson,
                    Email = s.Email,
                    Phone = s.Phone,
                    Address = s.Address
                })
                .ToListAsync();

            return View(suppliers);
        }

        [HttpGet]
        public async Task<IActionResult> GetSupplier(int id)
        {
            var supplier = await _context.Supplier2
                .Where(u => u.BOId == GetCurrentUserId()) // get the supplier by current user id
                .Where(s => s.SupplierID == id)
                .FirstOrDefaultAsync();

            if (supplier == null)
            {
                return NotFound();
            }

            // Create a simple anonymous object to return
            return Json(new
            {
                supplierID = supplier.SupplierID,
                //BOId = supplier.BOId,
                supplierName = supplier.SupplierName,
                contactPerson = supplier.ContactPerson,
                email = supplier.Email,
                phone = supplier.Phone,
                address = supplier.Address
            });
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                throw new InvalidOperationException("User is not authenticated");
            }

            var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
            int boId = int.TryParse(User.FindFirstValue("BOId"), out var tempBoId) ? tempBoId : 0;
            int who = currentUserRole == "Staff" ? boId : userId;
            return who;
        }

        [HttpPost]
        public async Task<IActionResult> Create(Supplier2Dto supplierDto)
        {
            if (ModelState.IsValid)
            {
                var supplier = new Supplier
                {
                    BOId = GetCurrentUserId(),
                    SupplierName = supplierDto.SupplierName,
                    ContactPerson = supplierDto.ContactPerson,
                    Email = supplierDto.Email,
                    Phone = supplierDto.Phone,
                    Address = supplierDto.Address
                };

                _context.Add(supplier);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }

            var errors = ModelState.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).FirstOrDefault()
            );

            return Json(new { success = false, errors });
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, Supplier2Dto supplierDto)
        {
            if (id != supplierDto.SupplierID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var supplier = await _context.Supplier2.FindAsync(id);
                if (supplier == null)
                {
                    return NotFound();
                }
                //supplier.BOId = GetCurrentUserId(); // Ensure the BOId is set to the current user's ID
                supplier.SupplierName = supplierDto.SupplierName;
                supplier.ContactPerson = supplierDto.ContactPerson;
                supplier.Email = supplierDto.Email;
                supplier.Phone = supplierDto.Phone;
                supplier.Address = supplierDto.Address;  // Fixed typo here

                try
                {
                    _context.Update(supplier);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!Supplier2Exists(id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            var errors = ModelState.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).FirstOrDefault()
            );

            return Json(new { success = false, errors });
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var supplier = await _context.Supplier2.FindAsync(id);
            if (supplier != null)
            {
                _context.Supplier2.Remove(supplier);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        private bool Supplier2Exists(int id)
        {
            return _context.Supplier2.Any(e => e.SupplierID == id);
        }
    }
}
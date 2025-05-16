using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Project_Creation.Data;
using Project_Creation.DTO;
using Project_Creation.Models.Entities;

namespace Project_Creation.Controllers
{
    public class LeadsController : Controller
    {
        private readonly AuthDbContext _context;

        public LeadsController(AuthDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdClaim, out int userId) ? userId : 0;
        }

        [HttpGet]
        public IActionResult GetCurrentUserId2()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userId = int.TryParse(userIdClaim, out int parsedUserId) ? parsedUserId : 0;
            return Json(new { userId });
        }


        // GET: Leads
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();
            var leads = await _context.Leads
                .Where(u => u.BOId == userId)
                .Include(p => p.Products)
                .ToListAsync();

            var totalLeads = await _context.Leads
                .Where(u => u.BOId == userId)
                .CountAsync();

            ViewBag.TotalLeads = totalLeads;
            return View(leads);
        }

        // GET: Leads/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var lead = await _context.Leads
                .FirstOrDefaultAsync(m => m.Id == id && m.BOId == GetCurrentUserId());

            return lead == null ? NotFound() : View(lead);
        }

        // GET: Leads/Create
        public IActionResult Create()
        {
            var userId = GetCurrentUserId();
            ViewBag.Products = _context.Products2
                .Where(p => p.BOId == userId)
                .ToList();
            return View();
        }

        // POST: Leads/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Leads leads)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var emailExist = _context.Leads.Any(l => l.LeadEmail == leads.LeadEmail);

                    if (emailExist)
                    {
                        return Json(new { success = false, message = "Lead email already exists!" });
                    }

                    // Convert selected product IDs to comma-separated string
                    leads.InterestedIn = leads.SelectedProductIds != null
                        ? string.Join(",", leads.SelectedProductIds)
                        : null;

                    leads.BOId = GetCurrentUserId();
                    leads.CreatedAt = DateTime.Now;

                    _context.Add(leads);
                    await _context.SaveChangesAsync();

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = true });
                    }
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, message = ex.Message });
                    }
                    ModelState.AddModelError("", "An error occurred while saving the lead.");
                }
            }

            // Repopulate products if validation fails
            ViewBag.Products = _context.Products2
                .Where(p => p.BOId == GetCurrentUserId())
                .ToList();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return Json(new { success = false, message = "Validation failed", errors });
            }

            return View(leads);
        }

        // GET: Leads/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var lead = await _context.Leads
                .FirstOrDefaultAsync(m => m.Id == id && m.BOId == GetCurrentUserId());

            if (lead == null) return NotFound();

            // Convert comma-separated string back to list of IDs
            if (!string.IsNullOrEmpty(lead.InterestedIn))
            {
                lead.SelectedProductIds = lead.InterestedIn.Split(',')
                    .Select(int.Parse)
                    .ToList();
            }

            ViewBag.Products = _context.Products2
                .Where(p => p.BOId == GetCurrentUserId())
                .ToList();

            return View(lead);
        }

        // POST: Leads/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Leads leads)
        {
            if (id != leads.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // 1. First get the existing lead from the database
                    var existingLead = await _context.Leads.FindAsync(id);
                    if (existingLead == null)
                    {
                        return NotFound();
                    }

                    // 2. Preserve the original CreatedAt value
                    leads.CreatedAt = existingLead.CreatedAt;

                    // 3. Convert selected product IDs to comma-separated string
                    leads.InterestedIn = leads.SelectedProductIds != null
                        ? string.Join(",", leads.SelectedProductIds)
                        : null;

                    // 4. Set the BOId and UpdatedAt
                    leads.BOId = GetCurrentUserId();
                    leads.UpdatedAt = DateTime.Now;

                    // 5. Update the entity without changing CreatedAt
                    _context.Entry(existingLead).CurrentValues.SetValues(leads);
                    await _context.SaveChangesAsync();

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = true });
                    }
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    if (!LeadsExists(leads.Id))
                    {
                        return NotFound();
                    }

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, message = ex.Message });
                    }
                    throw;
                }
                catch (Exception ex)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, message = ex.Message });
                    }
                    ModelState.AddModelError("", "An error occurred while saving the lead.");
                }
            }

            // Repopulate products if validation fails
            ViewBag.Products = _context.Products2
                .Where(p => p.BOId == GetCurrentUserId())
                .ToList();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return Json(new { success = false, message = "Validation failed", errors });
            }

            return View(leads);
        }

        // POST: Leads/Delete/5
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var lead = await _context.Leads
                .FirstOrDefaultAsync(m => m.Id == id && m.BOId == GetCurrentUserId());

            if (lead == null) return Json(new { success = false });

            _context.Leads.Remove(lead);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        private bool LeadsExists(int id)
        {
            return _context.Leads.Any(e => e.Id == id);
        }

        [HttpGet]
        public async Task<IActionResult> DetailsJson(int? id)
        {
            if (id == null) return NotFound();

            var lead = await _context.Leads
                .FirstOrDefaultAsync(m => m.Id == id && m.BOId == GetCurrentUserId());

            if (lead == null) return NotFound();

            var productNames = !string.IsNullOrEmpty(lead.InterestedIn)
                ? GetProductNames(lead.InterestedIn)
                : new List<string>();

            return Json(new
            {
                id = lead.Id,
                leadName = lead.LeadName,
                leadEmail = lead.LeadEmail,
                leadPhone = lead.LeadPhone,
                interestedProducts = productNames,
                notes = lead.Notes,
                createdAt = lead.CreatedAt,
                updatedAt = lead.UpdatedAt,
                boId = lead.BOId
            });
        }

        [HttpGet]
        public async Task<IActionResult> EditJson(int? id)
        {
            if (id == null) return NotFound();

            var lead = await _context.Leads
                .FirstOrDefaultAsync(m => m.Id == id && m.BOId == GetCurrentUserId());

            if (lead == null) return NotFound();

            var selectedProductIds = !string.IsNullOrEmpty(lead.InterestedIn)
                ? lead.InterestedIn.Split(',').ToList()
                : new List<string>();

            var products = _context.Products2
                .Where(p => p.BOId == GetCurrentUserId())
                .Select(p => new {
                    id = p.Id,
                    name = p.ProductName
                })
                .ToList();

            return Json(new
            {
                id = lead.Id,
                leadName = lead.LeadName,
                leadEmail = lead.LeadEmail,
                leadPhone = lead.LeadPhone,
                selectedProductIds = selectedProductIds,
                notes = lead.Notes,
                boId = lead.BOId,
                products = products
            });
        }

        private List<string> GetProductNames(string interestedIn)
        {
            var productIds = interestedIn.Split(',').Select(int.Parse).ToList();
            var products = _context.Products2.Where(p => productIds.Contains(p.Id)).ToList();
            return products.Select(p => p.ProductName).ToList();
        }
    }
}

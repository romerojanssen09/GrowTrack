using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Project_Creation.Data;
using Project_Creation.Models.Entities;
using System.Security.Claims;

namespace Project_Creation.Controllers
{
    public class LeadsController : Controller
    {
        private readonly AuthDbContext _context;

        public LeadsController(AuthDbContext context)
        {
            _context = context;
        }

        // GET: Leads
        public async Task<IActionResult> Index(string searchString, string status, int? companyId)
        {
            try
            {
                // Get current user's business owner ID
                int currentBOId = GetCurrentBusinessOwnerId();
                var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
                
                // Start with leads for the current business owner
                IQueryable<Leads> leadsQuery = _context.Leads;
                
                // Include the CreatedBy relationship
                leadsQuery = leadsQuery.Include(l => l.CreatedBy);
                
                // Filter out deleted leads by default
                leadsQuery = leadsQuery.Where(l => l.Status != Leads.LeadStatus.Deleted);

                // Apply filters
                if (!string.IsNullOrEmpty(searchString))
                {
                    leadsQuery = leadsQuery.Where(l => 
                        l.LeadName.Contains(searchString) || 
                        l.LeadEmail.Contains(searchString) || 
                        l.LeadPhone.Contains(searchString));
                }

                if (!string.IsNullOrEmpty(status) && Enum.TryParse<Leads.LeadStatus>(status, out var statusEnum))
                {
                    leadsQuery = leadsQuery.Where(l => l.Status == statusEnum);
                }

                if (companyId.HasValue)
                {
                    leadsQuery = leadsQuery.Where(l => l.CreatedById == companyId.Value);
                }

                // Get leads with their creators
                var leads = await leadsQuery.ToListAsync();

                // Get all products for lookups
                var allProducts = await _context.Products2.ToListAsync();
                var productsLookup = allProducts.ToDictionary(p => p.Id, p => p.ProductName);

                // Get all companies (business owners) for the filter dropdown
                var companies = await _context.Users
                    .Where(u => u.UserRole == "BusinessOwner" && !string.IsNullOrEmpty(u.BusinessName))
                    .GroupBy(u => u.BusinessName)
                    .Select(g => new { Id = g.First().Id, BusinessName = g.Key })
                    .ToListAsync();

                // Process each lead to extract product names
                foreach (var lead in leads)
                {
                    lead.InterestedProductNames = new List<string>();
                    lead.PurchaseHistory = new List<SaleItem>();
                    
                    // Only try to process product IDs if not null or empty
                    if (!string.IsNullOrEmpty(lead.InterestedProductIds))
                    {
                        // Parse comma-separated IDs
                        var productIds = lead.InterestedProductIds.Split(',')
                            .Where(id => !string.IsNullOrWhiteSpace(id))
                            .Select(id => int.TryParse(id.Trim(), out int parsedId) ? parsedId : -1)
                            .Where(id => id > 0)
                            .ToList();
                            
                        // Get product names for these IDs using the lookup
                        foreach (var productId in productIds)
                        {
                            if (productsLookup.TryGetValue(productId, out string productName))
                            {
                                lead.InterestedProductNames.Add(productName);
                            }
                        }
                    }
                    
                    // Set the last purchased product name if available
                    if (lead.LastPurchasedId.HasValue && productsLookup.TryGetValue(lead.LastPurchasedId.Value, out string lastPurchasedName))
                    {
                        lead.LastPurchasedName = lastPurchasedName;
                    }
                    
                    // Load purchase history for the lead
                    lead.PurchaseHistory = _context.SaleItems
                        .Include(si => si.Sale)
                        .Include(si => si.Product)
                        .Where(si => si.Sale.LeadId == lead.Id)
                        .OrderByDescending(si => si.Sale.SaleDate)
                        .ToList();
                    
                    // If the lead has no points but has a last purchase date, check if they're inactive
                    if (lead.LeadPoints == 0 && lead.LastPurchaseDate.HasValue && lead.LastPurchaseDate.Value < DateTime.Now.AddDays(-30))
                    {
                        // This is likely a "Lost" lead - they haven't purchased anything recently
                        lead.Status = Leads.LeadStatus.Lost;
                    }
                }

                // Store filter values for the view
                ViewBag.CurrentSearch = searchString;
                ViewBag.CurrentStatus = status;
                ViewBag.CurrentCompany = companyId;
                ViewBag.Companies = companies;

                return View(leads);
            }
            catch (Exception ex)
            {
                // Provide a fallback for errors
                ViewBag.ErrorMessage = ex.Message;
                return View(new List<Leads>());
            }
        }

        // GET: Leads/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
            var leads = await _context.Leads
                .Include(l => l.CreatedBy)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (leads == null)
            {
                return NotFound();
            }

                // Initialize the lists
                leads.InterestedProductNames = new List<string>();
                leads.PurchaseHistory = new List<SaleItem>();
                
                // Process interested product IDs if they exist
                if (!string.IsNullOrEmpty(leads.InterestedProductIds))
                {
                    // Parse comma-separated IDs
                    var productIds = leads.InterestedProductIds.Split(',')
                        .Where(pid => !string.IsNullOrWhiteSpace(pid))
                        .Select(pid => int.TryParse(pid.Trim(), out int parsedId) ? parsedId : -1)
                        .Where(pid => pid > 0)
                        .ToList();
                        
                    if (productIds.Any())
                    {
                        // Get product names for these IDs
                        var products = await _context.Products2
                            .Where(p => productIds.Contains(p.Id))
                            .ToListAsync();
                            
                        foreach (var product in products)
                        {
                            leads.InterestedProductNames.Add(product.ProductName);
                        }
                    }
                }
                
                // Get purchase history for this lead
                try 
                {
                    // Directly query for SaleItems related to this lead
                    leads.PurchaseHistory = await _context.SaleItems
                        .Include(si => si.Sale)
                        .Include(si => si.Product)
                        .Where(si => si.Sale.LeadId == leads.Id)
                        .OrderByDescending(si => si.Sale.SaleDate)
                        .ToListAsync();
                    
                    // Log how many purchase history items we found
                    Console.WriteLine($"Found {leads.PurchaseHistory.Count} purchase history items for lead {leads.Id}");
                    
                    if (leads.PurchaseHistory.Any())
                    {
                        // Don't override the lead points if they already exist in the database
                        // This preserves points calculated by the more sophisticated QuickSale algorithm
                        if (!leads.LeadPoints.HasValue || leads.LeadPoints == 0)
                        {
                            // Calculate lead points based on purchase history only if not already set
                            int totalPoints = 0;
                            var uniqueProductIds = new HashSet<int>();
                            
                            foreach (var item in leads.PurchaseHistory)
                            {
                                if (!uniqueProductIds.Contains(item.ProductId))
                                {
                                    uniqueProductIds.Add(item.ProductId);
                                    totalPoints += 2; // 2 points for each unique product
                                }
                                else
                                {
                                    totalPoints += 1; // 1 point per quantity for repeat products
                                }
                            }
                            
                            leads.LeadPoints = totalPoints;
                        }
                        
                        // Set the last purchase date to the most recent sale
                        var mostRecentSale = leads.PurchaseHistory.FirstOrDefault()?.Sale;
                        if (mostRecentSale != null)
                        {
                            leads.LastPurchaseDate = mostRecentSale.SaleDate;
                            
                            // Also fetch the name of the most recent product
                            if (leads.LastPurchasedId.HasValue)
                            {
                                var lastProduct = await _context.Products2.FindAsync(leads.LastPurchasedId.Value);
                                if (lastProduct != null)
                                {
                                    leads.LastPurchasedName = lastProduct.ProductName;
                                }
                            }
                            else if (leads.PurchaseHistory.FirstOrDefault()?.Product != null)
                            {
                                // If LastPurchasedId is not set but we have the product from the join
                                var firstProduct = leads.PurchaseHistory.FirstOrDefault()?.Product;
                                if (firstProduct != null)
                                {
                                    leads.LastPurchasedName = firstProduct.ProductName;
                                    
                                    // Update the LastPurchasedId in the lead record for future reference
                                    leads.LastPurchasedId = firstProduct.Id;
                                    await _context.SaveChangesAsync();
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"No purchase history found for lead {leads.Id}");
                    }
                }
                catch (Exception ex)
                {
                    // If there's an error with purchase history, just log it but continue showing the lead
                    Console.WriteLine($"Error loading purchase history: {ex.Message}");
                }

            return View(leads);
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Leads/Create
        public IActionResult Create()
        {
            try
            {
                // Get current user's business owner ID
                int currentBOId = GetCurrentBusinessOwnerId();

                // Filter products by business owner ID
                var products = _context.Products2.Where(p => p.BOId == currentBOId && p.IsDeleted == false).ToList();

                // Create select list for the products dropdown
                ViewData["Products"] = new SelectList(products, "Id", "ProductName");

                return View();
            }
            catch (Exception ex)
            {
                ViewData["ErrorMessage"] = $"Error loading products: {ex.Message}";
                ViewData["Products"] = new SelectList(new List<Product>(), "Id", "ProductName");
            return View();
            }
        }

        // POST: Leads/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("LeadName,LeadEmail,LeadPhone,Notes,SelectedProductIds")] Leads leads)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Check if lead already exists
                    var (exists, existingLead, matchType) = await LeadExists(
                        leads.LeadName,
                        leads.LeadEmail,
                        leads.LeadPhone
                    );

                    if (exists && existingLead != null)
                    {
                        // Lead already exists, add a message to inform the user
                        string message = $"A lead with this {matchType} already exists: {existingLead.LeadName}";
                        
                        // Show the message as a warning
                        ViewData["WarningMessage"] = message;
                        
                        // Optionally, update the existing lead with any new information
                        bool updatedExistingLead = false;
                        
                        // If existing lead doesn't have email but new one does
                        if (string.IsNullOrWhiteSpace(existingLead.LeadEmail) && 
                            !string.IsNullOrWhiteSpace(leads.LeadEmail))
                        {
                            existingLead.LeadEmail = leads.LeadEmail;
                            updatedExistingLead = true;
                        }
                        
                        // If existing lead doesn't have phone but new one does
                        if (string.IsNullOrWhiteSpace(existingLead.LeadPhone) && 
                            !string.IsNullOrWhiteSpace(leads.LeadPhone))
                        {
                            existingLead.LeadPhone = leads.LeadPhone;
                            updatedExistingLead = true;
                        }
                        
                        // If existing lead doesn't have notes but new one does
                        if (string.IsNullOrWhiteSpace(existingLead.Notes) && 
                            !string.IsNullOrWhiteSpace(leads.Notes))
                        {
                            existingLead.Notes = leads.Notes;
                            updatedExistingLead = true;
                        }
                        
                        // Update the selected products if provided
                        if (leads.SelectedProductIds != null && leads.SelectedProductIds.Count > 0)
                        {
                            // Get existing product IDs
                            var existingProductIds = !string.IsNullOrEmpty(existingLead.InterestedProductIds)
                                ? existingLead.InterestedProductIds.Split(',').Select(id => int.Parse(id.Trim())).ToList()
                                : new List<int>();
                            
                            // Combine with new product IDs (distinct)
                            var combinedProductIds = existingProductIds.Concat(leads.SelectedProductIds).Distinct();
                            existingLead.InterestedProductIds = string.Join(",", combinedProductIds);
                            updatedExistingLead = true;
                        }
                        
                        // Save changes if any updates were made
                        if (updatedExistingLead)
                        {
                            existingLead.UpdatedAt = DateTime.Now;
                            _context.Update(existingLead);
                            await _context.SaveChangesAsync();
                            ViewData["InfoMessage"] = "Existing lead information has been updated with the new data.";
                        }
                        
                        // Get current business owner ID to populate products dropdown
                        int productsBOId = GetCurrentBusinessOwnerId();
                        var products = _context.Products2.Where(p => p.BOId == productsBOId).ToList();
                        ViewData["Products"] = new SelectList(products, "Id", "ProductName");
                        
                        // Return to the create view with messages
                        return View(leads);
                    }

                    int currentBOId = GetCurrentBusinessOwnerId();
                    
                    // Only save fields that exist in the database
                    var newLead = new Leads
                    {
                        LeadName = leads.LeadName,
                        LeadEmail = leads.LeadEmail,
                        LeadPhone = leads.LeadPhone,
                        Notes = leads.Notes,
                        Status = Leads.LeadStatus.New,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        CreatedById = currentBOId
                    };

                    // Store the selected product IDs as a comma-separated string
                    if (leads.SelectedProductIds != null && leads.SelectedProductIds.Count > 0)
                    {
                        newLead.InterestedProductIds = string.Join(",", leads.SelectedProductIds);
                    }

                    _context.Add(newLead);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    // Get the inner exception details for better error diagnosis
                    string errorMessage = ex.Message;
                    if (ex.InnerException != null)
                    {
                        errorMessage += " Inner error: " + ex.InnerException.Message;
                    }
                    ModelState.AddModelError("", $"Error creating lead: {errorMessage}");
                }
            }

            // If we got this far, something failed, redisplay form with the same product options
            try
            {
                int currentBOId = GetCurrentBusinessOwnerId();
                var products = _context.Products2.Where(p => p.BOId == currentBOId).ToList();
                ViewData["Products"] = new SelectList(products, "Id", "ProductName");
            }
            catch
            {
                ViewData["Products"] = new SelectList(new List<Product>(), "Id", "ProductName");
            }

            return View(leads);
        }

        // GET: Leads/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var leads = await _context.Leads.FindAsync(id);
            if (leads == null)
            {
                return NotFound();
            }

            try
            {
                // Get current user's business owner ID
                int currentBOId = GetCurrentBusinessOwnerId();

                // Filter products by business owner ID
                var products = await _context.Products2.Where(p => p.BOId == currentBOId && p.IsDeleted == false).ToListAsync();

                // Parse interested product IDs from comma-separated string
                if (!string.IsNullOrEmpty(leads.InterestedProductIds))
                {
                    leads.SelectedProductIds = leads.InterestedProductIds
                        .Split(',')
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Select(id => int.TryParse(id.Trim(), out int parsedId) ? parsedId : -1)
                        .Where(id => id > 0)
                        .ToList();
                }
                
                // Get purchase history for this lead
                try 
                {
                    // Directly query for SaleItems related to this lead
                    leads.PurchaseHistory = await _context.SaleItems
                        .Include(si => si.Sale)
                        .Include(si => si.Product)
                        .Where(si => si.Sale.LeadId == leads.Id)
                        .OrderByDescending(si => si.Sale.SaleDate)
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    // If there's an error with purchase history, just log it but continue showing the lead
                    Console.WriteLine($"Error loading purchase history: {ex.Message}");
                }

                // Create select list for the products dropdown
                ViewData["Products"] = new SelectList(products, "Id", "ProductName");

                return View(leads);
            }
            catch (Exception ex)
            {
                ViewData["ErrorMessage"] = $"Error loading products: {ex.Message}";
                ViewData["Products"] = new SelectList(new List<Product>(), "Id", "ProductName");
            return View(leads);
            }
        }

        // POST: Leads/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,LeadName,LeadEmail,LeadPhone,Notes,Status,SelectedProductIds")] Leads leads)
        {
            if (id != leads.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Get existing lead first
                    var existingLead = await _context.Leads.FindAsync(id);
                    if (existingLead == null)
                    {
                        return NotFound();
                    }

                    // Update only the fields we know exist in the database
                    existingLead.LeadName = leads.LeadName;
                    existingLead.LeadEmail = leads.LeadEmail;
                    existingLead.LeadPhone = leads.LeadPhone;
                    existingLead.Notes = leads.Notes;
                    existingLead.Status = leads.Status;
                    existingLead.UpdatedAt = DateTime.Now;

                    // Store the selected product IDs as a comma-separated string
                    if (leads.SelectedProductIds != null && leads.SelectedProductIds.Count > 0)
                    {
                        existingLead.InterestedProductIds = string.Join(",", leads.SelectedProductIds);
                    }
                    else
                    {
                        existingLead.InterestedProductIds = null;
                    }

                    _context.Update(existingLead);
                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LeadsExists(leads.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            try
            {
                int currentBOId = GetCurrentBusinessOwnerId();
                var products = await _context.Products2.Where(p => p.BOId == currentBOId).ToListAsync();
                ViewData["Products"] = new SelectList(products, "Id", "ProductName");
            }
            catch
            {
                ViewData["Products"] = new SelectList(_context.Products2, "Id", "ProductName");
            }
            
            return View(leads);
        }

        // GET: Leads/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var leads = await _context.Leads
                .Include(l => l.CreatedBy)
                .Include(l => l.Product)
                .FirstOrDefaultAsync(m => m.Id == id);
                
            if (leads == null)
            {
                return NotFound();
            }
            
            // Check if the lead has associated sales records (for information only)
            bool hasSales = await _context.Sales.AnyAsync(s => s.LeadId == id);
            ViewBag.HasSales = hasSales;
            
            if (hasSales)
            {
                // Pass information to view
                ViewBag.SalesWarning = "This lead has associated sales records. Deleting will hide it from lists but preserve the sales data.";
            }

            return View(leads);
        }

        // POST: Leads/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                // Get the lead
                var lead = await _context.Leads.FindAsync(id);
                if (lead == null)
                {
                    TempData["ErrorMessage"] = "Lead not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Instead of deleting, mark as deleted (soft delete)
                Console.WriteLine($"Soft deleting lead ID {id}: Current status: {lead.Status}, changing to: {Leads.LeadStatus.Deleted}");
                lead.Status = Leads.LeadStatus.Deleted;
                lead.UpdatedAt = DateTime.Now;
                
                // Update the lead
                _context.Update(lead);
                await _context.SaveChangesAsync();
                
                // Verify the change was saved
                var verifyLead = await _context.Leads.FindAsync(id);
                if (verifyLead != null && verifyLead.Status == Leads.LeadStatus.Deleted)
                {
                    TempData["SuccessMessage"] = "Lead marked as deleted successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Lead status change failed. Please try again.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting lead: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Leads/TestSoftDelete/5
        public async Task<IActionResult> TestSoftDelete(int id)
        {
            var lead = await _context.Leads.FindAsync(id);
            if (lead == null)
            {
                return Content("Lead not found");
            }

            var output = new System.Text.StringBuilder();
            output.AppendLine($"<h3>Lead Soft Delete Test</h3>");
            output.AppendLine($"<p><strong>Lead ID:</strong> {id}</p>");
            output.AppendLine($"<p><strong>Name:</strong> {lead.LeadName}</p>");
            output.AppendLine($"<p><strong>Current Status:</strong> {lead.Status}</p>");
            
            var hasStatusDeleted = Enum.IsDefined(typeof(Leads.LeadStatus), "Deleted");
            output.AppendLine($"<p><strong>'Deleted' status exists in enum:</strong> {hasStatusDeleted}</p>");
            
            if (lead.Status == Leads.LeadStatus.Deleted)
            {
                output.AppendLine("<p><strong>Status:</strong> <span style='color:green'>Already marked as deleted</span></p>");
            }
            else
            {
                output.AppendLine("<p><strong>Status:</strong> <span style='color:orange'>Not deleted yet</span></p>");
                
                // Add a form to perform the deletion
                output.AppendLine("<form method='post' action='/Leads/DoTestDelete'>");
                output.AppendLine($"<input type='hidden' name='id' value='{id}' />");
                output.AppendLine("<button type='submit' style='padding: 10px; background-color: #dc3545; color: white; border: none; border-radius: 5px;'>Mark as Deleted</button>");
                output.AppendLine("</form>");
            }
            
            // Add link back to index
            output.AppendLine("<p><a href='/Leads'>Back to leads list</a></p>");
            
            return Content(output.ToString(), "text/html");
        }
        
        // POST: Leads/DoTestDelete
        [HttpPost]
        public async Task<IActionResult> DoTestDelete(int id)
        {
            var lead = await _context.Leads.FindAsync(id);
            if (lead == null)
            {
                return Content("Lead not found");
            }
            
            // Display the current status
            var currentStatus = lead.Status;
            
            // Attempt soft delete
            lead.Status = Leads.LeadStatus.Deleted;
            lead.UpdatedAt = DateTime.Now;
            
            // Update the lead
            _context.Update(lead);
            var result = await _context.SaveChangesAsync();
            
            // Verify the change was saved
            var verifyLead = await _context.Leads.FindAsync(id);
            
            var output = new System.Text.StringBuilder();
            output.AppendLine($"<h3>Soft Delete Result</h3>");
            output.AppendLine($"<p><strong>Lead ID:</strong> {id}</p>");
            output.AppendLine($"<p><strong>Name:</strong> {lead.LeadName}</p>");
            output.AppendLine($"<p><strong>Original Status:</strong> {currentStatus}</p>");
            output.AppendLine($"<p><strong>New Status:</strong> {verifyLead?.Status}</p>");
            output.AppendLine($"<p><strong>Rows affected:</strong> {result}</p>");
            
            if (verifyLead?.Status == Leads.LeadStatus.Deleted)
            {
                output.AppendLine("<p><strong>Result:</strong> <span style='color:green'>Success - Lead marked as deleted</span></p>");
            }
            else
            {
                output.AppendLine("<p><strong>Result:</strong> <span style='color:red'>Failed - Lead not marked as deleted</span></p>");
            }
            
            // Add link back to index
            output.AppendLine("<p><a href='/Leads'>Back to leads list</a></p>");
            
            return Content(output.ToString(), "text/html");
        }

        private bool LeadsExists(int id)
        {
            return _context.Leads.Any(e => e.Id == id);
        }

        // Helper method to get current business owner ID
        private int GetCurrentBusinessOwnerId()
        {
            try
            {
                // Try to get the user ID from claims
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
                int boId = int.TryParse(User.FindFirstValue("BOId"), out var tempBoId) ? tempBoId : 0;
                
                // If it's a staff member, use their BOId, otherwise use their own ID
                int who = currentUserRole == "Staff" ? boId : int.Parse(userIdClaim);
                
                return who;
            }
            catch (Exception)
            {
                // If there's an error, try session
                var userIdString = HttpContext.Session.GetString("UserId");
                if (!string.IsNullOrEmpty(userIdString) && int.TryParse(userIdString, out int userId))
                {
                    return userId;
                }
                
                // Default to 1 if not found (for testing purposes)
                return 1;
            }
        }

        // Helper method to find existing leads based on multiple criteria
        private async Task<(bool exists, Leads? lead, string matchType)> LeadExists(string name, string email, string phone)
        {
            // Build query - remove the BOId filter to find all leads regardless of creator
            var query = _context.Leads.Where(l => l.Status != Leads.LeadStatus.Deleted);
            
            // Store potential matches with priority
            List<(Leads lead, int priority, string matchType)> potentialMatches = new List<(Leads, int, string)>();
            
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
                                potentialMatches.Add((lead, 3, "email and phone"));
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
                        potentialMatches.Add((lead, 2, "email and name"));
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
                                potentialMatches.Add((lead, 1, "phone and name"));
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
                        potentialMatches.Add((lead, 0, "email"));
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
                            potentialMatches.Add((lead, 0, "phone"));
                        }
                    }
                }
                
                // Name exact match (lowest priority)
                if (!string.IsNullOrWhiteSpace(name) && !potentialMatches.Any())
                {
                    var lead = await query.FirstOrDefaultAsync(l => l.LeadName.ToLower() == name.ToLower());
                    if (lead != null)
                    {
                        potentialMatches.Add((lead, 0, "name"));
                    }
                }
            }
            
            // Return the lead with the highest priority
            if (potentialMatches.Any())
            {
                var bestMatch = potentialMatches.OrderByDescending(m => m.priority).First();
                return (true, bestMatch.lead, bestMatch.matchType);
            }
            
            // No matches found
            return (false, null, string.Empty);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using Project_Creation.Data;
using Project_Creation.DTO;
using Project_Creation.Models.Entities;
using Project_Creation.Models.ViewModels;

namespace Project_Creation.Controllers
{
    [Authorize]
    public class CampaignsController : Controller
    {
        private readonly IEmailService _emailService;
        private readonly AuthDbContext _context;
        private readonly ILogger<CampaignsController> _logger;

        public CampaignsController(IEmailService emailService, AuthDbContext context, ILogger<CampaignsController> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        // GET: Campaigns
        public async Task<IActionResult> Index()
        {
            ViewBag.Products = await _context.Products2.ToListAsync();
            ViewBag.Leads = await _context.Leads.ToListAsync();
            var userId = GetCurrentUserId();
            return View(await _context.Campaigns
                .Where(c => c.SenderId == userId)
                .OrderByDescending(c => c.Id)
                .ToListAsync());
        }

        // GET: Campaigns/Templates
        public async Task<IActionResult> Templates()
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("Templates GET called for user ID: {UserId}", userId);
            
            // Debug: First check if any templates exist at all, directly from SQL
            var allTemplatesCount = await _context.MessageTemplates.AsNoTracking().CountAsync();
            _logger.LogInformation("Total templates in database: {Count}", allTemplatesCount);
            
            // Debug: Get all templates in the database regardless of BOId
            var allTemplates = await _context.MessageTemplates.AsNoTracking().ToListAsync();
            _logger.LogInformation("All templates in database ({Count}):", allTemplates.Count);
            foreach (var template in allTemplates)
            {
                _logger.LogInformation("  Template ID: {Id}, Name: {Name}, BOId: {BOId}, CreatedAt: {CreatedAt}", 
                    template.Id, template.Name, template.BOId, template.CreatedAt);
            }
            
            // Get templates for this user, using AsNoTracking for better performance
            var templates = await _context.MessageTemplates
                .AsNoTracking()
                .Where(t => t.BOId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
                
            _logger.LogInformation("Retrieved {Count} templates for user ID {UserId}", templates.Count, userId);
            
            // Log template details for debugging
            foreach (var template in templates)
            {
                _logger.LogInformation("Template ID: {Id}, Name: {Name}, BOId: {BOId}, CreatedAt: {CreatedAt}", 
                    template.Id, template.Name, template.BOId, template.CreatedAt);
            }
            
            return View(templates);
        }
        
        // GET: Campaigns/CreateTemplate
        public IActionResult CreateTemplate()
        {
            return View();
        }
        
        // POST: Campaigns/CreateTemplate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTemplate(MessageTemplate template)
        {
            _logger.LogInformation("CreateTemplate POST called with template name: {TemplateName}", template.Name);
            
            // Log all form data for debugging
            foreach (var key in Request.Form.Keys)
            {
                _logger.LogInformation("Form data: {Key} = {Value}", key, Request.Form[key]);
            }
            
            // Explicitly remove BusinessOwner validation error since it's a navigation property
            if (ModelState.ContainsKey("BusinessOwner"))
            {
                ModelState.Remove("BusinessOwner");
            }
            
            if (ModelState.IsValid)
            {
                try
                {
                    var userId = GetCurrentUserId();
                    _logger.LogInformation("Model is valid, setting BOId: {BOId}", userId);
                    
                    // Force check to ensure BOId is valid and not 0
                    if (userId <= 0)
                    {
                        _logger.LogError("Invalid BOId: {BOId} for user {UserName}", userId, User.Identity?.Name);
                        ModelState.AddModelError("", "Unable to determine your business owner ID. Please try logging out and back in.");
                        return View(template);
                    }
                    
                    template.BOId = userId;
                    template.CreatedAt = DateTime.UtcNow;
                    
                    _logger.LogInformation("Template data before saving: Id={Id}, Name={Name}, Subject={Subject}, Content Length={ContentLength}, BOId={BOId}", 
                        template.Id, template.Name, template.Subject, template.Content?.Length, template.BOId);
                    
                    // We don't need to set the BusinessOwner property - EF Core will handle this relationship
                    _logger.LogInformation("Adding template to context");
                    _context.MessageTemplates.Add(template);
                    
                    try
                    {
                        var saveResult = await _context.SaveChangesAsync();
                        _logger.LogInformation("SaveChangesAsync result: {SaveResult} rows affected", saveResult);
                    }
                    catch (DbUpdateException dbEx)
                    {
                        _logger.LogError(dbEx, "Database error while saving template: {ErrorMessage}", dbEx.Message);
                        
                        if (dbEx.InnerException != null)
                        {
                            _logger.LogError("Inner exception: {InnerException}", dbEx.InnerException.Message);
                        }
                        
                        throw new Exception($"Database error: {dbEx.Message}", dbEx);
                    }
                    
                    // Verify the template was saved correctly by retrieving it
                    var savedTemplate = await _context.MessageTemplates
                        .AsNoTracking() // Ensure we get a fresh copy from the database
                        .FirstOrDefaultAsync(t => t.Id == template.Id);
                        
                    if (savedTemplate != null)
                    {
                        _logger.LogInformation("Template retrieved after save: Id={Id}, Name={Name}, BOId={BOId}", 
                            savedTemplate.Id, savedTemplate.Name, savedTemplate.BOId);
                        
                        // Get the business owner to send confirmation email
                        var bo = await _context.Users.FindAsync(userId);
                        if (bo != null)
                        {
                            try
                            {
                                // Send confirmation email to the business owner
                                string subject = $"Template Created: {template.Name}";
                                string body = $@"
                                <!DOCTYPE html>
                                <html>
                                <head>
                                    <meta charset='utf-8'>
                                    <meta name='viewport' content='width=device-width, initial-scale=1'>
                                    <title>Template Created</title>
                                    <style>
                                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                                        .header {{ background-color: #4e73df; color: white; padding: 15px; text-align: center; }}
                                        .content {{ padding: 20px; border: 1px solid #ddd; }}
                                        .footer {{ text-align: center; margin-top: 20px; font-size: 12px; color: #777; }}
                                    </style>
                                </head>
                                <body>
                                    <div class='container'>
                                        <div class='header'>
                                            <h2>Email Template Created Successfully</h2>
                                        </div>
                                        <div class='content'>
                                            <h3>Hello {bo.FirstName},</h3>
                                            <p>Your email template <strong>{template.Name}</strong> has been created successfully.</p>
                                            <p><strong>Subject:</strong> {template.Subject}</p>
                                            <p>You can use this template when creating campaigns to reach out to your leads.</p>
                                            <p>To view or edit your templates, visit the Templates section in your Campaign Manager.</p>
                                        </div>
                                        <div class='footer'>
                                            <p>© {DateTime.Now.Year} GrowSphere | This is an automated message.</p>
                                        </div>
                                    </div>
                                </body>
                                </html>";
                                
                                await _emailService.SendEmailToBO(bo, subject, body, true);
                                _logger.LogInformation("Template creation confirmation email sent to {Email}", bo.Email);
                            }
                            catch (Exception emailEx)
                            {
                                // Log the error but don't fail the operation if email sending fails
                                _logger.LogError(emailEx, "Error sending template creation confirmation email to {Email}", bo.Email);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Failed to retrieve saved template with ID {Id}", template.Id);
                        throw new Exception("Template was not found after saving");
                    }
                    
                    TempData["SuccessMessage"] = "Template created successfully!";
                    _logger.LogInformation("Template created successfully with ID: {TemplateId}", template.Id);
                    
                    // Check if it's an AJAX request
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        _logger.LogInformation("Returning JSON response for AJAX request");
                        return Json(new { success = true, redirectUrl = Url.Action("Templates"), templateId = template.Id });
                    }
                    
                    _logger.LogInformation("Redirecting to Templates action");
                    return RedirectToAction(nameof(Templates));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating template: {ErrorMessage}", ex.Message);
                    ModelState.AddModelError("", $"Error saving template: {ex.Message}");
                }
            }
            else
            {
                _logger.LogWarning("Model validation failed");
                foreach (var state in ModelState)
                {
                    foreach (var error in state.Value.Errors)
                    {
                        _logger.LogWarning("Validation error for {Property}: {Error}", 
                            state.Key, error.ErrorMessage);
                    }
                }
            }
            
            // If we got this far, something failed
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                _logger.LogInformation("Returning JSON error response for AJAX request");
                return Json(new { 
                    success = false, 
                    errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList() 
                });
            }
            
            _logger.LogInformation("Returning view with template model");
            return View(template);
        }
        
        // GET: Campaigns/EditTemplate/5
        public async Task<IActionResult> EditTemplate(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = GetCurrentUserId();
            var template = await _context.MessageTemplates
                .FirstOrDefaultAsync(t => t.Id == id && t.BOId == userId);
                
            if (template == null)
            {
                return NotFound();
            }
            
            return View(template);
        }
        
        // POST: Campaigns/EditTemplate/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTemplate(int id, MessageTemplate template)
        {
            if (id != template.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingTemplate = await _context.MessageTemplates
                        .FirstOrDefaultAsync(t => t.Id == id && t.BOId == GetCurrentUserId());
                        
                    if (existingTemplate == null)
                    {
                        return NotFound();
                    }
                    
                    existingTemplate.Name = template.Name;
                    existingTemplate.Subject = template.Subject;
                    existingTemplate.Content = template.Content;
                    existingTemplate.UpdatedAt = DateTime.UtcNow;
                    
                    _context.Update(existingTemplate);
                    await _context.SaveChangesAsync();
                    
                    // Get the business owner to send confirmation email
                    var userId = GetCurrentUserId();
                    var bo = await _context.Users.FindAsync(userId);
                    if (bo != null)
                    {
                        try
                        {
                            // Send confirmation email to the business owner
                            string subject = $"Template Updated: {template.Name}";
                            string body = $@"
                            <!DOCTYPE html>
                            <html>
                            <head>
                                <meta charset='utf-8'>
                                <meta name='viewport' content='width=device-width, initial-scale=1'>
                                <title>Template Updated</title>
                                <style>
                                    body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                                    .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                                    .header {{ background-color: #4e73df; color: white; padding: 15px; text-align: center; }}
                                    .content {{ padding: 20px; border: 1px solid #ddd; }}
                                    .footer {{ text-align: center; margin-top: 20px; font-size: 12px; color: #777; }}
                                </style>
                            </head>
                            <body>
                                <div class='container'>
                                    <div class='header'>
                                        <h2>Email Template Updated Successfully</h2>
                                    </div>
                                    <div class='content'>
                                        <h3>Hello {bo.FirstName},</h3>
                                        <p>Your email template <strong>{template.Name}</strong> has been updated successfully.</p>
                                        <p><strong>Subject:</strong> {template.Subject}</p>
                                        <p>You can use this template when creating campaigns to reach out to your leads.</p>
                                        <p>To view or edit your templates, visit the Templates section in your Campaign Manager.</p>
                                    </div>
                                    <div class='footer'>
                                        <p>© {DateTime.Now.Year} GrowSphere | This is an automated message.</p>
                                    </div>
                                </div>
                            </body>
                            </html>";
                            
                            await _emailService.SendEmailToBO(bo, subject, body, true);
                            _logger.LogInformation("Template update confirmation email sent to {Email}", bo.Email);
                        }
                        catch (Exception emailEx)
                        {
                            // Log the error but don't fail the operation if email sending fails
                            _logger.LogError(emailEx, "Error sending template update confirmation email to {Email}", bo.Email);
                        }
                    }
                    
                    TempData["SuccessMessage"] = "Template updated successfully!";
                    return RedirectToAction(nameof(Templates));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TemplateExists(template.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            
            return View(template);
        }
        
        // POST: Campaigns/DeleteTemplate/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTemplate(int id)
        {
            var template = await _context.MessageTemplates
                .FirstOrDefaultAsync(t => t.Id == id && t.BOId == GetCurrentUserId());
                
            if (template == null)
            {
                return NotFound();
            }
            
            _context.MessageTemplates.Remove(template);
            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = "Template deleted successfully!";
            return RedirectToAction(nameof(Templates));
        }
        
        // GET: Campaigns/GetTemplate/5
        [HttpGet]
        public async Task<IActionResult> GetTemplate(int id)
        {
            var userId = GetCurrentUserId();
            var template = await _context.MessageTemplates
                .FirstOrDefaultAsync(t => t.Id == id && t.BOId == userId);
                
            if (template == null)
            {
                return Json(new { success = false, message = "Template not found" });
            }
            
            return Json(new { 
                success = true,
                id = template.Id,
                name = template.Name,
                subject = template.Subject,
                content = template.Content
            });
        }
        
        private bool TemplateExists(int id)
        {
            return _context.MessageTemplates.Any(t => t.Id == id);
        }

        public class CampaignViewModel
        {
            public bool SendToAll { get; set; }
            public string? TargetProducts { get; set; }
            public List<string>? TargetProductNames { get; set; }
        }

        private int GetCurrentUserId()
        {
            try
            {
                _logger.LogInformation("Getting current user ID");
                
                // Check if the user is authenticated
                if (!User.Identity?.IsAuthenticated ?? true)
                {
                    _logger.LogWarning("User is not authenticated");
                    return 0;
                }
                
                var userRole = User.FindFirstValue(ClaimTypes.Role);
                _logger.LogInformation("User role: {Role}", userRole);
                
                var userIdClaim = "0";
                if (userRole == "Staff")
                {
                    // Get the BOId for staff user
                    userIdClaim = User.FindFirstValue("BOId");
                    _logger.LogInformation("Staff user, BOId from claim: {BOId}", userIdClaim);
                    
                    if (string.IsNullOrEmpty(userIdClaim))
                    {
                        _logger.LogWarning("BOId claim is missing for staff user");
                        return 0;
                    }
                }
                else
                {
                    // Get the user ID for business owner
                    userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    _logger.LogInformation("Business Owner user, Id from claim: {Id}", userIdClaim);
                    
                    if (string.IsNullOrEmpty(userIdClaim))
                    {
                        _logger.LogWarning("NameIdentifier claim is missing for business owner");
                        return 0;
                    }
                }
                
                if (int.TryParse(userIdClaim, out int userId))
                {
                    _logger.LogInformation("Parsed user ID: {UserId}", userId);
                    return userId;
                }
                
                _logger.LogWarning("Failed to parse user ID from claim: {Claim}, returning 0", userIdClaim);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user ID");
                return 0;
            }
        }

        // GET: Campaigns/GetProducts
        public IActionResult GetProducts()
        {
            var products = _context.Products2
                .Where(u => u.BOId == GetCurrentUserId() && u.IsDeleted == false)
                .Select(p => new {
                    id = p.Id,
                    name = p.ProductName
                }).ToList();
            return Json(products);
        }

        // GET: Campaigns/Create
        public async Task<IActionResult> Create()
        {
            var userId = GetCurrentUserId();

            var viewModel = new CampaignFilterViewModel
            {
                // Load templates
                Templates = await _context.MessageTemplates
                    .Where(t => t.BOId == userId)
                    .OrderByDescending(t => t.IsDefault)
                    .ThenByDescending(t => t.CreatedAt)
                    .ToListAsync(),
                
                // Get all product categories
                ProductCategories = await _context.Products2
                    .Where(p => p.BOId == userId)
                    .Select(p => p.Category)
                    .Distinct()
                    .Select(c => new SelectListItem { Value = c, Text = c })
                    .ToListAsync(),
                    
                // Get all business owners for staff
                BusinessOwners = User.FindFirstValue(ClaimTypes.Role) == "Staff" 
                    ? await _context.Users
                        .Where(u => u.UserRole == "BusinessOwner")
                        .Select(u => new SelectListItem { Value = u.Id.ToString(), Text = u.BusinessName })
                        .ToListAsync()
                    : new List<SelectListItem>(),
                
                // Get all products
                Products = await _context.Products2
                    .Where(p => p.BOId == userId)
                    .OrderBy(p => p.ProductName)
                    .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.ProductName })
                    .ToListAsync(),
                    
                // Load all leads without filtering by creator
                MyLeads = await _context.Leads
                    .Include(l => l.CreatedBy)
                    .ToListAsync()
            };

            // Load purchase history for leads
            foreach (var lead in viewModel.MyLeads)
            {
                lead.PurchaseHistory = await _context.SaleItems
                    .Include(si => si.Sale)
                    .Include(si => si.Product)
                    .Where(si => si.Sale.LeadId == lead.Id)
                    .ToListAsync();
            }

            return View(viewModel);
        }

        // POST: Campaigns/FilterLeads
        [HttpPost]
        public async Task<IActionResult> FilterLeads(CampaignFilterViewModel filters)
        {
            var userId = GetCurrentUserId();
            
            // Start with all leads
            var query = _context.Leads.AsQueryable();
            
            // Exclude deleted leads
            query = query.Where(l => l.Status != Leads.LeadStatus.Deleted);
            
            // Apply filters
            if (filters.FilterCreatedById.HasValue)
            {
                query = query.Where(l => l.CreatedById == filters.FilterCreatedById.Value);
            }
            // No else clause needed - we want all leads if no creator filter is specified
            
            // Filter by status
            if (filters.FilterStatus.HasValue)
            {
                query = query.Where(l => l.Status == filters.FilterStatus.Value);
            }
            
            // Filter by points range
            if (filters.FilterMinPoints.HasValue)
            {
                query = query.Where(l => l.LeadPoints >= filters.FilterMinPoints.Value);
            }
            
            if (filters.FilterMaxPoints.HasValue)
            {
                query = query.Where(l => l.LeadPoints <= filters.FilterMaxPoints.Value);
            }
            
            // Filter by search term
            if (!string.IsNullOrWhiteSpace(filters.FilterSearch))
            {
                string searchTerm = filters.FilterSearch.ToLower();
                query = query.Where(l => l.LeadName.ToLower().Contains(searchTerm) || 
                                        l.LeadEmail.ToLower().Contains(searchTerm) || 
                                        l.LeadPhone.ToLower().Contains(searchTerm));
            }
            
            // Filter by last contacted dates
            if (filters.FilterLastContactedBefore.HasValue)
            {
                query = query.Where(l => l.LastContacted <= filters.FilterLastContactedBefore.Value);
            }
            
            if (filters.FilterLastContactedAfter.HasValue)
            {
                query = query.Where(l => l.LastContacted >= filters.FilterLastContactedAfter.Value);
            }
            
            // Include necessary data
            //query = query.Include(l => l.CreatedBy);

            // Execute query
            var filteredLeads = await query.ToListAsync();
            
            // Apply further filtering that requires the data to be loaded
            if (!string.IsNullOrEmpty(filters.FilterProductCategory) || filters.FilterProductId.HasValue || filters.FilterHasPurchaseHistory.HasValue)
            {
                // Load purchase history for leads
                foreach (var lead in filteredLeads)
                {
                    lead.PurchaseHistory = await _context.SaleItems
                        .Include(si => si.Sale)
                        .Include(si => si.Product)
                        .Where(si => si.Sale.LeadId == lead.Id)
                        .ToListAsync();
                }
                
                // Filter by product category
                if (!string.IsNullOrEmpty(filters.FilterProductCategory))
                {
                    filteredLeads = filteredLeads
                        .Where(l => l.PurchaseHistory.Any(p => p.Product?.Category == filters.FilterProductCategory))
                        .ToList();
                }
                
                // Filter by specific product
                if (filters.FilterProductId.HasValue)
                {
                    filteredLeads = filteredLeads
                        .Where(l => l.PurchaseHistory.Any(p => p.ProductId == filters.FilterProductId.Value))
                        .ToList();
                }
                
                // Filter by having purchase history
                if (filters.FilterHasPurchaseHistory.HasValue)
                {
                    filteredLeads = filteredLeads
                        .Where(l => filters.FilterHasPurchaseHistory.Value ? l.PurchaseHistory.Any() : !l.PurchaseHistory.Any())
                        .ToList();
                }
            }
            
            // Return the filtered leads as a partial view
            return PartialView("_LeadsList", new CampaignFilterViewModel
            {
                FilteredLeads = filteredLeads
            });
        }

        // POST: Campaigns/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Campaign campaign, List<int> SelectedProductIds, List<int> SelectedLeadIds, bool SaveAsTemplate, string TemplateName, string Subject)
        {
            // Remove TemplateName validation error if SaveAsTemplate is false
            if (!SaveAsTemplate && ModelState.ContainsKey("TemplateName"))
            {
                ModelState.Remove("TemplateName");
            }
            
            if (ModelState.IsValid)
            {
                try
                {
                    // Get current user (BO)
                    var userId = GetCurrentUserId();
                    var bo = await _context.Users.FindAsync(userId);

                    if (bo == null)
                    {
                        _logger.LogError("Business Owner not found for user ID {UserId}", userId);
                        return Json(new { success = false, errors = new[] { "Business Owner not found" } });
                    }

                    // Set campaign properties
                    campaign.CampaignAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
                        TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time"));
                    campaign.IsSent = false;
                    campaign.SenderId = userId;

                    // Store selected leads in TargetLeads field
                    if (SelectedLeadIds != null && SelectedLeadIds.Any())
                    {
                        campaign.TargetLeads = string.Join(",", SelectedLeadIds);
                    }

                    // Store selected products in TargetProducts field if provided
                    if (SelectedProductIds != null && SelectedProductIds.Any())
                    {
                        campaign.TargetProducts = string.Join(",", SelectedProductIds);
                    }

                    _context.Add(campaign);
                    await _context.SaveChangesAsync();
                    
                    // Save as template if requested
                    if (SaveAsTemplate && !string.IsNullOrWhiteSpace(TemplateName))
                    {
                        var template = new MessageTemplate
                        {
                            Name = TemplateName,
                            Subject = Subject ?? campaign.CampaignName,
                            Content = campaign.Message,
                            BOId = userId,
                            CreatedAt = DateTime.UtcNow
                        };
                        
                        _context.MessageTemplates.Add(template);
                        await _context.SaveChangesAsync();
                        
                        // Send template creation notification
                        try
                        {
                            string templateSubject = $"New Template Created: {TemplateName}";
                            string templateBody = $@"
                            <!DOCTYPE html>
                            <html>
                            <head>
                                <meta charset='utf-8'>
                                <meta name='viewport' content='width=device-width, initial-scale=1'>
                                <title>Template Created</title>
                                <style>
                                    body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                                    .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                                    .header {{ background-color: #4e73df; color: white; padding: 15px; text-align: center; }}
                                    .content {{ padding: 20px; border: 1px solid #ddd; }}
                                    .footer {{ text-align: center; margin-top: 20px; font-size: 12px; color: #777; }}
                                </style>
                            </head>
                            <body>
                                <div class='container'>
                                    <div class='header'>
                                        <h2>Email Template Created During Campaign</h2>
                                    </div>
                                    <div class='content'>
                                        <h3>Hello {bo.FirstName},</h3>
                                        <p>Your email template <strong>{TemplateName}</strong> has been created successfully while creating the campaign <strong>{campaign.CampaignName}</strong>.</p>
                                        <p><strong>Subject:</strong> {Subject ?? campaign.CampaignName}</p>
                                        <p>You can use this template for future campaigns to reach out to your leads.</p>
                                        <p>To view or edit your templates, visit the Templates section in your Campaign Manager.</p>
                                    </div>
                                    <div class='footer'>
                                        <p>© {DateTime.Now.Year} GrowSphere | This is an automated message.</p>
                                    </div>
                                </div>
                            </body>
                            </html>";
                            
                            await _emailService.SendEmailToBO(bo, templateSubject, templateBody, true);
                            _logger.LogInformation("Template creation notification sent to {Email}", bo.Email);
                        }
                        catch (Exception emailEx)
                        {
                            // Log but don't fail the operation
                            _logger.LogError(emailEx, "Error sending template creation notification to {Email}", bo.Email);
                        }
                    }

                    // Get recipient emails
                    List<string> recipientEmails = new List<string>();
                    List<string> duplicateEmails = new List<string>();
                    List<Leads> selectedLeads = new List<Leads>();
                    
                    // Handle specific targets
                    if (SelectedLeadIds != null && SelectedLeadIds.Any())
                    {
                        // If specific leads are selected, use those
                        selectedLeads = await _context.Leads
                            .Where(l => SelectedLeadIds.Contains(l.Id))
                            .ToListAsync();
                            
                        // Check for duplicate emails before applying Distinct()
                        var allEmails = selectedLeads.Select(l => l.LeadEmail).ToList();
                        var uniqueEmails = new HashSet<string>();
                        
                        foreach (var email in allEmails)
                        {
                            if (!uniqueEmails.Add(email))
                            {
                                duplicateEmails.Add(email);
                            }
                        }
                        
                        recipientEmails = uniqueEmails.ToList();
                    }
                    else if (!string.IsNullOrEmpty(campaign.TargetProducts))
                    {
                        // If products are selected, use leads interested in those products
                        var productIds = campaign.TargetProducts.Split(',');
                        selectedLeads = await _context.Leads
                            //.Where(l => l.BOId == userId && 
                            //    l.InterestedIn != null && 
                            //    productIds.Any(p => l.InterestedIn.Contains(p)))
                            .ToListAsync();
                            
                        // Check for duplicate emails before applying Distinct()
                        var allEmails = selectedLeads.Select(l => l.LeadEmail).ToList();
                        var uniqueEmails = new HashSet<string>();
                        
                        foreach (var email in allEmails)
                        {
                            if (!uniqueEmails.Add(email))
                            {
                                duplicateEmails.Add(email);
                            }
                        }
                        
                        recipientEmails = uniqueEmails.ToList();
                    }

                    _logger.LogInformation("Preparing to send {Count} emails for campaign {CampaignId} (Duplicates found: {DuplicateCount})",
                        recipientEmails.Count, campaign.Id, duplicateEmails.Count);

                    // Send emails
                    int sentCount = 0;
                    int failedCount = 0;
                    List<string> failedEmails = new List<string>();
                    
                    if (recipientEmails.Any())
                    {
                        foreach (var email in recipientEmails)
                        {
                            try
                            {
                                // Find the lead to personalize the message
                                var lead = selectedLeads.FirstOrDefault(l => l.LeadEmail == email) ?? 
                                    await _context.Leads.FirstOrDefaultAsync(l => l.LeadEmail == email);
                                
                                // Replace placeholders in subject and message
                                string personalizedSubject = _emailService.ReplacePlaceholders(
                                    campaign.Subject ?? campaign.CampaignName, 
                                    lead, 
                                    bo);
                                    
                                string personalizedMessage = _emailService.ReplacePlaceholders(
                                    campaign.Message ?? "no message", 
                                    lead, 
                                    bo);
                                
                                // Ensure HTML formatting by wrapping in proper HTML structure if not already
                                if (!personalizedMessage.Trim().StartsWith("<html") && 
                                    !personalizedMessage.Trim().StartsWith("<!DOCTYPE"))
                                {
                                    personalizedMessage = $@"
                                    <!DOCTYPE html>
                                    <html>
                                    <head>
                                        <meta charset='utf-8'>
                                        <meta name='viewport' content='width=device-width, initial-scale=1'>
                                        <title>{personalizedSubject}</title>
                                    </head>
                                    <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                                        {personalizedMessage}
                                    </body>
                                    </html>";
                                }
                                
                                _logger.LogDebug("Sending email to {Email}", email);
                                await _emailService.SendEmailWithTracking(
                                    bo.Email,
                                    bo.BusinessName ?? $"{bo.FirstName} {bo.LastName}",
                                    email,
                                    personalizedSubject,
                                    personalizedMessage,
                                    campaign.Id.ToString(),
                                    true);

                                if (lead != null)
                                {
                                    lead.LastContacted = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, 
                                        TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time"));
                                    //lead.Status = Leads.LeadStatus.Contacted;
                                    _context.Update(lead);
                                    await _context.SaveChangesAsync();
                                }
                                
                                sentCount++;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error sending email to {Email}", email);
                                failedCount++;
                                failedEmails.Add(email);
                                // Continue with next email even if one fails
                            }
                        }

                        campaign.IsSent = true;
                        await _context.SaveChangesAsync();
                    }

                    // Send campaign creation confirmation email to business owner
                    try
                    {
                        // Get template information if a template was used
                        string templateInfo = "";
                        if (Request.Form.ContainsKey("SelectedTemplateId") && !string.IsNullOrEmpty(Request.Form["SelectedTemplateId"]))
                        {
                            if (int.TryParse(Request.Form["SelectedTemplateId"], out int templateId))
                            {
                                var usedTemplate = await _context.MessageTemplates.FindAsync(templateId);
                                if (usedTemplate != null)
                                {
                                    templateInfo = $"<p>Based on template: <strong>{usedTemplate.Name}</strong></p>";
                                }
                            }
                        }
                        
                        string campaignSubject = $"Campaign Created: {campaign.CampaignName}";
                        string campaignBody = $@"
                        <!DOCTYPE html>
                        <html>
                        <head>
                            <meta charset='utf-8'>
                            <meta name='viewport' content='width=device-width, initial-scale=1'>
                            <title>Campaign Created</title>
                            <style>
                                body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                                .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                                .header {{ background-color: #4e73df; color: white; padding: 15px; text-align: center; }}
                                .content {{ padding: 20px; border: 1px solid #ddd; }}
                                .stats {{ background-color: #f8f9fc; padding: 15px; margin: 15px 0; border-radius: 5px; }}
                                .footer {{ text-align: center; margin-top: 20px; font-size: 12px; color: #777; }}
                            </style>
                        </head>
                        <body>
                            <div class='container'>
                                <div class='header'>
                                    <h2>Campaign Created Successfully</h2>
                                </div>
                                <div class='content'>
                                    <h3>Hello {bo.FirstName},</h3>
                                    <p>Your campaign <strong>{campaign.CampaignName}</strong> has been created and sent successfully.</p>
                                    <p><strong>Subject:</strong> {campaign.Subject ?? campaign.CampaignName}</p>
                                    {templateInfo}
                                    
                                    <div class='stats'>
                                        <h4>Campaign Statistics:</h4>
                                        <p><strong>Total Recipients:</strong> {recipientEmails.Count}</p>
                                        <p><strong>Successfully Sent:</strong> {sentCount}</p>
                                        <p><strong>Failed:</strong> {failedCount}</p>
                                        <p><strong>Duplicate Emails Removed:</strong> {duplicateEmails.Count}</p>
                                    </div>
                                    
                                    <p>You can track the performance of your campaign in the Campaign Manager.</p>
                                </div>
                                <div class='footer'>
                                    <p>© {DateTime.Now.Year} GrowSphere | This is an automated message.</p>
                                </div>
                            </div>
                        </body>
                        </html>";
                        
                        await _emailService.SendEmailToBO(bo, campaignSubject, campaignBody, true);
                        _logger.LogInformation("Campaign creation confirmation email sent to {Email}", bo.Email);
                    }
                    catch (Exception emailEx)
                    {
                        // Log but don't fail the operation
                        _logger.LogError(emailEx, "Error sending campaign creation confirmation to {Email}", bo.Email);
                    }

                    return Json(new { 
                        success = true, 
                        totalRecipients = recipientEmails.Count,
                        sentCount = sentCount,
                        failedCount = failedCount,
                        failedEmails = failedEmails,
                        duplicateCount = duplicateEmails.Count,
                        duplicateEmails = duplicateEmails
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating campaign");
                    return Json(new { success = false, errors = new[] { ex.Message } });
                }
            }

            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return Json(new { success = false, errors });
        }

        // GET: Campaigns/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = GetCurrentUserId();
            var campaign = await _context.Campaigns
                .Include(c => c.Sender)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (campaign == null)
            {
                return NotFound();
            }

            ViewBag.Products = await _context.Products2
                .Where(p => p.BOId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
            ViewBag.Leads = await _context.Leads
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();
            return View(campaign);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Campaign campaign, List<int> SelectedLeadIds)
        {
            if (id != campaign.Id)
            {
                return Json(new { success = false, errors = new[] { "Invalid campaign ID" } });
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingCampaign = await _context.Campaigns.FindAsync(id);
                    if (existingCampaign == null)
                    {
                        return Json(new { success = false, errors = new[] { "Campaign not found" } });
                    }

                    // Update campaign properties
                    existingCampaign.CampaignName = campaign.CampaignName;
                    existingCampaign.Subject = campaign.Subject;
                    existingCampaign.Message = campaign.Message;
                    existingCampaign.TargetProducts = campaign.TargetProducts;
                    existingCampaign.Notes = campaign.Notes;
                    existingCampaign.UpdatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, 
                        TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time"));
                    
                    // Save selected leads
                    if (SelectedLeadIds != null && SelectedLeadIds.Any())
                    {
                        existingCampaign.TargetLeads = string.Join(",", SelectedLeadIds);
                    }

                    _context.Update(existingCampaign);
                    await _context.SaveChangesAsync();

                    return Json(new { success = true });
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogError(ex, "Concurrency error updating campaign");
                    return Json(new { success = false, errors = new[] { "Concurrency error. Please refresh and try again." } });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating campaign");
                    return Json(new { success = false, errors = new[] { ex.Message } });
                }
            }

            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return Json(new { success = false, errors });
        }

        [HttpPost("Campaigns/Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var campaign = await _context.Campaigns.FindAsync(id);
            if (campaign == null)
            {
                _logger.LogError("campaign Not Found");
                return NotFound();
            }

            try
            {
                _context.Campaigns.Remove(campaign);
                await _context.SaveChangesAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting campaign");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Resend(int id, List<int> SelectedLeadIds, string CampaignName = null, string Message = null, string Subject = null)
        {
            try
            {
                var campaign = await _context.Campaigns
                    .Include(c => c.Sender)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (campaign == null)
                {
                    return Json(new { success = false, message = "Campaign not found." });
                }
                
                // Update campaign properties if provided
                bool hasChanges = false;
                
                if (!string.IsNullOrEmpty(CampaignName))
                {
                    campaign.CampaignName = CampaignName;
                    hasChanges = true;
                }
                
                if (!string.IsNullOrEmpty(Subject))
                {
                    campaign.Subject = Subject;
                    hasChanges = true;
                }
                
                if (!string.IsNullOrEmpty(Message))
                {
                    campaign.Message = Message;
                    hasChanges = true;
                }
                
                // Update target leads if provided
                if (SelectedLeadIds != null && SelectedLeadIds.Any())
                {
                    campaign.TargetLeads = string.Join(",", SelectedLeadIds);
                    hasChanges = true;
                }
                
                // Save changes to database
                if (hasChanges)
                {
                    campaign.UpdatedAt = DateTime.UtcNow;
                    _context.Update(campaign);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Campaign {Id} updated before resending", campaign.Id);
                }

                var recipients = new List<string>();
                var duplicateEmails = new List<string>();
                var selectedLeads = new List<Leads>();

                // Get recipients based on selected leads or products
                if (SelectedLeadIds != null && SelectedLeadIds.Any())
                {
                    // Use the newly selected leads for this resend
                    selectedLeads = await _context.Leads
                        .Where(l => SelectedLeadIds.Contains(l.Id))
                        .ToListAsync();
                    
                    // Check for duplicate emails
                    var allEmails = selectedLeads.Select(l => l.LeadEmail).ToList();
                    var uniqueEmails = new HashSet<string>();
                    
                    foreach (var email in allEmails)
                    {
                        if (!uniqueEmails.Add(email))
                        {
                            duplicateEmails.Add(email);
                        }
                    }
                    
                    recipients.AddRange(uniqueEmails);
                }
                else if (!string.IsNullOrEmpty(campaign.TargetLeads))
                {
                    var leadIds = campaign.TargetLeads.Split(',');
                    selectedLeads = await _context.Leads
                        .Where(l => leadIds.Contains(l.Id.ToString()))
                        .ToListAsync();
                    
                    // Check for duplicate emails
                    var allEmails = selectedLeads.Select(l => l.LeadEmail).ToList();
                    var uniqueEmails = new HashSet<string>();
                    
                    foreach (var email in allEmails)
                    {
                        if (!uniqueEmails.Add(email))
                        {
                            duplicateEmails.Add(email);
                        }
                    }
                    
                    recipients.AddRange(uniqueEmails);
                }
                else if (!string.IsNullOrEmpty(campaign.TargetProducts))
                {
                    // If products are selected, use leads interested in those products
                    var productIds = campaign.TargetProducts.Split(',');
                    selectedLeads = await _context.Leads
                        //.Where(l => l.BOId == campaign.SenderId && 
                            //l.InterestedIn != null && 
                            //productIds.Any(p => l.InterestedIn.Contains(p)))
                        .ToListAsync();
                    
                    // Check for duplicate emails
                    var allEmails = selectedLeads.Select(l => l.LeadEmail).ToList();
                    var uniqueEmails = new HashSet<string>();
                    
                    foreach (var email in allEmails)
                    {
                        if (!uniqueEmails.Add(email))
                        {
                            duplicateEmails.Add(email);
                        }
                    }
                    
                    recipients.AddRange(uniqueEmails);
                }

                if (!recipients.Any())
                {
                    return Json(new { success = false, message = "No recipients found for this campaign." });
                }

                // Get the sender (BO)
                var bo = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == campaign.SenderId);

                // Send emails
                foreach (var recipient in recipients)
                {
                    try
                    {
                        // Find the lead to personalize the message
                        var lead = selectedLeads.FirstOrDefault(l => l.LeadEmail == recipient) ??
                            await _context.Leads.FirstOrDefaultAsync(l => l.LeadEmail == recipient);
                            
                        // Replace placeholders in subject and message
                        string personalizedSubject = _emailService.ReplacePlaceholders(
                            campaign.Subject ?? campaign.CampaignName, 
                            lead, 
                            bo);
                            
                        string personalizedMessage = _emailService.ReplacePlaceholders(
                            campaign.Message, 
                            lead, 
                            bo);
                        
                        // Ensure HTML formatting by wrapping in proper HTML structure if not already
                        if (!personalizedMessage.Trim().StartsWith("<html") && 
                            !personalizedMessage.Trim().StartsWith("<!DOCTYPE"))
                        {
                            personalizedMessage = $@"
                            <!DOCTYPE html>
                            <html>
                            <head>
                                <meta charset='utf-8'>
                                <meta name='viewport' content='width=device-width, initial-scale=1'>
                                <title>{personalizedSubject}</title>
                            </head>
                            <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                                {personalizedMessage}
                            </body>
                            </html>";
                        }
                        
                        await _emailService.SendEmailWithTracking(
                            bo.Email,
                            bo.BusinessName ?? $"{bo.FirstName} {bo.LastName}",
                            recipient,
                            personalizedSubject,
                            personalizedMessage,
                            campaign.Id.ToString(),
                            true
                        );

                        // Update lead status if applicable
                        if (lead != null)
                        {
                            //lead.Status = Leads.LeadStatus.Contacted;
                            lead.LastContacted = DateTime.UtcNow;
                            _context.Update(lead);
                            await _context.SaveChangesAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the error but continue with other recipients
                        _logger.LogError($"Error sending email to {recipient}: {ex.Message}");
                    }
                }

                // Update campaign status and resend timestamp
                campaign.IsSent = true;
                campaign.ResendAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                int sentCount = recipients.Count;
                int failedCount = 0;
                
                return Json(new { 
                    success = true, 
                    message = "Campaign resent successfully.",
                    totalRecipients = recipients.Count,
                    sentCount = sentCount,
                    failedCount = failedCount,
                    duplicateCount = duplicateEmails.Count,
                    duplicateEmails = duplicateEmails
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error resending campaign: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while resending the campaign." });
            }
        }

        private bool CampaignExists(int id)
        {
            return _context.Campaign.Any(e => e.Id == id);
        }

        [HttpPost("track/reply")]
        public IActionResult TrackReply([FromBody] EmailReplyDto reply)
        {
            try
            {
                _logger.LogInformation("Reply received for message {MessageId} from {Sender} at {ReplyDate}",
                    reply.InReplyTo, reply.From, reply.Date);

                _logger.LogInformation("Reply content: {Subject}\n{Body}",
                    reply.Subject, reply.Body);

                // You can add additional processing here later
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing email reply");
                return StatusCode(500);
            }
        }

        // GET: Campaigns/DetailsPartial/5
        public async Task<IActionResult> DetailsPartial(int id)
        {
            var campaign = await _context.Campaigns
                .Include(c => c.Sender)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (campaign == null)
            {
                return NotFound();
            }

            // Load the necessary data for the partial view without filtering
            ViewBag.Products = await _context.Products2.ToListAsync();
            ViewBag.Leads = await _context.Leads.Where(l => l.Status != Leads.LeadStatus.Deleted).ToListAsync();

            return PartialView("_DetailsPartial", campaign);
        }

        // POST: GetAllLeads
        [HttpPost]
        public async Task<IActionResult> GetAllLeads()
        {
            // Get all leads without filtering by creator, but exclude deleted leads
            var allLeads = await _context.Leads
                .Include(l => l.CreatedBy)
                .Where(l => l.Status != Leads.LeadStatus.Deleted)
                .ToListAsync();

            // Load purchase history for each lead
            foreach (var lead in allLeads)
            {
                lead.PurchaseHistory = await _context.SaleItems
                    .Include(si => si.Sale)
                    .Include(si => si.Product)
                    .Where(si => si.Sale.LeadId == lead.Id)
                    .ToListAsync();
            }

            // Return the leads as a partial view with the correct view model
            return PartialView("_LeadsList", new CampaignFilterViewModel
            {
                FilteredLeads = allLeads
            });
        }

        // GET: Direct SQL query for templates
        [HttpGet]
        public async Task<IActionResult> GetAllTemplates()
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("GetAllTemplates called for user ID: {UserId}", userId);
                
                // Direct SQL query to bypass EF Core
                var templates = new List<MessageTemplate>();
                
                // Use raw SQL to retrieve templates
                using (var command = _context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = "SELECT Id, Name, Subject, Content, IsDefault, CreatedAt, UpdatedAt, BOId FROM MessageTemplates WHERE BOId = @userId";
                    
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = "@userId";
                    parameter.Value = userId;
                    command.Parameters.Add(parameter);
                    
                    // Ensure connection is open
                    if (command.Connection.State != System.Data.ConnectionState.Open)
                    {
                        await command.Connection.OpenAsync();
                    }
                    
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        _logger.LogInformation("SQL query executed successfully, reading results");
                        while (await reader.ReadAsync())
                        {
                            var template = new MessageTemplate
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                Subject = reader.GetString(2),
                                Content = reader.GetString(3),
                                IsDefault = reader.GetBoolean(4),
                                CreatedAt = reader.GetDateTime(5),
                                UpdatedAt = reader.IsDBNull(6) ? null : (DateTime?)reader.GetDateTime(6),
                                BOId = reader.GetInt32(7)
                            };
                            
                            templates.Add(template);
                            _logger.LogInformation("Found template in DB: Id={Id}, Name={Name}, BOId={BOId}", 
                                template.Id, template.Name, template.BOId);
                        }
                    }
                }
                
                return Json(new { success = true, templates });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving templates");
                return Json(new { success = false, error = ex.Message });
            }
        }

        // GET: Campaigns/GetTemplateImages
        [HttpGet]
        public IActionResult GetTemplateImages()
        {
            try
            {
                var userId = GetCurrentUserId();
                
                // In a real implementation, you would fetch images from a database or storage service
                // For this demo, we'll return mock data
                var images = new List<object>
                {
                    new { id = 1, name = "Header Image", url = "/images/templates/header.jpg" },
                    new { id = 2, name = "Product Banner", url = "/images/templates/product.jpg" },
                    new { id = 3, name = "Logo", url = "/images/templates/logo.png" },
                    new { id = 4, name = "Background Pattern", url = "/images/templates/pattern.jpg" }
                };
                
                return Json(images);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting template images");
                return Json(new List<object>());
            }
        }

        // POST: Campaigns/UploadTemplateImage
        [HttpPost]
        public async Task<IActionResult> UploadTemplateImage()
        {
            try
            {
                var userId = GetCurrentUserId();
                var file = Request.Form.Files["image"];
                
                if (file == null || file.Length == 0)
                {
                    return Json(new { success = false, message = "No file uploaded" });
                }
                
                // In a real implementation, you would:
                // 1. Validate the file (size, type, etc.)
                // 2. Generate a unique filename
                // 3. Save the file to disk or cloud storage
                // 4. Save metadata to database
                
                // For this demo, we'll simulate a successful upload
                // In a real app, you would save the file and return the actual URL
                
                // Create a mock response
                var image = new 
                { 
                    id = new Random().Next(100, 999), // Random ID for demo purposes
                    name = Path.GetFileName(file.FileName),
                    url = $"/images/templates/uploaded_{Path.GetFileName(file.FileName)}"
                };
                
                return Json(new { success = true, image });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading template image");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: Campaigns/CreateSampleTemplate
        public async Task<IActionResult> CreateSampleTemplate()
        {
            try
            {
                var userId = GetCurrentUserId();
                
                // Check if the user already has a welcome template
                var existingTemplate = await _context.MessageTemplates
                    .FirstOrDefaultAsync(t => t.BOId == userId && t.Name.Contains("Welcome"));
                    
                if (existingTemplate != null)
                {
                    TempData["WarningMessage"] = "You already have a welcome template. Check your templates list.";
                    return RedirectToAction(nameof(Templates));
                }
                
                // Create a sample welcome template
                var template = new MessageTemplate
                {
                    Name = "Welcome to Our Business",
                    Subject = "Welcome to {{BusinessName}} – Let's Build Together!",
                    BOId = userId,
                    CreatedAt = DateTime.UtcNow,
                    Content = @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <title>Welcome to {{BusinessName}}</title>
    <style>
        body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0; }
        .container { max-width: 600px; margin: 0 auto; padding: 20px; }
        .header { background-color: #4e73df; color: white; padding: 20px; text-align: center; }
        .content { padding: 20px; background-color: #ffffff; }
        .footer { text-align: center; padding: 20px; font-size: 12px; color: #777; }
        .button { display: inline-block; background-color: #4e73df; color: white; text-decoration: none; padding: 10px 20px; border-radius: 5px; font-weight: bold; }
        .social-links { text-align: center; margin: 20px 0; }
        .social-links a { display: inline-block; margin: 0 10px; text-decoration: none; }
        .social-links img { border: none; width: 32px; height: 32px; }
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Welcome to {{BusinessName}}</h1>
        </div>
        <div class='content'>
            <h2>Hello {{LeadName}},</h2>
            
            <p>Thank you for your interest in our services. We're excited to help you grow your business and achieve your goals.</p>
            
            <p>Our team will contact you shortly at {{LeadEmail}} to discuss how we can best meet your needs.</p>
            
            <div style='text-align: center; margin: 30px 0;'>
                <a href='#' class='button'>Schedule a Consultation</a>
            </div>
            
            <p>If you have any questions, feel free to reply to this email or call us directly.</p>
            
            <p>Best regards,<br>
            {{BusinessOwnerName}}<br>
            {{BusinessName}}</p>
        </div>
        
        <div class='social-links'>
            <a href='#' target='_blank'>
                <img src='https://cdn4.iconfinder.com/data/icons/social-media-logos-6/512/83-facebook-512.png' alt='Facebook' width='32' height='32'>
            </a>
            <a href='#' target='_blank'>
                <img src='https://cdn4.iconfinder.com/data/icons/social-media-logos-6/512/36-twitter_social-512.png' alt='Twitter' width='32' height='32'>
            </a>
            <a href='#' target='_blank'>
                <img src='https://cdn4.iconfinder.com/data/icons/social-media-logos-6/512/56-linkedin-512.png' alt='LinkedIn' width='32' height='32'>
            </a>
            <a href='#' target='_blank'>
                <img src='https://cdn4.iconfinder.com/data/icons/social-media-logos-6/512/64-instagram-512.png' alt='Instagram' width='32' height='32'>
            </a>
        </div>
        
        <div class='footer'>
            <p>© {{BusinessName}} | {{CurrentDate}}</p>
            <p><a href='#'>Unsubscribe</a> | <a href='#'>Privacy Policy</a></p>
        </div>
    </div>
</body>
</html>"
                };
                
                _context.MessageTemplates.Add(template);
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = "Sample welcome template created successfully! You can now edit it to match your business needs.";
                return RedirectToAction(nameof(EditTemplate), new { id = template.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating sample template");
                TempData["ErrorMessage"] = "Error creating sample template: " + ex.Message;
                return RedirectToAction(nameof(Templates));
            }
        }
    }
}


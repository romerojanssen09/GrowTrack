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
            return View(await _context.Campaigns.ToListAsync());
        }

        public class CampaignViewModel
        {
            public bool SendToAll { get; set; }
            public string? TargetProducts { get; set; }
            public List<string>? TargetProductNames { get; set; }
        }


        // GET: Campaigns/GetProducts
        public IActionResult GetProducts()
        {
            var products = _context.Products2.Select(p => new {
                id = p.Id,
                name = p.ProductName
            }).ToList();
            return Json(products);
        }

        // GET: Campaigns/Create
        //public IActionResult Create()
        //{
        //    return View();
        //}

        // POST: Campaigns/Create
        [HttpPost("Campaigns/Create/")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Campaign campaign)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Get current user (BO)
                    var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
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

                    _context.Add(campaign);
                    await _context.SaveChangesAsync();

                    // Get recipient emails
                    List<string> recipientEmails;
                    if (campaign.SendToAll)
                    {
                        recipientEmails = await _context.Leads
                            .Select(l => l.LeadEmail)
                            .Distinct()
                            .ToListAsync();
                    }
                    else
                    {
                        var productIds = campaign.TargetProducts?.Split(',') ?? Array.Empty<string>();
                        recipientEmails = await _context.Leads
                            .Where(l => productIds.Any(p => l.InterestedIn.Contains(p)))
                            .Select(l => l.LeadEmail)
                            .Distinct()
                            .ToListAsync();
                    }

                    _logger.LogInformation("Preparing to send {Count} emails for campaign {CampaignId}",
                        recipientEmails.Count, campaign.Id);

                    // Send emails
                    if (recipientEmails.Any())
                    {
                        foreach (var email in recipientEmails)
                        {
                            try
                            {
                                _logger.LogDebug("Sending email to {Email}", email);
                                await _emailService.SendEmailWithTracking(
                                    bo.Email,
                                    $"{bo.FirstName} {bo.LastName}",
                                    email,
                                    campaign.CampaignName ?? "New Campaign",
                                    campaign.Message ?? "no message",
                                    campaign.Id.ToString(),
                                    true);

                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error sending email to {Email}", email);
                                // Continue with next email even if one fails
                            }
                        }

                        campaign.IsSent = true;
                        await _context.SaveChangesAsync();
                    }

                    return Json(new { success = true });
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

            var campaign = await _context.Campaigns
                .Include(c => c.Sender)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (campaign == null)
            {
                return NotFound();
            }

            ViewBag.Products = await _context.Products2.ToListAsync();
            return View(campaign);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [FromForm] CampaignEditModel model)
        {
            if (id != model.Id)
            {
                return Json(new { success = false, errors = new[] { "ID mismatch" } });
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

                    existingCampaign.CampaignName = model.CampaignName;
                    existingCampaign.Message = model.Message;
                    existingCampaign.TargetProducts = model.SendToAll ? null : model.TargetProducts;
                    existingCampaign.SendToAll = model.SendToAll;
                    existingCampaign.Notes = model.Notes;
                    existingCampaign.UpdatedAt = DateTime.Now;

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

        public async Task<IActionResult> EditPartial(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var campaign = await _context.Campaigns
                .Include(c => c.Sender)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (campaign == null)
            {
                return NotFound();
            }

            ViewBag.Products = await _context.Products2.ToListAsync();
            return PartialView("_EditPartial", campaign);
        }

        // Add this new action for partial details
        public async Task<IActionResult> DetailsPartial(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var campaign = await _context.Campaigns
                .Include(c => c.Sender)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (campaign == null)
            {
                return NotFound();
            }
            ViewBag.Products = await _context.Products2.ToListAsync();
            return PartialView("_DetailsPartial", campaign);
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resend(int id)
        {
            var campaign = await _context.Campaigns
                .Include(c => c.Sender)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (campaign == null)
            {
                return NotFound();
            }

            try
            {
                // Get recipient emails
                List<string> recipientEmails;
                if (campaign.SendToAll)
                {
                    recipientEmails = await _context.Leads
                        .Select(l => l.LeadEmail)
                        .Distinct()
                        .ToListAsync();
                }
                else
                {
                    var productIds = campaign.TargetProducts?.Split(',') ?? Array.Empty<string>();
                    recipientEmails = await _context.Leads
                        .Where(l => productIds.Any(p => l.InterestedIn.Contains(p)))
                        .Select(l => l.LeadEmail)
                        .Distinct()
                        .ToListAsync();
                }

                // Send emails
                if (recipientEmails.Any())
                {
                    foreach (var email in recipientEmails)
                    {
                        // In both Create and Resend actions, update the SendEmailWithTracking calls:
                        await _emailService.SendEmailWithTracking(
                            campaign.Sender.Email, // senderEmail
                            $"{campaign.Sender.FirstName} {campaign.Sender.LastName}",
                            email, // receiver
                            campaign.CampaignName ?? "New Campaign", // subject
                            campaign.Message ?? "no message", // body
                            campaign.Id.ToString(), // campaignId
                            true); // isBodyHtml
                    }
                }

                TempData["SuccessMessage"] = "Campaign resent successfully to all recipients";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error resending campaign: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
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
    }
}

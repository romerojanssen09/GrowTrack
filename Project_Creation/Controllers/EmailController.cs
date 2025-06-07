using System.Net.Mail;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Project_Creation.Data;
using Project_Creation.Models.Entities;
using System.Text.RegularExpressions;

namespace Project_Creation.Controllers
{
    [Route("api/emails")]
    [ApiController]
    public class EmailController : Controller
    {
        private readonly IEmailService emailService;
        public EmailController(IEmailService emailService)
        {
            this.emailService = emailService;
        }

        [HttpPost]
        public async Task<IActionResult> SendEmail(string receiver, string subject, string body, bool isBodyHtml = false)
        {
            if (string.IsNullOrEmpty(receiver) || string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(body))
            {
                return BadRequest(new { success = false, message = "Invalid email data" });
            }
            try
            {
                await emailService.SendEmail(receiver, subject, body, isBodyHtml);
                return Ok(new { success = true, message = "Email sent successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }

    public class HardcodedEmailService : IEmailService
    {
        private readonly ILogger<HardcodedEmailService> _logger;
        private readonly IConfiguration _configuration;
        private readonly AuthDbContext _context;

        public HardcodedEmailService(ILogger<HardcodedEmailService> logger, IConfiguration configuration, AuthDbContext context)
        {
            _logger = logger;
            _configuration = configuration;
            _context = context;
        }

        public string ReplacePlaceholders(string content, Leads lead, Users businessOwner)
        {
            if (string.IsNullOrEmpty(content))
                return content;
            
            var result = content;
            
            // Replace lead placeholders
            if (lead != null)
            {
                result = result.Replace("{{LeadName}}", lead.LeadName ?? "Customer");
                result = result.Replace("{{LeadEmail}}", lead.LeadEmail ?? "");
                result = result.Replace("{{LeadPhone}}", lead.LeadPhone ?? "");
                
                // Format date placeholders
                if (lead.LastPurchaseDate.HasValue)
                {
                    result = result.Replace("{{LastPurchaseDate}}", lead.LastPurchaseDate.Value.ToString("MMM dd, yyyy"));
                }
                else
                {
                    result = result.Replace("{{LastPurchaseDate}}", "N/A");
                }
                
                if (lead.LastContacted.HasValue)
                {
                    result = result.Replace("{{LastContacted}}", lead.LastContacted.Value.ToString("MMM dd, yyyy"));
                }
                else
                {
                    result = result.Replace("{{LastContacted}}", "N/A");
                }
            }
            
            // Replace business owner placeholders
            if (businessOwner != null)
            {
                result = result.Replace("{{BusinessName}}", businessOwner.BusinessName ?? "Our Business");
                result = result.Replace("{{BusinessOwnerName}}", $"{businessOwner.FirstName} {businessOwner.LastName}".Trim());
                result = result.Replace("{{BusinessEmail}}", businessOwner.Email ?? "");
                result = result.Replace("{{BusinessPhone}}", businessOwner.PhoneNumber ?? "");
            }
            
            // Replace product placeholders
            if (businessOwner != null)
            {
                try
                {
                    // Match all product placeholders like {{Product:123}}
                    var regex = new Regex(@"\{\{Product:(\d+)\}\}");
                    var matches = regex.Matches(result);
                    
                    if (matches.Count > 0)
                    {
                        foreach (Match match in matches)
                        {
                            if (match.Groups.Count > 1 && int.TryParse(match.Groups[1].Value, out int productId))
                            {
                                var product = _context.Products2
                                    .FirstOrDefault(p => p.Id == productId && p.BOId == businessOwner.Id);
                                
                                if (product != null)
                                {
                                    var productInfo = $"{product.ProductName} - {product.SellingPrice:C}";
                                    result = result.Replace(match.Value, productInfo);
                                }
                                else
                                {
                                    // Product not found, replace with empty string
                                    result = result.Replace(match.Value, "[Product not found]");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log the error but don't throw - just continue with other replacements
                    _logger.LogError($"Error replacing product placeholders: {ex.Message}");
                }
            }
            
            return result;
        }

        public async Task SendEmailWithTracking(string senderEmail, string senderName, string receiver, string subject, string body, string campaignId, bool isBodyHtml = false)
        {
            try
            {
                var emailConfig = _configuration.GetSection("EMAIL_CONFIGURATION");
                var smtpHost = emailConfig["HOST"];
                var smtpPort = emailConfig.GetValue<int>("PORT");
                var smtpUsername = emailConfig["EMAIL"];
                var smtpPassword = emailConfig["PASSWORD"];

                using (var message = new MailMessage())
                {
                    message.From = new MailAddress(senderEmail, senderName);
                    message.To.Add(receiver);
                    message.Subject = subject;
                    message.Body = body;
                    message.IsBodyHtml = isBodyHtml;
                    message.Headers.Add("X-Campaign-ID", campaignId);

                    using (var client = new SmtpClient(smtpHost, smtpPort))
                    {
                        client.EnableSsl = true;
                        client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);

                        await client.SendMailAsync(message);
                        _logger.LogInformation($"Email sent from {senderEmail} to {receiver}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email from {Sender} to {Receiver}", senderEmail, receiver);
                throw;
            }
        }

        public async Task SendEmail2(string senderEmail, string senderName, string receiver, string subject, string body, bool isBodyHtml = false)
        {
            try
            {
                var emailConfig = _configuration.GetSection("EMAIL_CONFIGURATION");
                var smtpHost = emailConfig["HOST"];
                var smtpPort = emailConfig.GetValue<int>("PORT");
                var smtpUsername = emailConfig["EMAIL"];
                var smtpPassword = emailConfig["PASSWORD"];

                using (var message = new MailMessage())
                {
                    message.From = new MailAddress(senderEmail, senderName);
                    message.To.Add(receiver);
                    message.Subject = subject;
                    message.Body = body;
                    message.IsBodyHtml = isBodyHtml;

                    using (var client = new SmtpClient(smtpHost, smtpPort))
                    {
                        client.EnableSsl = true;
                        client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);

                        await client.SendMailAsync(message);
                        _logger.LogInformation($"Email sent from {senderEmail} to {receiver}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email from {Sender} to {Receiver}", senderEmail, receiver);
                throw;
            }
        }

        public async Task SendEmail(string receiver, string subject, string body, bool isBodyHtml = false)
        {
            try
            {
                var emailConfig = _configuration.GetSection("EMAIL_CONFIGURATION");
                var senderEmail = emailConfig["EMAIL"];
                var senderPassword = emailConfig["PASSWORD"];
                var smtpHost = emailConfig["HOST"];
                var smtpPort = emailConfig.GetValue<int>("PORT");

                using (var message = new MailMessage(senderEmail, receiver))
                {
                    message.Subject = subject;
                    message.Body = body;
                    message.IsBodyHtml = isBodyHtml; // Now configurable

                    using (var client = new SmtpClient(smtpHost, smtpPort))
                    {
                        client.EnableSsl = true;
                        client.Credentials = new NetworkCredential(senderEmail, senderPassword);

                        await client.SendMailAsync(message);
                        _logger.LogInformation($"Email sent to {receiver}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email");
                throw; // Re-throw to handle in controller
            }
        }

        public async Task SendEmailToBO(Users receiver, string subject, string body, bool isBodyHtml = false)
        {
            try
            {
                var emailConfig = _configuration.GetSection("EMAIL_CONFIGURATION");
                var senderEmail = emailConfig["EMAIL"];
                var senderPassword = emailConfig["PASSWORD"];
                var smtpHost = emailConfig["HOST"];
                var smtpPort = emailConfig.GetValue<int>("PORT");

                using (var message = new MailMessage(senderEmail, receiver.Email))
                {
                    message.Subject = subject;
                    message.Body = body;
                    message.IsBodyHtml = isBodyHtml;

                    using (var client = new SmtpClient(smtpHost, smtpPort))
                    {
                        client.EnableSsl = true;
                        client.Credentials = new NetworkCredential(senderEmail, senderPassword);

                        await client.SendMailAsync(message);
                        _logger.LogInformation($"Email sent to {receiver}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email");
                throw; // Re-throw to handle in controller
            }
        }
    }
}
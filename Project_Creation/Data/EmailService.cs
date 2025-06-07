using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Project_Creation.Models.Entities;
using System.Web;

namespace Project_Creation.Data
{
    public interface IEmailService
    {
        Task SendEmail(string receiver, string subject, string body, bool isBodyHtml = false); // system
        Task SendEmailWithTracking(string senderEmail, string senderName, string receiver, string subject, string body, string campaignId, bool isBodyHtml = false);
        Task SendEmail2(string senderEmail, string senderName, string receiver, string subject, string body, bool isBodyHtml = false);
        Task SendEmailToBO(Users receiver, string subject, string body, bool isBodyHtml = false);
        string ReplacePlaceholders(string content, Leads lead, Users businessOwner);
    }

    public interface ICampaignTracker
    {
        Task MarkAsReplied(string messageId, DateTime replyDate);
        Task MarkAsOpened(string campaignId, string recipient);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly ICampaignTracker _campaignTracker;
        private readonly AuthDbContext _context;

        public EmailService(
            IConfiguration configuration, 
            ILogger<EmailService> logger,
            ICampaignTracker campaignTracker,
            AuthDbContext context)
        {
            _configuration = configuration;
            _logger = logger;
            _campaignTracker = campaignTracker;
            _context = context;
        }

        public async Task SendEmailWithTracking(
    string senderEmail,
    string senderName,
    string receiver,
    string subject,
    string body,
    string campaignId,
    bool isBodyHtml = false)
        {
            try
            {
                // Generate a unique message ID using your domain
                string messageId = $"<{Guid.NewGuid()}@yourdomain.com>";

                // Validate tracking base URL
                if (!Uri.TryCreate(_configuration["Tracking:BaseUrl"], UriKind.Absolute, out var trackingUri))
                {
                    throw new ConfigurationException("Invalid Tracking:BaseUrl configuration");
                }

                // Add tracking pixel to body without HTML encoding the body
                string trackedBody = body +
                    $"<img src='{trackingUri}track/open/{campaignId}?email={WebUtility.UrlEncode(receiver)}' style='display:none'/>";

                _logger.LogInformation("Sending tracked email for campaign {CampaignId} to {Receiver}",
                    campaignId, receiver);

                var emailConfig = _configuration.GetSection("EMAIL_CONFIGURATION");
                using var smtpClient = new SmtpClient(emailConfig["HOST"], emailConfig.GetValue<int>("PORT"))
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(emailConfig["EMAIL"], emailConfig["PASSWORD"]),
                    DeliveryMethod = SmtpDeliveryMethod.Network
                };

                using var message = new MailMessage
                {
                    From = new MailAddress(senderEmail, senderName),
                    Subject = subject,
                    Body = trackedBody,
                    IsBodyHtml = true // Force HTML for tracking pixel
                };
                message.To.Add(receiver);
                message.Headers.Add("Message-ID", messageId);
                message.Headers.Add("X-Campaign-ID", campaignId);

                await smtpClient.SendMailAsync(message);
                _logger.LogInformation("Email sent successfully to {Receiver}", receiver);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Receiver}", receiver);
                throw;
            }
        }

        public async Task SendEmail(string receiver, string subject, string body, bool isBodyHtml = false)
        {
            await SendEmailInternal(receiver, subject, body, isBodyHtml);
        }

        public async Task SendEmailWithTracking(string receiver, string subject, string body, string campaignId, bool isBodyHtml = false)
        {
            // Generate a unique message ID
            string messageId = $"<{Guid.NewGuid()}@{new Uri(_configuration["EMAIL_CONFIGURATION:HOST"]).Host}>";

            // Add tracking pixel to body without HTML encoding
            string trackedBody = body +
                $"<img src='{_configuration["Tracking:BaseUrl"]}/track/open/{campaignId}?email={WebUtility.UrlEncode(receiver)}' style='display:none'/>";

            // Log the sending with tracking info
            _logger.LogInformation("Sending tracked email for campaign {CampaignId} to {Receiver} with Message-ID: {MessageId}",
                campaignId, receiver, messageId);

            // Always use HTML for emails with tracking pixel
            await SendEmailInternal(receiver, subject, trackedBody, true, messageId, campaignId);
        }

        public async Task SendEmail2(string senderEmail, string senderName, string receiver, string subject, string body, bool isBodyHtml = false)
        {
            ValidateInputs(receiver, subject, body);

            var emailConfig = _configuration.GetSection("EMAIL_CONFIGURATION");
            var email = emailConfig["EMAIL"];
            var password = emailConfig["PASSWORD"];
            var host = emailConfig["HOST"];
            var port = emailConfig.GetValue<int>("PORT");

            ValidateConfiguration(email, password, host, port);

            using var smtpClient = new SmtpClient(host!, port)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(email, password),
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            using var message = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = subject,
                Body = body,
                IsBodyHtml = isBodyHtml,
                Priority = MailPriority.Normal
            };
            message.To.Add(receiver);

            await SendMessageAsync(smtpClient, message, receiver, senderEmail);
        }

        public async Task SendEmailToBO(Users receiver, string subject, string body, bool isBodyHtml = false)
        {
            await SendEmailInternal(receiver.Email, subject, body, isBodyHtml);
        }

        private async Task SendEmailInternal(
            string receiver, 
            string subject, 
            string body, 
            bool isBodyHtml,
            string messageId = null,
            string campaignId = null)
        {
            ValidateInputs(receiver, subject, body);

            var emailConfig = _configuration.GetSection("EMAIL_CONFIGURATION");
            var email = emailConfig["EMAIL"];
            var password = emailConfig["PASSWORD"];
            var host = emailConfig["HOST"];
            var port = emailConfig.GetValue<int>("PORT");

            ValidateConfiguration(email, password, host, port);

            using var smtpClient = CreateSmtpClient(host!, port, email!, password!);
            using var message = CreateMailMessage(email!, receiver, subject, body, isBodyHtml);

            if (!string.IsNullOrEmpty(messageId))
            {
                message.Headers.Add("Message-ID", messageId);
            }

            if (!string.IsNullOrEmpty(campaignId))
            {
                message.Headers.Add("X-Campaign-ID", campaignId);
            }

            await SendMessageAsync(smtpClient, message, receiver);
        }

        private async Task SendMessageAsync(SmtpClient smtpClient, MailMessage message, string receiver, string sender = null)
        {
            try
            {
                await smtpClient.SendMailAsync(message);
                _logger.LogInformation("Email sent successfully {Sender} to {Receiver}", 
                    sender ?? message.From.Address, receiver);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email {Sender} to {Receiver}", 
                    sender ?? message.From.Address, receiver);
                throw new EmailException("Failed to send email", ex);
            }
        }

        private void ValidateInputs(string receiver, string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(receiver))
                throw new ArgumentException("Receiver email cannot be empty", nameof(receiver));

            if (string.IsNullOrWhiteSpace(subject))
                throw new ArgumentException("Email subject cannot be empty", nameof(subject));

            if (string.IsNullOrWhiteSpace(body))
                throw new ArgumentException("Email body cannot be empty", nameof(body));
        }

        private void ValidateConfiguration(string? email, string? password, string? host, int port)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ConfigurationException("Email configuration is missing");

            if (string.IsNullOrWhiteSpace(password))
                throw new ConfigurationException("Email password is missing");

            if (string.IsNullOrWhiteSpace(host))
                throw new ConfigurationException("SMTP host is missing");

            if (port <= 0)
                throw new ConfigurationException("Invalid SMTP port");
        }

        private SmtpClient CreateSmtpClient(string host, int port, string email, string password)
        {
            return new SmtpClient(host, port)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(email, password),
                DeliveryMethod = SmtpDeliveryMethod.Network
            };
        }

        private MailMessage CreateMailMessage(string sender, string receiver, string subject, string body, bool isBodyHtml)
        {
            return new MailMessage(sender, receiver, subject, body)
            {
                IsBodyHtml = isBodyHtml,
                Priority = MailPriority.Normal
            };
        }

        /// <summary>
        /// Replaces placeholder values in a template with actual lead and business data
        /// </summary>
        public string ReplacePlaceholders(string content, Leads lead, Users businessOwner)
        {
            if (string.IsNullOrEmpty(content))
                return content;
            
            var result = content;
            
            // Replace lead placeholders
            if (lead != null)
            {
                result = result.Replace("{{LeadName}}", HttpUtility.HtmlEncode(lead.LeadName ?? "Customer"));
                result = result.Replace("{{LeadEmail}}", HttpUtility.HtmlEncode(lead.LeadEmail ?? ""));
                result = result.Replace("{{LeadPhone}}", HttpUtility.HtmlEncode(lead.LeadPhone ?? ""));
                
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
                result = result.Replace("{{BusinessName}}", HttpUtility.HtmlEncode(businessOwner.BusinessName ?? "Our Business"));
                result = result.Replace("{{BusinessOwnerName}}", HttpUtility.HtmlEncode($"{businessOwner.FirstName} {businessOwner.LastName}".Trim()));
                result = result.Replace("{{BusinessEmail}}", HttpUtility.HtmlEncode(businessOwner.Email ?? ""));
                result = result.Replace("{{BusinessPhone}}", HttpUtility.HtmlEncode(businessOwner.PhoneNumber ?? ""));
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
                    Console.WriteLine($"Error replacing product placeholders: {ex.Message}");
                }
            }
            
            return result;
        }
    }

    public class CampaignTracker : ICampaignTracker
    {
        private readonly AuthDbContext _context;
        private readonly ILogger<CampaignTracker> _logger;

        public CampaignTracker(AuthDbContext context, ILogger<CampaignTracker> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task MarkAsReplied(string messageId, DateTime replyDate)
        {
            try
            {
                // Find the campaign by Message-ID header
                var campaignMessage = await _context.Campaign
                    .FirstOrDefaultAsync(c => c.MessageId == messageId);

                if (campaignMessage != null)
                {
                    campaignMessage.HasReplied = true;
                    campaignMessage.ReplyDate = replyDate;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Marked message {MessageId} as replied", messageId);
                }
                else
                {
                    _logger.LogWarning("No campaign found with Message-ID: {MessageId}", messageId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking message as replied");
                throw;
            }
        }

        public async Task MarkAsOpened(string campaignId, string recipient)
        {
            try
            {
                // You'll need to implement this based on your actual database structure
                // This is a placeholder implementation
                _logger.LogInformation("Marked campaign {CampaignId} as opened by {Recipient}", campaignId, recipient);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking campaign as opened");
                throw;
            }
        }
    }
    public class EmailException : Exception
    {
        public EmailException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    public class ConfigurationException : Exception
    {
        public ConfigurationException(string message)
            : base(message) { }
    }
}
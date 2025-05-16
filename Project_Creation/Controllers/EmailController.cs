using System.Net.Mail;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Project_Creation.Data;
using Project_Creation.Models.Entities;

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

        public HardcodedEmailService(ILogger<HardcodedEmailService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
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
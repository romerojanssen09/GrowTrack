using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Project_Creation.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true);
        Task<bool> SendCalendarReminderAsync(string toEmail, string eventTitle, string eventDescription, DateTime eventTime, string eventUrl);
        Task<bool> SendBusinessMessageNotificationAsync(string toEmail, string senderName, string messagePreview, string chatUrl);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true)
        {
            try
            {
                var smtpServer = _configuration["EmailSettings:SmtpServer"];
                var port = int.Parse(_configuration["EmailSettings:Port"]);
                var fromEmail = _configuration["EmailSettings:FromEmail"];
                var password = _configuration["EmailSettings:Password"];

                using (var client = new SmtpClient(smtpServer, port))
                {
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(fromEmail, password);
                    client.EnableSsl = true;

                    using (var mailMessage = new MailMessage())
                    {
                        mailMessage.From = new MailAddress(fromEmail);
                        mailMessage.To.Add(toEmail);
                        mailMessage.Subject = subject;
                        mailMessage.Body = body;
                        mailMessage.IsBodyHtml = isHtml;

                        await client.SendMailAsync(mailMessage);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending email to {toEmail}");
                return false;
            }
        }

        public async Task<bool> SendCalendarReminderAsync(string toEmail, string eventTitle, string eventDescription, DateTime eventTime, string eventUrl)
        {
            try
            {
                string subject = $"Reminder: {eventTitle}";
                string body = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background-color: #4a6fdc; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
                        .content {{ background-color: #f9f9f9; padding: 20px; border-left: 1px solid #ddd; border-right: 1px solid #ddd; }}
                        .footer {{ background-color: #eee; padding: 15px; text-align: center; font-size: 12px; color: #666; border-radius: 0 0 5px 5px; }}
                        .button {{ display: inline-block; background-color: #4a6fdc; color: white; text-decoration: none; padding: 10px 20px; border-radius: 5px; margin-top: 15px; }}
                        .calendar-icon {{ font-size: 40px; margin-bottom: 10px; }}
                    </style>
                </head>
                <body>
                    <div class='header'>
                        <div class='calendar-icon'>📅</div>
                        <h2>Event Reminder</h2>
                    </div>
                    <div class='content'>
                        <h3>{eventTitle}</h3>
                        <p><strong>Time:</strong> {eventTime.ToString("dddd, MMMM d, yyyy at h:mm tt")}</p>
                        <p><strong>Description:</strong> {eventDescription}</p>
                        <p>This is a reminder for your upcoming event. Please make sure to prepare accordingly.</p>
                        <a href='{eventUrl}' class='button'>View Event Details</a>
                    </div>
                    <div class='footer'>
                        <p>This is an automated message, please do not reply to this email.</p>
                    </div>
                </body>
                </html>";

                return await SendEmailAsync(toEmail, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending calendar reminder email to {toEmail}");
                return false;
            }
        }

        public async Task<bool> SendBusinessMessageNotificationAsync(string toEmail, string senderName, string messagePreview, string chatUrl)
        {
            try
            {
                string subject = $"New message from {senderName}";
                string body = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background-color: #4a6fdc; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
                        .content {{ background-color: #f9f9f9; padding: 20px; border-left: 1px solid #ddd; border-right: 1px solid #ddd; }}
                        .footer {{ background-color: #eee; padding: 15px; text-align: center; font-size: 12px; color: #666; border-radius: 0 0 5px 5px; }}
                        .button {{ display: inline-block; background-color: #4a6fdc; color: white; text-decoration: none; padding: 10px 20px; border-radius: 5px; margin-top: 15px; }}
                        .message-icon {{ font-size: 40px; margin-bottom: 10px; }}
                    </style>
                </head>
                <body>
                    <div class='header'>
                        <div class='message-icon'>💬</div>
                        <h2>New Business Message</h2>
                    </div>
                    <div class='content'>
                        <h3>You have a new message from {senderName}</h3>
                        <p><strong>Message:</strong> {(messagePreview.Length > 100 ? messagePreview.Substring(0, 100) + "..." : messagePreview)}</p>
                        <p>Login to your account to view and respond to this message.</p>
                        <a href='{chatUrl}' class='button'>Reply to Message</a>
                    </div>
                    <div class='footer'>
                        <p>This is an automated message, please do not reply to this email.</p>
                    </div>
                </body>
                </html>";

                return await SendEmailAsync(toEmail, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending business message notification email to {toEmail}");
                return false;
            }
        }
    }
} 
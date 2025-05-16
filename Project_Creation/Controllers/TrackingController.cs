using Microsoft.AspNetCore.Mvc;
using Project_Creation.Data;
using Project_Creation.DTO;

[ApiController]
[Route("api/tracking")]
public class TrackingController : ControllerBase
{
    private readonly ILogger<TrackingController> _logger;
    private readonly ICampaignTracker _campaignTracker;

    public TrackingController(
        ILogger<TrackingController> logger,
        ICampaignTracker campaignTracker)
    {
        _logger = logger;
        _campaignTracker = campaignTracker;
    }

    [HttpPost("reply")]
    public async Task<IActionResult> TrackReply([FromBody] EmailReplyDto reply)
    {
        try
        {
            _logger.LogInformation("Received reply:\n" +
                                  "In-Reply-To: {InReplyTo}\n" +
                                  "From: {From}\n" +
                                  "To: {To}\n" +
                                  "Subject: {Subject}\n" +
                                  "Date: {Date}\n" +
                                  "Body: {Body}",
                reply.InReplyTo, reply.From, reply.To, reply.Subject, reply.Date, reply.Body);

            // Extract the message ID from the In-Reply-To header
            var messageId = reply.InReplyTo?.Trim();

            if (!string.IsNullOrEmpty(messageId))
            {
                await _campaignTracker.MarkAsReplied(messageId, reply.Date);
                _logger.LogInformation("Marked message {MessageId} as replied", messageId);
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing email reply");
            return StatusCode(500);
        }
    }

    [HttpGet("open/{campaignId}")]
    public async Task<IActionResult> TrackOpen(string campaignId, [FromQuery] string email)
    {
        try
        {
            _logger.LogInformation("Email opened for campaign {CampaignId} by {Email}", campaignId, email);

            await _campaignTracker.MarkAsOpened(campaignId, email);

            // Return a transparent 1x1 pixel
            var pixel = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x1, 0x0, 0x1, 0x0, 0x80, 0x0, 0x0, 0xff, 0xff, 0xff, 0x0, 0x0, 0x0, 0x2c, 0x0, 0x0, 0x0, 0x0, 0x1, 0x0, 0x1, 0x0, 0x0, 0x2, 0x2, 0x44, 0x1, 0x0, 0x3b };
            return File(pixel, "image/gif");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking email open");
            return StatusCode(500);
        }
    }
}
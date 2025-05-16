namespace Project_Creation.Models.Entities
{
    public class Campaign
    {
        public int Id { get; set; }
        public string? CampaignName { get; set; }
        public string? Message { get; set; }
        public string? TargetProducts { get; set; }
        public DateTime CampaignAt { get; set; } = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time"));
        public bool IsSent { get; set; }
        public bool SendToAll { get; set; }
        public int? SenderId { get; set; }
        public Users? Sender { get; set; }
        public string? Notes { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Add these for tracking
        public string? MessageId { get; set; }
        public bool HasReplied { get; set; }
        public DateTime? ReplyDate { get; set; }
        public string? ReplyContent { get; set; }
    }

    public class CampaignEditModel
    {
        public int Id { get; set; }
        public string CampaignName { get; set; }
        public string Message { get; set; }
        public string TargetProducts { get; set; }
        public bool SendToAll { get; set; }
        public string Notes { get; set; }
    }
}

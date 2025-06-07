using System.ComponentModel.DataAnnotations.Schema;

namespace Project_Creation.Models.Entities
{
    public class Campaign
    {
        public int Id { get; set; }
        public string? CampaignName { get; set; }
        public string? Subject { get; set; }
        public string? Message { get; set; }
        public string? TargetProducts { get; set; }
        public string? TargetLeads { get; set; }
        public DateTime CampaignAt { get; set; } = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time"));
        public bool IsSent { get; set; }
        public int? SenderId { get; set; }
        public Users? Sender { get; set; }
        public string? Notes { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ResendAt { get; set; }

        // Add these for tracking
        public string? MessageId { get; set; }
        public bool HasReplied { get; set; }
        public DateTime? ReplyDate { get; set; }
        public string? ReplyContent { get; set; }

        [NotMapped]
        public List<Leads> MyLeads { get; set; } = new();
        [NotMapped]
        public List<Leads> SharedLeads { get; set; } = new();
        [NotMapped]
        public List<Leads> PublicLeads { get; set; } = new();
    }

    public class CampaignEditModel
    {
        public int Id { get; set; }
        public string CampaignName { get; set; }
        public string Subject { get; set; }
        public string Message { get; set; }
        public string TargetProducts { get; set; }
        public string TargetLeads { get; set; }
        //public bool SendToAllProducts { get; set; }
        //public bool SendToAllLeads { get; set; }
        public string Notes { get; set; }
    }
}

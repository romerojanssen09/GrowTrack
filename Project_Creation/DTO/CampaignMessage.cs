namespace Project_Creation.DTO
{
    public class CampaignMessage
    {
        public int Id { get; set; }
        public string CampaignId { get; set; }  // Links to your Campaign entity
        public string RecipientEmail { get; set; }
        public string MessageId { get; set; }    // The Message-ID header value
        public DateTime SentDate { get; set; }
        public bool HasReplied { get; set; }
        public DateTime? ReplyDate { get; set; }
        public bool HasOpened { get; set; }
        public DateTime? OpenDate { get; set; }
        public string ReplyContent { get; set; }
    }
}

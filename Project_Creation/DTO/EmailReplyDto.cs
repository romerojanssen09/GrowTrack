namespace Project_Creation.DTO
{
    public class EmailReplyDto
    {
        public string From { get; set; }
        public string To { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public string InReplyTo { get; set; } // This should contain your Message-ID header
        public DateTime Date { get; set; }
    }
}

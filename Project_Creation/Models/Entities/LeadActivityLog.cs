namespace Project_Creation.Models.Entities
{
    public class LeadActivityLog
    {
        public int Id { get; set; }
        public int LeadId { get; set; } // Foreign key to Leads
        public ActionType ActionType { get; set; } // e.g., "Edited", "Assigned"
        public string? FieldChanged { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string PerformedBy { get; set; } = ""; // e.g., user or system name
        public DateTime Timestamp { get; set; } = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));

        // for products only 
        public string? productOldValue { get; set; }
        public string? productNewValue { get; set; }

        // Navigation
        public Leads? Lead { get; set; }
    }
    public enum ActionType
    {
        Edited,
        Assigned,
        Edit_And_Assigned,
        Created,
        Deleted
    }
}

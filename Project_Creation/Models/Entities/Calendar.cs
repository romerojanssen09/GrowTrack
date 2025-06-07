namespace Project_Creation.Models.Entities
{
    public class Calendar
    {
        public int Id { get; set; }
        public required string Title { get; set; }
        public required DateOnly Date { get; set; }
        public TimeOnly? Time { get; set; }
        public required Priority Priority { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));
        public DateTime? FinishedAt { get; set; }
        public string? BOViewers { get; set; }// list of staff
        public string? AdminViewers1 { get; set; }// list of business owners by business category
        public string? AdminViewers2 { get; set; }// list of business owners by business owners
        public bool IsAll { get; set; } = false; // flag for BO created appointments visible to all staff
        public bool IsAdminSetAll { get; set; } = false; // Flag for admin-created appointments visible to all BOs
        public WhoSetAppointments WhoSetAppointment { get; set; }
        public int? StaffId { get; set; }
        public int UserId { get; set; }
        public Users User { get; set; } = null!; // Navigation property
    }
    public enum Priority
    {
        Low,
        Medium,
        High
    }

    public enum WhoSetAppointments
    {
        BusinessOwner,
        Admin,
        Staff
    }
}

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
        public bool IsCompleted { get; set; }
        public DateTime CreatedAt { get; set; } = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));
        
        public int UserId { get; set; }
        public Users User { get; set; }
    }
    public enum Priority
    {
        Low,
        Medium,
        High
    }
}

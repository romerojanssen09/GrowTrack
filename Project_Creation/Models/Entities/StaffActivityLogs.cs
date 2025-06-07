namespace Project_Creation.Models.Entities
{
    public class StaffActivityLogs
    {
        public int Id { get; set; }
        public int StaffId { get; set; }
        public ActivityType Activity { get; set; } = ActivityType.None;
        public DateTime CreatedAt { get; set; } = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));
        public string? Description { get; set; } = null;
        public Staff Staff { get; set; }
    }

    [Flags]
    public enum ActivityType
    {
        None = 0,
        Inventory = 1 << 0,          // 1
        Leads = 1 << 1,              // 2
        QuickSales = 1 << 2,         // 4
        PublishedProducts = 1 << 3,  // 8
        Campaigns = 1 << 4,          // 16
        Reports = 1 << 5,            // 32
        Notifications = 1 << 6,      // 64
        Calendar = 1 << 7,           // 128
        Chat = 1 << 8                // 256
    }
}

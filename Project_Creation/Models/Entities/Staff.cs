using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project_Creation.Models.Entities
{
    public class Staff
    {
        public int Id { get; set; }
        public required string StaffName { get; set; }
        public required string StaffSEmail { get; set; }
        public required string StaffPhone { get; set; }
        public string? Password { get; set; }
        public bool IsSetPassword { get; set; } = false;
        public required string Role { get; set; } = "Staff";
        public AccountStatus IsActive { get; set; } = AccountStatus.Pending;
        public StaffAccessLevel StaffAccessLevel { get; set; } = StaffAccessLevel.None;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public OnlineStatus? IsOnline { get; set; } = OnlineStatus.Offline;
        public DateTime LastLoginDate { get; set; } = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));

        public enum OnlineStatus
        {
            Online,
            Away,
            Offline
        }

        [NotMapped]
        public string? Link { get; set; } = null;
    }

    public enum AccountStatus
    {
        Active,
        Pending,
        Suspended
    }

    [Flags]
    public enum StaffAccessLevel
    {
        None = 0,
        Inventory = 1 << 0,       // 1
        Leads = 1 << 1,           // 2
        Product = 1 << 2,         // 4
        QuickSales = 1 << 3,      // 8
        InventoryAndPublishes = 1 << 4 // 16
    }
}
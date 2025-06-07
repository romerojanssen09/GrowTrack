using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project_Creation.Models.Entities
{
    public enum NotificationType
    {
        General,
        Chat,
        Calendar,
        Lead,
        Order,
        System
    }

    public class Notification
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int UserId { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Title { get; set; }
        
        [Required]
        public string Message { get; set; }
        
        public bool IsRead { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));
        
        [StringLength(255)]
        public string? Url { get; set; }
        
        public NotificationType Type { get; set; } = NotificationType.General;
        
        // User type flags
        public bool IsForAdmin { get; set; } = false;
        public bool IsForStaff { get; set; } = false;
        public bool IsForBusinessOwner { get; set; } = false;
        
        [ForeignKey("UserId")]
        public virtual Users User { get; set; }

        public string? NotificationTypes { get; set; }
    }
} 
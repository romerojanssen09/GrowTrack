using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project_Creation.Models.Entities
{
    public enum TaskPriority
    {
        Low,
        Medium,
        High,
        Urgent
    }
    
    public class CalendarTask
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int UserId { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Title { get; set; }
        
        public string Description { get; set; }
        
        [Required]
        public DateTime StartTime { get; set; }
        
        public DateTime EndTime { get; set; }
        
        public TaskPriority Priority { get; set; }
        
        public bool IsCompleted { get; set; } = false;
        
        public bool HasReminder { get; set; } = true;
        
        public int ReminderMinutesBefore { get; set; } = 30;
        
        public bool ReminderSent { get; set; } = false;
        
        public DateTime? LastReminderSent { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? UpdatedAt { get; set; }
        
        // Navigation property
        [ForeignKey("UserId")]
        public virtual Users User { get; set; }
    }
} 
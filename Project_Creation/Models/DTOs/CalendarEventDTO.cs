using Project_Creation.Models.Entities;
using System.Collections.Generic;

namespace Project_Creation.Models.DTOs
{
    public class CalendarEventDTO
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Date { get; set; }
        public string EndDate { get; set; }
        public string Time { get; set; }
        public string EndTime { get; set; }
        public Priority Priority { get; set; } = Priority.Medium;
        public bool SendReminder { get; set; } = true;
        public int ReminderMinutesBefore { get; set; } = 30;
        
        // Added properties for sharing
        public List<string> SelectedBusinessCategories { get; set; }
        public List<int> SelectedBusinessOwners { get; set; }
        public List<int> SelectedStaff { get; set; }
    }
} 
using Project_Creation.Models.Entities;

namespace Project_Creation.DTO
{
    public class CalendarTaskDTO
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Date { get; set; }
        public string? Time { get; set; }
        public string Priority { get; set; }
        public string? Notes { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime CreatedAt { get; set; }
    }
} 
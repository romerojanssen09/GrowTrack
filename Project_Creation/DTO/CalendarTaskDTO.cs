using Project_Creation.Models.Entities;

namespace Project_Creation.DTO
{
    public class CalendarTaskDTO
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Date { get; set; }
        public string? EndDate { get; set; }
        public string? Time { get; set; }
        public string? EndTime { get; set; }
        public string Priority { get; set; }
        public string? Notes { get; set; }
        public WhoSetAppointments? WhoMarkedFinished { get; set; }
        public string? StaffNameWhoCompleted { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsAll { get; set; }
        public bool IsAdminSetAll { get; set; }
        public List<int>? SelectedBusinessCategories { get; set; }
        public List<int>? SelectedBusinessOwners { get; set; }
        public List<int>? SelectedStaff { get; set; }
        
        // Storage properties for sharing settings received from server
        public string? BOViewers { get; set; }
        public string? AdminViewers1 { get; set; }
        public string? AdminViewers2 { get; set; }
    }
}
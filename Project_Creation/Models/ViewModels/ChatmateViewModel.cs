namespace Project_Creation.Models.ViewModels
{
    public class ChatmateViewModel
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string LastMessage { get; set; }
        public DateTime LastMessageTime { get; set; }
        public bool IsCurrentUserSender { get; set; }
        public bool IsRead { get; set; }
        public int UnreadCount { get; set; }
        public int CurrentUser { get; set; }
        public bool IsEdited { get; set; }
        public DateTime CreatedAt { get; set; } = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));
    }
}

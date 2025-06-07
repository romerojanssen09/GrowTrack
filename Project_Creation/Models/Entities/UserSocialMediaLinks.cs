namespace Project_Creation.Models.Entities
{
    public class UserSocialMediaLinks
    {
        public int Id { get; set; }
        public string? FacebookLinks { get; set; }
        public string? InstagramLinks { get; set; }
        public string? LinkedInLinks { get; set; }
        public string? TwitterLinks { get; set; }
        public string? TikTokLinks { get; set; }
        public string? YouTubeLinks { get; set; }
        public string? PinterestLinks { get; set; }
        public string? WhatsAppLinks { get; set; }
        public string? ThreadsLinks { get; set; }
        public string? SnapchatLinks { get; set; }
        public int UserId { get; set; }
        public Users? User { get; set; } // Navigation property to User entity
    }
}

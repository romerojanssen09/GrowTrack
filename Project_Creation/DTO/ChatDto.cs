using ServiceStack.DataAnnotations;
using System.ComponentModel.DataAnnotations;

namespace Project_Creation.DTO
{
    public class ChatDto
    {
        [Key]
        [AutoIncrement]
        [PrimaryKey]
        public int Id { get; set; }
        public required int SenderId { get; set; }
        public required int ReceiverId { get; set; }
        public required string message { get; set; }
        public DateTime CreatedAt { get; set; } = TimeZoneInfo.ConvertTimeFromUtc(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore")), TimeZoneInfo.FindSystemTimeZoneById("Singapore"));
        public DateTime UpdatedAt { get; set; } = TimeZoneInfo.ConvertTimeFromUtc(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore")), TimeZoneInfo.FindSystemTimeZoneById("Singapore"));
        public bool IsRead { get; set; } = false; // Added this property
    }
}

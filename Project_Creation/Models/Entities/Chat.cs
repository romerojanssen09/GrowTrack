using System.ComponentModel.DataAnnotations;
using ServiceStack.DataAnnotations;

namespace Project_Creation.Models.Entities
{
    public class Chat
    {
        [Key]
        [AutoIncrement]
        [PrimaryKey]
        public int Id { get; set; }
        public required int SenderId { get; set; }
        public required int ReceiverId { get; set; }
        public required string Message { get; set; }
        public DateTime CreatedAt { get; set; } = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));
        public DateTime UpdatedAt { get; set; } = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));
        public bool IsRead { get; set; } = false;
        public ChatStatus Status { get; set; } = ChatStatus.Null;
        public string? JSONString { get; set; }
        public bool SenderDelete { get; set; } = false;
        public int? LeadAccessRequestsId { get; set; } // Nullable to allow for no lead sharing initially
        public bool IsEdited { get; set; } = false;
    }

    public enum ChatStatus
    {
        Null,
        Request,
        Accepted,
        Rejected,
        LeadRequest,
        LeadAccepted,
        LeadRejected
    }
}
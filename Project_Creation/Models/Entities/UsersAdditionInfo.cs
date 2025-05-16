using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project_Creation.Models.Entities
{
    public class UsersAdditionInfo
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public int UserId { get; set; }
        public string BrgyClearancePath { get; set; } = string.Empty;
        public bool IsAllowEditBrgyClearance { get; set; } = true;
        public string OperationToOperate { get; set; } = string.Empty;
        public string BusinessOwnerValidId { get; set; } = string.Empty;
        public bool IsAllowEditBusinessOwnerValidId { get; set; } = true;
        public string SecCertPath { get; set; } = string.Empty;
        public bool IsAllowEditSecCertPath { get; set; } = true;
        public string DtiCertPath { get; set; } = string.Empty;
        public bool IsAllowEditDtiCertPath { get; set; } = true;
        public DateTime SubmissionDate { get; set; } = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));

        [ForeignKey("UserId")]
        public Users User { get; set; }
    }
}
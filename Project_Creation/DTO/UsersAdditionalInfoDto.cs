using Microsoft.EntityFrameworkCore;
using ServiceStack.DataAnnotations;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using PrimaryKeyAttribute = Microsoft.EntityFrameworkCore.PrimaryKeyAttribute;
using RequiredAttribute = System.ComponentModel.DataAnnotations.RequiredAttribute;
using StringLengthAttribute = System.ComponentModel.DataAnnotations.StringLengthAttribute;

namespace Project_Creation.DTO
{
    [PrimaryKey(nameof(Id))]
    public class UsersAdditionalInfoDto
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }
        // These will be populated from file uploads
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
    }
}
using System.ComponentModel.DataAnnotations;
using ServiceStack.DataAnnotations;
using RequiredAttribute = ServiceStack.DataAnnotations.RequiredAttribute;
using StringLengthAttribute = ServiceStack.DataAnnotations.StringLengthAttribute;

namespace Project_Creation.Models.Entities
{
    public class Register
    {
        [PrimaryKey]
        [AutoIncrement]
        public int Id { get; set; }

        [StringLength(50)]
        public required string FirstName { get; set; }

        [StringLength(50)]
        public required string LastName { get; set; }

        [EmailAddress]
        [StringLength(100)]
        public required string Email { get; set; }

        [Phone]
        public required string PhoneNumber { get; set; }

        [StringLength(100, MinimumLength = 6)]
        public required string Password { get; set; }

        [StringLength(100)]
        public required string BusinessName { get; set; }

        [StringLength(100)]
        public required string BusinessAddress { get; set; }

        [StringLength(100)]
        public required string DTIReqistrationNumber { get; set; }

        [Required]
        [StringLength(100)]
        public string BusinessPermitPath { get; set; }

        [StringLength(100)]
        public required string NumberOfBusinessYearsOperation { get; set; }

        [StringLength(100)]
        public required string ScopeOfBusiness { get; set; }

        [StringLength(1000)]
        public required string CompanyBackground { get; set; }

        [StringLength(100)]
        public string? LogoPath { get; set; }

        public DateTime RegistrationDate { get; set; } = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));
        public bool IsVerified { get; set; } = false;
        public bool IsAllowedToMarketPlace { get; set; } = false;

        [StringLength(50)]
        public string UserRole { get; set; } = "BusinessOwner"; // Default role
    }
}
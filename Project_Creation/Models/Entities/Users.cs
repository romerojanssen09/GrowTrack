using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project_Creation.Models.Entities
{
    public class Users
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
        public bool IsSetPassword { get; set; } = false;

        [Required]
        [Phone]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string BusinessName { get; set; } = string.Empty;

        [Required]
        public string BusinessAddress { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string DTIReqistrationNumber { get; set; } = string.Empty;

        [Required]
        public string NumberOfBusinessYearsOperation { get; set; } = string.Empty;

        [Required]
        public string CategoryOfBusiness { get; set; } = string.Empty;

        [Required]
        public string CompanyBackground { get; set; } = string.Empty;

        public string UserRole { get; set; } = "BusinessOwner";
        public bool IsVerified { get; set; } = false;
        public MarketplaceStatus MarkerPlaceStatus { get; set; } = MarketplaceStatus.Pending;

        public enum MarketplaceStatus
        {
            Pending,
            Requesting,
            Approved,
            Rejected
        }

        public DateTime RegistrationDate { get; set; } = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));
        public string BusinessPermitPath { get; set; } = string.Empty;
        public string NumberOfEmployees { get; set; } = string.Empty;
        public bool IsAllowEditBusinessPermitPath { get; set; } = true;
        public string? LogoPath { get; set; }
        public int? ResetCode { get; set; }
        public DateTime? ResetCodeExpiry { get; set; }

        public OnlineStatus? IsOnline { get; set; } = OnlineStatus.Offline;
        public DateTime LastLoginDate { get; set; } = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));

        public enum OnlineStatus
        {
            Online,
            Away,
            Offline
        }

        // Navigation property
        public virtual UsersAdditionInfo? AdditionalInfo { get; set; }
    }
}
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;

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
        public MarketplaceStatus MarkerPlaceStatus { get; set; } = MarketplaceStatus.NotApplied;
        public DateTime RegistrationDate { get; set; } = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));
        public string BusinessPermitPath { get; set; } = string.Empty;
        public string NumberOfEmployees { get; set; } = string.Empty;
        public bool IsAllowEditBusinessPermitPath { get; set; } = true;
        public string? LogoPath { get; set; }
        public int? ResetCode { get; set; }
        public DateTime? ResetCodeExpiry { get; set; }
        public OnlineStatus? IsOnline { get; set; } = OnlineStatus.Offline;
        public DateTime LastLoginDate { get; set; } = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));
        public bool AllowLoginAlerts { get; set; } = false;
        public bool AllowEmailNotifications { get; set; } = true;
        public bool TwoFactorAuthentication { get; set; } = false;
        public int? TwoFactorAuthenticationCode { get; set; }
        public string? LoginAlerts { get; set; }
        public int StaffLimit { get; set; } = 5; // Default value for staff limit
        public AccountStatuss AccountStatus { get; set; } = AccountStatuss.Active; // Default account status
        public DateTime? LastStatusChangeDate { get; set; } // Track when account status was last changed

        public enum OnlineStatus
        {
            Online,
            Away,
            Offline,
        }
        public enum MarketplaceStatus
        {
            NotApplied,
            AwaitingApproval,
            Authorized,
            Rejected,
        }

        public enum AccountStatuss
        {
            Active,
            Suspended,
            Deactivated,
        }
        // Navigation property
        public virtual UsersAdditionInfo? AdditionalInfo { get; set; }
    }
}
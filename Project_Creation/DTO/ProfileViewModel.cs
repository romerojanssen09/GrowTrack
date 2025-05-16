using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Project_Creation.DTO
{
    public class ProfileViewModel
    {
        [Required]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Phone]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Business Name")]
        public string BusinessName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Business Address")]
        public string BusinessAddress { get; set; } = string.Empty;
        
        [Required]
        [Display(Name = "DTI Registration Number")]
        public string DTIRegistrationNumber { get; set; } = string.Empty;
        
        [Required]
        [Display(Name = "Years in Operation")]
        public string NumberOfBusinessYearsOperation { get; set; } = string.Empty;
        
        [Required]
        [Display(Name = "Company Background")]
        public string CompanyBackground { get; set; } = string.Empty;
        
        [Display(Name = "Business Permit")]
        public string BusinessPermitPath { get; set; } = string.Empty;
        public bool IsAllowEditBusinessPermitPath { get; set; } = false;
        public string CategoryOfBusiness { get; set; } = string.Empty;
        public string BrgyClearancePath { get; set; } = string.Empty;
        public bool IsAllowEditBrgyClearance { get; set; } = true;
        public string OperationToOperate { get; set; } = string.Empty;
        public string BusinessOwnerValidId { get; set; } = string.Empty;
        public bool IsAllowEditBusinessOwnerValidId { get; set; } = true;
        public string SecCertPath { get; set; } = string.Empty;
        public bool IsAllowEditSecCertPath { get; set; } = true;
        public string DtiCertPath { get; set; } = string.Empty;
        public bool IsAllowEditDtiCertPath { get; set; } = true;

        public string IsAllowedToMarketPlace { get; set; } = string.Empty;





        // For uploading a new business permit
        public IFormFile? BusinessPermitFile { get; set; }

        public UsersAdditionalInfoDto? UsersAdditionalInfo { get; set; } = new UsersAdditionalInfoDto();
    }

    public class SettingsViewModel
    {
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        public ChangePasswordViewModel? ChangePassword { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
} 
using Project_Creation.DTO;
using System.ComponentModel.DataAnnotations;

namespace Project_Creation.Models.ViewModels
{
    public class SettingsViewModel
    {
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;
        public string? Fullname { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public bool AllowEmailNotifications { get; set; }
        public bool AllowLoginAlerts { get; set; }

        [Required(ErrorMessage = "Current password is required")]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "New password is required")]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }

        public ChangePasswordViewModel? ChangePassword { get; set; }
    }
}

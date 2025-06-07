using System.ComponentModel.DataAnnotations;

namespace Project_Creation.Models.Entities
{
    public class TwoFactorAuthentication
    {
        [Required(ErrorMessage = "Verification code is required")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Code must be 6 digits")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Code must contain only numbers")]
        public string code { get; set; }
    }
} 
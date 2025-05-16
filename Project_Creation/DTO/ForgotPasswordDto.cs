using System.ComponentModel.DataAnnotations;

namespace Project_Creation.DTO
{
    public class ForgotPasswordDto
    {
        public int UserId { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords don't match")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}

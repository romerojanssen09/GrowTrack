namespace Project_Creation.DTO
{
    public class PasswordDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}

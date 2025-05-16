using System.ComponentModel.DataAnnotations;

namespace Project_Creation.Models.Entities
{
    public class BusinessOwner
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public required string Name { get; set; }

        [StringLength(500)]
        public string? Address { get; set; }

        [StringLength(50)]
        public string? PhoneNumber { get; set; }

        [StringLength(100)]
        [EmailAddress]
        public string? Email { get; set; }

        [StringLength(100)]
        public required string BusinessName { get; set; }
    }
} 
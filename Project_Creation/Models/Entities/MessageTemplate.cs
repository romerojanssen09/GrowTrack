using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project_Creation.Models.Entities
{
    public class MessageTemplate
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; }
        
        [Required]
        public string Subject { get; set; }
        
        [Required]
        public string Content { get; set; }
        
        public bool IsDefault { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? UpdatedAt { get; set; }
        
        public int BOId { get; set; } // Business Owner ID
        
        [ForeignKey("BOId")]
        public Users? BusinessOwner { get; set; } // Make optional with nullable reference type
        
        [NotMapped]
        public bool IsSelected { get; set; } = false;
    }
} 
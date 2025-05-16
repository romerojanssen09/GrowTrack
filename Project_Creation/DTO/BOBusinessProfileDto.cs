using System.ComponentModel.DataAnnotations;
using Project_Creation.Models.Entities;

namespace Project_Creation.DTO
{
    public class BOBusinessProfileDto
    {
        [Required]
        public int UserId { get; set; }

        [Required(ErrorMessage = "Business name is required")]
        public string? BusinessName { get; set; }

        public string? ShopName { get; set; }

        [Required(ErrorMessage = "Business address is required")]
        public string? BusinessAddress { get; set; }
        public string? Description { get; set; }
        public string? LogoPath { get; set; }
        public string? BusinessBackgroundImgPath { get; set; }
        
        // Contact information
        public string? ContactNumber { get; set; }
        
        // Products associated with this business profile
        public ICollection<Product2>? Products { get; set; } = new List<Product2>();
        
        // Flag to indicate if this is the current user's profile
        public bool IsCurrentUser { get; set; } = false;

        public Dictionary<string, List<Product2>> ProductsByCategory { get; set; } = new();
    }
}

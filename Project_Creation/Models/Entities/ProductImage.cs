using System;
using System.ComponentModel.DataAnnotations;

namespace Project_Creation.Models.Entities
{
    public class ProductImage
    {
        public int Id { get; set; }
        
        [Required]
        public int ProductId { get; set; }
        public Product? Product { get; set; }
        
        [Required]
        [StringLength(255)]
        public required string ImagePath { get; set; }
        
        public bool IsMainImage { get; set; } = false;
        
        [StringLength(100)]
        public string? Title { get; set; }
        
        [StringLength(255)]
        public string? AltText { get; set; }
        
        public int DisplayOrder { get; set; } = 0;
        
        public DateTime CreatedAt { get; set; } = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));
    }
} 
using System.ComponentModel.DataAnnotations;

namespace Project_Creation.DTO
{
    public class ProductImageDto
    {
        public int Id { get; set; }
        public string ImagePath { get; set; }
        public bool IsMainImage { get; set; }
        public string Title { get; set; }
        public int DisplayOrder { get; set; }

        // Marketplace fields - make them truly optional
        [Display(Name = "Marketplace Description")]
        public string MarketplaceDescription { get; set; } = string.Empty;  // Initialize as empty string

        [Display(Name = "Marketplace Price")]
        public decimal? MarketplacePrice { get; set; } = null;  // Explicitly nullable
    }
}

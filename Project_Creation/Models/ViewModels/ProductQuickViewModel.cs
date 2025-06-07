using Project_Creation.Models.Entities;

namespace Project_Creation.Models.ViewModels
{
    public class ProductQuickViewModel
    {
        public int Id { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal SellingPrice { get; set; }
        public int QuantityInStock { get; set; }
        public string Category { get; set; } = string.Empty;
        public List<ProductImage> Images { get; set; } = new List<ProductImage>();
        public bool IsCurrentUserOwner { get; set; }
        public bool IsPublished { get; set; }
    }
} 
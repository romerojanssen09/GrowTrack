using Project_Creation.Models.Entities;

namespace Project_Creation.Models.ViewModels
{
    public class HotSellingProductViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int TotalSold { get; set; }
        public int QuantityInStock { get; set; }
        public decimal SellingPrice { get; set; }
        public List<ProductImage>? Images { get; set; } // Optional if you want images too
    }
}

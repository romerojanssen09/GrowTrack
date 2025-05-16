using Project_Creation.DTO;
using Project_Creation.Models.Entities;

namespace Project_Creation.Models.ViewModels
{
    public class ProductGroupViewModel
    {
        public string? CategoryName { get; set; }
        public string? FeaturedProducts { get; set; }
        public List<ProductDto>? Products { get; set; }
    }
}

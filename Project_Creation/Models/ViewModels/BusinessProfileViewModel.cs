using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Project_Creation.Models.Entities;
using Project_Creation.DTO;

namespace Project_Creation.Models.ViewModels
{
    public class BusinessProfileViewModel
    {
        public int UserId { get; set; }
        public string? ShopName { get; set; }
        public string? ShopDescription { get; set; }
        public string? BusinessBackgroundImgPath { get; set; }
        public string? LogoPath { get; set; }
        public string? BusinessOwnerName { get; set; }
        public string? BusinessAddress { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? BusinessName { get; set; }
        public bool IsCurrentUser { get; set; } = false;
        public int? TotalSold { get; set; }

        public List<Category>? Categories { get; set; }
        public List<ProductDto>? Products { get; set; }
        public List<ProductDto>? Recommended { get; set; }
        public List<ProductDto>? HotSalesProducts { get; set; }
        public List<ProductGroupViewModel>? GroupedProductsByCategory { get; set; }
        public List<ProductDto>? FeaturedProductViewModel { get; set; }
        public UserSocialMediaLinks? UserLinks { get; set; }
    }
}

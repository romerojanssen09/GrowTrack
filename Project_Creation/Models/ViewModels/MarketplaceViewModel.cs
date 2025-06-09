using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Project_Creation.Models.ViewModels
{
    public class MarketplaceViewModel
    {
        public List<MarketplaceProductViewModel> Products { get; set; } = new List<MarketplaceProductViewModel>();
        public List<MarketplaceProductViewModel> FeaturedProducts { get; set; } = new List<MarketplaceProductViewModel>();
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public string CategoryFilter { get; set; }
        public string MainCategoryFilter { get; set; }
        public string SearchQuery { get; set; }
    }
    
    public class MarketplaceProductViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int BusinessOwnerId { get; set; }
        public bool InStock { get; set; }
        public string Description { get; set; }
        
        [DisplayFormat(DataFormatString = "{0:C}")]
        public decimal Price { get; set; }
        public string CategoryName { get; set; }
        public string BusinessName { get; set; }
        public string MainImageUrl { get; set; }
    }
    
    public class ProductDetailsViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int BusinessOwnerId { get; set; }
        public string Description { get; set; }
        
        [DisplayFormat(DataFormatString = "{0:C}")]
        public decimal Price { get; set; }
        public string CategoryName { get; set; }
        public string BusinessName { get; set; }
        public bool InStock { get; set; }
        public int QuantityInStock { get; set; }
        public List<string> Images { get; set; } = new List<string>();
        public List<MarketplaceProductViewModel> RelatedProducts { get; set; } = new List<MarketplaceProductViewModel>();
    }
} 
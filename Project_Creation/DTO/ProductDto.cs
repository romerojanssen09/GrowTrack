using Project_Creation.Models.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ServiceStackAutoIncrement = ServiceStack.DataAnnotations.AutoIncrementAttribute;


namespace Project_Creation.DTO
{
    public class ProductDto
    {
        [Key]
        [ServiceStackAutoIncrement]
        public int Id { get; set; }

        [Required]
        public int BOId { get; set; }

        [Required(ErrorMessage = "Product name is required")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Product name must be between 3 and 100 characters")]
        [Display(Name = "Product Name")]
        public string ProductName { get; set; } = string.Empty;

        [Display(Name = "Supplier")]
        public int? SupplierId { get; set; }

        [Display(Name = "Supplier Name")]
        public string? SupplierName => Supplier2?.SupplierName;

        [Required(ErrorMessage = "Purchase price is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Purchase price must be greater than 0")]
        [Display(Name = "Purchase Price")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PurchasePrice { get; set; }

        [Required(ErrorMessage = "SKU is required")]
        [Display(Name = "SKU")]
        [StringLength(20, ErrorMessage = "SKU cannot exceed 20 characters")]
        public string SKU { get; set; } = string.Empty;

        [Display(Name = "Barcode")]
        public string? Barcode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Quantity is required")]
        [Range(0, int.MaxValue, ErrorMessage = "Quantity cannot be negative")]
        [Display(Name = "Quantity In Stock")]
        public int QuantityInStock { get; set; }

        [Required(ErrorMessage = "Reorder level is required")]
        [Range(0, int.MaxValue, ErrorMessage = "Reorder level cannot be negative")]
        [Display(Name = "Reorder Level")]
        public int? ReorderLevel { get; set; }

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Selling price is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Selling price must be greater than 0")]
        [Display(Name = "Selling Price")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal SellingPrice { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<Supplier> Suppliers { get; set; } = new List<Supplier>();
        public List<Category> Category { get; set; } = new List<Category>();
        public Supplier? Supplier2 { get; set; }
        public List<ProductImage>? Images { get; set; }
        public string Category2 { get; set; } = string.Empty; // For existing categories

        [Display(Name = "New Category")]
        [StringLength(50, ErrorMessage = "Category name cannot exceed 50 characters")]
        public string? NewCategoryName { get; set; } = string.Empty;
        public bool DisplayFeatured { get; set; } = false;

        // Change the EffectiveCategory property to:
        public string EffectiveCategory =>
            !string.IsNullOrEmpty(NewCategoryName) ? NewCategoryName : Category2;
    }
}
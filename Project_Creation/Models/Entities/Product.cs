using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ServiceStack.DataAnnotations;
using StringLengthAttribute = System.ComponentModel.DataAnnotations.StringLengthAttribute;

namespace Project_Creation.Models.Entities
{
    public class Product
    {
        [Key]
        [AutoIncrement]
        public int Id { get; set; }
        public required int BOId { get; set; }
        public required string ProductName { get; set; }
        public string? SupplierId { get; set; }
        public required string Category { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal PurchasePrice { get; set; } // how much the product costs (TOTAL)
        public required string SKU { get; set; }
        public string? Barcode { get; set; } // img src of the barcode
        public int QuantityInStock { get; set; }
        public int? ReorderLevel { get; set; }
        [StringLength(500)]
        public string? Description { get; set; }
        public decimal SellingPrice { get; set; }
        public bool IsPublished { get; set; } = false; // false by default
        public bool IsAlreadyPublished { get; set; } = false; // false by default
        public bool DisplayFeatured { get; set; } = false; // false by default
        public bool IsDeleted { get; set; } = false; // false by default for soft delete
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public Supplier? Supplier2 { get; set; }
        public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
        public Transaction? InventoryTransaction { get; set; }
        //public Category? Categories { get; set; }

        // Navigation property
        public ICollection<SaleItem> SaleItems { get; set; }
    }
}

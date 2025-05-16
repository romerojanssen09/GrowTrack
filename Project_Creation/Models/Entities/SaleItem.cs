using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace Project_Creation.Models.Entities
{
    public class SaleItem
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Sale")]
        public int SaleId { get; set; }   // FK to Sale

        [ForeignKey("Product")]
        public int ProductId { get; set; } // FK to Product2

        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public string Notes { get; set; }

        // Navigation properties
        public Sale Sale { get; set; }
        public Product2 Product { get; set; }
    }
}

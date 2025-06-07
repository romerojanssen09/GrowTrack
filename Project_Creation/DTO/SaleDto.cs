using Project_Creation.Models.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project_Creation.DTO
{
    public class SaleDto
    {
        [Key]
        public int Id { get; set; }
        public string CustomerName { get; set; }
        public decimal TotalAmount { get; set; }
        public bool IsQuickSale { get; set; }
        public DateTime SaleDate { get; set; } = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));

        // Keep the original property if it's still needed elsewhere
        public List<SaleItemDto> SaleItems { get; set; }
    }

    public class SaleItemDto
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
        public string? Notes { get; set; }

        // Navigation properties
        public Sale Sale { get; set; }
        public Product Product { get; set; }
    }
}
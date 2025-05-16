using System.ComponentModel.DataAnnotations;

namespace Project_Creation.Models.Entities
{
    public class Sale
    {
        [Key]
        public int Id { get; set; }
        public int BOId { get; set; } // Business Owner ID
        public string CustomerName { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime SaleDate { get; set; } = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));
        
        // Navigation property
        public ICollection<SaleItem> SaleItems { get; set; }
    }
}

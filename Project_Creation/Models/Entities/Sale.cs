using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project_Creation.Models.Entities
{
    public class Sale
    {
        [Key]
        public int Id { get; set; }
        public int BOId { get; set; } // Business Owner ID
        public int ProductId { get; set; } // Business Owner ID
        public string CustomerName { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime SaleDate { get; set; } =
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("Singapore")
            );
        public int? LeadId { get; set; } // Optional FK to Leads
        public string? CustomerEmail { get; set; }
        public string? CustomerPhone { get; set; }
        public int? EarnedPoints { get; set; }
        public string? TransactionId { get; set; } // Unique ID to group items in a single transaction

        // Navigation properties
        public ICollection<SaleItem> SaleItems { get; set; }

        [ForeignKey("LeadId")]
        public Leads? Lead { get; set; }
    }
}

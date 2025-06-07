using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project_Creation.Models.Entities
{
    public class InventoryLog
    {
        [Key]
        public int Id { get; set; }
        public int BOId { get; set; } // Business Owner ID
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public DateTime Timestamp { get; set; } =
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("Singapore")
            );
        public int QuantityBefore { get; set; }
        public int QuantityAfter { get; set; }

        // Add this property
        [NotMapped] // Calculate on the fly
        public int QuantityChange => QuantityAfter - QuantityBefore;

        // Add this property
        public string MovementType { get; set; } = "Adjustment"; // Default value

        [Required]
        public string ReferenceId { get; set; } = "SYSTEM"; // Default value

        public string Notes { get; set; }

        // Navigation property
        [ForeignKey("ProductId")]
        public Product Product { get; set; }
    }
}

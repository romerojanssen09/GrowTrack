// InventoryTransaction.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace Project_Creation.Models.Entities
{
    public class Transaction
    {
        public int Id { get; set; }
        public int BOId { get; set; } // Business Owner ID

        [Required]
        public int ProductId { get; set; }
        public Product Product { get; set; }

        [Required]
        public TransactionType Type { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be positive")]
        public int Quantity { get; set; }

        public string Notes { get; set; }

        [Required]
        public DateTime TransactionDate { get; set; } = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));

        public string ReferenceNumber { get; set; }

        // For adjustments
        public int? PreviousQuantity { get; set; }
        public int? NewQuantity { get; set; }

        // For returns
        public string CustomerName { get; set; }
        public string Reason { get; set; }
    }

    public enum TransactionType
    {
        Purchase,    // Adding stock
        Sale,        // Removing stock (sales)
        Adjustment,  // Manual quantity correction
        Return,      // Customer returns
        Damage,      // Damaged goods
        Transfer     // Between locations
    }
}
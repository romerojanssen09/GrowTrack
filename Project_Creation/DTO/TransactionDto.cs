// TransactionDto.cs
using System;
using System.ComponentModel.DataAnnotations;
using Project_Creation.Models.Entities;

namespace Project_Creation.DTO
{
    public class TransactionDto
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Product")]
        public int ProductId { get; set; }

        [Required]
        [Display(Name = "Transaction Type")]
        public TransactionType Type { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be positive")]
        public int Quantity { get; set; }

        [Display(Name = "Notes")]
        [StringLength(500)]
        public string Notes { get; set; }

        // For adjustments
        [Display(Name = "Current Quantity")]
        public int? CurrentQuantity { get; set; }

        [Display(Name = "New Quantity")]
        public int? NewQuantity { get; set; }

        // For returns
        [Display(Name = "Customer Name")]
        public string CustomerName { get; set; }

        [Display(Name = "Reason")]
        public string Reason { get; set; }
    }
}
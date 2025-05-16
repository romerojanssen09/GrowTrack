using System.ComponentModel.DataAnnotations;

namespace Project_Creation.DTO
{
    public class InventoryTransactionDto
    {
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        public required string Type { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        public string? Notes { get; set; }

        public string? ReferenceNumber { get; set; }
    }
} 
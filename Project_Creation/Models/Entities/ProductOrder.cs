using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project_Creation.Models.Entities
{
    public class ProductOrder
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int BuyerId { get; set; } // The user who requested the product
        public int SellerId { get; set; } // The owner of the product

        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }
        
        public string? Message { get; set; }
        public int? RelatedChatId { get; set; } // ID of the chat message where this was requested

        public ProductOrderStatus Status { get; set; } = ProductOrderStatus.Pending;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? PreparedAt { get; set; }
        public DateTime? ShippedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? ReceivedAt { get; set; }

        // Navigation properties
        [ForeignKey("BuyerId")]
        public Users Buyer { get; set; }
        
        [ForeignKey("SellerId")]
        public Users Seller { get; set; }
        
        [ForeignKey("ProductId")]
        public Product Product { get; set; }
    }

    public enum ProductOrderStatus
    {
        Pending,    // Initial state when request is created
        Accepted,   // Seller has accepted the request
        Preparing,  // Seller is preparing the order
        Shipping,   // Order is on the way
        Delivered,  // Order has been delivered
        Received,   // Buyer has confirmed receipt
        Cancelled,  // Order was cancelled
        Rejected    // Seller rejected the request
    }
} 
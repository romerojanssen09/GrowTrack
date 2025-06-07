using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Project_Creation.Models.Entities;

namespace Project_Creation.Models.ViewModels
{
    public class ProductOrderListViewModel
    {
        public List<ProductOrderViewModel> Orders { get; set; } = new List<ProductOrderViewModel>();
        public int TotalOrders { get; set; }
        public int PageSize { get; set; } = 10;
        public int CurrentPage { get; set; } = 1;
        public int TotalPages => (int)Math.Ceiling((double)TotalOrders / PageSize);
        public string? StatusFilter { get; set; }
        public string? SearchQuery { get; set; }
        public bool IsSellerView { get; set; } // Whether viewing as a seller or buyer
        
        // Status counts
        public int PendingCount { get; set; }
        public int AcceptedCount { get; set; }
        public int PreparingCount { get; set; }
        public int ShippingCount { get; set; }
        public int DeliveredCount { get; set; }
        public int ReceivedCount { get; set; }
        public int CancelledCount { get; set; }
        public int RejectedCount { get; set; }
    }
    
    public class ProductOrderViewModel
    {
        public int Id { get; set; }
        
        public int BuyerId { get; set; }
        public string BuyerName { get; set; }
        
        public int SellerId { get; set; }
        public string SellerName { get; set; }
        public string BusinessName { get; set; }
        
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string ProductImageUrl { get; set; }
        
        public int Quantity { get; set; }
        
        [DataType(DataType.Currency)]
        public decimal UnitPrice { get; set; }
        
        [DataType(DataType.Currency)]
        public decimal TotalPrice { get; set; }
        
        public string? Message { get; set; }
        public int? RelatedChatId { get; set; }
        
        public ProductOrderStatus Status { get; set; }
        public string StatusText => Status.ToString();
        
        // Helper property to determine badge color
        public string StatusBadgeClass
        {
            get
            {
                return Status switch
                {
                    ProductOrderStatus.Pending => "bg-secondary",
                    ProductOrderStatus.Accepted => "bg-info",
                    ProductOrderStatus.Preparing => "bg-primary",
                    ProductOrderStatus.Shipping => "bg-warning",
                    ProductOrderStatus.Delivered => "bg-success",
                    ProductOrderStatus.Received => "bg-success",
                    ProductOrderStatus.Cancelled => "bg-danger",
                    ProductOrderStatus.Rejected => "bg-danger",
                    _ => "bg-secondary"
                };
            }
        }
        
        // Helper property to determine if the order can be cancelled
        public bool CanCancel
        {
            get
            {
                // Orders can be cancelled if they're pending, accepted, or preparing
                return Status == ProductOrderStatus.Pending || 
                       Status == ProductOrderStatus.Accepted || 
                       Status == ProductOrderStatus.Preparing;
            }
        }
        
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? PreparedAt { get; set; }
        public DateTime? ShippedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? ReceivedAt { get; set; }
    }
} 
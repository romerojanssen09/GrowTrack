using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Project_Creation.Models.Entities;

namespace Project_Creation.Models.ViewModels
{
    public class StockInViewModel
    {
        [Required(ErrorMessage = "Please select a product")]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "Quantity is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }

        [DataType(DataType.Date)]
        public DateTime ReceivingDate { get; set; }

        public string Notes { get; set; } = "Stock In";
        public List<Product>? AvailableProducts { get; set; }
    }
}
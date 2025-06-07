using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Project_Creation.Models.Entities;

namespace Project_Creation.Models.ViewModels
{
    public class QuickSaleViewModel : IValidatableObject
    {
        [StringLength(100, ErrorMessage = "Customer name cannot exceed 100 characters")]
        public string? CustomerName { get; set; }
        
        public string? CustomerEmail { get; set; }
        
        public string? CustomerPhone { get; set; }
        public bool IsAllowToCampaign { get; set; }

        [Required(ErrorMessage = "Sale date is required")]
        [DataType(DataType.DateTime)]
        public DateTime SaleDate { get; set; } = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"));

        // For existing lead selection
        public int? LeadId { get; set; }
        
        // Create a new lead from this sale?
        public bool CreateLead { get; set; } = true;

        //public int ProductId { get; set; }

        [MinLength(1, ErrorMessage = "At least one sale item is required")]
        public List<SaleItemViewModel> Items { get; set; } = new List<SaleItemViewModel>();

        // This should not be required - it's populated by the controller
        public List<Product>? AvailableProducts { get; set; }
        
        // This is populated by the controller
        public List<Leads>? AvailableLeads { get; set; }

        // Implement IValidatableObject for custom validation
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Validate that either LeadId or CustomerName is provided (but not both)
            if (!LeadId.HasValue && string.IsNullOrWhiteSpace(CustomerName) && CustomerName != "Anonymous Buyer")
            {
                yield return new ValidationResult(
                    "Either select an existing lead or provide a customer name",
                    new[] { nameof(LeadId), nameof(CustomerName) });
            }

            if (Items == null || Items.Count == 0)
            {
                yield return new ValidationResult(
                    "At least one sale item is required",
                    new[] { nameof(Items) });
            }
            else
            {
                // Validate each sale item
                for (int i = 0; i < Items.Count; i++)
                {
                    var item = Items[i];
                    if (item.Quantity <= 0)
                    {
                        yield return new ValidationResult(
                            "Quantity must be greater than 0",
                            new[] { $"Items[{i}].Quantity" });
                    }

                    if (item.Price <= 0)
                    {
                        yield return new ValidationResult(
                            "Price must be greater than 0",
                            new[] { $"Items[{i}].Price" });
                    }
                }
            }
        }

        [NotMapped]
        [Required(ErrorMessage = "Please select a customer type (Existing Lead, New Customer, or Anonymous Buyer)")]
        public required string SelectedOptions { get; set; }
    }

    public class SaleItemViewModel
    {
        [Required(ErrorMessage = "Product is required")]
        public int ProductId { get; set; }

        // This is just for display/processing, not for form submission
        public string? ProductName { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; } = 1;

        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
        public decimal Price { get; set; }

        public int AvailableStock { get; set; }
    }
}
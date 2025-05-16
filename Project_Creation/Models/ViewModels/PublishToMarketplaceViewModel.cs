using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Project_Creation.Models.ViewModels
{
    public class PublishToMarketplaceViewModel
    {
        public int ProductId { get; set; }

        [Display(Name = "Product Name")]
        public string ProductName { get; set; }

        [Display(Name = "Current Description")]
        public string Description { get; set; }

        [Display(Name = "Regular Price")]
        [DisplayFormat(DataFormatString = "{0:C}")]
        public decimal Price { get; set; }

        [Display(Name = "Publish to Marketplace")]
        public bool IsPublished { get; set; }

        [Display(Name = "Marketplace Description")]
        [DataType(DataType.MultilineText)]
        [RequiredIf("IsPublished", true, ErrorMessage = "Marketplace description is required when publishing")]
        public string MarketplaceDescription { get; set; }

        [Display(Name = "Marketplace Price")]
        [DataType(DataType.Currency)]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
        [RequiredIf("IsPublished", true, ErrorMessage = "Marketplace price is required when publishing")]
        public decimal? MarketplacePrice { get; set; }

        [Display(Name = "Featured Product")]
        public bool DisplayFeatured { get; set; }

        [Display(Name = "Product Images")]
        [ValidateFile(AllowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" }, MaxSize = 5 * 1024 * 1024)]
        public List<IFormFile> ImageFiles { get; set; } = new List<IFormFile>();

        public List<ProductImageViewModel> ExistingImages { get; set; } = new List<ProductImageViewModel>();
    }
    public class ProductImageViewModel
    {
        public int Id { get; set; }
        public string ImagePath { get; set; }
        public bool IsMainImage { get; set; }
        public string Title { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class RequiredIfAttribute : ValidationAttribute
    {
        private string PropertyName { get; set; }
        private object DesiredValue { get; set; }

        public RequiredIfAttribute(string propertyName, object desiredValue, string errorMessage = "")
        {
            PropertyName = propertyName;
            DesiredValue = desiredValue;
            ErrorMessage = errorMessage;
        }

        protected override ValidationResult IsValid(object value, ValidationContext context)
        {
            var instance = context.ObjectInstance;
            var type = instance.GetType();
            var propertyValue = type.GetProperty(PropertyName)?.GetValue(instance, null);

            if (propertyValue != null && propertyValue.ToString() == DesiredValue.ToString() && value == null)
            {
                return new ValidationResult(ErrorMessage);
            }
            return ValidationResult.Success;
        }
    }

    // Custom validation attribute for file uploads
    public class ValidateFileAttribute : ValidationAttribute
    {
        public string[] AllowedExtensions { get; set; }
        public int MaxSize { get; set; } = 5 * 1024 * 1024; // Default 5MB

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value is List<IFormFile> files)
            {
                foreach (var file in files)
                {
                    if (file != null)
                    {
                        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                        if (!AllowedExtensions.Contains(extension))
                        {
                            return new ValidationResult($"File type not allowed. Allowed types: {string.Join(", ", AllowedExtensions)}");
                        }

                        if (file.Length > MaxSize)
                        {
                            return new ValidationResult($"File size exceeds {MaxSize / (1024 * 1024)}MB limit");
                        }
                    }
                }
            }
            return ValidationResult.Success;
        }
    }
} 
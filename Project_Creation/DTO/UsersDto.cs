using Microsoft.EntityFrameworkCore;
using ServiceStack.DataAnnotations;
using System.ComponentModel.DataAnnotations;
using RequiredAttribute = System.ComponentModel.DataAnnotations.RequiredAttribute;
using StringLengthAttribute = System.ComponentModel.DataAnnotations.StringLengthAttribute;

namespace Project_Creation.DTO
{
    public class UsersDto
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public required string FirstName { get; set; }

        [Required]
        [StringLength(50)]
        public required string LastName { get; set; }

        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        [RegularExpression(@"^\d{11}$", ErrorMessage = "Phone number must be exactly 11 digits.")]
        public required string PhoneNumber { get; set; }

        [Required]
        [StringLength(100)]
        public required string BusinessName { get; set; }

        [Required]
        public required string BusinessAddress { get; set; }

        [Required]
        [StringLength(13, MinimumLength = 9, ErrorMessage = "DTI Registration Number must be between 9 and 13 characters.")]
        public required string DTIReqistrationNumber { get; set; }

        [Required]
        public required string NumberOfBusinessYearsOperation { get; set; }
        public string? NumberOfEmployees { get; set; }

        [Required(ErrorMessage = "Please select a business category")]
        public required string CategoryOfBusiness { get; set; }

        //public string? CategoryOfBusiness2 { get; set; }

        [Required]
        public required string CompanyBackground { get; set; }
        public string? BusinessPermitPath { get; set; }
    }
}
using System.ComponentModel.DataAnnotations;
using RequiredAttribute = ServiceStack.DataAnnotations.RequiredAttribute;

namespace Project_Creation.DTO
{
    public class Supplier2Dto
    {
        public int SupplierID { get; set; }
        public int? BOId { get; set; }

        [Required]
        public string SupplierName { get; set; } = string.Empty;

        [Required]
        public string ContactPerson { get; set; } = string.Empty;

        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Phone]
        public string Phone { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;
    }
}

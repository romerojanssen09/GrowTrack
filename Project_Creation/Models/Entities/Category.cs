using System.ComponentModel.DataAnnotations;

namespace Project_Creation.Models.Entities
{
    public class Category
    {
        public int Id { get; set; }
        [Required]
        [StringLength(100)]
        public required string CategoryName { get; set; }
        public required int BOId { get; set; }
    }
}

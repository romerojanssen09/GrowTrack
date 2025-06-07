using System.ComponentModel.DataAnnotations;
using ServiceStack.DataAnnotations;

namespace Project_Creation.Models.Entities
{
    public class Supplier
    {
        public Supplier()
        {
            InventoryTransactions = new List<Transaction>();
            Images = new List<ProductImage>();
        }
        public ICollection<ProductImage> Images { get; set; }
        public ICollection<Transaction> InventoryTransactions { get; set; }
        public ICollection<Product> Products { get; set; } = new List<Product>();

        [Key]
        [AutoIncrement]
        public int SupplierID { get; set; }
        public required int BOId { get; set; }
        public required string SupplierName { get; set; }
        public required string ContactPerson { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
    }
}

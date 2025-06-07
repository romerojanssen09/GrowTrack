using System.ComponentModel.DataAnnotations.Schema;

namespace Project_Creation.Models.Entities
{
    public class Leads
    {
        public int Id { get; set; }
        public required string LeadName { get; set; }
        public required string LeadEmail { get; set; }
        public required string LeadPhone { get; set; }
        public string? Notes { get; set; }
        public bool IsAllowToCampaign { get; set; } = true;
        public LeadStatus Status { get; set; } = LeadStatus.New;
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? LastContacted { get; set; }
        public int CreatedById { get; set; }
        public string? InterestedProductIds { get; set; }
        public int? LastPurchasedId { get; set; }
        public int? LeadPoints { get; set; }
        public DateTime? LastPurchaseDate { get; set; }

        public Users? CreatedBy { get; set; }
        public Product? Product { get; set; }
        
        [NotMapped]
        public List<Product> SelectedProduct { get; set; } = new List<Product>();
        
        [NotMapped]
        public List<int> SelectedProductIds { get; set; } = new List<int>();
        
        [NotMapped]
        public List<string> InterestedProductNames { get; set; } = new List<string>();
        
        [NotMapped]
        public List<SaleItem> PurchaseHistory { get; set; } = new List<SaleItem>();

        [NotMapped]
        public string? LastPurchasedName { get; set; }

        public enum LeadStatus
        {
            New,
            Warm,
            Hot,
            Cold,
            Lost,
            Deleted
        }
    }
}

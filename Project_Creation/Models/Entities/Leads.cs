using System.ComponentModel.DataAnnotations.Schema;

namespace Project_Creation.Models.Entities
{
    public class Leads
    {
        public int Id { get; set; }
        public required string LeadName { get; set; }
        public required string LeadEmail { get; set; }
        public required string LeadPhone { get; set; }
        public string? InterestedIn { get; set; } // Will store comma-separated product IDs
        public string? Notes { get; set; }
        public LeadStatus Status { get; set; } = LeadStatus.New;
        public required int BOId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Navigation property
        public List<Product2>? Products { get; set; }

        // Helper property for view (not mapped to DB)
        [NotMapped]
        public List<int> SelectedProductIds { get; set; } = new List<int>();

        public enum LeadStatus
        {
            New,
            Contacted
        }
    }
}
using Project_Creation.Models.Entities;

namespace Project_Creation.DTO
{
    public class CreateCampaignViewModel
    {
        public List<Leads> MyLeads { get; set; } = new();
        public List<Leads> SharedLeads { get; set; } = new();
        public List<Leads> PublicLeads { get; set; } = new();
    }
}

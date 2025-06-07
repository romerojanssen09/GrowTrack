using Project_Creation.Models.Entities;

namespace Project_Creation.Models.ViewModels
{
    public class LeadsViewModels
    {
        public List<Leads>? Leads { get; set; }
        public int? TotalLeads { get; set; }
        public int? TotalLeadsToday { get; set; }       //1
        public int? TotalLeadsThisWeek { get; set; }
        public int? TotalLeadsThisMonth { get; set; }
        public int? TotalLeadsThisYear { get; set; }
        public int? LastContactedThisDay { get; set; }  //1
        public int? LastContactedThisWeek { get; set; }
        public int? LastContactedThisMonth { get; set; }
        public int? LastContactedThisYear { get; set; }
    }
}

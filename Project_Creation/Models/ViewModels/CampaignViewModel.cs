using Microsoft.AspNetCore.Mvc.Rendering;
using Project_Creation.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace Project_Creation.Models.ViewModels
{
    public class CampaignFilterViewModel
    {
        // Campaign basic info
        [Required(ErrorMessage = "Campaign name is required")]
        [Display(Name = "Campaign Name")]
        public string CampaignName { get; set; }

        [Required(ErrorMessage = "Message content is required")]
        [Display(Name = "Message")]
        public string Message { get; set; }

        [Display(Name = "Subject")]
        public string Subject { get; set; } = "New Campaign Message";

        [Display(Name = "Notes")]
        public string Notes { get; set; }

        // Lead filtering options
        [Display(Name = "Lead Status")]
        public Leads.LeadStatus? FilterStatus { get; set; }

        [Display(Name = "Minimum Points")]
        public int? FilterMinPoints { get; set; }

        [Display(Name = "Maximum Points")]
        public int? FilterMaxPoints { get; set; }

        [Display(Name = "Product Category")]
        public string FilterProductCategory { get; set; }

        [Display(Name = "Product")]
        public int? FilterProductId { get; set; }

        [Display(Name = "Created By")]
        public int? FilterCreatedById { get; set; }

        [Display(Name = "Search")]
        public string FilterSearch { get; set; }

        [Display(Name = "Has Purchase History")]
        public bool? FilterHasPurchaseHistory { get; set; }

        [Display(Name = "Last Contacted Before")]
        public DateTime? FilterLastContactedBefore { get; set; }

        [Display(Name = "Last Contacted After")]
        public DateTime? FilterLastContactedAfter { get; set; }

        // Template options
        [Display(Name = "Save as Template")]
        public bool SaveAsTemplate { get; set; }

        [Display(Name = "Template Name")]
        public string TemplateName { get; set; }

        [Display(Name = "Select Template")]
        public int? SelectedTemplateId { get; set; }

        // Collections for display/selection
        public List<Leads> MyLeads { get; set; } = new List<Leads>();
        public List<Leads> SharedLeads { get; set; } = new List<Leads>();
        public List<Leads> PublicLeads { get; set; } = new List<Leads>();
        public List<Leads> FilteredLeads { get; set; } = new List<Leads>();
        public List<MessageTemplate> Templates { get; set; } = new List<MessageTemplate>();
        public List<SelectListItem> ProductCategories { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Products { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> BusinessOwners { get; set; } = new List<SelectListItem>();
        public List<int> SelectedLeadIds { get; set; } = new List<int>();
    }
} 
using Project_Creation.Models.Entities;
using System.Collections.Generic;

namespace Project_Creation.DTO
{
    public class AdminDashboardViewModel
    {
        public List<Users> Step1_Applicants { get; set; } = new(); // Not Verified
        public List<Users> Step2_Applicants { get; set; } = new(); // Verified, Not Yet Allowed/Disallowed
        public List<Users> AllUsers { get; set; } = new(); // All users for status display
        public List<string>? BusinessCategories { get; set; }
    }
}
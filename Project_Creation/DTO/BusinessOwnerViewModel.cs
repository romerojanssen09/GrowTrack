using System.Collections.Generic;

namespace Project_Creation.DTO
{
    public class BusinessOwnerViewModel
    {
        public int Id { get; set; }
        public string BusinessName { get; set; }
        public string OwnerName { get; set; }
        public string Email { get; set; }
        public int StaffLimit { get; set; }
        public int CurrentStaffCount { get; set; }
    }
} 
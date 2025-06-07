using Project_Creation.Models.Entities;
using static Project_Creation.Models.Entities.Users;
using AccountStatus = Project_Creation.Models.Entities.Users.AccountStatuss;

namespace Project_Creation.DTO
{
    public class AccountManagementViewModel
    {
        public int Id { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string CategoryOfBusiness { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
        public MarketplaceStatus MarketplaceStatus { get; set; }
        public AccountStatuss AccountStatus { get; set; } 
        public DateTime RegistrationDate { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public DateTime? LastStatusChangeDate { get; set; }
        public int CurrentStaffCount { get; set; }
        public int StaffLimit { get; set; }
    }
}

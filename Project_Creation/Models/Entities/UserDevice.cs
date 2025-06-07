using System;
using System.ComponentModel.DataAnnotations;

namespace Project_Creation.Models.Entities
{
    public class UserDevice
    {
        [Key]
        public int Id { get; set; }
        
        public int UserId { get; set; }
        public string DeviceId { get; set; }
        public string DeviceName { get; set; }
        public string BrowserName { get; set; }
        public string BrowserVersion { get; set; }
        public string OperatingSystem { get; set; }
        public string IpAddress { get; set; }
        public DateTime LastLogin { get; set; }
        public bool IsTrusted { get; set; }
    }
} 
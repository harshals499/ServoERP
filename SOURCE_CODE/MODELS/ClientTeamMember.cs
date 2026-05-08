using System;

namespace HVAC_Pro_Desktop.Models
{
    public class ClientTeamMember
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public string EmployeeName { get; set; }
        public string Position { get; set; }
        public string EmailId { get; set; }
        public string ContactNo { get; set; }
        public bool IsPrimary { get; set; }
        public bool IsActive { get; set; } = true;
    }
}

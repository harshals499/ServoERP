using System;

namespace HVAC_Pro_Desktop.Models
{
    public class ClientContact
    {
        public int ContactID { get; set; }
        public int ClientID { get; set; }
        public string ContactName { get; set; }
        public string Role { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public bool IsPrimary { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}

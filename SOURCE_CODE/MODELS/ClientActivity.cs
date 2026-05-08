using System;

namespace HVAC_Pro_Desktop.Models
{
    public class ClientActivity
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public string ActivityType { get; set; }
        public string Title { get; set; }
        public string Detail { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; }
    }
}

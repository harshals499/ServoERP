using System;

namespace HVAC_Pro_Desktop.Models
{
    public class PorterDelivery
    {
        public int PorterDeliveryId { get; set; }
        public string BookingReference { get; set; }
        public int? ClientId { get; set; }
        public int? SiteId { get; set; }
        public int? LinkedJobId { get; set; }
        public string ClientName { get; set; }
        public string SiteName { get; set; }
        public string PickupAddress { get; set; }
        public string DropAddress { get; set; }
        public string ContactName { get; set; }
        public string ContactPhone { get; set; }
        public string VehicleType { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public string Status { get; set; }
        public decimal EstimatedAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public string DriverName { get; set; }
        public string DriverPhone { get; set; }
        public string TrackingUrl { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedByName { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public string ModifiedByName { get; set; }
    }
}

using System;

namespace HVAC_Pro_Desktop.Models
{
    public class ClientSite
    {
        public int    SiteID                   { get; set; }
        public Guid? SyncPublicId { get; set; }
        public Guid? OriginNodeId { get; set; }
        public Guid? LastModifiedNodeId { get; set; }
        public DateTime? CreatedUtc { get; set; }
        public DateTime? UpdatedUtc { get; set; }
        public DateTime? DeletedUtc { get; set; }
        public long SyncVersion { get; set; }
        public int    ClientID                 { get; set; }
        public string SiteName                 { get; set; }
        public string Address                  { get; set; }
        public string City                     { get; set; }
        public int    ACSystemCount            { get; set; }
        public int    RefrigerationSystemCount { get; set; }
        public int    CoolingTowerCount        { get; set; }
        public bool   IsCritical               { get; set; }
        public int?   AssignedTechnicianID     { get; set; }
        public double? GeoLatitude             { get; set; }
        public double? GeoLongitude            { get; set; }
        public string GeocodeAddress           { get; set; }
        public string GeocodeStatus            { get; set; }
        public DateTime? GeocodeUpdatedOn      { get; set; }
        public decimal TravelRateINR           { get; set; }
        public string DisplayName              { get; set; }
        public override string ToString() => string.IsNullOrWhiteSpace(DisplayName) ? (SiteName ?? "") : DisplayName;
    }
}

using System;

namespace HVAC_Pro_Desktop.Models
{
    public class AMCContract
    {
        public int ContractID { get; set; }
        public int ClientID { get; set; }
        public int SiteID { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }  // CRITICAL for renewal alerts
        public decimal MonthlyValue { get; set; }
        public decimal AnnualValue { get; set; }
        public string ContractStatus { get; set; }  // Active, Expiring, Expired
        public int SLAResponseTimeHours { get; set; }  // e.g., 2 hours
        public decimal SLAUptimePercent { get; set; }  // e.g., 99.5%
        public int SLARepairTimeHours { get; set; }  // e.g., 4 hours
        public string MaintenanceFrequency { get; set; }  // Monthly, Quarterly, etc.
        public string ContractType         { get; set; } = "AMC";   // AMC | O&M | CMC | Warranty
        public string Notes                { get; set; }
        public int? CreatedByUserId { get; set; }
        public string CreatedByName { get; set; }
        public int? ModifiedByUserId { get; set; }
        public string ModifiedByName { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }
}

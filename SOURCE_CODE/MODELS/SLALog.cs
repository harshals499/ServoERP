using System;

namespace HVAC_Pro_Desktop.Models
{
    public class SLALog
    {
        public int LogID { get; set; }
        public int ContractID { get; set; }
        public string MetricType { get; set; }  // ResponseTime, Uptime, RepairTime
        public string Target { get; set; }  // e.g., "2 hours"
        public string Actual { get; set; }  // e.g., "1.5 hours"
        public DateTime LogDate { get; set; }
        public bool Compliant { get; set; }
        public string Notes { get; set; }
    }
}

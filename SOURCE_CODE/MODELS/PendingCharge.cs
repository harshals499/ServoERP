using System;

namespace HVAC_Pro_Desktop.Models
{
    public class PendingCharge
    {
        public int PendingChargeId { get; set; }
        public int WorkOrderId { get; set; }
        public string WorkOrderName { get; set; }
        public string ClientName { get; set; }
        public string Description { get; set; }
        public decimal Quantity { get; set; }
        public decimal Rate { get; set; }
        public string HsnSac { get; set; }
        public decimal GSTRate { get; set; }
        public int SourcePoId { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsBilled { get; set; }
    }

    public class PendingChargeResult
    {
        public bool Created { get; set; }
        public bool AlreadyExists { get; set; }
        public bool Skipped { get; set; }
        public string Message { get; set; }
        public string WorkOrderName { get; set; }
        public DateTime? CreatedDate { get; set; }
    }
}

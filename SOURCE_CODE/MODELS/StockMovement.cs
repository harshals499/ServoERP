using System;

namespace HVAC_Pro_Desktop.Models
{
    public class StockMovement
    {
        public int MovementID { get; set; }
        public int ItemID { get; set; }
        public string ItemName { get; set; }
        public string MovementType { get; set; }
        public decimal Quantity { get; set; }
        public decimal StockBefore { get; set; }
        public decimal StockAfter { get; set; }
        public string FromLocation { get; set; }
        public string ToLocation { get; set; }
        public string ReferenceNo { get; set; }
        public string Notes { get; set; }
        public int? CreatedByUserId { get; set; }
        public string CreatedByName { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}

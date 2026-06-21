using System;

namespace HVAC_Pro_Desktop.Models
{
    public class SupplierItemPrice
    {
        public int PriceID { get; set; }
        public int? ItemID { get; set; }
        public int VendorID { get; set; }
        public string VendorName { get; set; }
        public string ItemName { get; set; }
        public string Category { get; set; }
        public string Unit { get; set; }
        public decimal Rate { get; set; }
        public string Source { get; set; }
        public DateTime EffectiveDate { get; set; } = DateTime.Now;
        public bool IsPreferred { get; set; }
        public bool IsActive { get; set; } = true;
        public string Notes { get; set; }
    }
}

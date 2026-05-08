using System;

namespace HVAC_Pro_Desktop.Models
{
    public class StockItem
    {
        public int      ItemID           { get; set; }
        public string   ItemName         { get; set; }
        public string   Category         { get; set; }
        public decimal  CurrentStock     { get; set; }
        public string   Unit             { get; set; }
        public decimal  LastPurchaseRate { get; set; }
        public decimal  ReorderLevel     { get; set; }
        public decimal  ReservedStock    { get; set; }
        public int?     VendorID         { get; set; }
        public string   VendorName       { get; set; }
        public DateTime LastUpdated      { get; set; }

        public bool     IsLowStock       => CurrentStock <= ReorderLevel;
        public decimal  StockValue       => CurrentStock * LastPurchaseRate;
        public decimal  AvailableStock   => CurrentStock - ReservedStock;
    }
}

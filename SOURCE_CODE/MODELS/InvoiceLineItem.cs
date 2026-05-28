namespace HVAC_Pro_Desktop.Models
{
    public class InvoiceLineItem
    {
        public int    LineItemID  { get; set; }
        public int    InvoiceID   { get; set; }
        public int?   StockItemID { get; set; }
        public string Description { get; set; }
        public string HSNCode     { get; set; }
        public string Category    { get; set; } = "Service";
        public string Unit        { get; set; } = "Nos";
        public decimal Quantity   { get; set; } = 1;
        public decimal Rate       { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal GSTPercent { get; set; } = 18m;
        public string TaxType     { get; set; } = "Taxable";
        public decimal TaxAmount  { get; set; }
        public bool   IsStockItem { get; set; }
        public bool   IsBillable  { get; set; } = true;
        public string CoverageNote { get; set; }
        public decimal Amount     { get; set; }   // Quantity * Rate (calculated)
    }
}

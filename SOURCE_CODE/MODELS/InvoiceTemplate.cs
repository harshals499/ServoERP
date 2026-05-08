using System.Collections.Generic;

namespace HVAC_Pro_Desktop.Models
{
    public class InvoiceTemplate
    {
        public int TemplateID { get; set; }
        public string TemplateCode { get; set; }
        public string TemplateName { get; set; }
        public string WorkflowType { get; set; }
        public string DefaultSubject { get; set; }
        public string DefaultNotes { get; set; }
        public string DefaultGstMode { get; set; }
        public decimal DefaultGstPercent { get; set; } = 18m;
        public string DefaultPaymentTerms { get; set; }
        public string ContractCoverageType { get; set; }
        public string DefaultChecklist { get; set; }
        public string DefaultAssetInfo { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class InvoiceTemplateLine
    {
        public string Description { get; set; }
        public string HsnSacCode { get; set; }
        public string Unit { get; set; } = "Nos";
        public decimal Quantity { get; set; } = 1m;
        public decimal Rate { get; set; }
        public bool IsStockItem { get; set; }
        public string StockLookupName { get; set; }
        public decimal GstPercent { get; set; } = 18m;
        public bool IsBillable { get; set; } = true;
        public string CoverageNote { get; set; }
    }
}

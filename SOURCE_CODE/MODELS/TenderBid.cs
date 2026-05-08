using System;
using System.Collections.Generic;

namespace HVAC_Pro_Desktop.Models
{
    public class TenderBid
    {
        public int BidID { get; set; }
        public string QuotationNumber { get; set; }
        public string TenderName { get; set; }
        public int ClientID { get; set; }
        public int SiteID { get; set; }
        public int SystemCount { get; set; }
        public decimal BidValue { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? SubmittedDate { get; set; }
        public DateTime? RequiredByDate { get; set; }
        public string Status { get; set; }  // Draft, Analysed, Submitted, Won, Lost
        public string ClientName { get; set; }
        public string SiteName { get; set; }
        public string RequirementCategory { get; set; }
        public string ItemName { get; set; }
        public decimal RequiredQuantity { get; set; }
        public string Unit { get; set; }
        public decimal InventoryAvailable { get; set; }
        public decimal ShortfallQuantity { get; set; }
        public decimal EstimatedInternalRate { get; set; }
        public decimal EstimatedSupplierRate { get; set; }
        public decimal EstimatedInternalCost { get; set; }
        public decimal EstimatedExternalCost { get; set; }
        public int? RecommendedVendorID { get; set; }
        public string RecommendedVendorName { get; set; }
        public string ComparisonSummary { get; set; }
        public string AnalysisStatus { get; set; }
        public string Notes { get; set; }
        public bool IsMultiLine { get; set; }
        public decimal TotalTaxableValue { get; set; }
        public decimal TotalGSTAmount { get; set; }
        public decimal TotalWithGST { get; set; }
        public decimal AverageMarginPct { get; set; }
        public int? TemplateId { get; set; }
        public bool ClientPriceMemoryApplied { get; set; }
        public string SuggestionsJson { get; set; }
        public int? CreatedByUserId { get; set; }
        public string CreatedByName { get; set; }
        public int? ModifiedByUserId { get; set; }
        public string ModifiedByName { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public List<SupplierOption> SupplierOptions { get; set; } = new List<SupplierOption>();
        public List<TenderBidLineItem> LineItems { get; set; } = new List<TenderBidLineItem>();
        public List<string> Suggestions { get; set; } = new List<string>();
    }

    public class SupplierOption
    {
        public int VendorID { get; set; }
        public string VendorName { get; set; }
        public decimal Rate { get; set; }
        public string Unit { get; set; }
        public string Source { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public decimal EstimatedCost(decimal quantity) => Math.Round(quantity * Rate, 2);
        public override string ToString() => $"{VendorName} @ Rs {Rate:N2}/{Unit}";
    }

    public class TenderBidLineItem
    {
        public int LineItemId { get; set; }
        public int TenderBidId { get; set; }
        public int SortOrder { get; set; }
        public string Category { get; set; }
        public int? InventoryItemId { get; set; }
        public string ItemDescription { get; set; }
        public decimal Quantity { get; set; } = 1m;
        public string Unit { get; set; } = "Nos";
        public string HsnSacCode { get; set; }
        public decimal GSTRatePct { get; set; } = 18m;
        public int? BestSupplierId { get; set; }
        public string BestSupplierName { get; set; }
        public decimal CostPerUnit { get; set; }
        public decimal SellPricePerUnit { get; set; }
        public decimal TaxableLineTotal { get; set; }
        public decimal GSTAmount { get; set; }
        public decimal MarginPct { get; set; }
        public decimal StockAvailable { get; set; }
        public decimal Shortfall { get; set; }
        public bool IsInternalLabour { get; set; }
        public string AnalysisStatus { get; set; } = "Pending";
        public string AnalysisNotes { get; set; }
        public bool IsSellPriceManual { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime ModifiedDate { get; set; } = DateTime.Now;

        public bool PriceMemoryApplied { get; set; }
        public decimal SuggestedSellPrice { get; set; }
        public DateTime? PriceMemoryDate { get; set; }
        public string PriceMemoryQuotationNumber { get; set; }
        public decimal MinimumRecommendedPrice { get; set; }
    }

    public class QuoteTemplate
    {
        public int TemplateId { get; set; }
        public string TemplateName { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public List<QuoteTemplateItem> Items { get; set; } = new List<QuoteTemplateItem>();
    }

    public class QuoteTemplateItem
    {
        public int TemplateItemId { get; set; }
        public int TemplateId { get; set; }
        public int SortOrder { get; set; }
        public string Category { get; set; }
        public string ItemDescription { get; set; }
        public decimal DefaultQuantity { get; set; } = 1m;
        public string Unit { get; set; } = "Nos";
        public string HsnSacCode { get; set; }
        public decimal GSTRatePct { get; set; } = 18m;
        public decimal? DefaultMarkupPct { get; set; }
    }

    public class ClientPriceMemoryEntry
    {
        public int MemoryId { get; set; }
        public int ClientId { get; set; }
        public string ItemDescription { get; set; }
        public decimal LastQuotedPrice { get; set; }
        public DateTime LastQuoteDate { get; set; }
        public bool WasAccepted { get; set; }
        public string QuotationNumber { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}

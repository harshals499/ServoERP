using System;

namespace HVAC_Pro_Desktop.Models
{
    public class IndiaCompanySettings
    {
        public string CompanyName { get; set; } = string.Empty;
        public string GSTIN { get; set; } = string.Empty;
        public string PAN { get; set; } = string.Empty;
        public string TAN { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string CompanyState { get; set; } = "Maharashtra";
        public string GSTRegistrationType { get; set; } = "Regular";
        public string InvoicePrefix { get; set; } = "INV";
        public decimal DefaultGSTRate { get; set; } = 18m;
        public int DefaultPaymentTermsDays { get; set; } = 30;
        public decimal AnnualTurnover { get; set; }
        public decimal EInvoiceThresholdAmount { get; set; } = 50000000m;
        public bool EInvoiceAutoEnabled { get; set; }
        public string CurrencyCode { get; set; } = "INR";
        public string CurrencySymbol { get; set; } = "\u20B9";
        public string FinancialYearPattern { get; set; } = "01/04 - 31/03";
        public string DefaultPlaceOfSupply { get; set; } = "Maharashtra";
        public string DefaultCertificationNote { get; set; } = string.Empty;
        public double? OfficeLatitude { get; set; }
        public double? OfficeLongitude { get; set; }
        public DateTime SnapshotDate { get; set; } = DateTime.Today;
    }
}

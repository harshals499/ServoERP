using System;
using System.Collections.Generic;

namespace HVAC_Pro_Desktop.Models
{
    public sealed class JobProfitabilityRow
    {
        public int JobId { get; set; }
        public string JobNumber { get; set; }
        public string JobTitle { get; set; }
        public string ClientName { get; set; }
        public string SiteName { get; set; }
        public string JobStatus { get; set; }
        public DateTime ReportingDate { get; set; }
        public decimal QuotedRevenue { get; set; }
        public decimal BilledRevenue { get; set; }
        public decimal CommittedVendorCost { get; set; }
        public decimal MaterialCost { get; set; }
        public decimal LabourCost { get; set; }
        public decimal TravelCost { get; set; }
        public decimal OtherDirectCost { get; set; }
        public decimal ActualDirectCost { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal GrossMarginPercent { get; set; }
        public decimal AmountCollected { get; set; }
        public decimal OutstandingAmount { get; set; }
        public string CostStatus { get; set; }
        public string InvoiceNumber { get; set; }
    }

    public sealed class MonthlyProfitLossRow
    {
        public DateTime Month { get; set; }
        public decimal Revenue { get; set; }
        public decimal DirectCosts { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal GrossMarginPercent { get; set; }
        public decimal PayrollExpense { get; set; }
        public decimal OperatingExpenses { get; set; }
        public decimal NetProfit { get; set; }
        public decimal NetMarginPercent { get; set; }
    }

    public sealed class FinancialDashboardSnapshot
    {
        public decimal Revenue { get; set; }
        public decimal DirectCosts { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal GrossMarginPercent { get; set; }
        public decimal PayrollExpense { get; set; }
        public decimal OperatingExpenses { get; set; }
        public decimal NetProfit { get; set; }
        public decimal NetMarginPercent { get; set; }
        public decimal RevenueChangePercent { get; set; }
        public decimal GrossProfitChangePercent { get; set; }
        public decimal ExpenseChangePercent { get; set; }
        public decimal NetProfitChangePercent { get; set; }
        public IList<MonthlyProfitLossRow> MonthlyTrend { get; set; } = new List<MonthlyProfitLossRow>();
    }

    public sealed class ExpenseCategory
    {
        public int ExpenseCategoryId { get; set; }
        public string CategoryName { get; set; }
        public string ProfitLossGroup { get; set; }
        public bool IsActive { get; set; }
        public override string ToString() => CategoryName ?? string.Empty;
    }

    public sealed class ExpenseEntry
    {
        public int ExpenseEntryId { get; set; }
        public int ExpenseCategoryId { get; set; }
        public int? JobId { get; set; }
        public int? ClientId { get; set; }
        public int? SiteId { get; set; }
        public DateTime ExpenseDate { get; set; }
        public decimal Amount { get; set; }
        public string ReferenceNumber { get; set; }
        public string Description { get; set; }
        public string CategoryName { get; set; }
        public string ProfitLossGroup { get; set; }
        public string CreatedByName { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public sealed class ProfitabilityImportRow
    {
        public int SourceRowNumber { get; set; }
        public string CustomerName { get; set; }
        public string Description { get; set; }
        public string QuotationNumber { get; set; }
        public decimal? QuotationAmount { get; set; }
        public string CustomerPoNumber { get; set; }
        public decimal? CustomerPoAmount { get; set; }
        public string InvoiceNumber { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public decimal? TaxableRevenue { get; set; }
        public string VendorName { get; set; }
        public decimal? VendorCost { get; set; }
        public string WorkStatus { get; set; }
        public string PaymentStatus { get; set; }
        public int? MatchedInvoiceId { get; set; }
        public int? MatchedJobId { get; set; }
        public string ReviewStatus { get; set; }
        public string ReviewMessage { get; set; }
    }

    public sealed class ProfitabilityImportPreview
    {
        public string SourceFileName { get; set; }
        public string WorksheetName { get; set; }
        public int ValidRowCount { get; set; }
        public int MatchedRowCount { get; set; }
        public int ReviewRowCount { get; set; }
        public List<ProfitabilityImportRow> Rows { get; set; } = new List<ProfitabilityImportRow>();
    }
}

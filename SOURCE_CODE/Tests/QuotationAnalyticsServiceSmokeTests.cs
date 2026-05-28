using System;
using System.Collections.Generic;
using System.Linq;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.Tests
{
    public static class QuotationAnalyticsServiceSmokeTests
    {
        public static List<string> RunAll()
        {
            var passed = new List<string>();
            DateTime today = new DateTime(2026, 5, 20);
            var quotations = new List<TenderBid>
            {
                new TenderBid { BidID = 1, QuotationNumber = "QUO-001", ClientID = 1, ClientName = "Alpha Cooling", SiteName = "Plant", SubmittedDate = new DateTime(2026, 5, 2), DueDate = today.AddDays(7), RequiredByDate = today.AddDays(3), Status = "Draft", BidValue = 100000m, TotalWithGST = 118000m, LineItems = new List<TenderBidLineItem> { new TenderBidLineItem { ItemDescription = "Copper Pipe", Category = "Material", Quantity = 10m, SellPricePerUnit = 500m, TaxableLineTotal = 5000m } } },
                new TenderBid { BidID = 2, QuotationNumber = "QUO-002", ClientID = 2, ClientName = "Beta Towers", SiteName = "Office", SubmittedDate = new DateTime(2026, 5, 4), DueDate = today.AddDays(12), RequiredByDate = today.AddDays(5), Status = "Sent", BidValue = 50000m, TotalWithGST = 59000m, LineItems = new List<TenderBidLineItem> { new TenderBidLineItem { ItemDescription = "Installation Labour", Category = "Labour", Quantity = 1m, SellPricePerUnit = 15000m, TaxableLineTotal = 15000m } } },
                new TenderBid { BidID = 3, QuotationNumber = "QUO-003", ClientID = 1, ClientName = "Alpha Cooling", SiteName = "Plant", SubmittedDate = new DateTime(2026, 5, 8), DueDate = today.AddDays(-2), RequiredByDate = today.AddDays(-1), Status = "Follow Up", BidValue = 75000m, TotalWithGST = 88500m, Notes = "urgent follow up", LineItems = new List<TenderBidLineItem> { new TenderBidLineItem { ItemDescription = "Copper Pipe", Category = "Material", Quantity = 20m, SellPricePerUnit = 500m, TaxableLineTotal = 10000m } } },
                new TenderBid { BidID = 4, QuotationNumber = "QUO-004", ClientID = 3, ClientName = "Gamma Hospital", SiteName = "Main", SubmittedDate = new DateTime(2026, 5, 12), DueDate = today.AddDays(20), RequiredByDate = today.AddDays(10), Status = "Negotiation", BidValue = 200000m, TotalWithGST = 236000m, LineItems = new List<TenderBidLineItem> { new TenderBidLineItem { ItemDescription = "Daikin VRV IV System", Category = "HVAC", Quantity = 1m, SellPricePerUnit = 200000m, TaxableLineTotal = 200000m } } },
                new TenderBid { BidID = 5, QuotationNumber = "QUO-005", ClientID = 4, ClientName = "Delta Builders", SiteName = "Site", SubmittedDate = new DateTime(2026, 5, 14), ModifiedDate = new DateTime(2026, 5, 18), DueDate = today.AddDays(25), Status = "Won", BidValue = 300000m, TotalWithGST = 354000m },
                new TenderBid { BidID = 6, QuotationNumber = "QUO-006", ClientID = 5, ClientName = "Epsilon Hotels", SiteName = "Resort", SubmittedDate = new DateTime(2026, 5, 16), ModifiedDate = new DateTime(2026, 5, 19), DueDate = today.AddDays(15), Status = "Lost", BidValue = 90000m, TotalWithGST = 106200m, Notes = "Competitor selected" },
                new TenderBid { BidID = 7, QuotationNumber = "QUO-007", ClientID = 6, ClientName = "Old Client", SiteName = "Legacy", SubmittedDate = new DateTime(2026, 4, 10), DueDate = new DateTime(2026, 4, 25), Status = "Draft", BidValue = 10000m, TotalWithGST = 11800m }
            };

            var service = new QuotationAnalyticsService();
            QuotationDashboardSnapshot snapshot = service.BuildSnapshot(
                quotations,
                new QuotationAnalyticsFilter { DateFrom = new DateTime(2026, 5, 1), DateTo = new DateTime(2026, 5, 31), Grouping = QuotationAnalyticsGrouping.Week },
                today);

            if (snapshot.Kpis.TotalQuotations.Value != 6)
                throw new InvalidOperationException("Quotation count KPI did not respect selected date range.");
            if (snapshot.Kpis.TotalValue.Value != 961700m)
                throw new InvalidOperationException("Total quotation value should sum quotation totals in range.");
            if (snapshot.Kpis.ConvertedValue.Value != 354000m)
                throw new InvalidOperationException("Converted value should include converted/won quotations only.");
            if (snapshot.Kpis.PendingValue.Value != 501500m)
                throw new InvalidOperationException("Pending value should include Draft, Sent, Follow Up, and Negotiation.");
            if (Math.Round(snapshot.Kpis.ConversionRate.Value, 1) != 16.7m)
                throw new InvalidOperationException("Conversion rate should be converted quotations divided by total quotations.");
            if (snapshot.Kpis.AverageQuotationValue.Value != Math.Round(961700m / 6m, 2))
                throw new InvalidOperationException("Average quotation value should be total value divided by count.");
            passed.Add("quotation KPI totals and rates are calculated from selected date range");

            if (!snapshot.Statuses.Any(s => s.Status == "Converted" && s.Count == 1))
                throw new InvalidOperationException("Status chart should map Won quotations to Converted.");
            if (snapshot.TopClients.First().ClientName != "Delta Builders" || snapshot.TopClients.First().Amount != 354000m)
                throw new InvalidOperationException("Top clients should group by client and sort by amount.");
            if (!snapshot.TopClients.Any(c => c.ClientName == "Alpha Cooling" && c.Amount == 206500m))
                throw new InvalidOperationException("Top clients should aggregate repeat client quotation values.");
            if (snapshot.RecentQuotations.First().QuotationNumber != "QUO-006")
                throw new InvalidOperationException("Recent quotations should sort by quotation date descending.");
            if (!snapshot.TopItems.Any(i => i.ItemName == "Daikin VRV IV System" && i.TotalValue == 200000m))
                throw new InvalidOperationException("Top items should group quotation line items by material/service name.");
            if (!snapshot.LostReasons.Any(r => r.Reason == "Competitor" && r.Count == 1))
                throw new InvalidOperationException("Lost reason analysis should infer competitor losses.");
            if (!snapshot.Insights.Any(i => i.Text.IndexOf("overdue follow-up", StringComparison.OrdinalIgnoreCase) >= 0))
                throw new InvalidOperationException("Insights should include overdue follow-up counts.");
            passed.Add("quotation charts, tables, funnel, line-item analytics, and alerts are derived from quotation data");

            QuotationDashboardSnapshot empty = service.BuildSnapshot(
                new List<TenderBid>(),
                new QuotationAnalyticsFilter { DateFrom = new DateTime(2026, 5, 1), DateTo = new DateTime(2026, 5, 31) },
                today);
            if (!empty.UsesDemoData)
                throw new InvalidOperationException("Empty quotation dashboard should still report empty-source state.");
            if (empty.Kpis.TotalQuotations.Value != 0m || empty.Kpis.TotalValue.Value != 0m || empty.RecentQuotations.Any() || empty.TopClients.Any())
                throw new InvalidOperationException("Empty quotation dashboard must not show seeded amounts, clients, dates, or quotation numbers.");
            passed.Add("empty quotation dashboard stays empty without seeded business data");

            return passed;
        }
    }
}

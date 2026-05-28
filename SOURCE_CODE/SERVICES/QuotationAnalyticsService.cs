using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public enum QuotationAnalyticsGrouping
    {
        Day,
        Week,
        Month
    }

    public class QuotationAnalyticsFilter
    {
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public QuotationAnalyticsGrouping Grouping { get; set; } = QuotationAnalyticsGrouping.Week;
    }

    public class QuotationDashboardSnapshot
    {
        public bool UsesDemoData { get; set; }
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public QuotationAnalyticsGrouping Grouping { get; set; }
        public QuotationKpiSet Kpis { get; set; } = new QuotationKpiSet();
        public List<QuotationOverviewPoint> Overview { get; set; } = new List<QuotationOverviewPoint>();
        public List<QuotationStatusSlice> Statuses { get; set; } = new List<QuotationStatusSlice>();
        public List<QuotationFunnelStage> Funnel { get; set; } = new List<QuotationFunnelStage>();
        public List<QuotationTopClientRow> TopClients { get; set; } = new List<QuotationTopClientRow>();
        public List<QuotationRecentRow> RecentQuotations { get; set; } = new List<QuotationRecentRow>();
        public List<QuotationTrendPoint> ValueTrend { get; set; } = new List<QuotationTrendPoint>();
        public List<QuotationTopItemRow> TopItems { get; set; } = new List<QuotationTopItemRow>();
        public List<QuotationFollowUpRow> UpcomingFollowUps { get; set; } = new List<QuotationFollowUpRow>();
        public List<QuotationLostReasonSlice> LostReasons { get; set; } = new List<QuotationLostReasonSlice>();
        public List<QuotationInsight> Insights { get; set; } = new List<QuotationInsight>();
    }

    public class QuotationKpiSet
    {
        public QuotationKpi TotalQuotations { get; set; } = new QuotationKpi { Title = "Total Quotations" };
        public QuotationKpi TotalValue { get; set; } = new QuotationKpi { Title = "Total Value" };
        public QuotationKpi ConvertedValue { get; set; } = new QuotationKpi { Title = "Converted Value" };
        public QuotationKpi PendingValue { get; set; } = new QuotationKpi { Title = "Pending Value" };
        public QuotationKpi AverageQuotationValue { get; set; } = new QuotationKpi { Title = "Avg. Quotation Value" };
        public QuotationKpi ConversionRate { get; set; } = new QuotationKpi { Title = "Conversion Rate" };
        public QuotationKpi WinRate { get; set; } = new QuotationKpi { Title = "Win Rate" };
        public QuotationKpi AverageSalesCycle { get; set; } = new QuotationKpi { Title = "Avg. Sales Cycle" };
        public QuotationKpi RepeatClientRate { get; set; } = new QuotationKpi { Title = "Repeat Client Rate" };
        public QuotationKpi RevenuePipeline { get; set; } = new QuotationKpi { Title = "Revenue Pipeline" };
        public QuotationKpi WeightedPipeline { get; set; } = new QuotationKpi { Title = "Weighted Pipeline" };
        public QuotationKpi ExpectedRevenue { get; set; } = new QuotationKpi { Title = "Expected Revenue" };
    }

    public class QuotationKpi
    {
        public string Title { get; set; }
        public decimal Value { get; set; }
        public decimal MonthOverMonthPercent { get; set; }
    }

    public class QuotationOverviewPoint
    {
        public string Period { get; set; }
        public int TotalCount { get; set; }
        public int ConvertedCount { get; set; }
        public int PendingCount { get; set; }
        public int LostCount { get; set; }
        public decimal TotalValue { get; set; }
    }

    public class QuotationStatusSlice
    {
        public string Status { get; set; }
        public int Count { get; set; }
        public decimal Percentage { get; set; }
        public Color Color { get; set; }
    }

    public class QuotationFunnelStage
    {
        public string Stage { get; set; }
        public int Count { get; set; }
        public decimal Percentage { get; set; }
        public Color Color { get; set; }
    }

    public class QuotationTopClientRow
    {
        public string ClientName { get; set; }
        public decimal Amount { get; set; }
    }

    public class QuotationRecentRow
    {
        public int BidId { get; set; }
        public string QuotationNumber { get; set; }
        public string ClientName { get; set; }
        public string SiteName { get; set; }
        public DateTime QuotationDate { get; set; }
        public DateTime ValidTill { get; set; }
        public decimal Value { get; set; }
        public string Status { get; set; }
        public string CommercialFlow { get; set; }
        public string CustomerDocumentStatus { get; set; }
        public string SupplierDocumentStatus { get; set; }
        public DateTime? FollowUpDate { get; set; }
    }

    public class QuotationTrendPoint
    {
        public string Period { get; set; }
        public int Count { get; set; }
        public decimal Value { get; set; }
    }

    public class QuotationTopItemRow
    {
        public string ItemName { get; set; }
        public decimal TotalValue { get; set; }
        public decimal Quantity { get; set; }
    }

    public class QuotationFollowUpRow
    {
        public int BidId { get; set; }
        public string QuotationNumber { get; set; }
        public string ClientName { get; set; }
        public DateTime FollowUpDate { get; set; }
        public decimal Value { get; set; }
    }

    public class QuotationLostReasonSlice
    {
        public string Reason { get; set; }
        public int Count { get; set; }
        public decimal Percentage { get; set; }
        public Color Color { get; set; }
    }

    public class QuotationInsight
    {
        public string Title { get; set; }
        public string Text { get; set; }
        public Color Color { get; set; }
    }

    public class QuotationAnalyticsService
    {
        private static readonly Color Blue = Color.FromArgb(37, 99, 235);
        private static readonly Color Green = Color.FromArgb(22, 163, 74);
        private static readonly Color Orange = Color.FromArgb(249, 115, 22);
        private static readonly Color Red = Color.FromArgb(239, 68, 68);
        private static readonly Color Purple = Color.FromArgb(124, 58, 237);
        private static readonly Color Teal = Color.FromArgb(6, 182, 212);
        private static readonly Color Indigo = Color.FromArgb(79, 70, 229);

        public QuotationDashboardSnapshot BuildCurrentMonthSnapshot()
        {
            DateTime today = DateTime.Today;
            return BuildSnapshot(new QuotationAnalyticsFilter
            {
                DateFrom = new DateTime(today.Year, today.Month, 1),
                DateTo = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month)),
                Grouping = QuotationAnalyticsGrouping.Week
            });
        }

        public QuotationDashboardSnapshot BuildSnapshot(QuotationAnalyticsFilter filter)
        {
            return BuildSnapshot(SafeLoadQuotations(), filter, DateTime.Today);
        }

        public QuotationDashboardSnapshot BuildSnapshot(List<TenderBid> sourceQuotations, QuotationAnalyticsFilter filter, DateTime today)
        {
            filter = NormalizeFilter(filter, today);
            var allQuotations = sourceQuotations ?? new List<TenderBid>();
            bool usesDemo = !allQuotations.Any();

            DateTime from = filter.DateFrom.Value.Date;
            DateTime to = filter.DateTo.Value.Date;
            List<TenderBid> current = allQuotations
                .Where(q => QuoteDate(q).Date >= from && QuoteDate(q).Date <= to)
                .ToList();

            int days = Math.Max(1, (to - from).Days + 1);
            DateTime previousTo = from.AddDays(-1);
            DateTime previousFrom = previousTo.AddDays(-days + 1);
            List<TenderBid> previous = allQuotations
                .Where(q => QuoteDate(q).Date >= previousFrom && QuoteDate(q).Date <= previousTo)
                .ToList();

            var snapshot = new QuotationDashboardSnapshot
            {
                UsesDemoData = usesDemo,
                DateFrom = from,
                DateTo = to,
                Grouping = filter.Grouping
            };

            BuildKpis(snapshot, current, previous, today);
            snapshot.Overview = BuildOverview(current, filter.Grouping, from, to);
            snapshot.Statuses = BuildStatuses(current);
            snapshot.Funnel = BuildFunnel(current);
            snapshot.TopClients = current
                .GroupBy(q => Clean(q.ClientName, "Unassigned Client"))
                .Select(g => new QuotationTopClientRow { ClientName = g.Key, Amount = g.Sum(QuoteValue) })
                .OrderByDescending(r => r.Amount)
                .ThenBy(r => r.ClientName)
                .Take(5)
                .ToList();
            snapshot.RecentQuotations = current
                .OrderByDescending(QuoteDate)
                .ThenByDescending(q => q.BidID)
                .Take(10)
                .Select(ToRecentRow)
                .ToList();
            snapshot.ValueTrend = BuildTrend(current, filter.Grouping, from, to);
            snapshot.TopItems = BuildTopItems(current);
            snapshot.UpcomingFollowUps = BuildUpcomingFollowUps(allQuotations, today);
            snapshot.LostReasons = BuildLostReasons(current);
            snapshot.Insights = BuildInsights(allQuotations, current, previous, today);

            return snapshot;
        }

        private List<TenderBid> SafeLoadQuotations()
        {
            try
            {
                var svc = new TenderService();
                return (svc.GetAll() ?? new List<TenderBid>())
                    .Select(q =>
                    {
                        try { return svc.GetByIdDetailed(q.BidID) ?? q; }
                        catch { return q; }
                    })
                    .ToList();
            }
            catch
            {
                return new List<TenderBid>();
            }
        }

        private QuotationAnalyticsFilter NormalizeFilter(QuotationAnalyticsFilter filter, DateTime today)
        {
            filter = filter ?? new QuotationAnalyticsFilter();
            if (!filter.DateFrom.HasValue)
                filter.DateFrom = new DateTime(today.Year, today.Month, 1);
            if (!filter.DateTo.HasValue)
                filter.DateTo = today.Date;
            if (filter.DateFrom.Value.Date > filter.DateTo.Value.Date)
            {
                DateTime swap = filter.DateFrom.Value.Date;
                filter.DateFrom = filter.DateTo.Value.Date;
                filter.DateTo = swap;
            }
            return filter;
        }

        private void BuildKpis(QuotationDashboardSnapshot snapshot, List<TenderBid> current, List<TenderBid> previous, DateTime today)
        {
            decimal totalValue = current.Sum(QuoteValue);
            decimal previousTotal = previous.Sum(QuoteValue);
            decimal convertedValue = current.Where(IsConverted).Sum(QuoteValue);
            decimal previousConverted = previous.Where(IsConverted).Sum(QuoteValue);
            decimal pendingValue = current.Where(IsPendingPipeline).Sum(QuoteValue);
            decimal previousPending = previous.Where(IsPendingPipeline).Sum(QuoteValue);
            decimal revenuePipeline = current.Where(q => IsPendingPipeline(q) || NormalizeStatus(q.Status) == "Negotiation").Sum(QuoteValue);
            decimal previousPipeline = previous.Where(q => IsPendingPipeline(q) || NormalizeStatus(q.Status) == "Negotiation").Sum(QuoteValue);
            decimal weightedPipeline = current.Sum(q => QuoteValue(q) * Probability(q));
            decimal previousWeighted = previous.Sum(q => QuoteValue(q) * Probability(q));
            decimal expectedRevenue = current
                .Where(q => !IsClosed(q) && FollowUpDate(q, today).Date <= today.Date.AddDays(30))
                .Sum(q => QuoteValue(q) * Probability(q));
            decimal previousExpected = previous
                .Where(q => !IsClosed(q))
                .Sum(q => QuoteValue(q) * Probability(q));

            snapshot.Kpis.TotalQuotations.Value = current.Count;
            snapshot.Kpis.TotalQuotations.MonthOverMonthPercent = Change(current.Count, previous.Count);
            snapshot.Kpis.TotalValue.Value = totalValue;
            snapshot.Kpis.TotalValue.MonthOverMonthPercent = Change(totalValue, previousTotal);
            snapshot.Kpis.ConvertedValue.Value = convertedValue;
            snapshot.Kpis.ConvertedValue.MonthOverMonthPercent = Change(convertedValue, previousConverted);
            snapshot.Kpis.PendingValue.Value = pendingValue;
            snapshot.Kpis.PendingValue.MonthOverMonthPercent = Change(pendingValue, previousPending);
            snapshot.Kpis.AverageQuotationValue.Value = current.Count == 0 ? 0m : Math.Round(totalValue / current.Count, 2);
            snapshot.Kpis.AverageQuotationValue.MonthOverMonthPercent = Change(
                snapshot.Kpis.AverageQuotationValue.Value,
                previous.Count == 0 ? 0m : Math.Round(previousTotal / previous.Count, 2));
            snapshot.Kpis.ConversionRate.Value = current.Count == 0 ? 0m : Math.Round(current.Count(IsConverted) * 100m / current.Count, 1);
            snapshot.Kpis.ConversionRate.MonthOverMonthPercent = Change(
                snapshot.Kpis.ConversionRate.Value,
                previous.Count == 0 ? 0m : Math.Round(previous.Count(IsConverted) * 100m / previous.Count, 1));

            int closedCount = current.Count(IsClosed);
            int previousClosed = previous.Count(IsClosed);
            snapshot.Kpis.WinRate.Value = closedCount == 0 ? 0m : Math.Round(current.Count(IsConverted) * 100m / closedCount, 1);
            snapshot.Kpis.WinRate.MonthOverMonthPercent = Change(
                snapshot.Kpis.WinRate.Value,
                previousClosed == 0 ? 0m : Math.Round(previous.Count(IsConverted) * 100m / previousClosed, 1));

            snapshot.Kpis.AverageSalesCycle.Value = AverageSalesCycle(current);
            snapshot.Kpis.AverageSalesCycle.MonthOverMonthPercent = Change(snapshot.Kpis.AverageSalesCycle.Value, AverageSalesCycle(previous));
            snapshot.Kpis.RepeatClientRate.Value = RepeatClientRate(current);
            snapshot.Kpis.RepeatClientRate.MonthOverMonthPercent = Change(snapshot.Kpis.RepeatClientRate.Value, RepeatClientRate(previous));
            snapshot.Kpis.RevenuePipeline.Value = revenuePipeline;
            snapshot.Kpis.RevenuePipeline.MonthOverMonthPercent = Change(revenuePipeline, previousPipeline);
            snapshot.Kpis.WeightedPipeline.Value = weightedPipeline;
            snapshot.Kpis.WeightedPipeline.MonthOverMonthPercent = Change(weightedPipeline, previousWeighted);
            snapshot.Kpis.ExpectedRevenue.Value = expectedRevenue;
            snapshot.Kpis.ExpectedRevenue.MonthOverMonthPercent = Change(expectedRevenue, previousExpected);
        }

        private List<QuotationOverviewPoint> BuildOverview(List<TenderBid> quotations, QuotationAnalyticsGrouping grouping, DateTime from, DateTime to)
        {
            var points = new List<QuotationOverviewPoint>();
            DateTime cursor = from;
            while (cursor <= to)
            {
                DateTime periodStart = cursor;
                DateTime periodEnd = GetPeriodEnd(cursor, grouping, to);
                List<TenderBid> periodQuotes = quotations
                    .Where(q => QuoteDate(q).Date >= periodStart && QuoteDate(q).Date <= periodEnd)
                    .ToList();
                points.Add(new QuotationOverviewPoint
                {
                    Period = FormatPeriod(periodStart, periodEnd, grouping),
                    TotalCount = periodQuotes.Count,
                    ConvertedCount = periodQuotes.Count(IsConverted),
                    PendingCount = periodQuotes.Count(IsPendingPipeline),
                    LostCount = periodQuotes.Count(q => NormalizeStatus(q.Status) == "Lost"),
                    TotalValue = periodQuotes.Sum(QuoteValue)
                });
                cursor = periodEnd.AddDays(1);
            }
            return points;
        }

        private List<QuotationStatusSlice> BuildStatuses(List<TenderBid> quotations)
        {
            string[] statuses = { "Draft", "Sent", "Follow Up", "Negotiation", "Converted", "Lost" };
            int total = Math.Max(1, quotations.Count);
            return statuses.Select(status =>
            {
                int count = quotations.Count(q => NormalizeStatus(q.Status) == status);
                return new QuotationStatusSlice
                {
                    Status = status,
                    Count = count,
                    Percentage = Math.Round(count * 100m / total, 1),
                    Color = StatusColor(status)
                };
            }).ToList();
        }

        private List<QuotationFunnelStage> BuildFunnel(List<TenderBid> quotations)
        {
            int total = quotations.Count;
            int sent = quotations.Count(q =>
            {
                string status = NormalizeStatus(q.Status);
                return status == "Sent" || status == "Follow Up" || status == "Negotiation" || status == "Converted";
            });
            int follow = quotations.Count(q =>
            {
                string status = NormalizeStatus(q.Status);
                return status == "Follow Up" || status == "Negotiation" || status == "Converted";
            });
            int negotiation = quotations.Count(q =>
            {
                string status = NormalizeStatus(q.Status);
                return status == "Negotiation" || status == "Converted";
            });
            int converted = quotations.Count(IsConverted);
            var rows = new[]
            {
                new { Stage = "Total Quotations", Count = total, Color = Blue },
                new { Stage = "Sent", Count = sent, Color = Teal },
                new { Stage = "Follow Up", Count = follow, Color = Purple },
                new { Stage = "Negotiation", Count = negotiation, Color = Indigo },
                new { Stage = "Converted", Count = converted, Color = Green }
            };
            int denominator = Math.Max(1, total);
            return rows.Select(r => new QuotationFunnelStage
            {
                Stage = r.Stage,
                Count = r.Count,
                Percentage = Math.Round(r.Count * 100m / denominator, 1),
                Color = r.Color
            }).ToList();
        }

        private List<QuotationTrendPoint> BuildTrend(List<TenderBid> quotations, QuotationAnalyticsGrouping grouping, DateTime from, DateTime to)
        {
            var points = new List<QuotationTrendPoint>();
            DateTime cursor = from;
            while (cursor <= to)
            {
                DateTime periodStart = cursor;
                DateTime periodEnd = GetPeriodEnd(cursor, grouping, to);
                List<TenderBid> periodQuotes = quotations
                    .Where(q => QuoteDate(q).Date >= periodStart && QuoteDate(q).Date <= periodEnd)
                    .ToList();
                points.Add(new QuotationTrendPoint
                {
                    Period = FormatPeriod(periodStart, periodEnd, grouping),
                    Count = periodQuotes.Count,
                    Value = periodQuotes.Sum(QuoteValue)
                });
                cursor = periodEnd.AddDays(1);
            }
            return points;
        }

        private List<QuotationTopItemRow> BuildTopItems(List<TenderBid> quotations)
        {
            return quotations
                .SelectMany(q => q.LineItems ?? new List<TenderBidLineItem>())
                .Where(i => !string.IsNullOrWhiteSpace(i.ItemDescription))
                .GroupBy(i => Clean(i.ItemDescription, "Unspecified Item"))
                .Select(g => new QuotationTopItemRow
                {
                    ItemName = g.Key,
                    Quantity = g.Sum(i => i.Quantity),
                    TotalValue = g.Sum(LineValue)
                })
                .OrderByDescending(i => i.TotalValue)
                .ThenBy(i => i.ItemName)
                .Take(5)
                .ToList();
        }

        private List<QuotationFollowUpRow> BuildUpcomingFollowUps(List<TenderBid> quotations, DateTime today)
        {
            return quotations
                .Where(q => !IsClosed(q))
                .Select(q => new { Quote = q, FollowDate = FollowUpDate(q, today) })
                .Where(x => x.FollowDate.Date >= today.Date)
                .OrderBy(x => x.FollowDate)
                .ThenByDescending(x => QuoteValue(x.Quote))
                .Take(8)
                .Select(x => new QuotationFollowUpRow
                {
                    BidId = x.Quote.BidID,
                    QuotationNumber = Clean(x.Quote.QuotationNumber, "QUO-DRAFT"),
                    ClientName = Clean(x.Quote.ClientName, "Unassigned Client"),
                    FollowUpDate = x.FollowDate,
                    Value = QuoteValue(x.Quote)
                })
                .ToList();
        }

        private List<QuotationLostReasonSlice> BuildLostReasons(List<TenderBid> quotations)
        {
            string[] reasons = { "Price High", "Competitor", "Requirement Change", "No Response", "Other" };
            List<TenderBid> lost = quotations.Where(q => NormalizeStatus(q.Status) == "Lost").ToList();
            int total = Math.Max(1, lost.Count);
            return reasons.Select(reason =>
            {
                int count = lost.Count(q => LostReason(q) == reason);
                return new QuotationLostReasonSlice
                {
                    Reason = reason,
                    Count = count,
                    Percentage = Math.Round(count * 100m / total, 1),
                    Color = LostReasonColor(reason)
                };
            }).ToList();
        }

        private List<QuotationInsight> BuildInsights(List<TenderBid> allQuotations, List<TenderBid> current, List<TenderBid> previous, DateTime today)
        {
            int overdueFollowUps = allQuotations.Count(q => !IsClosed(q) && FollowUpDate(q, today).Date < today.Date);
            decimal expectedRevenue = current
                .Where(q => !IsClosed(q) && FollowUpDate(q, today).Date <= today.Date.AddDays(30))
                .Sum(q => QuoteValue(q) * Probability(q));
            int highValuePending = current.Count(q => IsPendingPipeline(q) && QuoteValue(q) >= 50000m);
            int expiringSoon = current.Count(q => !IsClosed(q) && q.DueDate.Date >= today.Date && q.DueDate.Date <= today.Date.AddDays(7));
            decimal currentRate = current.Count == 0 ? 0m : current.Count(IsConverted) * 100m / current.Count;
            decimal previousRate = previous.Count == 0 ? 0m : previous.Count(IsConverted) * 100m / previous.Count;
            string trendWord = currentRate >= previousRate ? "improved" : "dropped";

            return new List<QuotationInsight>
            {
                new QuotationInsight { Title = "Follow Ups", Text = overdueFollowUps + " overdue follow-up" + (overdueFollowUps == 1 ? "" : "s") + " need attention.", Color = Red },
                new QuotationInsight { Title = "Expected Revenue", Text = IndiaFormatHelper.FormatCurrency(expectedRevenue) + " expected revenue in next 30 days.", Color = Orange },
                new QuotationInsight { Title = "High Value Pending", Text = highValuePending + " high-value quotation" + (highValuePending == 1 ? "" : "s") + " pending above Rs 50,000.", Color = Blue },
                new QuotationInsight { Title = "Conversion Rate", Text = "Conversion rate " + trendWord + " by " + Math.Abs(Math.Round(currentRate - previousRate, 1)).ToString("0.0") + "% vs previous period.", Color = trendWord == "improved" ? Green : Red },
                new QuotationInsight { Title = "Expiring Soon", Text = expiringSoon + " quotation" + (expiringSoon == 1 ? "" : "s") + " expiring within 7 days.", Color = Purple }
            };
        }

        private QuotationRecentRow ToRecentRow(TenderBid quote)
        {
            return new QuotationRecentRow
            {
                BidId = quote.BidID,
                QuotationNumber = Clean(quote.QuotationNumber, "QUO-DRAFT"),
                ClientName = Clean(quote.ClientName, "Unassigned Client"),
                SiteName = Clean(quote.SiteName, "-"),
                QuotationDate = QuoteDate(quote),
                ValidTill = quote.DueDate == default(DateTime) ? QuoteDate(quote).AddDays(30) : quote.DueDate,
                Value = QuoteValue(quote),
                Status = NormalizeStatus(quote.Status),
                CommercialFlow = string.IsNullOrWhiteSpace(quote.CommercialFlow) ? "Revenue" : quote.CommercialFlow,
                CustomerDocumentStatus = string.IsNullOrWhiteSpace(quote.CustomerDocumentStatus) ? "Quote Draft" : quote.CustomerDocumentStatus,
                SupplierDocumentStatus = string.IsNullOrWhiteSpace(quote.SupplierDocumentStatus) ? "Not Required" : quote.SupplierDocumentStatus,
                FollowUpDate = quote.RequiredByDate ?? (DateTime?)quote.DueDate
            };
        }

        private static DateTime QuoteDate(TenderBid quote)
        {
            if (quote == null)
                return DateTime.Today;
            if (quote.SubmittedDate.HasValue)
                return quote.SubmittedDate.Value.Date;
            if (quote.RequiredByDate.HasValue)
                return quote.RequiredByDate.Value.Date;
            if (quote.DueDate != default(DateTime))
                return quote.DueDate.Date;
            return quote.ModifiedDate.HasValue ? quote.ModifiedDate.Value.Date : DateTime.Today;
        }

        private static DateTime CloseDate(TenderBid quote)
        {
            if (quote == null)
                return DateTime.Today;
            if (quote.ModifiedDate.HasValue)
                return quote.ModifiedDate.Value.Date;
            if (quote.DueDate != default(DateTime))
                return quote.DueDate.Date;
            return QuoteDate(quote);
        }

        private static DateTime FollowUpDate(TenderBid quote, DateTime today)
        {
            if (quote == null)
                return today;
            if (quote.RequiredByDate.HasValue)
                return quote.RequiredByDate.Value.Date;
            if (quote.DueDate != default(DateTime))
                return quote.DueDate.Date;
            return QuoteDate(quote).AddDays(7);
        }

        private static decimal QuoteValue(TenderBid quote)
        {
            if (quote == null)
                return 0m;
            if (quote.TotalWithGST > 0m)
                return Math.Round(quote.TotalWithGST, 2);
            if (quote.BidValue > 0m)
                return Math.Round(quote.BidValue, 2);
            if (quote.LineItems != null && quote.LineItems.Any())
                return quote.LineItems.Sum(LineValue);
            return 0m;
        }

        private static decimal LineValue(TenderBidLineItem item)
        {
            if (item == null)
                return 0m;
            if (item.TaxableLineTotal > 0m)
                return Math.Round(item.TaxableLineTotal, 2);
            return Math.Round(item.Quantity * item.SellPricePerUnit, 2);
        }

        private static string NormalizeStatus(string status)
        {
            string s = (status ?? "Draft").Trim();
            if (string.Equals(s, "Won", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "Converted", StringComparison.OrdinalIgnoreCase))
                return "Converted";
            if (string.Equals(s, "Submitted", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "Analysed", StringComparison.OrdinalIgnoreCase))
                return "Sent";
            if (s.IndexOf("follow", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Follow Up";
            if (s.IndexOf("nego", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Negotiation";
            if (string.Equals(s, "Lost", StringComparison.OrdinalIgnoreCase))
                return "Lost";
            if (string.Equals(s, "Sent", StringComparison.OrdinalIgnoreCase))
                return "Sent";
            return "Draft";
        }

        private static bool IsConverted(TenderBid quote)
        {
            return NormalizeStatus(quote?.Status) == "Converted";
        }

        private static bool IsClosed(TenderBid quote)
        {
            string status = NormalizeStatus(quote?.Status);
            return status == "Converted" || status == "Lost";
        }

        private static bool IsPendingPipeline(TenderBid quote)
        {
            string status = NormalizeStatus(quote?.Status);
            return status == "Draft" || status == "Sent" || status == "Follow Up" || status == "Negotiation";
        }

        private static decimal Probability(TenderBid quote)
        {
            switch (NormalizeStatus(quote?.Status))
            {
                case "Converted": return 1m;
                case "Negotiation": return 0.65m;
                case "Follow Up": return 0.45m;
                case "Sent": return 0.25m;
                case "Draft": return 0.10m;
                default: return 0m;
            }
        }

        private static decimal AverageSalesCycle(List<TenderBid> quotations)
        {
            var closed = quotations.Where(IsClosed).ToList();
            if (!closed.Any())
                return 0m;
            return Math.Round((decimal)closed.Average(q => Math.Max(0, (CloseDate(q) - QuoteDate(q)).TotalDays)), 1);
        }

        private static decimal RepeatClientRate(List<TenderBid> quotations)
        {
            var clients = quotations
                .Where(q => q.ClientID > 0 || !string.IsNullOrWhiteSpace(q.ClientName))
                .GroupBy(q => q.ClientID > 0 ? q.ClientID.ToString() : Clean(q.ClientName, "Client"))
                .ToList();
            if (!clients.Any())
                return 0m;
            return Math.Round(clients.Count(g => g.Count() > 1) * 100m / clients.Count, 1);
        }

        private static DateTime GetPeriodEnd(DateTime start, QuotationAnalyticsGrouping grouping, DateTime max)
        {
            DateTime end;
            switch (grouping)
            {
                case QuotationAnalyticsGrouping.Day:
                    end = start;
                    break;
                case QuotationAnalyticsGrouping.Month:
                    end = new DateTime(start.Year, start.Month, DateTime.DaysInMonth(start.Year, start.Month));
                    break;
                default:
                    end = start.AddDays(6);
                    break;
            }
            return end > max ? max : end;
        }

        private static string FormatPeriod(DateTime start, DateTime end, QuotationAnalyticsGrouping grouping)
        {
            if (grouping == QuotationAnalyticsGrouping.Month)
                return start.ToString("MMM yy");
            if (grouping == QuotationAnalyticsGrouping.Day || start == end)
                return start.ToString("dd MMM");
            return start.ToString("dd MMM");
        }

        private static decimal Change(decimal current, decimal previous)
        {
            if (previous == 0m)
                return current == 0m ? 0m : 100m;
            return Math.Round((current - previous) * 100m / previous, 1);
        }

        private static string LostReason(TenderBid quote)
        {
            string text = ((quote?.Notes ?? "") + " " + (quote?.ComparisonSummary ?? "") + " " + (quote?.AnalysisStatus ?? "")).ToLowerInvariant();
            if (text.Contains("price") || text.Contains("high") || text.Contains("cost"))
                return "Price High";
            if (text.Contains("competitor") || text.Contains("competition"))
                return "Competitor";
            if (text.Contains("requirement") || text.Contains("scope") || text.Contains("change"))
                return "Requirement Change";
            if (text.Contains("no response") || text.Contains("no-response") || text.Contains("unresponsive"))
                return "No Response";
            return "Other";
        }

        private static Color StatusColor(string status)
        {
            switch (status)
            {
                case "Converted": return Green;
                case "Lost": return Red;
                case "Negotiation": return Purple;
                case "Follow Up": return Teal;
                case "Sent": return Orange;
                default: return Blue;
            }
        }

        private static Color LostReasonColor(string reason)
        {
            switch (reason)
            {
                case "Price High": return Blue;
                case "Competitor": return Orange;
                case "Requirement Change": return Teal;
                case "No Response": return Red;
                default: return Green;
            }
        }

        private static string Clean(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

    }
}

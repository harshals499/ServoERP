using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public enum InvoiceAnalyticsGrouping
    {
        Day,
        Week,
        Month
    }

    public class InvoiceAnalyticsFilter
    {
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public InvoiceAnalyticsGrouping Grouping { get; set; } = InvoiceAnalyticsGrouping.Week;
    }

    public class InvoiceDashboardSnapshot
    {
        public bool UsesDemoData { get; set; }
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public InvoiceAnalyticsGrouping Grouping { get; set; }
        public InvoiceKpiSet Kpis { get; set; } = new InvoiceKpiSet();
        public List<InvoiceOverviewPoint> Overview { get; set; } = new List<InvoiceOverviewPoint>();
        public List<InvoiceStatusSlice> Statuses { get; set; } = new List<InvoiceStatusSlice>();
        public List<InvoiceTopClientRow> TopClients { get; set; } = new List<InvoiceTopClientRow>();
        public List<InvoiceRecentRow> RecentInvoices { get; set; } = new List<InvoiceRecentRow>();
        public List<InvoiceAgingBucket> AgingBuckets { get; set; } = new List<InvoiceAgingBucket>();
        public List<InvoiceWorkflowSummaryRow> Workflow { get; set; } = new List<InvoiceWorkflowSummaryRow>();
        public List<string> Reminders { get; set; } = new List<string>();
    }

    public class InvoiceKpiSet
    {
        public InvoiceKpi TotalInvoices { get; set; } = new InvoiceKpi { Title = "Total Invoices" };
        public InvoiceKpi TotalAmount { get; set; } = new InvoiceKpi { Title = "Total Amount" };
        public InvoiceKpi PaidAmount { get; set; } = new InvoiceKpi { Title = "Paid Amount" };
        public InvoiceKpi PendingAmount { get; set; } = new InvoiceKpi { Title = "Pending Amount" };
        public InvoiceKpi OverdueAmount { get; set; } = new InvoiceKpi { Title = "Overdue Amount" };
    }

    public class InvoiceKpi
    {
        public string Title { get; set; }
        public decimal Value { get; set; }
        public decimal MonthOverMonthPercent { get; set; }
    }

    public class InvoiceOverviewPoint
    {
        public string Period { get; set; }
        public int TotalCount { get; set; }
        public int PaidCount { get; set; }
        public int PendingCount { get; set; }
        public int OverdueCount { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class InvoiceStatusSlice
    {
        public string Status { get; set; }
        public int Count { get; set; }
        public decimal Percentage { get; set; }
        public Color Color { get; set; }
    }

    public class InvoiceTopClientRow
    {
        public string ClientName { get; set; }
        public decimal Amount { get; set; }
    }

    public class InvoiceRecentRow
    {
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; }
        public string ClientName { get; set; }
        public string SiteName { get; set; }
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
    }

    public class InvoiceAgingBucket
    {
        public string Bucket { get; set; }
        public int Count { get; set; }
        public decimal Amount { get; set; }
    }

    public class InvoiceWorkflowSummaryRow
    {
        public string Status { get; set; }
        public int Count { get; set; }
        public Color Color { get; set; }
    }

    public class InvoiceAnalyticsService
    {
        private static readonly Color Blue = Color.FromArgb(37, 99, 235);
        private static readonly Color Green = Color.FromArgb(22, 163, 74);
        private static readonly Color Orange = Color.FromArgb(249, 115, 22);
        private static readonly Color Red = Color.FromArgb(239, 68, 68);
        private static readonly Color Purple = Color.FromArgb(124, 58, 237);
        private static readonly Color Teal = Color.FromArgb(6, 182, 212);
        private static readonly Color Indigo = Color.FromArgb(79, 70, 229);

        public InvoiceDashboardSnapshot BuildCurrentMonthSnapshot()
        {
            DateTime today = DateTime.Today;
            return BuildSnapshot(
                SafeLoadInvoices(),
                SafeLoadContracts(),
                new InvoiceAnalyticsFilter
                {
                    DateFrom = new DateTime(today.Year, today.Month, 1),
                    DateTo = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month)),
                    Grouping = InvoiceAnalyticsGrouping.Week
                },
                today);
        }

        public InvoiceDashboardSnapshot BuildSnapshot(InvoiceAnalyticsFilter filter)
        {
            return BuildSnapshot(SafeLoadInvoices(), SafeLoadContracts(), filter, DateTime.Today);
        }

        public InvoiceDashboardSnapshot BuildSnapshot(List<Invoice> sourceInvoices, List<AMCContract> sourceContracts, InvoiceAnalyticsFilter filter, DateTime today)
        {
            filter = NormalizeFilter(filter, today);
            var allInvoices = sourceInvoices ?? new List<Invoice>();
            var allContracts = sourceContracts ?? new List<AMCContract>();
            bool usesDemo = !allInvoices.Any();

            DateTime from = filter.DateFrom.Value.Date;
            DateTime to = filter.DateTo.Value.Date;
            List<Invoice> current = allInvoices
                .Where(i => i.InvoiceDate.Date >= from && i.InvoiceDate.Date <= to)
                .ToList();

            int days = Math.Max(1, (to - from).Days + 1);
            DateTime previousTo = from.AddDays(-1);
            DateTime previousFrom = previousTo.AddDays(-days + 1);
            List<Invoice> previous = allInvoices
                .Where(i => i.InvoiceDate.Date >= previousFrom && i.InvoiceDate.Date <= previousTo)
                .ToList();

            var snapshot = new InvoiceDashboardSnapshot
            {
                UsesDemoData = usesDemo,
                DateFrom = from,
                DateTo = to,
                Grouping = filter.Grouping
            };

            BuildKpis(snapshot, current, previous, today);
            snapshot.Overview = BuildOverview(current, filter.Grouping, from, to, today);
            snapshot.Statuses = BuildStatuses(current, today);
            snapshot.TopClients = current
                .GroupBy(i => Clean(i.ClientName, "Unassigned Client"))
                .Select(g => new InvoiceTopClientRow { ClientName = g.Key, Amount = g.Sum(i => i.TotalAmount) })
                .OrderByDescending(r => r.Amount)
                .ThenBy(r => r.ClientName)
                .Take(5)
                .ToList();
            snapshot.RecentInvoices = current
                .OrderByDescending(i => i.InvoiceDate)
                .ThenByDescending(i => i.InvoiceID)
                .Take(10)
                .Select(i => new InvoiceRecentRow
                {
                    InvoiceId = i.InvoiceID,
                    InvoiceNumber = Clean(i.InvoiceNumber, "INV-DRAFT"),
                    ClientName = Clean(i.ClientName, "Unassigned Client"),
                    SiteName = Clean(i.SiteName, "-"),
                    InvoiceDate = i.InvoiceDate,
                    DueDate = i.DueDate,
                    Amount = i.TotalAmount,
                    Status = ResolveStatus(i, today)
                })
                .ToList();
            snapshot.AgingBuckets = BuildAging(allInvoices, today);
            snapshot.Workflow = BuildWorkflow(current, today);
            snapshot.Reminders = BuildReminders(allInvoices, allContracts, today);

            return snapshot;
        }

        private List<Invoice> SafeLoadInvoices()
        {
            try { return new InvoiceRepository().GetAll(); }
            catch { return new List<Invoice>(); }
        }

        private List<AMCContract> SafeLoadContracts()
        {
            try { return new ContractService().GetAllContracts(); }
            catch { return new List<AMCContract>(); }
        }

        private InvoiceAnalyticsFilter NormalizeFilter(InvoiceAnalyticsFilter filter, DateTime today)
        {
            filter = filter ?? new InvoiceAnalyticsFilter();
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

        private void BuildKpis(InvoiceDashboardSnapshot snapshot, List<Invoice> current, List<Invoice> previous, DateTime today)
        {
            snapshot.Kpis.TotalInvoices.Value = current.Count;
            snapshot.Kpis.TotalInvoices.MonthOverMonthPercent = Change(current.Count, previous.Count);

            snapshot.Kpis.TotalAmount.Value = current.Sum(i => i.TotalAmount);
            snapshot.Kpis.TotalAmount.MonthOverMonthPercent = Change(snapshot.Kpis.TotalAmount.Value, previous.Sum(i => i.TotalAmount));

            snapshot.Kpis.PaidAmount.Value = current.Sum(i => i.PaidAmount);
            snapshot.Kpis.PaidAmount.MonthOverMonthPercent = Change(snapshot.Kpis.PaidAmount.Value, previous.Sum(i => i.PaidAmount));

            snapshot.Kpis.PendingAmount.Value = current
                .Where(i => i.BalanceDue > 0m && i.DueDate.Date >= today.Date && !IsPaid(i))
                .Sum(i => i.BalanceDue);
            decimal previousPending = previous
                .Where(i => i.BalanceDue > 0m && i.DueDate.Date >= today.Date && !IsPaid(i))
                .Sum(i => i.BalanceDue);
            snapshot.Kpis.PendingAmount.MonthOverMonthPercent = Change(snapshot.Kpis.PendingAmount.Value, previousPending);

            snapshot.Kpis.OverdueAmount.Value = current
                .Where(i => IsOverdue(i, today))
                .Sum(i => i.BalanceDue);
            decimal previousOverdue = previous.Where(i => IsOverdue(i, today)).Sum(i => i.BalanceDue);
            snapshot.Kpis.OverdueAmount.MonthOverMonthPercent = Change(snapshot.Kpis.OverdueAmount.Value, previousOverdue);
        }

        private List<InvoiceOverviewPoint> BuildOverview(List<Invoice> invoices, InvoiceAnalyticsGrouping grouping, DateTime from, DateTime to, DateTime today)
        {
            var points = new List<InvoiceOverviewPoint>();
            DateTime cursor = from;
            while (cursor <= to)
            {
                DateTime periodStart = cursor;
                DateTime periodEnd = GetPeriodEnd(cursor, grouping, to);
                List<Invoice> periodInvoices = invoices
                    .Where(i => i.InvoiceDate.Date >= periodStart && i.InvoiceDate.Date <= periodEnd)
                    .ToList();
                points.Add(new InvoiceOverviewPoint
                {
                    Period = FormatPeriod(periodStart, periodEnd, grouping),
                    TotalCount = periodInvoices.Count,
                    PaidCount = periodInvoices.Count(IsPaid),
                    PendingCount = periodInvoices.Count(i => !IsPaid(i) && !IsOverdue(i, today)),
                    OverdueCount = periodInvoices.Count(i => IsOverdue(i, today)),
                    TotalAmount = periodInvoices.Sum(i => i.TotalAmount)
                });
                cursor = periodEnd.AddDays(1);
            }
            return points;
        }

        private List<InvoiceStatusSlice> BuildStatuses(List<Invoice> invoices, DateTime today)
        {
            string[] statuses = { "Draft", "Sent for Approval", "Approved", "Partially Paid", "Paid", "Overdue" };
            int total = Math.Max(1, invoices.Count);
            return statuses.Select(status =>
            {
                int count = invoices.Count(i => ResolveStatus(i, today) == status);
                return new InvoiceStatusSlice
                {
                    Status = status,
                    Count = count,
                    Percentage = Math.Round(count * 100m / total, 1),
                    Color = StatusColor(status)
                };
            }).ToList();
        }

        private List<InvoiceAgingBucket> BuildAging(List<Invoice> invoices, DateTime today)
        {
            string[] buckets = { "0-30 Days", "31-60 Days", "61-90 Days", "90+ Days" };
            var rows = buckets.Select(b => new InvoiceAgingBucket { Bucket = b }).ToList();
            foreach (Invoice invoice in invoices.Where(i => i.BalanceDue > 0m && !IsPaid(i) && i.DueDate.Date < today.Date))
            {
                int age = Math.Max(0, (today.Date - invoice.DueDate.Date).Days);
                string bucket = age <= 30 ? "0-30 Days" : age <= 60 ? "31-60 Days" : age <= 90 ? "61-90 Days" : "90+ Days";
                InvoiceAgingBucket row = rows.First(r => r.Bucket == bucket);
                row.Count++;
                row.Amount += invoice.BalanceDue;
            }
            return rows;
        }

        private List<InvoiceWorkflowSummaryRow> BuildWorkflow(List<Invoice> invoices, DateTime today)
        {
            return BuildStatuses(invoices, today)
                .Select(s => new InvoiceWorkflowSummaryRow { Status = s.Status, Count = s.Count, Color = s.Color })
                .ToList();
        }

        private List<string> BuildReminders(List<Invoice> invoices, List<AMCContract> contracts, DateTime today)
        {
            int pendingApproval = invoices.Count(i => ResolveStatus(i, today) == "Sent for Approval");
            int dueThisWeek = invoices.Count(i => i.BalanceDue > 0m && !IsPaid(i) && i.DueDate.Date >= today.Date && i.DueDate.Date <= today.Date.AddDays(7));
            int overdue = invoices.Count(i => IsOverdue(i, today));
            int expiringThisMonth = contracts.Count(c => c.EndDate.Date >= new DateTime(today.Year, today.Month, 1) && c.EndDate.Date <= new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month)));

            return new List<string>
            {
                pendingApproval + " invoices are pending approval.",
                dueThisWeek + " payments are expected this week.",
                overdue + " invoices are overdue.",
                "AMC contracts expiring this month: " + expiringThisMonth
            };
        }

        private DateTime GetPeriodEnd(DateTime start, InvoiceAnalyticsGrouping grouping, DateTime max)
        {
            DateTime end = grouping == InvoiceAnalyticsGrouping.Day
                ? start
                : grouping == InvoiceAnalyticsGrouping.Month
                    ? new DateTime(start.Year, start.Month, DateTime.DaysInMonth(start.Year, start.Month))
                    : start.AddDays(6);
            return end > max ? max : end;
        }

        private string FormatPeriod(DateTime start, DateTime end, InvoiceAnalyticsGrouping grouping)
        {
            if (grouping == InvoiceAnalyticsGrouping.Month)
                return start.ToString("MMM yyyy");
            if (grouping == InvoiceAnalyticsGrouping.Day || start == end)
                return start.ToString("dd MMM");
            return start.ToString("dd MMM") + "-" + end.ToString("dd MMM");
        }

        private string ResolveStatus(Invoice invoice, DateTime today)
        {
            if (invoice == null)
                return "Draft";
            if (IsOverdue(invoice, today))
                return "Overdue";
            string status = Clean(invoice.PaymentStatus, "Draft");
            if (status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                return "Sent for Approval";
            if (status.Equals("Partial", StringComparison.OrdinalIgnoreCase))
                return "Partially Paid";
            if (status.Equals("Sent", StringComparison.OrdinalIgnoreCase))
                return "Sent for Approval";
            return status;
        }

        private bool IsPaid(Invoice invoice)
        {
            return invoice != null &&
                   (string.Equals(invoice.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase) ||
                    (invoice.TotalAmount > 0m && invoice.PaidAmount >= invoice.TotalAmount));
        }

        private bool IsOverdue(Invoice invoice, DateTime today)
        {
            return invoice != null &&
                   invoice.BalanceDue > 0m &&
                   invoice.DueDate.Date < today.Date &&
                   !IsPaid(invoice);
        }

        private decimal Change(decimal current, decimal previous)
        {
            if (previous == 0m)
                return current == 0m ? 0m : 100m;
            return Math.Round(((current - previous) / previous) * 100m, 1);
        }

        private Color StatusColor(string status)
        {
            switch (status)
            {
                case "Draft": return Blue;
                case "Sent for Approval": return Orange;
                case "Approved": return Green;
                case "Partially Paid": return Purple;
                case "Paid": return Teal;
                case "Overdue": return Red;
                default: return Indigo;
            }
        }

        private string Clean(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public class DashboardAnalyticsFilter
    {
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string Customer { get; set; }
        public string Site { get; set; }
        public string JobStatus { get; set; }
        public string Technician { get; set; }
        public string Supplier { get; set; }
        public string PaymentStatus { get; set; }
    }

    public class DashboardAnalyticsInput
    {
        public List<Invoice> Invoices { get; set; } = new List<Invoice>();
        public List<Payment> Payments { get; set; } = new List<Payment>();
        public List<Job> Jobs { get; set; } = new List<Job>();
        public List<TenderBid> Quotes { get; set; } = new List<TenderBid>();
        public List<AMCContract> Contracts { get; set; } = new List<AMCContract>();
        public List<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
        public List<StockItem> InventoryItems { get; set; } = new List<StockItem>();
        public List<Vendor> Vendors { get; set; } = new List<Vendor>();
        public List<B2BClient> Clients { get; set; } = new List<B2BClient>();
        public List<Employee> Employees { get; set; } = new List<Employee>();
        public PayrollDashboardSnapshot Payroll { get; set; } = new PayrollDashboardSnapshot();
    }

    public class DashboardCommandCenterSnapshot
    {
        public bool UsesDemoData { get; set; }
        public DateTime GeneratedOn { get; set; } = DateTime.Now;
        public DashboardAnalyticsFilter Filter { get; set; }
        public List<DashboardMetricTile> Metrics { get; set; } = new List<DashboardMetricTile>();
        public List<DashboardChartSeries> Charts { get; set; } = new List<DashboardChartSeries>();
        public List<DashboardForecastItem> Forecasts { get; set; } = new List<DashboardForecastItem>();
        public Dictionary<string, List<DashboardDetailRow>> Details { get; set; } = new Dictionary<string, List<DashboardDetailRow>>(StringComparer.OrdinalIgnoreCase);
        public List<string> Customers { get; set; } = new List<string>();
        public List<string> Sites { get; set; } = new List<string>();
        public List<string> JobStatuses { get; set; } = new List<string>();
        public List<string> Technicians { get; set; } = new List<string>();
        public List<string> Suppliers { get; set; } = new List<string>();
        public List<string> PaymentStatuses { get; set; } = new List<string>();
    }

    public class DashboardMetricTile
    {
        public string Key { get; set; }
        public string Title { get; set; }
        public string Value { get; set; }
        public string Subtitle { get; set; }
        public Color Accent { get; set; }
    }

    public class DashboardChartSeries
    {
        public string Key { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public DashboardChartKind Kind { get; set; }
        public DashboardValueFormat ValueFormat { get; set; }
        public List<DashboardChartPoint> Points { get; set; } = new List<DashboardChartPoint>();
    }

    public class DashboardChartPoint
    {
        public string Label { get; set; }
        public decimal Value { get; set; }
        public string DetailKey { get; set; }
        public Color Color { get; set; }
    }

    public class DashboardForecastItem
    {
        public string Key { get; set; }
        public string Title { get; set; }
        public string Value { get; set; }
        public string Detail { get; set; }
        public Color Accent { get; set; }
    }

    public class DashboardDetailRow
    {
        public string Module { get; set; }
        public string Reference { get; set; }
        public string Customer { get; set; }
        public string Site { get; set; }
        public string Status { get; set; }
        public string Owner { get; set; }
        public DateTime? Date { get; set; }
        public decimal Amount { get; set; }
        public string Notes { get; set; }
    }

    public enum DashboardChartKind
    {
        Line,
        Bar,
        Donut,
        Funnel
    }

    public enum DashboardValueFormat
    {
        Number,
        Currency,
        Percent
    }

    public class DashboardCommandCenterService
    {
        private static readonly Color Blue = Color.FromArgb(37, 99, 235);
        private static readonly Color Green = Color.FromArgb(22, 163, 74);
        private static readonly Color Orange = Color.FromArgb(249, 115, 22);
        private static readonly Color Red = Color.FromArgb(239, 68, 68);
        private static readonly Color Purple = Color.FromArgb(124, 58, 237);
        private static readonly Color Teal = Color.FromArgb(6, 182, 212);
        private static readonly Color Slate = Color.FromArgb(71, 85, 105);

        public DashboardCommandCenterSnapshot BuildSnapshot(DashboardAnalyticsInput input, DashboardAnalyticsFilter filter)
        {
            input = input ?? new DashboardAnalyticsInput();
            filter = NormalizeFilter(filter);

            DashboardAnalyticsInput working = CloneInput(input);
            bool usesDemo = IsEmpty(working);

            DashboardCommandCenterSnapshot snapshot = new DashboardCommandCenterSnapshot
            {
                UsesDemoData = usesDemo,
                Filter = filter,
                Customers = BuildCustomers(working),
                Sites = BuildSites(working),
                JobStatuses = BuildJobStatuses(working),
                Technicians = BuildTechnicians(working),
                Suppliers = BuildSuppliers(working),
                PaymentStatuses = BuildPaymentStatuses(working)
            };

            List<Invoice> invoices = FilterInvoices(working.Invoices, filter).ToList();
            List<Payment> payments = FilterPayments(working.Payments, filter).ToList();
            List<Job> jobs = FilterJobs(working.Jobs, filter).ToList();
            List<TenderBid> quotes = FilterQuotes(working.Quotes, filter).ToList();
            List<AMCContract> contracts = FilterContracts(working.Contracts, filter).ToList();
            List<PurchaseOrder> purchases = FilterPurchases(working.PurchaseOrders, filter).ToList();
            List<StockItem> inventory = FilterInventory(working.InventoryItems, filter).ToList();
            List<Employee> employees = FilterEmployees(working.Employees, filter).ToList();

            BuildDetails(snapshot, invoices, payments, jobs, quotes, contracts, purchases, inventory, employees);
            BuildMetrics(snapshot, invoices, payments, jobs, quotes, contracts, purchases, inventory, employees, working.Payroll);
            BuildCharts(snapshot, invoices, payments, jobs, quotes, contracts, purchases, inventory, employees, working.Payroll, filter);
            BuildForecasts(snapshot, invoices, payments, jobs, contracts, inventory);

            return snapshot;
        }

        private DashboardAnalyticsFilter NormalizeFilter(DashboardAnalyticsFilter filter)
        {
            filter = filter ?? new DashboardAnalyticsFilter();
            DateTime today = DateTime.Today;
            if (!filter.DateFrom.HasValue)
                filter.DateFrom = new DateTime(today.Year, today.Month, 1).AddMonths(-5);
            if (!filter.DateTo.HasValue)
                filter.DateTo = today;
            if (filter.DateFrom.Value.Date > filter.DateTo.Value.Date)
            {
                DateTime swap = filter.DateFrom.Value;
                filter.DateFrom = filter.DateTo.Value;
                filter.DateTo = swap;
            }
            filter.Customer = NormalizeChoice(filter.Customer);
            filter.Site = NormalizeChoice(filter.Site);
            filter.JobStatus = NormalizeChoice(filter.JobStatus);
            filter.Technician = NormalizeChoice(filter.Technician);
            filter.Supplier = NormalizeChoice(filter.Supplier);
            filter.PaymentStatus = NormalizeChoice(filter.PaymentStatus);
            return filter;
        }

        private string NormalizeChoice(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "All", StringComparison.OrdinalIgnoreCase))
                return null;
            return value.Trim();
        }

        private DashboardAnalyticsInput CloneInput(DashboardAnalyticsInput input)
        {
            return new DashboardAnalyticsInput
            {
                Invoices = input.Invoices ?? new List<Invoice>(),
                Payments = input.Payments ?? new List<Payment>(),
                Jobs = input.Jobs ?? new List<Job>(),
                Quotes = input.Quotes ?? new List<TenderBid>(),
                Contracts = input.Contracts ?? new List<AMCContract>(),
                PurchaseOrders = input.PurchaseOrders ?? new List<PurchaseOrder>(),
                InventoryItems = input.InventoryItems ?? new List<StockItem>(),
                Vendors = input.Vendors ?? new List<Vendor>(),
                Clients = input.Clients ?? new List<B2BClient>(),
                Employees = input.Employees ?? new List<Employee>(),
                Payroll = input.Payroll ?? new PayrollDashboardSnapshot()
            };
        }

        private bool IsEmpty(DashboardAnalyticsInput input)
        {
            return !input.Invoices.Any()
                && !input.Payments.Any()
                && !input.Jobs.Any()
                && !input.Quotes.Any()
                && !input.Contracts.Any()
                && !input.PurchaseOrders.Any()
                && !input.InventoryItems.Any();
        }

        private void BuildMetrics(DashboardCommandCenterSnapshot snapshot, List<Invoice> invoices, List<Payment> payments, List<Job> jobs, List<TenderBid> quotes, List<AMCContract> contracts, List<PurchaseOrder> purchases, List<StockItem> inventory, List<Employee> employees, PayrollDashboardSnapshot payroll)
        {
            decimal revenue = invoices.Sum(i => i.TotalAmount);
            decimal collected = payments.Sum(p => p.AmountPaid);
            decimal pending = invoices.Sum(i => Math.Max(0m, i.BalanceDue));
            decimal supplierDues = purchases.Sum(p => Math.Max(0m, p.BalanceDue));
            decimal profit = revenue - purchases.Sum(p => p.TotalAmount);
            int openJobs = jobs.Count(j => !IsCompletedJob(j));
            int renewals = contracts.Count(c => c.EndDate.Date >= DateTime.Today && c.EndDate.Date <= DateTime.Today.AddDays(90));
            int lowStock = inventory.Count(i => i.IsLowStock);

            snapshot.Metrics.Add(Metric("revenue", "Revenue", FormatCurrency(revenue), invoices.Count + " invoices", Green));
            snapshot.Metrics.Add(Metric("pending_invoices", "Pending Invoices", FormatCurrency(pending), invoices.Count(i => i.BalanceDue > 0) + " open", pending > 0 ? Orange : Green));
            snapshot.Metrics.Add(Metric("open_jobs", "Open Jobs", openJobs.ToString("N0"), jobs.Count(IsCompletedJob) + " completed", Blue));
            snapshot.Metrics.Add(Metric("amc_renewals", "AMC Renewals", renewals.ToString("N0"), "next 90 days", renewals > 0 ? Orange : Green));
            snapshot.Metrics.Add(Metric("low_stock", "Low Stock", lowStock.ToString("N0"), inventory.Count + " stocked items", lowStock > 0 ? Red : Green));
            snapshot.Metrics.Add(Metric("supplier_dues", "Supplier Dues", FormatCurrency(supplierDues), purchases.Count(p => p.BalanceDue > 0) + " unpaid POs", supplierDues > 0 ? Purple : Green));
            snapshot.Metrics.Add(Metric("payroll", "Payroll", payroll != null && payroll.LastRun != null ? FormatCurrency(payroll.LastRun.TotalNetPay) : employees.Count.ToString("N0"), payroll != null && payroll.LastRun != null ? "last net pay" : "employees", Teal));
            snapshot.Metrics.Add(Metric("profit", "Profit", FormatCurrency(profit), profit >= 0 ? "invoice less purchase" : "cost pressure", profit >= 0 ? Green : Red));
        }

        private DashboardMetricTile Metric(string key, string title, string value, string subtitle, Color accent)
        {
            return new DashboardMetricTile { Key = key, Title = title, Value = value, Subtitle = subtitle, Accent = accent };
        }

        private void BuildCharts(DashboardCommandCenterSnapshot snapshot, List<Invoice> invoices, List<Payment> payments, List<Job> jobs, List<TenderBid> quotes, List<AMCContract> contracts, List<PurchaseOrder> purchases, List<StockItem> inventory, List<Employee> employees, PayrollDashboardSnapshot payroll, DashboardAnalyticsFilter filter)
        {
            List<DateTime> months = BuildMonths(filter.DateFrom.Value, filter.DateTo.Value);
            snapshot.Charts.Add(new DashboardChartSeries
            {
                Key = "revenue_trend",
                Title = "Revenue Trend",
                Subtitle = "Monthly invoice value over time",
                Kind = DashboardChartKind.Line,
                ValueFormat = DashboardValueFormat.Currency,
                Points = months.Select(m => new DashboardChartPoint
                {
                    Label = m.ToString("MMM yy"),
                    Value = invoices.Where(i => SameMonth(i.InvoiceDate, m)).Sum(i => i.TotalAmount),
                    DetailKey = "revenue",
                    Color = Green
                }).ToList()
            });
            snapshot.Charts.Add(new DashboardChartSeries
            {
                Key = "payment_trend",
                Title = "Payment Collection Trend",
                Subtitle = "Cash received by month",
                Kind = DashboardChartKind.Line,
                ValueFormat = DashboardValueFormat.Currency,
                Points = months.Select(m => new DashboardChartPoint
                {
                    Label = m.ToString("MMM yy"),
                    Value = payments.Where(p => SameMonth(p.PaymentDate, m)).Sum(p => p.AmountPaid),
                    DetailKey = "payments",
                    Color = Teal
                }).ToList()
            });
            snapshot.Charts.Add(new DashboardChartSeries
            {
                Key = "invoice_status",
                Title = "Invoice Status",
                Subtitle = "Paid, partial, pending, overdue",
                Kind = DashboardChartKind.Donut,
                ValueFormat = DashboardValueFormat.Currency,
                Points = GroupCurrency(invoices, i => NormalizeInvoiceStatus(i), i => Math.Max(0m, i.BalanceDue) > 0 ? i.BalanceDue : i.TotalAmount, "pending_invoices")
            });
            snapshot.Charts.Add(new DashboardChartSeries
            {
                Key = "jobs_by_status",
                Title = "Jobs by Status",
                Subtitle = "Open work and completion load",
                Kind = DashboardChartKind.Bar,
                ValueFormat = DashboardValueFormat.Number,
                Points = GroupCount(jobs, j => NormalizeJobStatus(j.Status), "open_jobs")
            });
            snapshot.Charts.Add(new DashboardChartSeries
            {
                Key = "supplier_spend",
                Title = "Supplier Spend",
                Subtitle = "Top vendors by PO value",
                Kind = DashboardChartKind.Bar,
                ValueFormat = DashboardValueFormat.Currency,
                Points = purchases
                    .GroupBy(p => Safe(p.VendorName, "Unknown supplier"))
                    .OrderByDescending(g => g.Sum(p => p.TotalAmount))
                    .Take(8)
                    .Select((g, index) => Point(g.Key, g.Sum(p => p.TotalAmount), "suppliers", Palette(index)))
                    .ToList()
            });
            snapshot.Charts.Add(new DashboardChartSeries
            {
                Key = "amc_renewals",
                Title = "AMC Renewal Risk",
                Subtitle = "Expiry buckets based on contract end dates",
                Kind = DashboardChartKind.Donut,
                ValueFormat = DashboardValueFormat.Number,
                Points = new List<DashboardChartPoint>
                {
                    Point("0-30d", contracts.Count(c => DaysUntil(c.EndDate) >= 0 && DaysUntil(c.EndDate) <= 30), "amc_renewals", Red),
                    Point("31-60d", contracts.Count(c => DaysUntil(c.EndDate) > 30 && DaysUntil(c.EndDate) <= 60), "amc_renewals", Orange),
                    Point("61-90d", contracts.Count(c => DaysUntil(c.EndDate) > 60 && DaysUntil(c.EndDate) <= 90), "amc_renewals", Blue)
                }
            });
            snapshot.Charts.Add(new DashboardChartSeries
            {
                Key = "payroll",
                Title = "Payroll Load",
                Subtitle = "People cost and statutory risk",
                Kind = DashboardChartKind.Bar,
                ValueFormat = DashboardValueFormat.Currency,
                Points = new List<DashboardChartPoint>
                {
                    Point("Net Pay", payroll != null && payroll.LastRun != null ? payroll.LastRun.TotalNetPay : employees.Sum(e => e.GrossSalary), "payroll", Teal),
                    Point("Employer Liability", payroll != null && payroll.LastRun != null ? payroll.LastRun.TotalEPFEmployer + payroll.LastRun.TotalESIEmployer : 0m, "payroll", Purple),
                    Point("TDS/PT", payroll != null && payroll.LastRun != null ? payroll.LastRun.TotalTDS + payroll.LastRun.TotalPT : 0m, "payroll", Orange)
                }
            });
            snapshot.Charts.Add(new DashboardChartSeries
            {
                Key = "profit",
                Title = "Profit vs Purchase Cost",
                Subtitle = "Revenue minus supplier spend",
                Kind = DashboardChartKind.Bar,
                ValueFormat = DashboardValueFormat.Currency,
                Points = new List<DashboardChartPoint>
                {
                    Point("Revenue", invoices.Sum(i => i.TotalAmount), "revenue", Green),
                    Point("Supplier Cost", purchases.Sum(p => p.TotalAmount), "supplier_dues", Orange),
                    Point("Gross Profit", invoices.Sum(i => i.TotalAmount) - purchases.Sum(p => p.TotalAmount), "profit", Purple)
                }
            });
            snapshot.Charts.Add(new DashboardChartSeries
            {
                Key = "quote_funnel",
                Title = "Quote to Cash Funnel",
                Subtitle = "Quote -> Job -> Invoice -> Payment conversion",
                Kind = DashboardChartKind.Funnel,
                ValueFormat = DashboardValueFormat.Number,
                Points = new List<DashboardChartPoint>
                {
                    Point("Quotes", Math.Max(0, quotes.Count), "quotes", Purple),
                    Point("Jobs", Math.Max(0, jobs.Count), "open_jobs", Blue),
                    Point("Invoices", Math.Max(0, invoices.Count), "pending_invoices", Orange),
                    Point("Payments", Math.Max(0, payments.Count), "payments", Green)
                }
            });
        }

        private void BuildForecasts(DashboardCommandCenterSnapshot snapshot, List<Invoice> invoices, List<Payment> payments, List<Job> jobs, List<AMCContract> contracts, List<StockItem> inventory)
        {
            decimal lastThreeMonthRevenue = invoices
                .Where(i => i.InvoiceDate.Date >= DateTime.Today.AddMonths(-3))
                .GroupBy(i => new DateTime(i.InvoiceDate.Year, i.InvoiceDate.Month, 1))
                .DefaultIfEmpty()
                .Average(g => g == null ? 0m : g.Sum(i => i.TotalAmount));
            decimal recurring = contracts.Where(c => IsActiveContract(c)).Sum(c => c.MonthlyValue);
            decimal expectedRevenue = Math.Max(lastThreeMonthRevenue, recurring);
            decimal upcomingCollection = invoices.Where(i => i.BalanceDue > 0 && i.DueDate.Date <= DateTime.Today.AddDays(30)).Sum(i => i.BalanceDue);
            int renewalCount = contracts.Count(c => c.EndDate.Date >= DateTime.Today && c.EndDate.Date <= DateTime.Today.AddDays(90));
            int stockRisk = inventory.Count(i => i.CurrentStock <= Math.Max(i.ReorderLevel, 1m));
            double averageCompletionDays = jobs.Where(j => j.CompletedDate.HasValue)
                .Select(j => Math.Max(1, (j.CompletedDate.Value.Date - j.ScheduledDate.Date).TotalDays))
                .DefaultIfEmpty(3d)
                .Average();
            int openJobs = jobs.Count(j => !IsCompletedJob(j));
            DateTime expectedCompletion = DateTime.Today.AddDays(Math.Ceiling(averageCompletionDays));

            snapshot.Forecasts.Add(Forecast("expected_revenue", "Expected Monthly Revenue", FormatCurrency(expectedRevenue), "Based on recent invoice trend and active AMC value.", Green));
            snapshot.Forecasts.Add(Forecast("amc_renewals", "Upcoming AMC Renewals", renewalCount.ToString("N0"), "Contracts expiring in the next 90 days.", renewalCount > 0 ? Orange : Green));
            snapshot.Forecasts.Add(Forecast("stock_risk", "Stock Depletion Risk", stockRisk.ToString("N0"), "Items at or below reorder level.", stockRisk > 0 ? Red : Green));
            snapshot.Forecasts.Add(Forecast("collection_forecast", "30-Day Collection Forecast", FormatCurrency(upcomingCollection), "Open invoices due within 30 days.", upcomingCollection > 0 ? Teal : Green));
            snapshot.Forecasts.Add(Forecast("job_completion", "Job Completion Forecast", openJobs == 0 ? "Clear" : expectedCompletion.ToString("dd MMM"), openJobs + " open jobs, avg " + averageCompletionDays.ToString("N1") + " days.", Blue));
        }

        private DashboardForecastItem Forecast(string key, string title, string value, string detail, Color accent)
        {
            return new DashboardForecastItem { Key = key, Title = title, Value = value, Detail = detail, Accent = accent };
        }

        private void BuildDetails(DashboardCommandCenterSnapshot snapshot, List<Invoice> invoices, List<Payment> payments, List<Job> jobs, List<TenderBid> quotes, List<AMCContract> contracts, List<PurchaseOrder> purchases, List<StockItem> inventory, List<Employee> employees)
        {
            snapshot.Details["revenue"] = invoices.Select(i => Row("Invoice", i.InvoiceNumber, i.ClientName, i.SiteName, i.PaymentStatus, i.CreatedByName, i.InvoiceDate, i.TotalAmount, i.Subject)).ToList();
            snapshot.Details["pending_invoices"] = invoices.Where(i => i.BalanceDue > 0).Select(i => Row("Invoice", i.InvoiceNumber, i.ClientName, i.SiteName, i.PaymentStatus, i.CreatedByName, i.DueDate, i.BalanceDue, "Due " + i.DueDate.ToString("dd MMM yyyy"))).ToList();
            snapshot.Details["payments"] = payments.Select(p => Row("Payment", p.PaymentNumber, p.ClientName, string.Empty, p.PaymentMode, p.CreatedByName, p.PaymentDate, p.AmountPaid, p.ReferenceNumber)).ToList();
            snapshot.Details["open_jobs"] = jobs.Where(j => !IsCompletedJob(j)).Select(j => Row("Job", j.JobNumber, j.ClientName, j.SiteName, NormalizeJobStatus(j.Status), j.AssignedEmployeeName, j.ScheduledDate, JobRevenue(j), j.JobTitle ?? j.Title)).ToList();
            snapshot.Details["amc_renewals"] = contracts.Where(c => c.EndDate.Date >= DateTime.Today && c.EndDate.Date <= DateTime.Today.AddDays(90)).Select(c => Row("AMC", "AMC-" + c.ContractID, string.Empty, string.Empty, c.ContractStatus, c.CreatedByName, c.EndDate, c.AnnualValue, c.ContractType)).ToList();
            snapshot.Details["low_stock"] = inventory.Where(i => i.IsLowStock).Select(i => Row("Inventory", i.ItemName, string.Empty, string.Empty, "Low Stock", i.VendorName, i.LastUpdated, i.StockValue, "Stock " + i.CurrentStock.ToString("N2") + " / reorder " + i.ReorderLevel.ToString("N2"))).ToList();
            snapshot.Details["suppliers"] = purchases.Select(p => Row("Purchase", p.PONumber, p.ClientName, p.SiteName, p.Status, p.VendorName, p.PODate, p.TotalAmount, p.Notes)).ToList();
            snapshot.Details["supplier_dues"] = purchases.Where(p => p.BalanceDue > 0).Select(p => Row("Purchase", p.PONumber, p.ClientName, p.SiteName, p.Status, p.VendorName, p.PayByDate, p.BalanceDue, "Vendor due")).ToList();
            snapshot.Details["quotes"] = quotes.Select(q => Row("Quote", q.QuotationNumber, q.ClientName, q.SiteName, q.Status, q.RecommendedVendorName, q.DueDate, QuoteValue(q), q.TenderName)).ToList();
            snapshot.Details["payroll"] = employees.Select(e => Row("Employee", e.EmployeeCode, e.Name, e.ClientSite, e.Status, e.Designation, e.JoiningDate ?? e.DateOfJoining, e.GrossSalary, e.Department)).ToList();
            snapshot.Details["profit"] = snapshot.Details["revenue"].Concat(snapshot.Details["suppliers"]).ToList();
        }

        private DashboardDetailRow Row(string module, string reference, string customer, string site, string status, string owner, DateTime? date, decimal amount, string notes)
        {
            return new DashboardDetailRow
            {
                Module = Safe(module),
                Reference = Safe(reference),
                Customer = Safe(customer),
                Site = Safe(site),
                Status = Safe(status),
                Owner = Safe(owner),
                Date = date,
                Amount = amount,
                Notes = Safe(notes)
            };
        }

        private IEnumerable<Invoice> FilterInvoices(IEnumerable<Invoice> source, DashboardAnalyticsFilter filter)
        {
            return source.Where(i => InRange(i.InvoiceDate, filter)
                && Matches(filter.Customer, i.ClientName)
                && Matches(filter.Site, i.SiteName)
                && Matches(filter.PaymentStatus, i.PaymentStatus));
        }

        private IEnumerable<Payment> FilterPayments(IEnumerable<Payment> source, DashboardAnalyticsFilter filter)
        {
            return source.Where(p => InRange(p.PaymentDate, filter)
                && Matches(filter.Customer, p.ClientName)
                && Matches(filter.PaymentStatus, p.PaymentMode));
        }

        private IEnumerable<Job> FilterJobs(IEnumerable<Job> source, DashboardAnalyticsFilter filter)
        {
            return source.Where(j => InRange(j.CompletedDate ?? j.ScheduledDate, filter)
                && Matches(filter.Customer, j.ClientName)
                && Matches(filter.Site, j.SiteName)
                && Matches(filter.JobStatus, NormalizeJobStatus(j.Status))
                && Matches(filter.Technician, j.AssignedEmployeeName));
        }

        private IEnumerable<TenderBid> FilterQuotes(IEnumerable<TenderBid> source, DashboardAnalyticsFilter filter)
        {
            return source.Where(q => InRange(q.SubmittedDate ?? q.DueDate, filter)
                && Matches(filter.Customer, q.ClientName)
                && Matches(filter.Site, q.SiteName)
                && Matches(filter.Supplier, q.RecommendedVendorName));
        }

        private IEnumerable<AMCContract> FilterContracts(IEnumerable<AMCContract> source, DashboardAnalyticsFilter filter)
        {
            return source.Where(c => InRange(c.EndDate, filter));
        }

        private IEnumerable<PurchaseOrder> FilterPurchases(IEnumerable<PurchaseOrder> source, DashboardAnalyticsFilter filter)
        {
            return source.Where(p => InRange(p.PODate, filter)
                && Matches(filter.Customer, p.ClientName)
                && Matches(filter.Site, p.SiteName)
                && Matches(filter.Technician, p.AssignedTechnicianName)
                && Matches(filter.Supplier, p.VendorName)
                && Matches(filter.PaymentStatus, p.Status));
        }

        private IEnumerable<StockItem> FilterInventory(IEnumerable<StockItem> source, DashboardAnalyticsFilter filter)
        {
            return source.Where(i => Matches(filter.Supplier, i.VendorName));
        }

        private IEnumerable<Employee> FilterEmployees(IEnumerable<Employee> source, DashboardAnalyticsFilter filter)
        {
            return source.Where(e => Matches(filter.Technician, e.Name) || string.IsNullOrWhiteSpace(filter.Technician));
        }

        private bool InRange(DateTime date, DashboardAnalyticsFilter filter)
        {
            return date.Date >= filter.DateFrom.Value.Date && date.Date <= filter.DateTo.Value.Date;
        }

        private bool Matches(string filter, string value)
        {
            return string.IsNullOrWhiteSpace(filter) || string.Equals(Safe(value), filter, StringComparison.OrdinalIgnoreCase);
        }

        private List<string> BuildCustomers(DashboardAnalyticsInput input)
        {
            return BuildOptions(input.Clients.Select(c => c.CompanyName)
                .Concat(input.Invoices.Select(i => i.ClientName))
                .Concat(input.Jobs.Select(j => j.ClientName))
                .Concat(input.PurchaseOrders.Select(p => p.ClientName))
                .Concat(input.Quotes.Select(q => q.ClientName)));
        }

        private List<string> BuildSites(DashboardAnalyticsInput input)
        {
            return BuildOptions(input.Invoices.Select(i => i.SiteName)
                .Concat(input.Jobs.Select(j => j.SiteName))
                .Concat(input.PurchaseOrders.Select(p => p.SiteName))
                .Concat(input.Quotes.Select(q => q.SiteName)));
        }

        private List<string> BuildJobStatuses(DashboardAnalyticsInput input)
        {
            return BuildOptions(input.Jobs.Select(j => NormalizeJobStatus(j.Status)));
        }

        private List<string> BuildTechnicians(DashboardAnalyticsInput input)
        {
            return BuildOptions(input.Employees.Select(e => e.Name)
                .Concat(input.Jobs.Select(j => j.AssignedEmployeeName))
                .Concat(input.PurchaseOrders.Select(p => p.AssignedTechnicianName)));
        }

        private List<string> BuildSuppliers(DashboardAnalyticsInput input)
        {
            return BuildOptions(input.Vendors.Select(v => v.VendorName)
                .Concat(input.PurchaseOrders.Select(p => p.VendorName))
                .Concat(input.InventoryItems.Select(i => i.VendorName))
                .Concat(input.Quotes.Select(q => q.RecommendedVendorName)));
        }

        private List<string> BuildPaymentStatuses(DashboardAnalyticsInput input)
        {
            return BuildOptions(input.Invoices.Select(i => i.PaymentStatus)
                .Concat(input.PurchaseOrders.Select(p => p.Status))
                .Concat(input.Payments.Select(p => p.PaymentMode)));
        }

        private List<string> BuildOptions(IEnumerable<string> values)
        {
            List<string> options = values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v)
                .Take(80)
                .ToList();
            options.Insert(0, "All");
            return options;
        }

        private List<DateTime> BuildMonths(DateTime start, DateTime end)
        {
            DateTime cursor = new DateTime(start.Year, start.Month, 1);
            DateTime last = new DateTime(end.Year, end.Month, 1);
            List<DateTime> months = new List<DateTime>();
            while (cursor <= last && months.Count < 18)
            {
                months.Add(cursor);
                cursor = cursor.AddMonths(1);
            }
            if (!months.Any())
                months.Add(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1));
            return months;
        }

        private List<DashboardChartPoint> GroupCurrency<T>(IEnumerable<T> source, Func<T, string> label, Func<T, decimal> value, string detailKey)
        {
            return source.GroupBy(label)
                .Select((group, index) => Point(group.Key, group.Sum(value), detailKey, Palette(index)))
                .Where(point => point.Value > 0)
                .ToList();
        }

        private List<DashboardChartPoint> GroupCount<T>(IEnumerable<T> source, Func<T, string> label, string detailKey)
        {
            return source.GroupBy(label)
                .Select((group, index) => Point(group.Key, group.Count(), detailKey, Palette(index)))
                .Where(point => point.Value > 0)
                .ToList();
        }

        private DashboardChartPoint Point(string label, decimal value, string detailKey, Color color)
        {
            return new DashboardChartPoint { Label = Safe(label, "None"), Value = Math.Max(0m, value), DetailKey = detailKey, Color = color };
        }

        private bool SameMonth(DateTime value, DateTime month)
        {
            return value.Year == month.Year && value.Month == month.Month;
        }

        private string NormalizeInvoiceStatus(Invoice invoice)
        {
            if (invoice == null)
                return "Unknown";
            if (invoice.BalanceDue <= 0)
                return "Paid";
            if (invoice.DueDate.Date < DateTime.Today)
                return "Overdue";
            return Safe(invoice.PaymentStatus, "Pending");
        }

        private string NormalizeJobStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return "Unassigned";
            if (string.Equals(status, "InProgress", StringComparison.OrdinalIgnoreCase))
                return "In Progress";
            return status.Trim();
        }

        private bool IsCompletedJob(Job job)
        {
            return job != null && (job.CompletedDate.HasValue || string.Equals(NormalizeJobStatus(job.Status), "Completed", StringComparison.OrdinalIgnoreCase));
        }

        private bool IsActiveContract(AMCContract contract)
        {
            return contract != null && contract.EndDate.Date >= DateTime.Today && !string.Equals(contract.ContractStatus, "Expired", StringComparison.OrdinalIgnoreCase);
        }

        private int DaysUntil(DateTime date)
        {
            return (date.Date - DateTime.Today).Days;
        }

        private decimal QuoteValue(TenderBid quote)
        {
            if (quote == null)
                return 0m;
            return quote.TotalWithGST > 0 ? quote.TotalWithGST : quote.BidValue;
        }

        private decimal JobRevenue(Job job)
        {
            if (job == null)
                return 0m;
            if (job.ActualRevenue > 0)
                return job.ActualRevenue;
            if (job.Revenue > 0)
                return job.Revenue;
            return job.QuotedRevenue;
        }

        private Color Palette(int index)
        {
            Color[] colors = { Blue, Green, Orange, Purple, Teal, Red, Slate, Color.FromArgb(14, 165, 233) };
            return colors[Math.Abs(index) % colors.Length];
        }

        private string Safe(string value, string fallback = "")
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private string FormatCurrency(decimal amount)
        {
            decimal absolute = Math.Abs(amount);
            string prefix = amount < 0 ? "-" : string.Empty;
            if (absolute >= 10000000m)
                return prefix + "Rs " + (absolute / 10000000m).ToString("N2") + " Cr";
            if (absolute >= 100000m)
                return prefix + "Rs " + (absolute / 100000m).ToString("N2") + " L";
            return prefix + "Rs " + absolute.ToString("N0");
        }

    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace HVAC_Pro_Desktop.UI
{
    public class ReportForm : DeferredPageControl
    {
        private readonly ContractService _contractSvc = new ContractService();
        private readonly InvoiceService _invoiceSvc = new InvoiceService();
        private readonly PurchaseService _purchaseSvc = new PurchaseService();
        private readonly ClientService _clientSvc = new ClientService();
        private readonly JobService _jobSvc = new JobService();
        private readonly EmployeeService _employeeSvc = new EmployeeService();
        private readonly InventoryService _inventorySvc = new InventoryService();
        private readonly VendorAdvancePaymentService _vendorAdvanceSvc = new VendorAdvancePaymentService();
        private readonly PayrollService _payrollSvc = new PayrollService();

        private static int? PendingTabIndex;
        private int _currentReportIndex;

        private Label _lblStatus;
        private Label _lblRevenue, _lblRevenueSub, _lblReceivable, _lblReceivableSub, _lblSla, _lblSlaSub;
        private Label _lblMargin, _lblMarginSub, _lblPayroll, _lblPayrollSub, _lblInventory, _lblInventorySub;
        private Chart _revenueChart, _agingChart, _workloadChart;
        private DataGridView _clientGrid, _detailGrid;
        private FlowLayoutPanel _actionQueue;
        private FlowLayoutPanel _reportLibrary;
        private Panel _dashboardFlow;
        private readonly Dictionary<string, ResizableCard> _dashboardCards = new Dictionary<string, ResizableCard>(StringComparer.OrdinalIgnoreCase);
        private ResizableCard _dragCard;

        private List<AMCContract> _contracts = new List<AMCContract>();
        private List<Invoice> _invoices = new List<Invoice>();
        private List<PurchaseOrder> _purchases = new List<PurchaseOrder>();
        private List<Job> _jobs = new List<Job>();
        private List<Employee> _technicians = new List<Employee>();
        private List<StockItem> _stock = new List<StockItem>();
        private List<VendorAdvancePayment> _vendorAdvances = new List<VendorAdvancePayment>();
        private Dictionary<int, string> _clientNames = new Dictionary<int, string>();
        private bool _refreshing;

        private static readonly string[] ReportNames =
        {
            "Revenue", "Collections", "Contracts", "Jobs", "Technicians", "Materials", "Purchases", "Supplier Advances", "Clients / Sites"
        };
        private const string PageKey = "ReportsCommandCenter";
        private const string CardOrderPath = @"C:\HVAC_PRO_MSE\CONFIG\reports_card_order.txt";

        private static readonly Color PageBg = DS.BgPage;
        private static readonly Color CardBg = DS.White;
        private static readonly Color Border = DS.Border;
        private static readonly Color TextDark = DS.Slate900;
        private static readonly Color TextMid = DS.Slate500;
        private static readonly Color Blue = DS.Primary600;
        private static readonly Color Green = DS.Green600;
        private static readonly Color Amber = DS.Amber500;
        private static readonly Color Red = DS.Red600;
        private static readonly Color Teal = DS.Teal500;

        public ReportForm()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            BuildLayout();
            UIHelper.ApplyInputStyles(Controls);
            EnableDeferredLoad(
                () => RefreshAllAsync(),
                ex =>
                {
                    AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Reports"), "Loading reports", ex);
                    _lblStatus.Text = "Reports could not load. Refresh and try again.";
                    _lblStatus.ForeColor = Red;
                });
        }

        protected override bool EnableAutomaticLayoutScaling => false;
        protected override bool EnableMainScrollCanvas => false;
        protected override bool SuppressAutomaticChildPolish => true;

        public static void QueueTechnicianEfficiencyNavigation()
        {
            PendingTabIndex = 4;
        }

        public void ApplyNavigationRequest()
        {
            if (!PendingTabIndex.HasValue)
                return;
            SelectReport(Math.Max(0, Math.Min(ReportNames.Length - 1, PendingTabIndex.Value)));
            PendingTabIndex = null;
        }

        private void BuildLayout()
        {
            Controls.Clear();

            Panel surface = new Panel
            {
                Name = "ReportsSurface",
                Tag = "NO_CARD_SURFACE",
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = PageBg,
                Padding = new Padding(16)
            };
            Controls.Add(surface);

            Panel detailSection = BuildDetailSection();
            Panel librarySection = BuildLibrarySection();
            Panel commandSection = BuildCommandSection();
            TableLayoutPanel kpiStrip = BuildKpiStrip();
            Panel header = BuildHeader();

            surface.Controls.Add(detailSection);
            surface.Controls.Add(librarySection);
            surface.Controls.Add(commandSection);
            surface.Controls.Add(kpiStrip);
            surface.Controls.Add(header);
        }

        private Panel BuildHeader()
        {
            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 78,
                BackColor = PageBg,
                Padding = new Padding(0, 8, 0, 12)
            };

            Label title = new Label
            {
                Text = "Reports Command Center",
                Location = new Point(0, 4),
                Size = new Size(420, 30),
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = TextDark,
                TextAlign = ContentAlignment.MiddleLeft
            };
            Label subtitle = new Label
            {
                Text = "Real-time insights and analytics across your business.",
                Location = new Point(1, 38),
                Size = new Size(520, 22),
                Font = DS.Body,
                ForeColor = TextMid,
                TextAlign = ContentAlignment.MiddleLeft
            };

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 500,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 12, 0, 0),
                BackColor = header.BackColor
            };
            Button export = MakeButton("Export CSV", Green, 104);
            Button pnl = MakeButton("Export P&L Excel", Color.White, 138);
            Button refresh = MakeButton("Refresh", Blue, 94);
            Button forms = MakeButton("Service Forms", Color.White, 108);
            ModernIconSystem.AddButtonIcon(export, ModernIconKind.Export);
            ModernIconSystem.AddButtonIcon(pnl, ModernIconKind.Export);
            ModernIconSystem.AddButtonIcon(refresh, ModernIconKind.Refresh);
            ModernIconSystem.AddButtonIcon(forms, ModernIconKind.Document);
            export.Click += (s, e) => ExportCurrentReport();
            pnl.Click += (s, e) => ExportMonthlyProfitLoss();
            refresh.Click += async (s, e) => await RefreshAllAsync();
            forms.Click += (s, e) => FormTemplateWorkflowLauncher.Open(this, "Reports", "Reports", null, "service completion report AMC visit report compliance audit job costing sheet export analytics");
            actions.Controls.Add(export);
            actions.Controls.Add(pnl);
            actions.Controls.Add(forms);
            actions.Controls.Add(refresh);

            _lblStatus = new Label
            {
                Text = "Loading reports...",
                Dock = DockStyle.Right,
                Width = 320,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Green,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 22, 12, 0)
            };

            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            header.Controls.Add(_lblStatus);
            header.Controls.Add(actions);
            return header;
        }

        private TableLayoutPanel BuildKpiStrip()
        {
            TableLayoutPanel strip = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 110,
                ColumnCount = 6,
                BackColor = PageBg,
                Padding = new Padding(0, 0, 0, 12)
            };
            for (int i = 0; i < 6; i++)
                strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.666f));

            _lblRevenue = AddKpi(strip, 0, "Revenue", Green, out _lblRevenueSub);
            _lblReceivable = AddKpi(strip, 1, "Overdue Receivables", Red, out _lblReceivableSub);
            _lblSla = AddKpi(strip, 2, "SLA Risk", Amber, out _lblSlaSub);
            _lblMargin = AddKpi(strip, 3, "Live Margin", Blue, out _lblMarginSub);
            _lblPayroll = AddKpi(strip, 4, "Tech Load", Teal, out _lblPayrollSub);
            _lblInventory = AddKpi(strip, 5, "Material Plan", Amber, out _lblInventorySub);
            return strip;
        }

        private Panel BuildCommandSection()
        {
            Panel wrapper = new Panel { Dock = DockStyle.Top, Height = 520, BackColor = PageBg, Padding = new Padding(0, 0, 0, 12) };
            _dashboardFlow = new Panel
            {
                Dock = DockStyle.Top,
                BackColor = PageBg,
                AllowDrop = true,
                Padding = new Padding(0)
            };
            _dashboardFlow.DragEnter += DashboardFlow_DragEnter;
            _dashboardFlow.DragDrop += DashboardFlow_DragDrop;
            _dashboardFlow.Resize += (s, e) => LayoutDashboardCards();
            wrapper.Controls.Add(_dashboardFlow);

            _revenueChart = MakeChart("Monthly revenue", SeriesChartType.Line, Green);
            _agingChart = MakeChart("Collections aging", SeriesChartType.Column, Blue);
            _workloadChart = MakeChart("Technician workload", SeriesChartType.Bar, Amber);
            _clientGrid = MakeGrid();
            _clientGrid.Columns.Add(C("Client", 190));
            _clientGrid.Columns.Add(C("Revenue", 90));
            _clientGrid.Columns.Add(C("Cost", 80));
            _clientGrid.Columns.Add(C("Margin", 75));
            _clientGrid.Columns[1].MinimumWidth = 105;
            _clientGrid.Columns[2].MinimumWidth = 95;
            _clientGrid.Columns[3].MinimumWidth = 85;

            _actionQueue = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(12),
                BackColor = CardBg
            };

            _dashboardCards.Clear();
            AddDashboardCard("revenue_trend", "Revenue trend", _revenueChart, 680, 220, "Large");
            AddDashboardCard("receivables_aging", "Receivables aging", _agingChart, 680, 220, "Large");
            AddDashboardCard("action_queue", "Owner action queue", _actionQueue, 390, 452, "Medium");
            AddDashboardCard("service_workload", "Service workload", _workloadChart, 680, 220, "Large");
            AddDashboardCard("client_profitability", "Top client profitability", _clientGrid, 680, 220, "Large");
            ApplySavedCardOrder();
            new CardLayoutService().ApplyLayoutToPage(this, PageKey, CardLayoutService.ResolveCurrentUserId());
            LayoutDashboardCards();
            return wrapper;
        }

        private Panel BuildLibrarySection()
        {
            Panel wrapper = new Panel { Dock = DockStyle.Top, Height = 168, BackColor = PageBg, Padding = new Padding(0, 0, 0, 12) };
            Panel card = MakePlainCard("Report Library");
            _reportLibrary = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(12, 8, 12, 10),
                BackColor = CardBg
            };
            for (int i = 0; i < ReportNames.Length; i++)
                _reportLibrary.Controls.Add(MakeReportTile(i, ReportNames[i]));
            Label hint = new Label
            {
                Text = "Pinned owner views: Revenue, Collections, SLA Risk, Materials, Purchases, Payroll, and Client/Site reports.",
                Dock = DockStyle.Top,
                Height = 28,
                Font = DS.Small,
                ForeColor = TextMid,
                TextAlign = ContentAlignment.MiddleLeft
            };
            card.Controls.Add(_reportLibrary);
            card.Controls.Add(hint);
            wrapper.Controls.Add(card);
            return wrapper;
        }

        private Panel BuildDetailSection()
        {
            Panel wrapper = new Panel { Dock = DockStyle.Top, Height = 310, BackColor = PageBg };
            Panel card = MakePlainCard("Detailed report");
            _detailGrid = MakeGrid();
            _detailGrid.Dock = DockStyle.Fill;
            card.Controls.Add(_detailGrid);
            wrapper.Controls.Add(card);
            return wrapper;
        }

        private async Task RefreshAllAsync()
        {
            if (_refreshing)
                return;

            _refreshing = true;
            try
            {
                _lblStatus.Text = "Refreshing reports...";
                _lblStatus.ForeColor = Blue;
                await Task.Run(() => LoadData());
                if (IsDisposed)
                    return;
                BindKpis();
                BindCharts();
                BindClientProfitability();
                BindActionQueue();
                SelectReport(_currentReportIndex);
                _lblStatus.Text = "Reports updated " + DateTime.Now.ToString("dd-MMM HH:mm");
                _lblStatus.ForeColor = Green;
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Reports"), "Refreshing report", ex);
                _lblStatus.Text = "Report could not be refreshed. Review the filters and try again.";
                _lblStatus.ForeColor = Red;
            }
            finally
            {
                _refreshing = false;
            }
        }

        private void LoadData()
        {
            TimeSpan ttl = TimeSpan.FromMinutes(2);
            try { _contracts = AppDataCache.GetOrCreate("contracts:all", ttl, () => _contractSvc.GetAllContracts() ?? new List<AMCContract>()).ToList(); } catch { _contracts = new List<AMCContract>(); }
            try { _invoices = AppDataCache.GetOrCreate("invoices:all", ttl, () => _invoiceSvc.GetAllInvoices() ?? new List<Invoice>()).ToList(); } catch { _invoices = new List<Invoice>(); }
            try { _purchases = AppDataCache.GetOrCreate("purchases:all", ttl, () => _purchaseSvc.GetAll() ?? new List<PurchaseOrder>()).ToList(); } catch { _purchases = new List<PurchaseOrder>(); }
            try { _jobs = AppDataCache.GetOrCreate("jobs:all", ttl, () => _jobSvc.GetAll() ?? new List<Job>()).ToList(); } catch { _jobs = new List<Job>(); }
            try { _technicians = AppDataCache.GetOrCreate("employees:technicians-active", ttl, () => _employeeSvc.GetActiveTechnicians() ?? new List<Employee>()).ToList(); } catch { _technicians = new List<Employee>(); }
            try { _stock = AppDataCache.GetOrCreate("inventory:all", ttl, () => _inventorySvc.GetAll() ?? new List<StockItem>()).ToList(); } catch { _stock = new List<StockItem>(); }
            try { _vendorAdvances = AppDataCache.GetOrCreate("vendors:advances", ttl, () => _vendorAdvanceSvc.GetAll() ?? new List<VendorAdvancePayment>()).ToList(); } catch { _vendorAdvances = new List<VendorAdvancePayment>(); }
            try { _clientNames = AppDataCache.GetOrCreate("clients:active", ttl, () => _clientSvc.GetAllClients() ?? new List<B2BClient>()).ToDictionary(c => c.ClientID, c => c.CompanyName); } catch { _clientNames = new Dictionary<int, string>(); }
        }

        private void BindKpis()
        {
            decimal arr = _contracts.Where(c => c.ContractStatus == "Active").Sum(c => c.AnnualValue);
            decimal mrr = _contracts.Where(c => c.ContractStatus == "Active").Sum(c => c.MonthlyValue);
            List<Invoice> openInvoices = _invoices.Where(i => i.PaymentStatus != "Paid").ToList();
            decimal receivables = openInvoices.Sum(i => i.BalanceDue);
            int overdueInvoices = openInvoices.Count(i => i.PaymentStatus == "Overdue" || i.DueDate < DateTime.Today);
            int overdueJobs = _jobs.Count(j => j.IsOverdue || (j.ScheduledDate.Date < DateTime.Today && !IsComplete(j.Status)));
            int urgentJobs = _jobs.Count(j => (j.Priority ?? "").IndexOf("High", StringComparison.OrdinalIgnoreCase) >= 0 && !IsComplete(j.Status));
            decimal jobRevenue = _jobs.Sum(j => Math.Max(j.Revenue, Math.Max(j.ActualRevenue, j.QuotedRevenue)));
            decimal jobCost = _jobs.Sum(j => j.EstimatedCost);
            decimal margin = jobRevenue <= 0 ? 0 : Math.Round((jobRevenue - jobCost) / jobRevenue * 100m, 1);
            int openJobs = _jobs.Count(j => !IsComplete(j.Status));
            int toOrder = _stock.Count(s => s.IsLowStock);

            _lblRevenue.Text = "Rs " + arr.ToString("N0");
            _lblRevenueSub.Text = "MRR Rs " + mrr.ToString("N0");
            _lblReceivable.Text = "Rs " + receivables.ToString("N0");
            _lblReceivableSub.Text = overdueInvoices + " overdue invoices";
            _lblSla.Text = (overdueJobs + urgentJobs).ToString();
            _lblSlaSub.Text = overdueJobs + " overdue, " + urgentJobs + " high priority";
            _lblMargin.Text = margin.ToString("N1") + "%";
            _lblMarginSub.Text = "Revenue Rs " + jobRevenue.ToString("N0");
            _lblPayroll.Text = openJobs.ToString();
            _lblPayrollSub.Text = _technicians.Count + " active technicians";
            _lblInventory.Text = toOrder.ToString();
            _lblInventorySub.Text = "materials to order";
        }

        private void BindCharts()
        {
            _revenueChart.Series[0].Points.Clear();
            DateTime firstMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-5);
            for (int i = 0; i < 6; i++)
            {
                DateTime month = firstMonth.AddMonths(i);
                decimal amount = _invoices
                    .Where(inv => inv.InvoiceDate.Year == month.Year && inv.InvoiceDate.Month == month.Month)
                    .Sum(inv => inv.TotalAmount);
                _revenueChart.Series[0].Points.AddXY(month.ToString("MMM"), amount);
            }

            int[] aging = new int[4];
            foreach (Invoice inv in _invoices.Where(i => i.PaymentStatus != "Paid" && i.BalanceDue > 0))
            {
                int days = Math.Max(0, (DateTime.Today - inv.DueDate.Date).Days);
                if (days <= 30) aging[0]++;
                else if (days <= 60) aging[1]++;
                else if (days <= 90) aging[2]++;
                else aging[3]++;
            }
            string[] buckets = { "0-30", "31-60", "61-90", "90+" };
            _agingChart.Series[0].Points.Clear();
            for (int i = 0; i < buckets.Length; i++)
                _agingChart.Series[0].Points.AddXY(buckets[i], aging[i]);

            _workloadChart.Series[0].Points.Clear();
            var loads = _technicians
                .Select(t => new
                {
                    Name = t.Name,
                    Count = _jobs.Count(j => j.AssignedEmployeeID.HasValue && j.AssignedEmployeeID.Value == t.EmployeeID && !IsComplete(j.Status))
                })
                .OrderByDescending(x => x.Count)
                .Take(8)
                .ToList();
            if (loads.Count == 0)
                _workloadChart.Series[0].Points.AddXY("No load", 0);
            foreach (var load in loads)
                _workloadChart.Series[0].Points.AddXY(ShortText(load.Name, 18), load.Count);
        }

        private void BindClientProfitability()
        {
            _clientGrid.Rows.Clear();
            var rows = _jobs
                .GroupBy(j => string.IsNullOrWhiteSpace(j.ClientName) ? "Client #" + j.ClientID : j.ClientName)
                .Select(g =>
                {
                    decimal revenue = g.Sum(j => Math.Max(j.Revenue, Math.Max(j.ActualRevenue, j.QuotedRevenue)));
                    decimal cost = g.Sum(j => j.EstimatedCost);
                    decimal margin = revenue <= 0 ? 0 : Math.Round((revenue - cost) / revenue * 100m, 1);
                    return new { Client = g.Key, Revenue = revenue, Cost = cost, Margin = margin };
                })
                .OrderByDescending(x => x.Revenue)
                .Take(7);
            foreach (var row in rows)
                _clientGrid.Rows.Add(ShortText(row.Client, 28), IndiaFormatHelper.FormatCurrency(row.Revenue), IndiaFormatHelper.FormatCurrency(row.Cost), row.Margin.ToString("N1") + "%");
        }

        private void BindActionQueue()
        {
            _actionQueue.Controls.Clear();
            int overdueInvoices = _invoices.Count(i => i.PaymentStatus != "Paid" && (i.PaymentStatus == "Overdue" || i.DueDate < DateTime.Today));
            int renewals = _contracts.Count(c => (c.EndDate - DateTime.Today).Days <= 30 && (c.EndDate - DateTime.Today).Days >= -365);
            int unassigned = _jobs.Count(j => !j.AssignedEmployeeID.HasValue && !IsComplete(j.Status));
            int toOrder = _stock.Count(s => s.IsLowStock);
            int overduePo = _purchases.Count(p => p.IsOverdue);

            AddAction("Chase overdue invoices", overdueInvoices + " invoices need collection follow-up", Red, 1);
            AddAction("Renew expiring contracts", renewals + " contracts need owner review", Amber, 2);
            AddAction("Assign unassigned jobs", unassigned + " jobs need technician assignment", Blue, 3);
            AddAction("Plan material orders", toOrder + " materials need supplier ordering", Amber, 5);
            AddAction("Clear vendor payables", overduePo + " purchase payments overdue", Teal, 7);
        }

        private void SelectReport(int index)
        {
            _currentReportIndex = Math.Max(0, Math.Min(ReportNames.Length - 1, index));
            foreach (Control tile in _reportLibrary.Controls)
            {
                int tileIndex = Convert.ToInt32(tile.Tag);
                tile.BackColor = tileIndex == _currentReportIndex ? Color.FromArgb(239, 246, 255) : CardBg;
            }
            BindDetailGrid();
        }

        private void BindDetailGrid()
        {
            _detailGrid.Columns.Clear();
            _detailGrid.Rows.Clear();
            switch (_currentReportIndex)
            {
                case 0: BindRevenueDetail(); break;
                case 1: BindCollectionDetail(); break;
                case 2: BindContractDetail(); break;
                case 3: BindJobDetail(); break;
                case 4: BindTechnicianDetail(); break;
                case 5: BindInventoryDetail(); break;
                case 6: BindPurchaseDetail(); break;
                case 7: BindVendorAdvanceDetail(); break;
                default: BindClientSiteDetail(); break;
            }
        }

        private void BindRevenueDetail()
        {
            AddColumns("Client", "Monthly", "Annual", "Type", "Status");
            foreach (AMCContract c in _contracts.Where(c => c.ContractStatus == "Active").OrderByDescending(c => c.AnnualValue))
            {
                string client = ResolveClientName(c.ClientID);
                _detailGrid.Rows.Add(client, c.MonthlyValue.ToString("N0"), c.AnnualValue.ToString("N0"), c.ContractType, c.ContractStatus);
            }
        }

        private void BindCollectionDetail()
        {
            AddColumns("Invoice", "Client", "Due", "Balance", "Status");
            foreach (Invoice inv in _invoices.Where(i => i.PaymentStatus != "Paid").OrderByDescending(i => i.BalanceDue))
                _detailGrid.Rows.Add(inv.InvoiceNumber, inv.ClientName ?? "", inv.DueDate.ToString("dd-MMM-yy"), inv.BalanceDue.ToString("N0"), inv.PaymentStatus);
        }

        private void BindContractDetail()
        {
            AddColumns("Client", "Type", "Expires", "Days", "Monthly", "Action");
            foreach (AMCContract c in _contracts.OrderBy(c => c.EndDate).Take(100))
            {
                int days = (c.EndDate - DateTime.Today).Days;
                string client = ResolveClientName(c.ClientID);
                _detailGrid.Rows.Add(client, c.ContractType, c.EndDate.ToString("dd-MMM-yyyy"), days.ToString(), c.MonthlyValue.ToString("N0"), days <= 30 ? "Renew" : "Monitor");
            }
        }

        private string ResolveClientName(int clientId)
        {
            string name;
            return _clientNames != null && _clientNames.TryGetValue(clientId, out name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : "Client #" + clientId;
        }

        private void BindJobDetail()
        {
            AddColumns("Job", "Client", "Type", "Priority", "Technician", "Status");
            foreach (Job j in _jobs.OrderByDescending(j => j.ScheduledDate).Take(200))
                _detailGrid.Rows.Add(j.JobNumber, j.ClientName ?? "", j.JobType, j.Priority, j.AssignedEmployeeName ?? "", j.Status);
        }

        private void BindTechnicianDetail()
        {
            AddColumns("Technician", "Open Jobs", "Completed", "Revenue", "Avg / Job");
            foreach (Employee tech in _technicians)
            {
                var techJobs = _jobs.Where(j => j.AssignedEmployeeID.HasValue && j.AssignedEmployeeID.Value == tech.EmployeeID).ToList();
                int open = techJobs.Count(j => !IsComplete(j.Status));
                int completed = techJobs.Count(j => IsComplete(j.Status));
                decimal revenue = techJobs.Sum(j => Math.Max(j.Revenue, Math.Max(j.ActualRevenue, j.QuotedRevenue)));
                decimal avg = techJobs.Count == 0 ? 0 : revenue / techJobs.Count;
                _detailGrid.Rows.Add(tech.Name, open.ToString(), completed.ToString(), revenue.ToString("N0"), avg.ToString("N0"));
            }
        }

        private void BindInventoryDetail()
        {
            AddColumns("Item", "Category", "Current Qty", "Reserved", "Plan Qty", "Purchase Value");
            foreach (StockItem item in _stock.OrderByDescending(i => i.IsLowStock).ThenBy(i => i.ItemName).Take(200))
                _detailGrid.Rows.Add(item.ItemName, item.Category, item.CurrentStock.ToString("N1"), item.ReservedStock.ToString("N1"), item.ReorderLevel.ToString("N1"), item.StockValue.ToString("N0"));
        }

        private void BindPurchaseDetail()
        {
            AddColumns("PO", "Supplier", "Date", "Amount", "Balance", "Status");
            foreach (PurchaseOrder po in _purchases.OrderByDescending(p => p.PODate).Take(200))
                _detailGrid.Rows.Add(po.PONumber, po.VendorName ?? "", po.PODate.ToString("dd-MMM-yy"), po.TotalAmount.ToString("N0"), po.BalanceDue.ToString("N0"), po.Status);
        }

        private void BindVendorAdvanceDetail()
        {
            AddColumns("Supplier", "Type", "Date", "Amount", "Applied", "Balance", "Reference");
            foreach (VendorAdvancePayment advance in _vendorAdvances.Take(300))
                _detailGrid.Rows.Add(
                    advance.VendorName ?? ("Supplier #" + advance.VendorId),
                    advance.TransactionType,
                    advance.TransactionDate.ToString("dd-MMM-yy"),
                    advance.Amount.ToString("N0"),
                    advance.AppliedAmount.ToString("N0"),
                    advance.Balance.ToString("N0"),
                    string.IsNullOrWhiteSpace(advance.ReferenceNumber) ? advance.PONumber : advance.ReferenceNumber);
        }

        private void BindClientSiteDetail()
        {
            AddColumns("Client", "Jobs", "Revenue", "Open Jobs", "Last Update");
            foreach (var row in _jobs.GroupBy(j => string.IsNullOrWhiteSpace(j.ClientName) ? "Client #" + j.ClientID : j.ClientName).OrderByDescending(g => g.Count()).Take(200))
            {
                decimal revenue = row.Sum(j => Math.Max(j.Revenue, Math.Max(j.ActualRevenue, j.QuotedRevenue)));
                int open = row.Count(j => !IsComplete(j.Status));
                DateTime last = row.Max(j => j.ScheduledDate);
                _detailGrid.Rows.Add(row.Key, row.Count().ToString(), revenue.ToString("N0"), open.ToString(), last.ToString("dd-MMM-yy"));
            }
        }

        private void ExportCurrentReport()
        {
            using (var dlg = new SaveFileDialog { FileName = ReportNames[_currentReportIndex].Replace(" ", "") + "_" + DateTime.Today.ToString("yyyyMMdd") + ".csv", Filter = "CSV|*.csv" })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                StringBuilder sb = new StringBuilder();
                List<string> headers = new List<string>();
                foreach (DataGridViewColumn col in _detailGrid.Columns)
                    headers.Add(col.HeaderText);
                sb.AppendLine(string.Join(",", headers));
                foreach (DataGridViewRow row in _detailGrid.Rows)
                {
                    List<string> values = new List<string>();
                    foreach (DataGridViewCell cell in row.Cells)
                        values.Add("\"" + (cell.Value == null ? "" : cell.Value.ToString()).Replace("\"", "\"\"") + "\"");
                    sb.AppendLine(string.Join(",", values));
                }
                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                _lblStatus.Text = "Exported: " + Path.GetFileName(dlg.FileName);
                _lblStatus.ForeColor = Green;
            }
        }

        private void ExportMonthlyProfitLoss()
        {
            using (var dlg = new SaveFileDialog { FileName = "Monthly_PL_" + DateTime.Today.ToString("yyyyMMdd") + ".xlsx", Filter = "Excel workbook (*.xlsx)|*.xlsx" })
            {
                if (dlg.ShowDialog() != DialogResult.OK)
                    return;

                try
                {
                    using (ExcelPackage package = new ExcelPackage())
                    {
                        ExcelWorksheet sheet = package.Workbook.Worksheets.Add("Monthly P&L");
                        WriteProfitLossSheet(sheet);
                        package.SaveAs(new FileInfo(dlg.FileName));
                    }

                    _lblStatus.Text = "P&L exported: " + Path.GetFileName(dlg.FileName);
                    _lblStatus.ForeColor = Green;
                }
                catch (Exception ex)
                {
                    AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Reports"), "Exporting monthly profit and loss", ex);
                    _lblStatus.Text = "P&L export failed.";
                    _lblStatus.ForeColor = Red;
                }
            }
        }

        private void WriteProfitLossSheet(ExcelWorksheet sheet)
        {
            DateTime firstMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-11);
            string[] headers = { "Month", "Revenue", "Purchases", "Salaries", "Total Expenses", "Net Profit", "Margin %" };
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[1, i + 1].Value = headers[i];
                sheet.Cells[1, i + 1].Style.Font.Bold = true;
                sheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(239, 246, 255));
            }

            for (int i = 0; i < 12; i++)
            {
                DateTime month = firstMonth.AddMonths(i);
                decimal revenue = _invoices.Where(inv => inv.InvoiceDate.Year == month.Year && inv.InvoiceDate.Month == month.Month).Sum(inv => inv.TotalAmount);
                decimal purchases = _purchases.Where(po => po.PODate.Year == month.Year && po.PODate.Month == month.Month).Sum(po => po.TotalAmount);
                PayrollRun run = _payrollSvc.GetPayrollRun(month.Month, month.Year);
                decimal salaries = run == null ? 0m : run.TotalNetPay + run.TotalEPFEmployer + run.TotalESIEmployer;
                decimal expenses = purchases + salaries;
                decimal profit = revenue - expenses;
                decimal margin = revenue <= 0 ? 0 : Math.Round(profit / revenue * 100m, 2);
                int row = i + 2;

                sheet.Cells[row, 1].Value = month.ToString("MMM yyyy", CultureInfo.InvariantCulture);
                sheet.Cells[row, 2].Value = revenue;
                sheet.Cells[row, 3].Value = purchases;
                sheet.Cells[row, 4].Value = salaries;
                sheet.Cells[row, 5].Value = expenses;
                sheet.Cells[row, 6].Value = profit;
                sheet.Cells[row, 7].Value = margin;
            }

            int totalRow = 14;
            sheet.Cells[totalRow, 1].Value = "Total";
            sheet.Cells[totalRow, 1].Style.Font.Bold = true;
            for (int col = 2; col <= 6; col++)
            {
                sheet.Cells[totalRow, col].Formula = "SUM(" + sheet.Cells[2, col].Address + ":" + sheet.Cells[13, col].Address + ")";
                sheet.Cells[totalRow, col].Style.Font.Bold = true;
            }
            sheet.Cells[totalRow, 7].Formula = "IF(" + sheet.Cells[totalRow, 2].Address + "=0,0," + sheet.Cells[totalRow, 6].Address + "/" + sheet.Cells[totalRow, 2].Address + "*100)";
            sheet.Cells[totalRow, 7].Style.Font.Bold = true;
            sheet.Cells[2, 2, totalRow, 6].Style.Numberformat.Format = "#,##0.00";
            sheet.Cells[2, 7, totalRow, 7].Style.Numberformat.Format = "0.00";
            sheet.Cells.AutoFitColumns();
        }

        private Label AddKpi(TableLayoutPanel host, int column, string title, Color accent, out Label subLabel)
        {
            Panel card = new Panel { Dock = DockStyle.Fill, BackColor = CardBg, Margin = new Padding(column == 0 ? 0 : 5, 0, column == 5 ? 0 : 5, 0), Padding = new Padding(14, 10, 10, 8) };
            card.Paint += (s, e) => DrawBorder(e.Graphics, card);
            Panel icon = new Panel { Dock = DockStyle.Left, Width = 38, BackColor = CardBg, Padding = new Padding(0, 3, 8, 0) };
            Label badge = ModernIconSystem.Badge(ModernIconSystem.KindForTitle(title), 30, DS.Lighten(accent, 0.82f), accent, 10);
            badge.Dock = DockStyle.Top;
            icon.Controls.Add(badge);
            Label titleLabel = new Label { Text = title.ToUpperInvariant(), Dock = DockStyle.Top, Height = 18, Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = TextMid };
            Label valueLabel = new Label { Text = "-", Dock = DockStyle.Top, Height = 31, Font = new Font("Segoe UI", 16f, FontStyle.Bold), ForeColor = accent };
            subLabel = new Label { Text = "Loading...", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.2f), ForeColor = TextMid };
            card.Controls.Add(subLabel);
            card.Controls.Add(valueLabel);
            card.Controls.Add(titleLabel);
            card.Controls.Add(icon);
            host.Controls.Add(card, column, 0);
            return valueLabel;
        }

        private ResizableCard AddDashboardCard(string key, string title, Control content, int width, int height, string preset)
        {
            ResizableCard card = new ResizableCard
            {
                PageKey = PageKey,
                CardKey = key,
                CardTitle = title,
                Size = new Size(width, height),
                MinimumSize = new Size(300, 170),
                SizePreset = preset,
                ResizeAxes = CardResizeAxes.Both,
                AllowResize = true,
                Margin = new Padding(0, 0, 12, 12)
            };
            content.Dock = DockStyle.Fill;
            card.ContentPanel.Tag = MergeTag(card.ContentPanel.Tag, "NO_CARD_SURFACE");
            content.Tag = MergeTag(content.Tag, "NO_CARD_SURFACE");
            card.ContentPanel.Controls.Add(content);
            card.CardDragRequested += DashboardCard_DragRequested;
            card.CardResizeComplete += (s, e) => LayoutDashboardCards();
            _dashboardCards[key] = card;
            _dashboardFlow.Controls.Add(card);
            CardLayoutService.RegisterDefaultSize(PageKey, key, card.Size, preset);
            return card;
        }

        /// <summary>Adds a metadata token to an existing control tag.</summary>
        private static string MergeTag(object existing, string token)
        {
            string current = existing == null ? string.Empty : existing.ToString();
            if (current.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                return current;

            return string.IsNullOrWhiteSpace(current) ? token : current + " " + token;
        }

        private void DashboardCard_DragRequested(object sender, MouseEventArgs e)
        {
            ResizableCard card = sender as ResizableCard;
            if (card == null)
                return;

            _dragCard = card;
            card.DoDragDrop(card.CardKey ?? string.Empty, DragDropEffects.Move);
        }

        private void DashboardFlow_DragEnter(object sender, DragEventArgs e)
        {
            if (_dragCard != null)
                e.Effect = DragDropEffects.Move;
        }

        private void DashboardFlow_DragDrop(object sender, DragEventArgs e)
        {
            if (_dragCard == null || _dashboardFlow == null)
                return;

            Point clientPoint = _dashboardFlow.PointToClient(new Point(e.X, e.Y));
            int newIndex = _dashboardFlow.Controls.Count - 1;
            for (int i = 0; i < _dashboardFlow.Controls.Count; i++)
            {
                Control candidate = _dashboardFlow.Controls[i];
                if (candidate == _dragCard)
                    continue;

                Rectangle bounds = candidate.Bounds;
                if (clientPoint.Y < bounds.Top + bounds.Height / 2 ||
                    (clientPoint.Y <= bounds.Bottom && clientPoint.X < bounds.Left + bounds.Width / 2))
                {
                    newIndex = i;
                    break;
                }
            }

            _dashboardFlow.Controls.SetChildIndex(_dragCard, Math.Max(0, newIndex));
            _dashboardFlow.PerformLayout();
            SaveCardOrder();
            _dragCard = null;
        }

        private void ApplySavedCardOrder()
        {
            try
            {
                if (!File.Exists(CardOrderPath))
                    return;

                string[] keys = File.ReadAllText(CardOrderPath)
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(k => k.Trim())
                    .ToArray();
                for (int i = keys.Length - 1; i >= 0; i--)
                {
                    ResizableCard card;
                    if (_dashboardCards.TryGetValue(keys[i], out card))
                        _dashboardFlow.Controls.SetChildIndex(card, 0);
                }
            }
            catch
            {
            }
        }

        private void SaveCardOrder()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CardOrderPath));
                string order = string.Join(",", _dashboardFlow.Controls.OfType<ResizableCard>().Select(c => c.CardKey));
                File.WriteAllText(CardOrderPath, order);
            }
            catch
            {
            }
        }

        private void LayoutDashboardCards()
        {
            if (_dashboardFlow == null || _dashboardFlow.ClientSize.Width <= 0)
                return;

            int gap = 12;
            int availableWidth = Math.Max(320, _dashboardFlow.ClientSize.Width);
            int columns = availableWidth >= 1160 ? 3 : (availableWidth >= 780 ? 2 : 1);
            int cardWidth = columns == 1 ? availableWidth : (availableWidth - (gap * (columns - 1))) / columns;
            int[] columnHeights = new int[columns];
            ResizableCard[] cards = _dashboardFlow.Controls
                .OfType<ResizableCard>()
                .ToArray();

            _dashboardFlow.SuspendLayout();
            foreach (ResizableCard card in cards)
            {
                bool actionQueue = string.Equals(card.CardKey, "action_queue", StringComparison.OrdinalIgnoreCase);
                card.Width = Math.Max(300, cardWidth);
                card.Height = actionQueue ? 452 : 220;
                card.Margin = Padding.Empty;

                int column = 0;
                for (int i = 1; i < columns; i++)
                {
                    if (columnHeights[i] < columnHeights[column])
                        column = i;
                }

                card.Location = new Point(column * (card.Width + gap), columnHeights[column]);
                columnHeights[column] += card.Height + gap;
            }
            _dashboardFlow.ResumeLayout(false);

            int contentHeight = columnHeights.Length == 0 ? 0 : columnHeights.Max();
            if (contentHeight > 0)
                contentHeight -= gap;
            _dashboardFlow.Height = contentHeight;

            Panel wrapper = _dashboardFlow.Parent as Panel;
            if (wrapper != null)
                wrapper.Height = Math.Max(220, contentHeight + wrapper.Padding.Vertical + 12);
        }

        private Panel MakeCard(string title, Control content)
        {
            Panel card = MakePlainCard(title);
            content.Dock = DockStyle.Fill;
            card.Controls.Add(content);
            return card;
        }

        private Panel MakePlainCard(string title)
        {
            Panel card = new Panel { Dock = DockStyle.Fill, BackColor = CardBg, Margin = new Padding(0, 0, 12, 12), Padding = new Padding(14, 44, 14, 14) };
            card.Paint += (s, e) => DrawBorder(e.Graphics, card);
            Label titleIcon = ModernIconSystem.Badge(ModernIconSystem.KindForTitle(title), 24, DS.Indigo50, Blue, 8);
            titleIcon.Location = new Point(12, 9);
            Label titleLabel = new Label { Text = title, Location = new Point(44, 10), AutoSize = true, Font = new Font("Segoe UI", 11.5f, FontStyle.Bold), ForeColor = TextDark, BackColor = CardBg };
            card.Controls.Add(titleLabel);
            card.Controls.Add(titleIcon);
            titleLabel.BringToFront();
            return card;
        }

        private Button MakeReportTile(int index, string title)
        {
            Button tile = new Button
            {
                Tag = index,
                Text = title,
                Image = ModernIconSystem.IconBitmap(ModernIconSystem.KindForTitle(title), 18, Blue),
                ImageAlign = ContentAlignment.MiddleLeft,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                Width = 184,
                Height = 62,
                Margin = new Padding(0, 0, 12, 0),
                BackColor = CardBg,
                ForeColor = TextDark,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(10, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            tile.FlatAppearance.BorderColor = DS.InputBorder;
            tile.FlatAppearance.BorderSize = 1;
            tile.AccessibleName = "Open " + title + " report";
            UIHelper.ApplyButtonStyle(tile, ButtonRole.Secondary);
            tile.ImageAlign = ContentAlignment.MiddleLeft;
            tile.TextImageRelation = TextImageRelation.ImageBeforeText;
            tile.TextAlign = ContentAlignment.MiddleCenter;
            tile.Click += (s, e) => SelectReport(Convert.ToInt32(((Control)s).Tag));
            return tile;
        }

        private void AddAction(string title, string body, Color accent, int reportIndex)
        {
            Panel item = new Panel { Width = 300, Height = 62, BackColor = Color.FromArgb(249, 250, 251), Margin = new Padding(0, 0, 0, 8), Padding = new Padding(10, 8, 8, 6), Cursor = Cursors.Hand };
            item.Paint += (s, e) => DrawBorder(e.Graphics, item);
            Label icon = ModernIconSystem.Badge(ModernIconSystem.KindForTitle(title), 26, DS.Lighten(accent, 0.82f), accent, 8);
            icon.Dock = DockStyle.Left;
            Label titleLabel = new Label { Text = title, Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = TextDark, Cursor = Cursors.Hand };
            Label bodyLabel = new Label { Text = body, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.4f), ForeColor = TextMid, Cursor = Cursors.Hand };
            EventHandler openReport = (s, e) =>
            {
                SelectReport(reportIndex);
                _lblStatus.Text = "Opened " + ReportNames[reportIndex] + " report from action queue.";
                _lblStatus.ForeColor = Blue;
            };
            item.Click += openReport;
            titleLabel.Click += openReport;
            bodyLabel.Click += openReport;
            icon.Click += openReport;
            item.Controls.Add(bodyLabel);
            item.Controls.Add(titleLabel);
            item.Controls.Add(icon);
            _actionQueue.Controls.Add(item);
        }

        private Chart MakeChart(string name, SeriesChartType type, Color color)
        {
            Chart chart = new Chart { Dock = DockStyle.Fill, BackColor = CardBg };
            ChartArea area = new ChartArea(name);
            area.BackColor = CardBg;
            area.AxisX.MajorGrid.Enabled = false;
            area.AxisX.LabelStyle.Font = new Font("Segoe UI", 8f);
            area.AxisX.LabelStyle.ForeColor = TextMid;
            area.AxisY.MajorGrid.LineColor = Color.FromArgb(235, 239, 245);
            area.AxisY.LabelStyle.Font = new Font("Segoe UI", 8f);
            area.AxisY.LabelStyle.ForeColor = TextMid;
            area.AxisY.LineColor = Border;
            area.AxisX.LineColor = Border;
            chart.ChartAreas.Add(area);
            Series series = new Series("Value") { ChartType = type, Color = color, BorderWidth = 3, ChartArea = name };
            series.IsValueShownAsLabel = type == SeriesChartType.Column || type == SeriesChartType.Bar;
            series.Font = new Font("Segoe UI", 8f);
            chart.Series.Add(series);
            return chart;
        }

        private DataGridView MakeGrid()
        {
            DataGridView grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = CardBg,
                GridColor = Border,
                Font = new Font("Segoe UI", 8.8f),
                BorderStyle = BorderStyle.None,
                EnableHeadersVisualStyles = false
            };
            DS.StyleGrid(grid);
            return grid;
        }

        private static DataGridViewTextBoxColumn C(string header, int width)
        {
            return new DataGridViewTextBoxColumn { HeaderText = header, Width = width, MinimumWidth = Math.Max(70, width), SortMode = DataGridViewColumnSortMode.Automatic };
        }

        private void AddColumns(params string[] headers)
        {
            foreach (string header in headers)
                _detailGrid.Columns.Add(C(header, 120));
        }

        private Button MakeButton(string text, Color bg, int width)
        {
            Button button = new Button
            {
                Text = text,
                Width = Math.Max(100, width),
                Height = 34,
                BackColor = bg,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(6, 0, 0, 0)
            };
            button.FlatAppearance.BorderSize = 0;
            UIHelper.ApplyButtonStyle(button, UIHelper.ResolveButtonRole(button));
            return button;
        }

        private void DrawBorder(Graphics g, Control control)
        {
            using (Pen pen = new Pen(Border, 1))
                g.DrawRectangle(pen, 0, 0, control.Width - 1, control.Height - 1);
        }

        private static bool IsComplete(string status)
        {
            return string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase);
        }

        private static string ShortText(string text, int max)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";
            return text.Length <= max ? text : text.Substring(0, max - 3) + "...";
        }
    }
}


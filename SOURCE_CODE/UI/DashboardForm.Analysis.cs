using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public partial class DashboardForm
    {
        private readonly Dictionary<string, DashboardAnalysisDateRange> _analysisRanges = new Dictionary<string, DashboardAnalysisDateRange>();
        private readonly object _analysisLoadSync = new object();
        private int _analysisRequestVersion;

        private void OpenAnalysis(DashboardAnalysisModel model)
        {
            if (model == null)
                return;

            if (_dashboardHost != null && !_dashboardHost.IsDisposed)
                _dashboardHost.Visible = false;

            if (_analysisDrawer == null || _analysisDrawer.IsDisposed)
            {
                _analysisDrawer = new DashboardAnalysisDrawer();
                _analysisDrawer.BackRequested += (s, e) => CloseAnalysis();
                if (!Controls.Contains(_analysisDrawer))
                    Controls.Add(_analysisDrawer);
            }

            _analysisDrawer.ShowAnalysis(model);
            _analysisDrawer.Visible = true;
            _analysisDrawer.BringToFront();
        }

        private void OpenAnalysisAsync(
            string key,
            Func<DashboardAnalysisDateRange, DashboardAnalysisModel> builder,
            Func<DashboardAnalysisDateRange, DashboardAnalysisModel> loadingBuilder,
            bool reloadData = true)
        {
            DashboardAnalysisDateRange range = GetAnalysisRange(key);
            DashboardAnalysisModel loadingModel = loadingBuilder(range);
            loadingModel.SelectedRange = range;
            loadingModel.RangeChanged = newRange =>
            {
                _analysisRanges[key] = newRange;
                OpenAnalysisAsync(key, builder, loadingBuilder, true);
            };
            OpenAnalysis(loadingModel);

            int requestVersion = Interlocked.Increment(ref _analysisRequestVersion);
            Task.Run(() =>
            {
                try
                {
                    lock (_analysisLoadSync)
                    {
                        if (reloadData)
                            LoadData();

                        DashboardAnalysisModel model = builder(range) ?? loadingBuilder(range);
                        model.SelectedRange = range;
                        model.RangeChanged = loadingModel.RangeChanged;
                        return model;
                    }
                }
                catch (Exception ex)
                {
                    LogAnalysisError(key, ex);
                    DashboardAnalysisModel failed = CreateLoadingModel(
                        loadingModel.Title,
                        loadingModel.Subtitle,
                        loadingModel.BadgeText,
                        loadingModel.BadgeVariant,
                        loadingModel.Metrics.Select(metric => metric.Label).ToArray());
                    failed.SummaryText = "Data could not be loaded right now. Try refresh again.";
                    failed.AlgorithmTitle = "Data unavailable";
                    failed.AlgorithmSummary = "The issue was logged to the app logs folder.";
                    failed.SelectedRange = range;
                    failed.RangeChanged = loadingModel.RangeChanged;
                    return failed;
                }
            }).ContinueWith(task =>
            {
                if (IsDisposed || requestVersion != _analysisRequestVersion)
                    return;

                DashboardAnalysisModel model = task.Result;
                if (IsHandleCreated)
                    BeginInvoke((Action)(() => OpenAnalysis(model)));
            }, TaskScheduler.Default);
        }

        private void CloseAnalysis()
        {
            if (_analysisDrawer != null && !_analysisDrawer.IsDisposed)
                _analysisDrawer.Visible = false;
            if (_dashboardHost != null && !_dashboardHost.IsDisposed)
            {
                _dashboardHost.Visible = true;
                _dashboardHost.BringToFront();
            }
        }

        private void RefreshDashboard(Action reopen = null)
        {
            LoadData();
            BuildUI();
            reopen?.Invoke();
        }

        private void OpenQuotationAnalysis()
        {
            OpenAnalysisAsync(
                "QuotationAnalysis",
                range => BuildQuotationPortfolioAnalysis(range),
                range => CreateLoadingModel(
                    "Quotation intelligence",
                    "Quote pipeline, conversion posture, and pricing readiness",
                    "Loading quotes",
                    DashboardAnalysisBadgeVariant.Teal,
                    "Total quotes",
                    "Pipeline value",
                    "Submitted",
                    "Won"));
        }

        private void OpenInvoiceAnalysis()
        {
            OpenAnalysisAsync(
                "InvoiceAnalysis",
                range => BuildInvoicePortfolioAnalysis(false, range),
                range => CreateLoadingModel(
                    "Invoice portfolio",
                    "Open invoice position with customer payment signals",
                    "Loading invoices",
                    DashboardAnalysisBadgeVariant.Red,
                    "Outstanding",
                    "Total receivable",
                    "Overdue amount",
                    "Avg ticket"));
        }

        private void OpenJobsAnalysis()
        {
            OpenAnalysisAsync(
                "JobsAnalysis",
                range => BuildJobPerformanceAnalysis(range),
                range => CreateLoadingModel(
                    "Jobs performance board",
                    "Field execution, revenue delivery, and margin readout",
                    "Loading jobs",
                    DashboardAnalysisBadgeVariant.Amber,
                    "Revenue this month",
                    "Profit this month",
                    "Completed",
                    "In progress"));
        }

        private void OpenInventoryAnalysis()
        {
            OpenAnalysisAsync(
                "InventoryAnalysis",
                range => BuildInventoryPortfolioAnalysis(false, range),
                range => CreateLoadingModel(
                    "Inventory position",
                    "Live inventory coverage across the HVAC ERP",
                    "Loading inventory",
                    DashboardAnalysisBadgeVariant.Teal,
                    "Active items",
                    "Low stock",
                    "Stock value",
                    "Reorder urgency"));
        }

        private void OpenPurchaseAnalysis()
        {
            OpenAnalysisAsync(
                "PurchaseAnalysis",
                range => BuildPurchasePortfolioAnalysis(false, range),
                range => CreateLoadingModel(
                    "Purchase order portfolio",
                    "Live purchase commitments across vendors and jobs",
                    "Loading POs",
                    DashboardAnalysisBadgeVariant.Amber,
                    "Open POs",
                    "Outstanding",
                    "Overdue",
                    "Price variance flags"));
        }

        private DashboardAnalysisModel BuildPurchasePortfolioAnalysis(bool overdueOnly)
        {
            return BuildPurchasePortfolioAnalysis(overdueOnly, DashboardAnalysisDateRange.ThisMonth);
        }

        private DashboardAnalysisModel BuildPurchasePortfolioAnalysis(bool overdueOnly, DashboardAnalysisDateRange range)
        {
            List<PurchaseOrder> allOpenOrders = (_purchaseOrders ?? new List<PurchaseOrder>())
                .Where(po => po.BalanceDue > 0.01m && IsInDateRange(po.PODate, range))
                .ToList();
            List<PurchaseOrder> records = overdueOnly ? allOpenOrders.Where(po => po.IsOverdue).ToList() : allOpenOrders;
            decimal totalOutstanding = records.Sum(po => po.BalanceDue);
            decimal overdueAmount = records.Where(po => po.IsOverdue).Sum(po => po.BalanceDue);
            int varianceFlags = records.Count(po => po.PriceVarianceFlag);

            var vendorExposure = records
                .GroupBy(po => NormalizeVendorName(po.VendorName))
                .Select(group => new
                {
                    Name = group.Key,
                    DisplayName = group.FirstOrDefault(po => !string.IsNullOrWhiteSpace(po.VendorName))?.VendorName ?? group.Key,
                    Exposure = group.Sum(item => item.BalanceDue),
                    Count = group.Count(),
                    HasDuplicate = group.Select(item => Safe(item.VendorName)).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1
                })
                .OrderByDescending(group => group.Exposure)
                .ToList();

            var duplicateVendors = vendorExposure.Where(group => group.Count >= 2 && group.HasDuplicate).ToList();
            decimal topVendorExposure = vendorExposure.FirstOrDefault()?.Exposure ?? 0m;
            decimal concentrationRatio = totalOutstanding <= 0 ? 0 : topVendorExposure / totalOutstanding;
            decimal overdueRatio = totalOutstanding <= 0 ? 0 : overdueAmount / totalOutstanding;
            int score = ClampScore((overdueRatio * 40m) + (concentrationRatio * 40m) + Math.Min(varianceFlags * 5m, 20m));

            var ageing = new
            {
                Days0To30 = records.Where(po => po.AgeDays <= 30).Sum(po => po.BalanceDue),
                Days31To60 = records.Where(po => po.AgeDays > 30 && po.AgeDays <= 60).Sum(po => po.BalanceDue),
                Days61To90 = records.Where(po => po.AgeDays > 60 && po.AgeDays <= 90).Sum(po => po.BalanceDue),
                Days90Plus = records.Where(po => po.AgeDays > 90).Sum(po => po.BalanceDue)
            };

            DashboardAnalysisModel model = new DashboardAnalysisModel
            {
                PageKey = "PurchaseAnalysis",
                EntityType = DashboardAnalysisEntityType.Purchase,
                Title = "Purchase order portfolio",
                Subtitle = "Live purchase commitments across vendors and jobs",
                BadgeText = records.Count + " open",
                BadgeVariant = DashboardAnalysisBadgeVariant.Amber,
                SelectedRange = range,
                SummaryText = "Open supplier exposure " + FormatCurrency(totalOutstanding) + " across " + vendorExposure.Count + " vendors."
            };

            if (duplicateVendors.Any())
            {
                model.Alert = new DashboardAnalysisAlert
                {
                    Variant = DashboardAnalysisAlertVariant.Red,
                    Title = "Vendor data quality issue detected",
                    Body = string.Join(" | ", duplicateVendors.Take(3).Select(group => group.DisplayName + " (" + group.Count + ") " + FormatCurrency(group.Exposure))) + " — Merge these in Vendors to correct exposure figures"
                };
            }

            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Open POs", Value = records.Count(po => string.Equals(Safe(po.Status), "Pending", StringComparison.OrdinalIgnoreCase) || po.BalanceDue > 0.01m).ToString(), Subtitle = "Pending supplier commitments", Accent = AccentOrange });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Outstanding", Value = FormatCurrency(totalOutstanding), Subtitle = "Live unpaid supplier amount", Accent = AccentRed });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Overdue", Value = overdueAmount <= 0 ? "—" : FormatCurrency(overdueAmount), Subtitle = overdueAmount <= 0 ? "No overdue payments" : "Past agreed payment dates", Accent = overdueAmount <= 0 ? Color.FromArgb(158, 158, 158) : AccentRed });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Price variance flags", Value = varianceFlags.ToString(), Subtitle = varianceFlags == 0 ? "No anomalies detected" : "Orders needing price review", Accent = varianceFlags == 0 ? AccentGreen : AccentBlue });

            model.AlgorithmTitle = "Payables risk score";
            model.AlgorithmSummary = "Combines overdue exposure, vendor concentration, and price variance flags.";
            model.AlgorithmScore = score;
            model.AlgorithmStatusText = ResolveRiskScoreStatus(score);
            model.AlgorithmStatusColor = ResolveRiskScoreColor(score);
            model.AlgorithmFactors = new List<DashboardAnalysisAlgorithmFactor>
            {
                MakeFactor("Vendor concentration", FormatPercentage(concentrationRatio * 100m), "Top vendor share of open supplier exposure.", 0, concentrationRatio > 0.50m ? AccentRed : AccentOrange),
                MakeFactor("Largest single exposure", FormatCurrency(topVendorExposure), "Highest outstanding amount against one vendor.", 0, AccentOrange),
                MakeFactor("Price variance flags", varianceFlags.ToString(), varianceFlags == 0 ? "Pricing is clean across open POs." : "Open POs with historical rate variance.", 0, varianceFlags == 0 ? AccentBlue : AccentRed)
            };

            model.Charts.Add(CreateChart(
                "Open exposure by vendor",
                "Top 8 vendor balances by outstanding amount",
                SeriesChartType.Bar,
                vendorExposure.Take(8).Select(group => new DashboardAnalysisChartPoint
                {
                    Label = group.DisplayName,
                    Value = group.Exposure,
                    Color = group.HasDuplicate ? AccentRed : AccentOrange
                }),
                DashboardAnalysisValueFormat.Currency));

            model.ComparisonTitle = "Payables ageing";
            model.ComparisonFacts.Add(new DashboardAnalysisFact { Label = "0–30 days", Value = FormatCurrency(ageing.Days0To30) });
            model.ComparisonFacts.Add(new DashboardAnalysisFact { Label = "31–60 days", Value = FormatCurrency(ageing.Days31To60) });
            model.ComparisonFacts.Add(new DashboardAnalysisFact { Label = "61–90 days", Value = FormatCurrency(ageing.Days61To90) });
            model.ComparisonFacts.Add(new DashboardAnalysisFact { Label = "90+ days", Value = FormatCurrency(ageing.Days90Plus) });

            if (duplicateVendors.Any())
                model.InsightCards.Add(new DashboardAnalysisInsight { Title = "Next actions", Detail = "Merge " + duplicateVendors.Count + " duplicate vendor entries.", Accent = AccentRed });
            if (ageing.Days90Plus > 0)
                model.InsightCards.Add(new DashboardAnalysisInsight { Title = "Next actions", Detail = "Settle overdue POs in the 90+ day bucket.", Accent = AccentOrange });
            model.InsightCards.Add(new DashboardAnalysisInsight { Title = "Next actions", Detail = varianceFlags == 0 ? "Pricing is clean — no variance flags." : "Review " + varianceFlags + " price variance flags.", Accent = varianceFlags == 0 ? AccentBlue : AccentRed });
            while (model.InsightCards.Count < 3)
                model.InsightCards.Add(new DashboardAnalysisInsight { Title = "Next actions", Detail = "Refresh the payable queue before releasing vendor payments.", Accent = AccentTeal });

            model.Tables.Add(CreateTable(
                "Open payables register",
                "Priority supplier commitments from the live dashboard feed",
                new[] { "PO", "Vendor", "Pay by", "Status", "Balance" },
                records.OrderByDescending(po => po.BalanceDue).Take(8).Select(po => new[]
                {
                    Safe(po.PONumber),
                    Safe(po.VendorName),
                    DateText(po.PayByDate),
                    ResolvePurchaseStatus(po),
                    FormatCurrency(po.BalanceDue)
                })));

            model.Actions.Add(MakeExportAction(model, "Export summary", DashboardAnalysisActionStyle.Amber));
            model.Actions.Add(new DashboardAnalysisAction { Label = "Refresh data", Style = DashboardAnalysisActionStyle.Teal, Handler = () => OpenPurchaseAnalysis() });
            model.Actions.Add(new DashboardAnalysisAction { Label = "View all POs", Style = DashboardAnalysisActionStyle.Secondary, Handler = () => OnNavigate?.Invoke(10) });
            if (duplicateVendors.Any())
            {
                model.Actions.Add(new DashboardAnalysisAction
                {
                    Label = "Merge vendor duplicates",
                    Style = DashboardAnalysisActionStyle.Secondary,
                    Handler = () =>
                    {
                        CopyText("Duplicate vendor groups detected: " + string.Join(", ", duplicateVendors.Select(group => group.DisplayName + " (" + group.Count + ")")));
                        OnNavigate?.Invoke(9);
                    }
                });
            }

            return model;
        }

        private DashboardAnalysisModel BuildPurchaseOrderAnalysis(PurchaseOrder po, string context)
        {
            PurchaseOrder full = po == null ? null : (_purchaseSvc.GetById(po.POID) ?? po);
            if (full == null)
                return null;

            Vendor vendor = _vendors.FirstOrDefault(v => v.VendorID == full.VendorID);
            List<PurchaseOrder> vendorOrders = (_purchaseOrders ?? new List<PurchaseOrder>())
                .Where(item => item.VendorID == full.VendorID)
                .OrderByDescending(item => item.PODate)
                .ToList();

            DashboardAnalysisModel model = new DashboardAnalysisModel
            {
                EntityType = DashboardAnalysisEntityType.Purchase,
                Title = Safe(full.PONumber, "Purchase Order"),
                Subtitle = Safe(full.VendorName, "Unknown vendor") + (string.IsNullOrWhiteSpace(full.LinkedToLabel) ? string.Empty : " | " + full.LinkedToLabel),
                StatusText = ResolvePurchaseStatus(full),
                StatusColor = ResolvePurchaseColor(full),
                AccentColor = ResolvePurchaseColor(full),
                SummaryText = string.IsNullOrWhiteSpace(context)
                    ? "Balance due " + FormatCurrency(full.BalanceDue) + " with pay-by date " + DateText(full.PayByDate) + "."
                    : context
            };

            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Order Value", Value = FormatCurrency(full.TotalAmount), Subtitle = "gross PO amount", Accent = AccentOrange });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Paid", Value = FormatCurrency(full.PaidAmount), Subtitle = "supplier payment booked", Accent = AccentGreen });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Balance", Value = FormatCurrency(full.BalanceDue), Subtitle = full.IsOverdue ? "payment overdue" : "still outstanding", Accent = ResolvePurchaseColor(full) });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Age", Value = full.AgeDays + " days", Subtitle = "since purchase date", Accent = AccentBlue });

            model.Facts.Add(new DashboardAnalysisFact { Label = "Vendor GSTIN", Value = Safe(full.VendorGSTIN ?? vendor?.GSTNumber) });
            model.Facts.Add(new DashboardAnalysisFact { Label = "Vendor invoice #", Value = Safe(full.VendorInvoiceNumber) });
            model.Facts.Add(new DashboardAnalysisFact { Label = "Purchase date", Value = DateText(full.PODate) });
            model.Facts.Add(new DashboardAnalysisFact { Label = "Pay by", Value = DateText(full.PayByDate) });
            model.Facts.Add(new DashboardAnalysisFact { Label = "Linked to", Value = Safe(full.LinkedToLabel, full.LinkedToType) });
            model.Facts.Add(new DashboardAnalysisFact { Label = "Technician", Value = Safe(full.AssignedTechnicianName) });
            model.Facts.Add(new DashboardAnalysisFact { Label = "Delivery", Value = string.Equals(full.DeliveryMode, "SiteDelivery", StringComparison.OrdinalIgnoreCase) ? Safe(full.DeliveryAddress, "Site delivery") : "Tech pickup" });

            if (full.IsOverdue)
                model.Insights.Add("This purchase order is overdue against vendor terms, so the supplier is likely to follow up for payment.");
            else if (full.BalanceDue > 0.01m)
                model.Insights.Add("The order is still unpaid but within terms, which gives some time before vendor escalation.");
            else
                model.Insights.Add("Payment has been fully settled, so this PO is no longer creating supplier liability.");
            if (full.PriceVarianceFlag)
                model.Insights.Add("One or more line items are above the recorded historical purchase rate and should be checked before approval.");
            if (full.PendingChargeCreated)
                model.Insights.Add("Client-side billing has already been queued from this PO, which reduces missed recovery risk.");

            ApplyAlgorithm(
                model,
                "Purchase Order Decision Algorithm",
                "The score weighs overdue state, open balance, line price variance, and billing recovery to show how much attention this PO needs.",
                MakeFactor("Payment urgency", ResolvePurchaseStatus(full), "Overdue and unpaid orders are ranked as higher priority.", full.IsOverdue ? 40 : full.BalanceDue > 0.01m ? 20 : 0, ResolvePurchaseColor(full)),
                MakeFactor("Open balance", FormatCurrency(full.BalanceDue), "Bigger unpaid balances carry more vendor follow-up pressure.", full.BalanceDue >= 100000m ? 25 : full.BalanceDue >= 25000m ? 15 : full.BalanceDue > 0 ? 8 : 0, AccentOrange),
                MakeFactor("Rate anomaly", full.PriceVarianceFlag ? "Flagged" : "Clear", "Price variance over historical buying rate increases approval scrutiny.", full.PriceVarianceFlag ? 20 : 0, full.PriceVarianceFlag ? AccentRed : AccentGreen),
                MakeFactor("Client recovery", full.PendingChargeCreated ? "Queued" : "Not queued", "Queued recovery lowers net business risk because the spend is already mapped to billing.", full.PendingChargeCreated ? 0 : 10, full.PendingChargeCreated ? AccentGreen : AccentBlue));

            model.ComparisonTitle = "Vendor Context";
            model.ComparisonFacts.Add(new DashboardAnalysisFact { Label = "Vendor open balance", Value = FormatCurrency(vendorOrders.Sum(item => item.BalanceDue)) });
            model.ComparisonFacts.Add(new DashboardAnalysisFact { Label = "Vendor PO count", Value = vendorOrders.Count.ToString() });
            model.ComparisonFacts.Add(new DashboardAnalysisFact { Label = "Credit days", Value = (vendor?.DefaultCreditDays ?? 30) + " days" });
            model.Charts.Add(CreateChart(
                "Payment Position",
                "Paid versus open amount for this PO.",
                SeriesChartType.Doughnut,
                new[]
                {
                    new DashboardAnalysisChartPoint { Label = "Paid", Value = full.PaidAmount, Color = AccentGreen },
                    new DashboardAnalysisChartPoint { Label = "Open", Value = Math.Max(0m, full.BalanceDue), Color = ResolvePurchaseColor(full) }
                }));

            model.Tables.Add(CreateTable(
                "Line Items",
                "Raw PO lines available directly inside the dashboard.",
                new[] { "Item", "Qty", "Rate", "GST", "Total" },
                (full.LineItems ?? new List<PurchaseLineItem>()).Select(item => new[]
                {
                    Safe(item.Description),
                    item.Quantity.ToString("N2"),
                    FormatCurrency(item.Rate),
                    item.GSTRate.ToString("N0") + "%",
                    FormatCurrency(item.Amount)
                })));

            model.Tables.Add(CreateTable(
                "Recent Vendor Orders",
                "Recent activity with the same supplier for quick pattern reading.",
                new[] { "PO", "Date", "Status", "Balance" },
                vendorOrders.Take(5).Select(item => new[]
                {
                    Safe(item.PONumber),
                    DateText(item.PODate),
                    ResolvePurchaseStatus(item),
                    FormatCurrency(item.BalanceDue)
                })));
            model.Timeline.Add(new DashboardAnalysisTimelineItem
            {
                Title = "Purchase created",
                Detail = "PO raised for " + FormatCurrency(full.TotalAmount),
                TimeText = DateText(full.PODate),
                Accent = AccentBlue
            });
            if (string.Equals(full.Status, "Received", StringComparison.OrdinalIgnoreCase))
            {
                model.Timeline.Add(new DashboardAnalysisTimelineItem
                {
                    Title = "Goods received",
                    Detail = "Receipt status was marked as received.",
                    TimeText = DateText(full.CreatedDate == default ? full.PODate : full.CreatedDate),
                    Accent = AccentTeal
                });
            }
            if (full.BalanceDue <= 0.01m || string.Equals(full.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                model.Timeline.Add(new DashboardAnalysisTimelineItem
                {
                    Title = "Payment completed",
                    Detail = "Supplier liability is fully settled.",
                    TimeText = DateText(full.PayByDate),
                    Accent = AccentGreen
                });
            }

            model.Actions.Add(MakeExportAction(model));
            if (full.BalanceDue > 0.01m)
            {
                model.Actions.Add(new DashboardAnalysisAction
                {
                    Label = "Mark Paid",
                    BackColor = AccentGreen,
                    Handler = () =>
                    {
                        DialogResult confirm = MessageBox.Show("Mark " + Safe(full.PONumber) + " as fully paid from the dashboard?", "Confirm Payment", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (confirm != DialogResult.Yes)
                            return;

                        _paymentSvc.BatchPayPurchaseOrders(new[] { full.POID }, "DASH-" + DateTime.Now.ToString("yyyyMMddHHmm"));
                        RefreshDashboard(() =>
                        {
                            PurchaseOrder refreshed = _purchaseSvc.GetById(full.POID) ?? full;
                            OpenAnalysis(BuildPurchaseOrderAnalysis(refreshed, "Payment status was updated from the dashboard analysis drawer."));
                        });
                    }
                });
            }
            if (vendor != null)
            {
                model.Actions.Add(new DashboardAnalysisAction
                {
                    Label = "Vendor View",
                    BackColor = AccentBlue,
                    Handler = () => OpenAnalysis(BuildVendorAnalysis(vendor))
                });
            }
            model.Actions.Add(new DashboardAnalysisAction
            {
                Label = "Follow Up",
                BackColor = AccentOrange,
                Handler = () => CopyText("Supplier follow-up for PO " + Safe(full.PONumber) + ": balance " + FormatCurrency(full.BalanceDue) + ", due " + DateText(full.PayByDate) + ".")
            });

            return model;
        }

        private DashboardAnalysisAction MakeExportAction(DashboardAnalysisModel model)
        {
            return MakeExportAction(model, "Export summary", DashboardAnalysisActionStyle.Secondary);
        }

        private DashboardAnalysisAction MakeExportAction(DashboardAnalysisModel model, string label, DashboardAnalysisActionStyle style)
        {
            return new DashboardAnalysisAction
            {
                Label = label,
                Style = style,
                Handler = () => ExportAnalysisCsv(model)
            };
        }

        private DashboardAnalysisModel BuildInvoicePortfolioAnalysis(bool overdueOnly)
        {
            return BuildInvoicePortfolioAnalysis(overdueOnly, DashboardAnalysisDateRange.ThisMonth);
        }

        private DashboardAnalysisModel BuildInvoicePortfolioAnalysis(bool overdueOnly, DashboardAnalysisDateRange range)
        {
            List<Invoice> allInvoices = _invoiceSvc.GetAllInvoices() ?? new List<Invoice>();
            List<Invoice> records = allInvoices
                .Where(inv => inv.BalanceDue > 0.01m && IsInDateRange(inv.InvoiceDate, range))
                .Where(inv => !overdueOnly || inv.DueDate.Date < DateTime.Today && !string.Equals(inv.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
                .ToList();

            decimal totalReceivable = records.Sum(inv => inv.BalanceDue);
            decimal overdueAmount = records.Where(inv => inv.DueDate.Date < DateTime.Today && !string.Equals(inv.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase)).Sum(inv => inv.BalanceDue);
            decimal avgTicket = records.Any() ? totalReceivable / records.Count : 0m;
            var clientGroups = records
                .GroupBy(inv => Safe(inv.ClientName, "Client #" + inv.ClientID))
                .Select(group => new
                {
                    Name = group.Key,
                    Outstanding = group.Sum(item => item.BalanceDue),
                    Has90Plus = group.Any(item => item.DueDate.Date <= DateTime.Today.AddDays(-90))
                })
                .OrderByDescending(group => group.Outstanding)
                .ToList();

            decimal maxSingleClientBalance = clientGroups.FirstOrDefault()?.Outstanding ?? 0m;
            decimal overdueRatio = totalReceivable <= 0 ? 0 : overdueAmount / totalReceivable;
            decimal concentrationRatio = totalReceivable <= 0 ? 0 : maxSingleClientBalance / totalReceivable;
            int score = ClampScore((overdueRatio * 60m) + (concentrationRatio * 40m));
            int clients90Plus = clientGroups.Count(group => group.Has90Plus);

            DashboardAnalysisModel model = new DashboardAnalysisModel
            {
                PageKey = "InvoiceAnalysis",
                EntityType = DashboardAnalysisEntityType.Invoice,
                Title = "Invoice portfolio",
                Subtitle = "Open invoice position with customer payment signals",
                BadgeText = records.Count + " outstanding",
                BadgeVariant = DashboardAnalysisBadgeVariant.Red,
                SelectedRange = range,
                SummaryText = "Receivables balance " + FormatCurrency(totalReceivable) + " across " + clientGroups.Count + " clients."
            };

            if (score > 80 && overdueAmount > 0)
            {
                model.Alert = new DashboardAnalysisAlert
                {
                    Variant = DashboardAnalysisAlertVariant.Red,
                    Title = "Receivables balance " + FormatCurrency(totalReceivable) + " across " + clientGroups.Count + " clients",
                    Body = "Collection priority score is critical."
                };
            }

            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Outstanding", Value = records.Count.ToString(), Subtitle = "Open invoices needing collection", Accent = AccentBlue });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Total receivable", Value = FormatCurrency(totalReceivable), Subtitle = "Open customer balance", Accent = Color.FromArgb(163, 45, 45) });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Overdue amount", Value = FormatCurrency(overdueAmount), Subtitle = "Past due invoices not yet paid", Accent = Color.FromArgb(163, 45, 45) });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Avg ticket", Value = FormatCurrency(avgTicket), Subtitle = "Average open receivable", Accent = Color.FromArgb(186, 117, 23) });

            model.AlgorithmTitle = "Collections priority score";
            model.AlgorithmSummary = "Combines overdue share and concentration in the largest client balance.";
            model.AlgorithmScore = score;
            model.AlgorithmStatusText = ResolveRiskScoreStatus(score);
            model.AlgorithmStatusColor = ResolveRiskScoreColor(score);
            model.AlgorithmFactors = new List<DashboardAnalysisAlgorithmFactor>
            {
                MakeFactor("Largest single balance", FormatCurrency(maxSingleClientBalance), "Highest outstanding client exposure in the selected range.", 0, AccentRed),
                MakeFactor("Clients 90+ days", clients90Plus.ToString(), "Clients carrying invoices overdue by 90+ days.", 0, AccentOrange),
                MakeFactor("Paid this month", FormatCurrency(_collectedMonth), "Cash collected during the current month.", 0, AccentGreen)
            };

            model.Charts.Add(CreateChart(
                "Receivables by client",
                "Top balances driving the current collection queue",
                SeriesChartType.Bar,
                clientGroups.Take(8).Select(group => new DashboardAnalysisChartPoint
                {
                    Label = group.Name,
                    Value = group.Outstanding,
                    Color = group.Has90Plus ? AccentRed : AccentBlue
                }),
                DashboardAnalysisValueFormat.Currency));

            model.Tables.Add(CreateTable(
                "Open invoice queue",
                "Customer receivables sorted by due date",
                new[] { "Invoice", "Client", "Due", "Status", "Balance" },
                records.OrderBy(inv => inv.DueDate).ThenByDescending(inv => inv.BalanceDue).Take(8).Select(inv => new[]
                {
                    Safe(inv.InvoiceNumber),
                    Safe(inv.ClientName, "Client #" + inv.ClientID),
                    DateText(inv.DueDate),
                    Safe(inv.PaymentStatus),
                    FormatCurrency(inv.BalanceDue)
                })));

            model.Actions.Add(new DashboardAnalysisAction
            {
                Label = "Follow up overdue",
                Style = DashboardAnalysisActionStyle.Destructive,
                Handler = () =>
                {
                    CopyText("Overdue receivables: " + FormatCurrency(overdueAmount) + " across " + records.Count(inv => inv.DueDate.Date < DateTime.Today) + " invoices.");
                    OnNavigate?.Invoke(3);
                }
            });
            model.Actions.Add(new DashboardAnalysisAction { Label = "Refresh data", Style = DashboardAnalysisActionStyle.Teal, Handler = () => OpenInvoiceAnalysis() });
            model.Actions.Add(MakeExportAction(model, "Export summary", DashboardAnalysisActionStyle.Secondary));
            return model;
        }

        private DashboardAnalysisModel BuildQuotationPortfolioAnalysis()
        {
            return BuildQuotationPortfolioAnalysis(DashboardAnalysisDateRange.ThisMonth);
        }

        private DashboardAnalysisModel BuildQuotationPortfolioAnalysis(DashboardAnalysisDateRange range)
        {
            List<TenderBid> quotes = (_quotes ?? new List<TenderBid>())
                .Where(item => IsInDateRange(item.DueDate, range))
                .ToList();
            int activeQuotes = quotes.Count(item => !IsQuoteWon(item) && !IsQuoteLost(item));
            decimal pipelineValue = quotes.Where(item => !IsQuoteWon(item)).Sum(item => item.TotalWithGST > 0 ? item.TotalWithGST : item.BidValue);
            int submittedCount = quotes.Count(item => IsQuoteSent(item));
            int wonCount = quotes.Count(IsQuoteWon);
            int overdueCount = quotes.Count(item => item.DueDate != default(DateTime) && item.DueDate.Date < DateTime.Today && !IsQuoteWon(item) && !IsQuoteLost(item));
            int dueSoonCount = quotes.Count(item => item.DueDate != default(DateTime) && item.DueDate.Date >= DateTime.Today && item.DueDate.Date <= DateTime.Today.AddDays(7));
            decimal wonRate = quotes.Count == 0 ? 0 : (wonCount / (decimal)quotes.Count) * 100m;
            int score = ClampScore(50m - (overdueCount * 15m) + (wonRate * 0.3m) - (dueSoonCount * 5m));

            DashboardAnalysisModel model = new DashboardAnalysisModel
            {
                PageKey = "QuotationAnalysis",
                EntityType = DashboardAnalysisEntityType.Quotation,
                Title = "Quotation intelligence",
                Subtitle = "Quote pipeline, conversion posture, and pricing readiness",
                BadgeText = activeQuotes + " active quotes",
                BadgeVariant = DashboardAnalysisBadgeVariant.Teal,
                SelectedRange = range,
                SummaryText = "Live quoted amount stands at " + FormatCurrency(pipelineValue) + " in the selected view."
            };

            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Total quotes", Value = quotes.Count.ToString(), Subtitle = "All loaded quotations", Accent = AccentBlue });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Pipeline value", Value = FormatCurrency(pipelineValue), Subtitle = "Live quoted amount", Accent = AccentTeal });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Submitted", Value = submittedCount.ToString(), Subtitle = "With client", Accent = AccentOrange });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Won", Value = wonCount.ToString(), Subtitle = "Converted to invoice", Accent = AccentTeal });

            model.AlgorithmTitle = "Conversion algorithm score";
            model.AlgorithmSummary = "Combines submission readiness, win rate, and due-date pressure";
            model.AlgorithmScore = score;
            model.AlgorithmStatusText = ResolveQuotationScoreStatus(score);
            model.AlgorithmStatusColor = ResolveQuotationScoreColor(score);
            model.AlgorithmFactors = new List<DashboardAnalysisAlgorithmFactor>
            {
                MakeFactor("Due soon", dueSoonCount.ToString(), "Quotes due in the next 7 days.", 0, AccentOrange),
                MakeFactor("Overdue", overdueCount.ToString(), "Quotes past due date that are not won or lost.", 0, AccentRed),
                MakeFactor("Win rate", FormatPercentage(wonRate), "Won quotations as a share of total loaded quotes.", 0, AccentGreen)
            };

            int draftCount = quotes.Count(item => IsQuoteDraftOrAnalysed(item));
            int sentCount = submittedCount;
            int lostCount = quotes.Count(IsQuoteLost);

            model.Charts.Add(CreateChart(
                "Quote status mix",
                "How the quotation pipeline is distributed right now",
                SeriesChartType.Doughnut,
                new[]
                {
                    new DashboardAnalysisChartPoint { Label = "Draft/Analysed", Value = draftCount, Color = AccentOrange },
                    new DashboardAnalysisChartPoint { Label = "Sent", Value = sentCount, Color = AccentTeal },
                    new DashboardAnalysisChartPoint { Label = "Won", Value = wonCount, Color = AccentBlue },
                    new DashboardAnalysisChartPoint { Label = "Lost", Value = lostCount, Color = AccentRed }
                },
                DashboardAnalysisValueFormat.Count,
                quotes.Count.ToString(),
                "quotes"));

            model.Charts.Add(CreateChart(
                "Value by status",
                "Quoted value grouped by commercial stage",
                SeriesChartType.Bar,
                new[]
                {
                    new DashboardAnalysisChartPoint { Label = "Draft", Value = quotes.Where(IsQuoteDraftOrAnalysed).Sum(item => QuoteValue(item)), Color = AccentOrange },
                    new DashboardAnalysisChartPoint { Label = "Sent", Value = quotes.Where(IsQuoteSent).Sum(item => QuoteValue(item)), Color = AccentTeal },
                    new DashboardAnalysisChartPoint { Label = "Won", Value = quotes.Where(IsQuoteWon).Sum(item => QuoteValue(item)), Color = AccentBlue },
                    new DashboardAnalysisChartPoint { Label = "Lost", Value = quotes.Where(IsQuoteLost).Sum(item => QuoteValue(item)), Color = AccentRed }
                },
                DashboardAnalysisValueFormat.Currency));

            model.Tables.Add(CreateTable(
                "Quotation queue",
                "Priority quotations visible from the dashboard",
                new[] { "Quote", "Client", "Status", "Due", "Value" },
                quotes.OrderBy(item => item.DueDate).ThenByDescending(QuoteValue).Take(8).Select(item => new[]
                {
                    Safe(item.QuotationNumber, "Quote #" + item.BidID),
                    Safe(item.ClientName, "Client #" + item.ClientID),
                    ResolveQuoteStatus(item),
                    DateText(item.DueDate),
                    FormatCurrency(QuoteValue(item))
                })));

            model.Actions.Add(MakeExportAction(model, "Export summary", DashboardAnalysisActionStyle.Primary));
            model.Actions.Add(new DashboardAnalysisAction { Label = "View all quotations", Style = DashboardAnalysisActionStyle.Secondary, Handler = () => OnNavigate?.Invoke(6) });
            return model;
        }

        private DashboardAnalysisModel BuildInvoiceAnalysis(Invoice invoice)
        {
            Invoice full = invoice == null ? null : (_invoiceSvc.GetInvoiceById(invoice.InvoiceID) ?? invoice);
            if (full == null)
                return null;

            B2BClient client = _clients.FirstOrDefault(item => item.ClientID == full.ClientID);
            List<Invoice> clientHistory = _invoiceSvc.GetInvoicesForClient(full.ClientID).OrderByDescending(item => item.InvoiceDate).ToList();
            List<Payment> paymentHistory = (_payments ?? new List<Payment>())
                .Where(payment => payment.InvoiceID == full.InvoiceID)
                .OrderByDescending(payment => payment.PaymentDate)
                .ToList();
            bool overdue = full.BalanceDue > 0.01m && full.DueDate < DateTime.Today;

            DashboardAnalysisModel model = new DashboardAnalysisModel
            {
                EntityType = DashboardAnalysisEntityType.Invoice,
                Title = Safe(full.InvoiceNumber, "Invoice"),
                Subtitle = Safe(full.ClientName, "Client #" + full.ClientID) + (string.IsNullOrWhiteSpace(full.SiteName) ? string.Empty : " | " + full.SiteName),
                StatusText = overdue ? "Overdue" : Safe(full.PaymentStatus, "Draft"),
                StatusColor = overdue ? AccentRed : string.Equals(full.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase) ? AccentGreen : AccentBlue,
                AccentColor = overdue ? AccentRed : AccentBlue,
                SummaryText = "Balance due " + FormatCurrency(full.BalanceDue) + " with due date " + DateText(full.DueDate) + "."
            };

            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Invoice Value", Value = FormatCurrency(full.TotalAmount), Subtitle = "gross invoice", Accent = AccentBlue });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Paid", Value = FormatCurrency(full.PaidAmount), Subtitle = "payments received", Accent = AccentGreen });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Balance", Value = FormatCurrency(full.BalanceDue), Subtitle = overdue ? "overdue balance" : "open balance", Accent = overdue ? AccentRed : AccentOrange });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Age", Value = Math.Max(0, (DateTime.Today - full.InvoiceDate.Date).Days) + " days", Subtitle = "since issue date", Accent = AccentPurple });

            model.Facts.Add(new DashboardAnalysisFact { Label = "Invoice date", Value = DateText(full.InvoiceDate) });
            model.Facts.Add(new DashboardAnalysisFact { Label = "Due date", Value = DateText(full.DueDate) });
            model.Facts.Add(new DashboardAnalysisFact { Label = "Client", Value = Safe(full.ClientName, "Client #" + full.ClientID) });
            model.Facts.Add(new DashboardAnalysisFact { Label = "PO reference", Value = Safe(full.PONumber) });
            model.Facts.Add(new DashboardAnalysisFact { Label = "Contract", Value = full.ContractID > 0 ? "Contract #" + full.ContractID : "-" });
            model.Facts.Add(new DashboardAnalysisFact { Label = "Payment terms", Value = Safe(full.PaymentTerms) });
            model.Facts.Add(new DashboardAnalysisFact { Label = "Reservation", Value = Safe(full.InventoryReservationStatus) });

            if (overdue)
                model.Insights.Add("This invoice is overdue, so cash collection risk is active for this customer.");
            else if (full.BalanceDue > 0.01m)
                model.Insights.Add("This invoice is still open but not yet overdue, which gives room for proactive follow-up.");
            else
                model.Insights.Add("The balance is cleared, so no collection action is needed on this invoice.");
            if (clientHistory.Count(inv => string.Equals(inv.PaymentStatus, "Overdue", StringComparison.OrdinalIgnoreCase)) >= 2)
                model.Insights.Add("The customer has multiple overdue invoices in recent history, which suggests a slower payment pattern.");
            if (paymentHistory.Any())
                model.Insights.Add("Payment activity already exists on this invoice, so the account is partially moving.");

            ApplyAlgorithm(
                model,
                "Invoice Recovery Algorithm",
                "The score blends aging, unpaid balance, customer payment pattern, and live receipt activity.",
                MakeFactor("Aging", Math.Max(0, (DateTime.Today - full.InvoiceDate.Date).Days) + " days", "Older invoices need stronger recovery effort.", overdue ? 35 : full.BalanceDue > 0.01m ? 15 : 0, overdue ? AccentRed : AccentBlue),
                MakeFactor("Balance risk", FormatCurrency(full.BalanceDue), "Higher open balance means more cash tied up.", full.BalanceDue >= 100000m ? 30 : full.BalanceDue >= 25000m ? 15 : full.BalanceDue > 0 ? 8 : 0, AccentOrange),
                MakeFactor("Customer pattern", clientHistory.Count(inv => string.Equals(inv.PaymentStatus, "Overdue", StringComparison.OrdinalIgnoreCase)).ToString() + " overdue history", "Repeated overdue history suggests slower client payment behavior.", Math.Min(20, clientHistory.Count(inv => string.Equals(inv.PaymentStatus, "Overdue", StringComparison.OrdinalIgnoreCase)) * 7), AccentPurple),
                MakeFactor("Receipt momentum", paymentHistory.Any() ? "Payments logged" : "No receipts", "Active receipts reduce collection risk.", paymentHistory.Any() ? 0 : 10, paymentHistory.Any() ? AccentGreen : AccentRed));

            model.ComparisonTitle = "Customer Context";
            model.ComparisonFacts.Add(new DashboardAnalysisFact { Label = "Customer since", Value = client != null && client.CustomerSince != default ? DateText(client.CustomerSince) : "-" });
            model.ComparisonFacts.Add(new DashboardAnalysisFact { Label = "Customer open balance", Value = FormatCurrency(clientHistory.Sum(inv => inv.BalanceDue)) });
            model.ComparisonFacts.Add(new DashboardAnalysisFact { Label = "Customer invoice count", Value = clientHistory.Count.ToString() });
            model.Charts.Add(CreateChart(
                "Collection Position",
                "Paid and unpaid split for this invoice.",
                SeriesChartType.Doughnut,
                new[]
                {
                    new DashboardAnalysisChartPoint { Label = "Paid", Value = full.PaidAmount, Color = AccentGreen },
                    new DashboardAnalysisChartPoint { Label = "Balance", Value = Math.Max(0m, full.BalanceDue), Color = overdue ? AccentRed : AccentOrange }
                }));

            model.Tables.Add(CreateTable(
                "Invoice Lines",
                "Raw line-level value and service detail.",
                new[] { "Description", "Qty", "Rate", "Amount" },
                (full.LineItems ?? new List<InvoiceLineItem>()).Select(line => new[]
                {
                    Safe(line.Description),
                    line.Quantity.ToString("N2"),
                    FormatCurrency(line.Rate),
                    FormatCurrency(line.Amount)
                })));

            model.Tables.Add(CreateTable(
                "Customer History",
                "Recent invoices for the same customer.",
                new[] { "Invoice", "Date", "Status", "Balance" },
                clientHistory.Take(5).Select(item => new[]
                {
                    Safe(item.InvoiceNumber),
                    DateText(item.InvoiceDate),
                    Safe(item.PaymentStatus),
                    FormatCurrency(item.BalanceDue)
                })));
            model.Timeline.Add(new DashboardAnalysisTimelineItem
            {
                Title = "Invoice raised",
                Detail = "Invoice issued for " + FormatCurrency(full.TotalAmount),
                TimeText = DateText(full.InvoiceDate),
                Accent = AccentBlue
            });
            foreach (Payment payment in paymentHistory.Take(3))
            {
                model.Timeline.Add(new DashboardAnalysisTimelineItem
                {
                    Title = "Payment received",
                    Detail = Safe(payment.PaymentNumber, "Receipt") + " booked for " + FormatCurrency(payment.AmountPaid),
                    TimeText = DateText(payment.PaymentDate),
                    Accent = AccentGreen
                });
            }

            model.Actions.Add(MakeExportAction(model));
            model.Actions.Add(new DashboardAnalysisAction
            {
                Label = "Follow Up",
                BackColor = AccentOrange,
                Handler = () => CopyText("Follow-up note for " + Safe(full.ClientName, "Client #" + full.ClientID) + " on invoice " + Safe(full.InvoiceNumber) + ": balance " + FormatCurrency(full.BalanceDue) + ", due " + DateText(full.DueDate) + ".")
            });
            if (client != null)
            {
                model.Actions.Add(new DashboardAnalysisAction
                {
                    Label = "Client View",
                    BackColor = AccentBlue,
                    Handler = () => OpenAnalysis(BuildClientAnalysis(client))
                });
            }
            return model;
        }

        private DashboardAnalysisModel BuildContractPortfolioAnalysis()
        {
            List<AMCContract> activeContracts = _contractSvc.GetAllContracts().Where(contract => string.Equals(contract.ContractStatus, "Active", StringComparison.OrdinalIgnoreCase)).ToList();
            DashboardAnalysisModel model = new DashboardAnalysisModel
            {
                EntityType = DashboardAnalysisEntityType.Contract,
                Title = "Contract Performance",
                Subtitle = "Recurring service agreements and renewal posture in one place.",
                StatusText = activeContracts.Count + " live",
                StatusColor = AccentBlue,
                AccentColor = AccentBlue,
                SummaryText = "Recurring revenue run-rate sits at " + FormatLakhs(_mrr) + " per month with " + _expiringContracts + " renewals due soon."
            };

            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Live Contracts", Value = _activeContracts.ToString(), Subtitle = "active agreements", Accent = AccentBlue });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "MRR", Value = FormatLakhs(_mrr), Subtitle = "recurring revenue", Accent = AccentGreen });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Renewals (90d)", Value = _expiringContracts.ToString(), Subtitle = "need attention", Accent = AccentOrange });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "ARR", Value = FormatLakhs(_mrr * 12), Subtitle = "annualized value", Accent = AccentPurple });
            model.Charts.Add(CreateChart(
                "Renewal Window Mix",
                "Contracts bucketed by days remaining.",
                SeriesChartType.Column,
                new[]
                {
                    new DashboardAnalysisChartPoint { Label = "0-30d", Value = _expiringList.Count(item => (item.EndDate - DateTime.Today).Days <= 30), Color = AccentRed },
                    new DashboardAnalysisChartPoint { Label = "31-60d", Value = _expiringList.Count(item => { int days = (item.EndDate - DateTime.Today).Days; return days > 30 && days <= 60; }), Color = AccentOrange },
                    new DashboardAnalysisChartPoint { Label = "61-90d", Value = _expiringList.Count(item => { int days = (item.EndDate - DateTime.Today).Days; return days > 60 && days <= 90; }), Color = AccentBlue }
                }));

            if (_expiringContracts > 0)
                model.Insights.Add(_expiringContracts + " contracts are within the next 90 days, so renewal prep should start from the dashboard instead of waiting for the contract page.");
            if (activeContracts.Any())
                model.Insights.Add("Average monthly value is " + FormatCurrency(activeContracts.Average(contract => contract.MonthlyValue)) + ", which helps size the revenue mix.");

            ApplyAlgorithm(
                model,
                "Recurring Revenue Stability Algorithm",
                "The score looks at renewal exposure and recurring revenue concentration to highlight contract risk on the dashboard.",
                MakeFactor("Renewals due", _expiringContracts.ToString(), "More renewals inside 90 days increase retention workload.", Math.Min(40, _expiringContracts * 5), AccentOrange),
                MakeFactor("Run-rate", FormatLakhs(_mrr), "Higher MRR raises the value at stake in renewal execution.", _mrr >= 1000000m ? 20 : _mrr >= 500000m ? 12 : 6, AccentGreen),
                MakeFactor("Urgent renewals", _expiringList.Count(item => (item.EndDate - DateTime.Today).Days <= 30).ToString(), "Contracts near expiry create the sharpest risk.", Math.Min(30, _expiringList.Count(item => (item.EndDate - DateTime.Today).Days <= 30) * 8), AccentRed));

            model.Tables.Add(CreateTable(
                "Renewal Queue",
                "Most time-sensitive contracts from the current dashboard feed.",
                new[] { "Contract", "Client", "Expires", "MRR", "Days Left" },
                _expiringList.Take(8).Select(contract => new[]
                {
                    "Contract #" + contract.ContractID,
                    ResolveClientName(contract.ClientID),
                    DateText(contract.EndDate),
                    FormatCurrency(contract.MonthlyValue),
                    Math.Max(0, (contract.EndDate - DateTime.Today).Days) + " days"
                })));

            model.Actions.Add(MakeExportAction(model));
            model.Actions.Add(new DashboardAnalysisAction
            {
                Label = "Renewals View",
                BackColor = AccentOrange,
                Handler = () => OpenAnalysis(BuildRenewalAnalysis())
            });
            return model;
        }

        private DashboardAnalysisModel BuildRenewalAnalysis()
        {
            DashboardAnalysisModel model = new DashboardAnalysisModel
            {
                EntityType = DashboardAnalysisEntityType.Contract,
                Title = "Renewal Pipeline",
                Subtitle = "Contracts expiring inside the next 90 days.",
                StatusText = _expiringList.Count + " expiring",
                StatusColor = _expiringList.Any() ? AccentOrange : AccentGreen,
                AccentColor = _expiringList.Any() ? AccentOrange : AccentGreen,
                SummaryText = _expiringList.Any() ? "Upcoming renewals are concentrated around " + ResolveClientName(_expiringList.First().ClientID) + " and similar accounts." : "No contracts are expiring soon."
            };

            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Expiring Contracts", Value = _expiringList.Count.ToString(), Subtitle = "within 90 days", Accent = _expiringList.Any() ? AccentOrange : AccentGreen });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "At-Risk ARR", Value = FormatLakhs(_expiringList.Sum(item => item.AnnualValue)), Subtitle = "renewal value exposed", Accent = AccentRed });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Urgent (30d)", Value = _expiringList.Count(item => (item.EndDate - DateTime.Today).Days <= 30).ToString(), Subtitle = "immediate action", Accent = AccentRed });
            model.Charts.Add(CreateChart(
                "Renewal Risk Bands",
                "Which expiring contracts need attention first.",
                SeriesChartType.Column,
                new[]
                {
                    new DashboardAnalysisChartPoint { Label = "Urgent", Value = _expiringList.Count(item => (item.EndDate - DateTime.Today).Days <= 30), Color = AccentRed },
                    new DashboardAnalysisChartPoint { Label = "Plan Now", Value = _expiringList.Count(item => { int days = (item.EndDate - DateTime.Today).Days; return days > 30 && days <= 60; }), Color = AccentOrange },
                    new DashboardAnalysisChartPoint { Label = "Monitor", Value = _expiringList.Count(item => { int days = (item.EndDate - DateTime.Today).Days; return days > 60 && days <= 90; }), Color = AccentBlue }
                }));

            if (_expiringList.Any(item => (item.EndDate - DateTime.Today).Days <= 30))
                model.Insights.Add(_expiringList.Count(item => (item.EndDate - DateTime.Today).Days <= 30) + " contracts are inside the 30-day window, so renewal outreach should start immediately.");
            if (!_expiringList.Any())
                model.Insights.Add("No contracts are close to expiry, so the renewal board is currently quiet.");

            ApplyAlgorithm(
                model,
                "Renewal Urgency Algorithm",
                "The score prioritizes contracts by time-to-expiry and renewal value at risk.",
                MakeFactor("Urgent contracts", _expiringList.Count(item => (item.EndDate - DateTime.Today).Days <= 30).ToString(), "The 30-day bucket has the strongest urgency weight.", Math.Min(50, _expiringList.Count(item => (item.EndDate - DateTime.Today).Days <= 30) * 10), AccentRed),
                MakeFactor("At-risk ARR", FormatLakhs(_expiringList.Sum(item => item.AnnualValue)), "Higher annual value raises the commercial impact of delayed renewals.", _expiringList.Sum(item => item.AnnualValue) >= 1000000m ? 30 : _expiringList.Sum(item => item.AnnualValue) > 0 ? 15 : 0, AccentOrange),
                MakeFactor("Pipeline size", _expiringList.Count.ToString(), "A larger pipeline adds coordination complexity.", Math.Min(20, _expiringList.Count * 2), AccentBlue));

            model.Tables.Add(CreateTable(
                "Contracts Needing Action",
                "Prioritized by expiry date.",
                new[] { "Contract", "Client", "Type", "Expires", "Action" },
                _expiringList.OrderBy(item => item.EndDate).Take(8).Select(item => new[]
                {
                    "Contract #" + item.ContractID,
                    ResolveClientName(item.ClientID),
                    Safe(item.ContractType),
                    DateText(item.EndDate),
                    (item.EndDate - DateTime.Today).Days <= 30 ? "Renew now" : "Prepare proposal"
                })));

            model.Actions.Add(MakeExportAction(model));
            return model;
        }

        private DashboardAnalysisModel BuildContractAnalysis(AMCContract contract)
        {
            if (contract == null)
                return null;

            List<Invoice> relatedInvoices = _invoiceSvc.GetInvoicesForContract(contract.ContractID).OrderByDescending(item => item.InvoiceDate).ToList();
            B2BClient client = _clients.FirstOrDefault(item => item.ClientID == contract.ClientID);
            int daysLeft = Math.Max(0, (contract.EndDate - DateTime.Today).Days);

            DashboardAnalysisModel model = new DashboardAnalysisModel
            {
                EntityType = DashboardAnalysisEntityType.Contract,
                Title = "Contract #" + contract.ContractID,
                Subtitle = ResolveClientName(contract.ClientID) + " | " + Safe(contract.ContractType),
                StatusText = daysLeft <= 30 ? "Urgent Renewal" : Safe(contract.ContractStatus, "Active"),
                StatusColor = daysLeft <= 30 ? AccentRed : daysLeft <= 90 ? AccentOrange : AccentGreen,
                AccentColor = AccentBlue,
                SummaryText = "Contract ends on " + DateText(contract.EndDate) + " with monthly value " + FormatCurrency(contract.MonthlyValue) + "."
            };

            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Monthly Value", Value = FormatCurrency(contract.MonthlyValue), Subtitle = "recurring amount", Accent = AccentBlue });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Annual Value", Value = FormatCurrency(contract.AnnualValue), Subtitle = "annual contract value", Accent = AccentGreen });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Days Left", Value = daysLeft + " days", Subtitle = "to contract end", Accent = daysLeft <= 30 ? AccentRed : AccentOrange });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Invoices", Value = relatedInvoices.Count.ToString(), Subtitle = "billed against contract", Accent = AccentPurple });

            model.Facts.Add(new DashboardAnalysisFact { Label = "Client", Value = ResolveClientName(contract.ClientID) });
            model.Facts.Add(new DashboardAnalysisFact { Label = "Start date", Value = DateText(contract.StartDate) });
            model.Facts.Add(new DashboardAnalysisFact { Label = "End date", Value = DateText(contract.EndDate) });
            model.Facts.Add(new DashboardAnalysisFact { Label = "Maintenance", Value = Safe(contract.MaintenanceFrequency) });
            model.Facts.Add(new DashboardAnalysisFact { Label = "SLA response", Value = contract.SLAResponseTimeHours + " hours" });
            model.Facts.Add(new DashboardAnalysisFact { Label = "SLA repair", Value = contract.SLARepairTimeHours + " hours" });
            model.Facts.Add(new DashboardAnalysisFact { Label = "Uptime target", Value = contract.SLAUptimePercent.ToString("N1") + "%" });

            if (daysLeft <= 30)
                model.Insights.Add("The renewal window is urgent, so account management action belongs on today's dashboard review.");
            else if (daysLeft <= 90)
                model.Insights.Add("This contract is approaching renewal and should move into proposal planning soon.");
            if (relatedInvoices.Any(inv => inv.BalanceDue > 0.01m))
                model.Insights.Add("There are unpaid invoices linked to this contract, which may affect renewal conversations.");

            model.Tables.Add(CreateTable(
                "Related Invoices",
                "Billing history for this contract.",
                new[] { "Invoice", "Date", "Status", "Balance" },
                relatedInvoices.Take(6).Select(inv => new[]
                {
                    Safe(inv.InvoiceNumber),
                    DateText(inv.InvoiceDate),
                    Safe(inv.PaymentStatus),
                    FormatCurrency(inv.BalanceDue)
                })));

            model.Actions.Add(MakeExportAction(model));
            model.Actions.Add(new DashboardAnalysisAction
            {
                Label = "Renewal Note",
                BackColor = AccentOrange,
                Handler = () => CopyText("Renewal follow-up for Contract #" + contract.ContractID + " (" + ResolveClientName(contract.ClientID) + ") due " + DateText(contract.EndDate) + ".")
            });
            if (client != null)
            {
                model.Actions.Add(new DashboardAnalysisAction
                {
                    Label = "Client View",
                    BackColor = AccentBlue,
                    Handler = () => OpenAnalysis(BuildClientAnalysis(client))
                });
            }
            return model;
        }

        private DashboardAnalysisModel BuildInventoryPortfolioAnalysis(bool lowOnly)
        {
            return BuildInventoryPortfolioAnalysis(lowOnly, DashboardAnalysisDateRange.ThisMonth);
        }

        private DashboardAnalysisModel BuildInventoryPortfolioAnalysis(bool lowOnly, DashboardAnalysisDateRange range)
        {
            List<StockItem> items = (_inventoryItems ?? new List<StockItem>())
                .Where(item => !lowOnly || item.IsLowStock)
                .OrderByDescending(item => item.StockValue)
                .ToList();

            int totalItems = items.Count;
            int lowStockCount = items.Count(item => item.IsLowStock);
            int healthyItems = Math.Max(0, totalItems - lowStockCount);
            decimal totalStockValue = items.Sum(item => item.StockValue);
            decimal topItemValue = items.OrderByDescending(item => item.StockValue).FirstOrDefault()?.StockValue ?? 0m;
            decimal reorderRatio = totalItems == 0 ? 0 : lowStockCount / (decimal)totalItems;
            decimal valueConcentration = totalStockValue <= 0 ? 0 : topItemValue / totalStockValue;
            int score = ClampScore((reorderRatio * 70m) + (valueConcentration * 30m));

            DashboardAnalysisModel model = new DashboardAnalysisModel
            {
                PageKey = "InventoryAnalysis",
                EntityType = DashboardAnalysisEntityType.Inventory,
                Title = "Inventory position",
                Subtitle = "Live inventory coverage across the HVAC ERP",
                BadgeText = totalItems + " items",
                BadgeVariant = DashboardAnalysisBadgeVariant.Teal,
                SelectedRange = range,
                SummaryText = "Inventory value is " + FormatCurrency(totalStockValue) + " across live stock lines."
            };

            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Active items", Value = totalItems.ToString(), Subtitle = "Active inventory lines", Accent = AccentBlue });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Low stock", Value = lowStockCount.ToString(), Subtitle = "Below reorder level", Accent = AccentRed });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Stock value", Value = FormatCurrency(totalStockValue), Subtitle = "Qty on hand × last purchase rate", Accent = AccentOrange });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Reorder urgency", Value = FormatPercentage(reorderRatio * 100m), Subtitle = "Low stock as a share of items", Accent = AccentRed });

            model.AlgorithmTitle = "Inventory pressure score";
            model.AlgorithmSummary = "Blends reorder pressure and stock-value concentration.";
            model.AlgorithmScore = score;
            model.AlgorithmStatusText = ResolveRiskScoreStatus(score);
            model.AlgorithmStatusColor = ResolveRiskScoreColor(score);
            model.AlgorithmFactors = new List<DashboardAnalysisAlgorithmFactor>
            {
                MakeFactor("Items below reorder", lowStockCount.ToString(), "Live items currently below reorder threshold.", 0, AccentRed),
                MakeFactor("Inventory value", FormatCurrency(totalStockValue), "Total estimated live stock value.", 0, AccentOrange),
                MakeFactor("Healthy items", healthyItems.ToString(), "Items currently above reorder level.", 0, AccentGreen)
            };

            model.Charts.Add(CreateChart(
                "Top stock value items",
                "Highest value material positions in inventory",
                SeriesChartType.Bar,
                items.Take(8).Select(item => new DashboardAnalysisChartPoint
                {
                    Label = Safe(item.ItemName),
                    Value = item.StockValue,
                    Color = AccentTeal
                }),
                DashboardAnalysisValueFormat.Currency));

            if (reorderRatio > 0.5m)
                model.InsightCards.Add(new DashboardAnalysisInsight { Title = "Critical reorder required", Detail = "More than half of tracked items are below reorder level.", Accent = AccentRed });
            if (valueConcentration > 0.3m)
                model.InsightCards.Add(new DashboardAnalysisInsight { Title = "Value concentration risk", Detail = "A large share of stock value sits in one material position.", Accent = AccentOrange });
            if (DateTime.Today.Month == 2 || DateTime.Today.Month == 3)
                model.InsightCards.Add(new DashboardAnalysisInsight { Title = "Pre-summer stocking", Detail = "Seasonal preparation window is active for HVAC demand.", Accent = AccentGreen });
            model.InsightCards.Add(new DashboardAnalysisInsight { Title = healthyItems + " items healthy", Detail = "These lines currently sit above reorder thresholds.", Accent = AccentGreen });

            model.Tables.Add(CreateTable(
                "Inventory snapshot",
                "Material coverage with supplier reference",
                new[] { "Item", "Stock", "Reorder", "Vendor", "Value" },
                items.Take(8).Select(item => new[]
                {
                    Safe(item.ItemName),
                    item.CurrentStock.ToString("N2") + " " + Safe(item.Unit, string.Empty),
                    item.ReorderLevel.ToString("N2"),
                    Safe(item.VendorName),
                    FormatCurrency(item.StockValue)
                })));

            model.Actions.Add(MakeExportAction(model, "Export summary", DashboardAnalysisActionStyle.Primary));
            model.Actions.Add(new DashboardAnalysisAction { Label = "View full inventory", Style = DashboardAnalysisActionStyle.Secondary, Handler = () => OnNavigate?.Invoke(11) });
            model.Actions.Add(new DashboardAnalysisAction { Label = "Raise reorder POs", Style = DashboardAnalysisActionStyle.Secondary, Handler = () => OnNavigate?.Invoke(10) });
            return model;
        }

        private DashboardAnalysisModel BuildVendorPortfolioAnalysis()
        {
            var vendorRows = (_purchaseOrders ?? new List<PurchaseOrder>())
                .GroupBy(po => new { po.VendorID, Name = string.IsNullOrWhiteSpace(po.VendorName) ? "Vendor #" + po.VendorID : po.VendorName })
                .Select(group => new
                {
                    group.Key.VendorID,
                    group.Key.Name,
                    Outstanding = group.Sum(item => item.BalanceDue),
                    Orders = group.Count(),
                    Overdue = group.Count(item => item.IsOverdue)
                })
                .OrderByDescending(item => item.Outstanding)
                .ToList();

            DashboardAnalysisModel model = new DashboardAnalysisModel
            {
                EntityType = DashboardAnalysisEntityType.Vendor,
                Title = "Vendor Intelligence",
                Subtitle = "Supplier concentration, payable pressure, and recent engagement.",
                StatusText = _vendorCount + " active",
                StatusColor = AccentBlue,
                AccentColor = AccentBlue,
                SummaryText = "Vendor data is anchored by purchases, stock lines, and payable exposure."
            };

            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Active Vendors", Value = _vendorCount.ToString(), Subtitle = "supplier master count", Accent = AccentBlue });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "With Open Dues", Value = vendorRows.Count(item => item.Outstanding > 0.01m).ToString(), Subtitle = "vendors awaiting payment", Accent = AccentOrange });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Highest Exposure", Value = FormatCurrency(vendorRows.FirstOrDefault()?.Outstanding ?? 0m), Subtitle = Safe(vendorRows.FirstOrDefault()?.Name, "No exposure"), Accent = AccentRed });
            ApplyAlgorithm(
                model,
                "Vendor Dependence Algorithm",
                "The score measures supplier concentration and overdue payable dependence.",
                MakeFactor("Open vendors", vendorRows.Count(item => item.Outstanding > 0.01m).ToString(), "More open vendor balances means a busier payable network.", Math.Min(30, vendorRows.Count(item => item.Outstanding > 0.01m) * 3), AccentBlue),
                MakeFactor("Top exposure", FormatCurrency(vendorRows.FirstOrDefault()?.Outstanding ?? 0m), "Heavy exposure to one supplier raises negotiation pressure.", (vendorRows.FirstOrDefault()?.Outstanding ?? 0m) >= 200000m ? 40 : (vendorRows.FirstOrDefault()?.Outstanding ?? 0m) > 0 ? 15 : 0, AccentRed),
                MakeFactor("Overdue vendors", vendorRows.Count(item => item.Overdue > 0).ToString(), "Suppliers with overdue orders increase service friction.", Math.Min(30, vendorRows.Count(item => item.Overdue > 0) * 6), AccentOrange));
            model.Charts.Add(CreateChart(
                "Vendor Exposure",
                "Top supplier balances shown visually.",
                SeriesChartType.Bar,
                vendorRows.Take(6).Select(item => new DashboardAnalysisChartPoint
                {
                    Label = item.Name,
                    Value = item.Outstanding,
                    Color = item.Overdue > 0 ? AccentRed : AccentBlue
                })));

            model.Tables.Add(CreateTable(
                "Top Vendor Exposure",
                "Suppliers with the largest live financial exposure.",
                new[] { "Vendor", "Orders", "Outstanding", "Overdue" },
                vendorRows.Take(8).Select(item => new[]
                {
                    item.Name,
                    item.Orders.ToString(),
                    FormatCurrency(item.Outstanding),
                    item.Overdue.ToString()
                })));

            model.Actions.Add(MakeExportAction(model));
            return model;
        }

        private DashboardAnalysisModel BuildVendorAnalysis(Vendor vendor)
        {
            if (vendor == null)
                return null;

            List<PurchaseOrder> orders = (_purchaseOrders ?? new List<PurchaseOrder>())
                .Where(item => item.VendorID == vendor.VendorID)
                .OrderByDescending(item => item.PODate)
                .ToList();

            DashboardAnalysisModel model = new DashboardAnalysisModel
            {
                EntityType = DashboardAnalysisEntityType.Vendor,
                Title = Safe(vendor.VendorName, "Vendor"),
                Subtitle = Safe(vendor.Category, "Supplier") + (string.IsNullOrWhiteSpace(vendor.City) ? string.Empty : " | " + vendor.City),
                StatusText = vendor.IsActive ? "Active" : "Inactive",
                StatusColor = vendor.IsActive ? AccentGreen : AccentRed,
                AccentColor = AccentBlue,
                SummaryText = "Outstanding supplier dues " + FormatCurrency(orders.Sum(item => item.BalanceDue)) + " across " + orders.Count + " purchase orders."
            };

            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Open Balance", Value = FormatCurrency(orders.Sum(item => item.BalanceDue)), Subtitle = "supplier dues", Accent = AccentOrange });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Orders", Value = orders.Count.ToString(), Subtitle = "purchase orders", Accent = AccentBlue });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Credit Days", Value = vendor.DefaultCreditDays + " days", Subtitle = "default payment window", Accent = AccentPurple });

            model.Facts.Add(new DashboardAnalysisFact { Label = "GSTIN", Value = Safe(vendor.GSTNumber) });
            model.Facts.Add(new DashboardAnalysisFact { Label = "Phone", Value = Safe(vendor.Phone) });
            model.Facts.Add(new DashboardAnalysisFact { Label = "Email", Value = Safe(vendor.Email) });
            model.Facts.Add(new DashboardAnalysisFact { Label = "Address", Value = Safe(vendor.Address) });

            model.Tables.Add(CreateTable(
                "Recent Purchases",
                "Most recent orders with this supplier.",
                new[] { "PO", "Date", "Status", "Balance" },
                orders.Take(6).Select(item => new[]
                {
                    Safe(item.PONumber),
                    DateText(item.PODate),
                    ResolvePurchaseStatus(item),
                    FormatCurrency(item.BalanceDue)
                })));

            model.Actions.Add(MakeExportAction(model));
            model.Actions.Add(new DashboardAnalysisAction
            {
                Label = "Follow Up",
                BackColor = AccentOrange,
                Handler = () => CopyText("Vendor follow-up for " + Safe(vendor.VendorName) + ": open balance " + FormatCurrency(orders.Sum(item => item.BalanceDue)) + ".")
            });
            return model;
        }

        private DashboardAnalysisModel BuildClientAnalysis(B2BClient client)
        {
            if (client == null)
                return null;

            List<Invoice> invoices = _invoiceSvc.GetInvoicesForClient(client.ClientID).OrderByDescending(item => item.InvoiceDate).ToList();
            List<AMCContract> contracts = _contractSvc.GetAllContracts().Where(item => item.ClientID == client.ClientID).ToList();
            List<Job> jobs = (_jobs ?? new List<Job>()).Where(item => item.ClientID == client.ClientID).ToList();

            DashboardAnalysisModel model = new DashboardAnalysisModel
            {
                EntityType = DashboardAnalysisEntityType.Client,
                Title = Safe(client.CompanyName, "Client"),
                Subtitle = Safe(client.IndustryType, "Client account") + (string.IsNullOrWhiteSpace(client.City) ? string.Empty : " | " + client.City),
                StatusText = client.IsActive ? "Active" : "Archived",
                StatusColor = client.IsActive ? AccentGreen : AccentRed,
                AccentColor = AccentBlue,
                SummaryText = "Open receivables " + FormatCurrency(invoices.Sum(item => item.BalanceDue)) + " with " + contracts.Count + " contracts linked."
            };

            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Open Balance", Value = FormatCurrency(invoices.Sum(item => item.BalanceDue)), Subtitle = "customer receivables", Accent = AccentBlue });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Contracts", Value = contracts.Count.ToString(), Subtitle = "linked contracts", Accent = AccentGreen });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Jobs", Value = jobs.Count.ToString(), Subtitle = "service work orders", Accent = AccentTeal });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Annual Value", Value = FormatCurrency(client.TotalAnnualValue), Subtitle = "recorded client value", Accent = AccentPurple });

            model.Facts.Add(new DashboardAnalysisFact { Label = "Primary contact", Value = Safe(client.PrimaryContact) });
            model.Facts.Add(new DashboardAnalysisFact { Label = "Phone", Value = Safe(client.Phone) });
            model.Facts.Add(new DashboardAnalysisFact { Label = "Email", Value = Safe(client.Email) });
            model.Facts.Add(new DashboardAnalysisFact { Label = "GSTIN", Value = Safe(client.GSTNumber) });
            model.Facts.Add(new DashboardAnalysisFact { Label = "Payment terms", Value = client.PaymentTermsDays + " days" });

            model.Tables.Add(CreateTable(
                "Recent Invoices",
                "Billing history for this client.",
                new[] { "Invoice", "Date", "Status", "Balance" },
                invoices.Take(5).Select(item => new[]
                {
                    Safe(item.InvoiceNumber),
                    DateText(item.InvoiceDate),
                    Safe(item.PaymentStatus),
                    FormatCurrency(item.BalanceDue)
                })));

            model.Tables.Add(CreateTable(
                "Recent Jobs",
                "Operational work tied to this client.",
                new[] { "Job", "Scheduled", "Status", "Revenue" },
                jobs.OrderByDescending(item => item.ScheduledDate).Take(5).Select(item => new[]
                {
                    Safe(item.JobNumber, "Job #" + item.JobID),
                    DateText(item.ScheduledDate),
                    Safe(item.Status),
                    FormatCurrency(item.Revenue)
                })));

            model.Actions.Add(MakeExportAction(model));
            model.Actions.Add(new DashboardAnalysisAction
            {
                Label = "Follow Up",
                BackColor = AccentOrange,
                Handler = () => CopyText("Client follow-up for " + Safe(client.CompanyName) + ": open receivables " + FormatCurrency(invoices.Sum(item => item.BalanceDue)) + ".")
            });
            return model;
        }

        private DashboardAnalysisModel BuildCashCollectionAnalysis()
        {
            List<Payment> monthPayments = (_payments ?? new List<Payment>())
                .Where(payment => payment.PaymentDate.Month == DateTime.Today.Month && payment.PaymentDate.Year == DateTime.Today.Year)
                .OrderByDescending(payment => payment.PaymentDate)
                .ToList();

            DashboardAnalysisModel model = new DashboardAnalysisModel
            {
                EntityType = DashboardAnalysisEntityType.Invoice,
                Title = "Cash Collection Pulse",
                Subtitle = "Payment receipts and collection momentum for the current month.",
                StatusText = monthPayments.Count + " receipts",
                StatusColor = AccentGreen,
                AccentColor = AccentGreen,
                SummaryText = "Collected this month: " + FormatCurrency(monthPayments.Sum(payment => payment.AmountPaid)) + "."
            };

            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Collected", Value = FormatCurrency(monthPayments.Sum(payment => payment.AmountPaid)), Subtitle = "this month", Accent = AccentGreen });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Payments", Value = monthPayments.Count.ToString(), Subtitle = "receipt count", Accent = AccentBlue });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Average Receipt", Value = monthPayments.Any() ? FormatCurrency(monthPayments.Average(payment => payment.AmountPaid)) : FormatCurrency(0m), Subtitle = "per payment", Accent = AccentPurple });
            ApplyAlgorithm(
                model,
                "Cash Momentum Algorithm",
                "The score looks at payment volume, average receipt size, and recent monthly trend to estimate collection momentum.",
                MakeFactor("Monthly cash", FormatCurrency(monthPayments.Sum(payment => payment.AmountPaid)), "Higher current-month receipts improve short-term liquidity.", monthPayments.Sum(payment => payment.AmountPaid) >= 500000m ? 10 : monthPayments.Any() ? 5 : 0, AccentGreen),
                MakeFactor("Receipt count", monthPayments.Count.ToString(), "Low receipt counts suggest more concentrated cash inflow.", monthPayments.Count == 0 ? 35 : monthPayments.Count <= 3 ? 20 : 8, AccentBlue),
                MakeFactor("Average receipt", monthPayments.Any() ? FormatCurrency(monthPayments.Average(payment => payment.AmountPaid)) : FormatCurrency(0m), "Large average receipts can indicate concentration in fewer accounts.", monthPayments.Any() && monthPayments.Average(payment => payment.AmountPaid) >= 100000m ? 20 : monthPayments.Any() ? 8 : 0, AccentPurple));
            model.Charts.Add(CreateChart(
                "Last 6 Months Collections",
                "Monthly receipt trend from recorded payments.",
                SeriesChartType.Column,
                (_payments ?? new List<Payment>())
                    .GroupBy(payment => new DateTime(payment.PaymentDate.Year, payment.PaymentDate.Month, 1))
                    .OrderByDescending(group => group.Key)
                    .Take(6)
                    .OrderBy(group => group.Key)
                    .Select(group => new DashboardAnalysisChartPoint
                    {
                        Label = group.Key.ToString("MMM yy"),
                        Value = group.Sum(payment => payment.AmountPaid),
                        Color = AccentGreen
                    })));

            model.Tables.Add(CreateTable(
                "Recent Receipts",
                "Latest payment events already recorded in the ERP.",
                new[] { "Payment", "Client", "Date", "Amount" },
                monthPayments.Take(8).Select(payment => new[]
                {
                    Safe(payment.PaymentNumber),
                    Safe(payment.ClientName, "Client #" + payment.ClientID),
                    DateText(payment.PaymentDate),
                    FormatCurrency(payment.AmountPaid)
                })));

            model.Actions.Add(MakeExportAction(model));
            return model;
        }

        private DashboardAnalysisModel BuildJobPerformanceAnalysis()
        {
            return BuildJobPerformanceAnalysis(DashboardAnalysisDateRange.ThisMonth);
        }

        private DashboardAnalysisModel BuildJobPerformanceAnalysis(DashboardAnalysisDateRange range)
        {
            List<Job> allJobs = _jobs ?? new List<Job>();
            List<Job> rangeJobs = allJobs.Where(job => IsInDateRange(GetJobRelevantDate(job), range)).ToList();
            List<Job> completedJobs = rangeJobs.Where(job => string.Equals(Safe(job.Status), "Completed", StringComparison.OrdinalIgnoreCase)).ToList();
            List<Job> pendingJobs = allJobs.Where(job => string.Equals(Safe(job.Status), "Pending", StringComparison.OrdinalIgnoreCase)).ToList();
            List<Job> inProgressJobs = allJobs.Where(job => string.Equals(NormalizeJobStatus(job.Status), "In Progress", StringComparison.OrdinalIgnoreCase)).ToList();
            decimal revenueThisMonth = completedJobs.Sum(job => job.Revenue);
            decimal profitThisMonth = completedJobs.Sum(job => job.Revenue - job.EstimatedCost);
            decimal avgMargin = completedJobs.Any() ? completedJobs.Average(GetJobMarginPct) : 0m;
            decimal completionRate = rangeJobs.Any() ? (completedJobs.Count / (decimal)rangeJobs.Count) * 100m : 0m;
            int score = ClampScore((pendingJobs.Count * 10m) + (inProgressJobs.Count * 5m) + Math.Max(0m, (20m - avgMargin) * 1.5m));

            DashboardAnalysisModel model = new DashboardAnalysisModel
            {
                PageKey = "JobAnalysis",
                EntityType = DashboardAnalysisEntityType.Job,
                Title = "Jobs performance board",
                Subtitle = "Field execution, revenue delivery, and margin readout",
                BadgeText = pendingJobs.Count + " pending",
                BadgeVariant = DashboardAnalysisBadgeVariant.Amber,
                SelectedRange = range,
                SummaryText = "Completed jobs produced " + FormatCurrency(revenueThisMonth) + " revenue and " + FormatCurrency(profitThisMonth) + " profit."
            };

            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Revenue this month", Value = FormatCurrency(revenueThisMonth), Subtitle = "Closed job revenue", Accent = AccentTeal });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Profit this month", Value = FormatCurrency(profitThisMonth), Subtitle = "Closed job gross profit", Accent = AccentTeal });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "Completed", Value = completedJobs.Count.ToString(), Subtitle = "Status = Completed", Accent = AccentBlue });
            model.Metrics.Add(new DashboardAnalysisMetric { Label = "In progress", Value = inProgressJobs.Count.ToString(), Subtitle = "Active field execution", Accent = AccentOrange });

            model.AlgorithmTitle = "Operational throughput score";
            model.AlgorithmSummary = "Measures pending backlog, live execution load, and margin quality.";
            model.AlgorithmScore = score;
            model.AlgorithmStatusText = ResolveRiskScoreStatus(score);
            model.AlgorithmStatusColor = ResolveRiskScoreColor(score);
            model.AlgorithmFactors = new List<DashboardAnalysisAlgorithmFactor>
            {
                MakeFactor("Profit posture", FormatPercentage(avgMargin), "Average gross margin on closed jobs.", 0, avgMargin >= 25m ? AccentGreen : AccentOrange),
                MakeFactor("Completion rate", FormatPercentage(completionRate), "Completed jobs as a share of total jobs in range.", 0, AccentGreen)
            };

            model.Charts.Add(CreateChart(
                "Jobs by status",
                "Current operational mix across the field team",
                SeriesChartType.Doughnut,
                new[]
                {
                    new DashboardAnalysisChartPoint { Label = "Pending", Value = pendingJobs.Count, Color = AccentOrange },
                    new DashboardAnalysisChartPoint { Label = "In Progress", Value = inProgressJobs.Count, Color = AccentTeal },
                    new DashboardAnalysisChartPoint { Label = "Completed", Value = completedJobs.Count, Color = AccentBlue }
                },
                DashboardAnalysisValueFormat.Count,
                allJobs.Count.ToString(),
                "jobs"));

            model.Tables.Add(new DashboardAnalysisTable
            {
                Title = "Active job tracker",
                Subtitle = "Open and in-progress jobs on the live board",
                StyleKey = "JobsTracker",
                Columns = new List<string> { "Job number", "Client", "Technician", "Type", "Revenue", "Est margin", "Status" },
                Rows = allJobs
                    .Where(job => !string.Equals(Safe(job.Status), "Completed", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(job => job.ScheduledDate)
                    .Take(12)
                    .Select(job => new[]
                    {
                        Safe(job.JobNumber, "Job #" + job.JobID),
                        Safe(job.ClientName, "Client #" + job.ClientID),
                        Safe(job.AssignedEmployeeName, "-"),
                        Safe(job.Title, Safe(job.Priority)),
                        FormatCurrency(job.Revenue),
                        FormatPercentage(GetJobMarginPct(job)),
                        NormalizeJobStatus(job.Status)
                    })
                    .ToList()
            });

            model.Actions.Add(MakeExportAction(model, "Export summary", DashboardAnalysisActionStyle.Primary));
            model.Actions.Add(new DashboardAnalysisAction { Label = "View all jobs", Style = DashboardAnalysisActionStyle.Secondary, Handler = () => OnNavigate?.Invoke(15) });
            return model;
        }

        private DashboardAnalysisTable CreateTable(string title, string subtitle, IEnumerable<string> columns, IEnumerable<string[]> rows)
        {
            return new DashboardAnalysisTable
            {
                Title = title,
                Subtitle = subtitle,
                Columns = columns.ToList(),
                Rows = rows.ToList()
            };
        }

        private DashboardAnalysisChart CreateChart(string title, string subtitle, SeriesChartType chartType, IEnumerable<DashboardAnalysisChartPoint> points)
        {
            return CreateChart(title, subtitle, chartType, points, DashboardAnalysisValueFormat.Number, null, null);
        }

        private DashboardAnalysisChart CreateChart(string title, string subtitle, SeriesChartType chartType, IEnumerable<DashboardAnalysisChartPoint> points, DashboardAnalysisValueFormat valueFormat, string centerText = null, string centerSubtext = null)
        {
            return new DashboardAnalysisChart
            {
                Title = title,
                Subtitle = subtitle,
                ChartType = chartType,
                ValueFormat = valueFormat,
                CenterText = centerText,
                CenterSubtext = centerSubtext,
                ShowLegend = chartType == SeriesChartType.Doughnut,
                Points = points.Where(point => point != null && point.Value > 0).ToList()
            };
        }

        private void ApplyAlgorithm(DashboardAnalysisModel model, string title, string summary, params DashboardAnalysisAlgorithmFactor[] factors)
        {
            List<DashboardAnalysisAlgorithmFactor> factorList = (factors ?? new DashboardAnalysisAlgorithmFactor[0]).Where(factor => factor != null).ToList();
            model.AlgorithmTitle = title;
            model.AlgorithmSummary = summary;
            model.AlgorithmFactors = factorList;
            model.AlgorithmScore = factorList.Any() ? Math.Min(100, factorList.Sum(factor => Math.Max(0, factor.Score))) : 0;
        }

        private DashboardAnalysisAlgorithmFactor MakeFactor(string label, string value, string detail, int score, Color accent)
        {
            return new DashboardAnalysisAlgorithmFactor
            {
                Label = label,
                Value = value,
                Detail = detail,
                Score = score,
                Accent = accent
            };
        }

        private DashboardAnalysisModel CreateLoadingModel(string title, string subtitle, string badgeText, DashboardAnalysisBadgeVariant badgeVariant, params string[] metricLabels)
        {
            DashboardAnalysisModel model = new DashboardAnalysisModel
            {
                Title = title,
                Subtitle = subtitle,
                BadgeText = badgeText,
                BadgeVariant = badgeVariant
            };

            foreach (string label in metricLabels ?? new string[0])
            {
                model.Metrics.Add(new DashboardAnalysisMetric
                {
                    Label = label,
                    Value = "Loading...",
                    Subtitle = "Fetching live data",
                    Accent = Color.FromArgb(24, 95, 165)
                });
            }

            model.AlgorithmTitle = "Loading...";
            model.AlgorithmSummary = "Pulling live data from the database on a background thread.";
            return model;
        }

        private DashboardAnalysisDateRange GetAnalysisRange(string key)
        {
            DashboardAnalysisDateRange range;
            return _analysisRanges.TryGetValue(key, out range) ? range : DashboardAnalysisDateRange.ThisMonth;
        }

        private bool IsInDateRange(DateTime date, DashboardAnalysisDateRange range)
        {
            if (date == default(DateTime))
                return range == DashboardAnalysisDateRange.AllTime;

            DateTime start;
            DateTime end;
            GetDateRangeBounds(range, out start, out end);
            return range == DashboardAnalysisDateRange.AllTime || (date.Date >= start && date.Date <= end);
        }

        private void GetDateRangeBounds(DashboardAnalysisDateRange range, out DateTime start, out DateTime end)
        {
            DateTime today = DateTime.Today;
            end = today;
            switch (range)
            {
                case DashboardAnalysisDateRange.Last30Days:
                    start = today.AddDays(-29);
                    return;
                case DashboardAnalysisDateRange.QuarterToDate:
                    int quarterStartMonth = ((today.Month - 1) / 3) * 3 + 1;
                    start = new DateTime(today.Year, quarterStartMonth, 1);
                    return;
                case DashboardAnalysisDateRange.FinancialYear:
                    int fyStartYear = today.Month >= 4 ? today.Year : today.Year - 1;
                    start = new DateTime(fyStartYear, 4, 1);
                    end = new DateTime(fyStartYear + 1, 3, 31);
                    return;
                case DashboardAnalysisDateRange.AllTime:
                    start = DateTime.MinValue.Date;
                    end = DateTime.MaxValue.Date;
                    return;
                default:
                    start = new DateTime(today.Year, today.Month, 1);
                    end = start.AddMonths(1).AddDays(-1);
                    return;
            }
        }

        private int ClampScore(decimal score)
        {
            return (int)Math.Max(0m, Math.Min(100m, Math.Round(score, MidpointRounding.AwayFromZero)));
        }

        private string FormatPercentage(decimal percent)
        {
            return percent.ToString("N1") + "%";
        }

        private string NormalizeVendorName(string vendorName)
        {
            string normalized = Safe(vendorName, string.Empty).ToUpperInvariant();
            string[] prefixes = { "M/S", "MR", "MRS", "VENDOR:", "OR " };
            foreach (string prefix in prefixes)
            {
                if (normalized.StartsWith(prefix, StringComparison.Ordinal))
                    normalized = normalized.Substring(prefix.Length).Trim();
            }
            return string.IsNullOrWhiteSpace(normalized) ? "UNKNOWN VENDOR" : normalized;
        }

        private decimal QuoteValue(TenderBid quote)
        {
            if (quote == null)
                return 0m;
            return quote.TotalWithGST > 0 ? quote.TotalWithGST : quote.BidValue;
        }

        private bool IsQuoteSent(TenderBid quote)
        {
            string status = Safe(quote?.Status).ToUpperInvariant();
            return status == "SENT" || status == "SUBMITTED";
        }

        private bool IsQuoteWon(TenderBid quote)
        {
            return string.Equals(Safe(quote?.Status), "Won", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsQuoteLost(TenderBid quote)
        {
            return string.Equals(Safe(quote?.Status), "Lost", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsQuoteDraftOrAnalysed(TenderBid quote)
        {
            return !IsQuoteSent(quote) && !IsQuoteWon(quote) && !IsQuoteLost(quote);
        }

        private string ResolveQuoteStatus(TenderBid quote)
        {
            if (IsQuoteWon(quote))
                return "Won";
            if (IsQuoteLost(quote))
                return "Lost";
            if (IsQuoteSent(quote))
                return "Sent";
            return "Draft/Analysed";
        }

        private DateTime GetJobRelevantDate(Job job)
        {
            return job?.CompletedDate ?? job?.ScheduledDate ?? DateTime.MinValue;
        }

        private decimal GetJobMarginPct(Job job)
        {
            if (job == null || job.Revenue <= 0)
                return 0m;
            return Math.Round(((job.Revenue - job.EstimatedCost) / job.Revenue) * 100m, 1, MidpointRounding.AwayFromZero);
        }

        private string NormalizeJobStatus(string status)
        {
            string safe = Safe(status);
            if (string.Equals(safe, "InProgress", StringComparison.OrdinalIgnoreCase))
                return "In Progress";
            if (string.Equals(safe, "Pending", StringComparison.OrdinalIgnoreCase))
                return "Pending";
            if (string.Equals(safe, "Completed", StringComparison.OrdinalIgnoreCase))
                return "Completed";
            return safe;
        }

        private string ResolveQuotationScoreStatus(int score)
        {
            if (score < 40)
                return "Needs attention";
            if (score <= 70)
                return "Moderate";
            return "Healthy";
        }

        private Color ResolveQuotationScoreColor(int score)
        {
            if (score < 40)
                return Color.FromArgb(163, 45, 45);
            if (score <= 70)
                return Color.FromArgb(133, 79, 11);
            return AccentTeal;
        }

        private string ResolveRiskScoreStatus(int score)
        {
            if (score < 30)
                return "Healthy";
            if (score <= 60)
                return "Moderate";
            if (score <= 80)
                return "Elevated";
            return "Critical";
        }

        private Color ResolveRiskScoreColor(int score)
        {
            if (score < 30)
                return AccentTeal;
            if (score <= 80)
                return AccentOrange;
            return AccentRed;
        }

        private string GetDownloadsFolder()
        {
            string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (!Directory.Exists(downloads))
                Directory.CreateDirectory(downloads);
            return downloads;
        }

        private void ExportAnalysisCsv(DashboardAnalysisModel model)
        {
            if (model == null)
                return;

            try
            {
                string fileName = SanitizeFileName((model.Title ?? "analysis").Replace(" ", "_")) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
                string path = Path.Combine(GetDownloadsFolder(), fileName);
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("Section,Label,Value,Notes");
                foreach (DashboardAnalysisMetric metric in model.Metrics)
                    builder.AppendLine(ToCsvRow("Metric", metric.Label, metric.Value, metric.Subtitle));
                foreach (DashboardAnalysisAlgorithmFactor factor in model.AlgorithmFactors)
                    builder.AppendLine(ToCsvRow("Algorithm", factor.Label, factor.Value, factor.Detail));
                foreach (DashboardAnalysisFact fact in model.Facts.Concat(model.ComparisonFacts))
                    builder.AppendLine(ToCsvRow("Fact", fact.Label, fact.Value, string.Empty));
                foreach (DashboardAnalysisInsight insight in model.InsightCards)
                    builder.AppendLine(ToCsvRow("Insight", insight.Title, insight.Detail, string.Empty));
                foreach (string insight in model.Insights)
                    builder.AppendLine(ToCsvRow("Insight", "Insight", insight, string.Empty));
                foreach (DashboardAnalysisChart chart in model.Charts)
                {
                    foreach (DashboardAnalysisChartPoint point in chart.Points)
                        builder.AppendLine(ToCsvRow(chart.Title, point.Label, point.Value.ToString("0.##"), string.Empty));
                }
                foreach (DashboardAnalysisTable table in model.Tables)
                {
                    builder.AppendLine();
                    builder.AppendLine(EscapeCsv(table.Title ?? "Table"));
                    builder.AppendLine(string.Join(",", table.Columns.Select(EscapeCsv)));
                    foreach (string[] row in table.Rows)
                        builder.AppendLine(string.Join(",", row.Select(EscapeCsv)));
                }

                File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
                MessageBox.Show("CSV exported to " + path, BrandingService.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                LogAnalysisError("ExportAnalysisCsv", ex);
                MessageBox.Show("Export failed. The issue was logged in app logs.", BrandingService.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private string ToCsvRow(string section, string label, string value, string notes)
        {
            return string.Join(",", new[] { section, label, value, notes }.Select(EscapeCsv));
        }

        private string EscapeCsv(string value)
        {
            string safe = value ?? string.Empty;
            if (safe.Contains(",") || safe.Contains("\"") || safe.Contains("\n"))
                return "\"" + safe.Replace("\"", "\"\"") + "\"";
            return safe;
        }

        private string SanitizeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return value;
        }

        private void LogAnalysisError(string context, Exception ex)
        {
            try
            {
                string logDir = @"C:\HVAC_PRO_MSE\LOGS";
                Directory.CreateDirectory(logDir);
                File.AppendAllText(
                    Path.Combine(logDir, "dashboard_analysis.log"),
                    DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") + " | " + context + " | " + ex + Environment.NewLine + Environment.NewLine,
                    Encoding.UTF8);
            }
            catch { }
        }

        private string BuildExportText(DashboardAnalysisModel model)
        {
            if (model == null)
                return string.Empty;

            StringBuilder builder = new StringBuilder();
            builder.AppendLine(model.Title ?? "Analysis");
            if (!string.IsNullOrWhiteSpace(model.Subtitle))
                builder.AppendLine(model.Subtitle);
            if (!string.IsNullOrWhiteSpace(model.StatusText))
                builder.AppendLine("Status: " + model.StatusText);
            if (!string.IsNullOrWhiteSpace(model.SummaryText))
                builder.AppendLine(model.SummaryText);

            foreach (DashboardAnalysisMetric metric in model.Metrics)
                builder.AppendLine(metric.Label + ": " + metric.Value + (string.IsNullOrWhiteSpace(metric.Subtitle) ? string.Empty : " (" + metric.Subtitle + ")"));
            foreach (DashboardAnalysisFact fact in model.Facts)
                builder.AppendLine(fact.Label + ": " + fact.Value);
            foreach (string insight in model.Insights)
                builder.AppendLine("- " + insight);

            return builder.ToString().Trim();
        }

        private void CopyText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            try
            {
                Clipboard.SetText(text);
                MessageBox.Show("Dashboard summary copied to clipboard.", BrandingService.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                LogAnalysisError("CopyText", ex);
                MessageBox.Show("Copy failed. The issue was logged in app logs.", BrandingService.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private string ResolveTopVendorExposureText(IEnumerable<PurchaseOrder> records)
        {
            var topVendor = records
                .GroupBy(po => string.IsNullOrWhiteSpace(po.VendorName) ? "Vendor #" + po.VendorID : po.VendorName)
                .Select(group => new { Name = group.Key, Balance = group.Sum(item => item.BalanceDue) })
                .OrderByDescending(group => group.Balance)
                .FirstOrDefault();

            return topVendor == null ? "-" : topVendor.Name + " | " + FormatCurrency(topVendor.Balance);
        }

        private static string ResolvePurchaseStatus(PurchaseOrder po)
        {
            if (po == null)
                return "-";
            if (po.BalanceDue <= 0.01m || string.Equals(po.Status, "Paid", StringComparison.OrdinalIgnoreCase))
                return "Paid";
            if (po.IsOverdue)
                return "Overdue";
            if (string.Equals(po.Status, "Received", StringComparison.OrdinalIgnoreCase))
                return "Received";
            if (po.PendingChargeCreated)
                return "Billing queued";
            return Safe(po.Status, "Created");
        }

        private Color ResolvePurchaseColor(PurchaseOrder po)
        {
            string status = ResolvePurchaseStatus(po);
            if (status == "Paid")
                return AccentGreen;
            if (status == "Overdue")
                return AccentRed;
            if (status == "Received")
                return AccentTeal;
            if (status == "Billing queued")
                return AccentOrange;
            return AccentBlue;
        }

        private string ResolveClientName(int clientId)
        {
            B2BClient client = _clients.FirstOrDefault(item => item.ClientID == clientId);
            return client?.CompanyName ?? ("Client #" + clientId);
        }

        private static string Safe(string value, string fallback = "-")
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string DateText(DateTime date)
        {
            return date == default ? "-" : date.ToString("dd MMM yyyy");
        }

        private static string DateText(DateTime? date)
        {
            return date.HasValue ? DateText(date.Value) : "-";
        }

        private static string FormatCurrency(decimal value)
        {
            return "\u20B9" + value.ToString("N0", CultureInfo.GetCultureInfo("en-IN"));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public partial class DashboardForm
    {
        private readonly ContractService _actionContractService = new ContractService();
        private readonly ActionCenterService _actionCenterService = new ActionCenterService();
        private readonly ActionCenterRepository _actionCenterRepository = new ActionCenterRepository();
        private List<AMCContract> _actionContracts = new List<AMCContract>();
        private ActionCenterInput _actionInput;
        private ActionCenterSnapshot _actionSnapshot = new ActionCenterSnapshot();
        private string _actionFilter = "All";
        private bool _actionOpening;
        private bool _actionCenterPreviewMode;
        private bool _actionTimerRefreshRunning;
        private DateTime _nextActionRefreshAt;

        private async void RefreshActionCenterOnTimer()
        {
            if (_actionCenterPreviewMode || _actionTimerRefreshRunning || DateTime.Now < _nextActionRefreshAt || IsDisposed)
                return;

            _actionTimerRefreshRunning = true;
            _nextActionRefreshAt = DateTime.Now.AddMinutes(5);
            try
            {
                await RefreshDashboardDataAsync();
            }
            finally
            {
                _actionTimerRefreshRunning = false;
            }
        }

        public void LoadActionCenterPreviewForVisualTest()
        {
            _actionCenterPreviewMode = true;
            DateTime now = DateTime.Now;
            SessionManager.SetSession(new AppUserDto
            {
                UserId = 1,
                Username = "manager",
                DisplayName = "Harshal Sonawane",
                RoleName = "Manager",
                IsActive = true
            });
            _jobs = new List<Job>
            {
                new Job { JobID = 101, JobNumber = "JOB-1042", ClientID = 1, SiteID = 1, ClientName = "ABC Industries",
                    SiteName = "Chakan Plant", JobTitle = "AC breakdown", Priority = "Critical", PipelineStatus = "Created",
                    ScheduledDate = now.AddMinutes(-43), CreatedDate = now.AddHours(-1) },
                new Job { JobID = 102, JobNumber = "JOB-1043", ClientID = 2, SiteID = 2, ClientName = "Nimbus Foods",
                    SiteName = "Cold Store", JobTitle = "Preventive maintenance", Priority = "High", PipelineStatus = "Assigned",
                    AssignedEmployeeID = 7, AssignedEmployeeName = "Ravi Patil", ScheduledDate = now.Date.AddHours(14), CreatedDate = now.AddDays(-1) },
                new Job { JobID = 103, JobNumber = "JOB-1038", ClientID = 3, SiteID = 3, ClientName = "Vertex Pharma",
                    SiteName = "Unit 2", JobTitle = "Compressor replacement", PipelineStatus = "Completed", CompletedDate = now.AddHours(-2),
                    ScheduledDate = now.AddDays(-1), QuotedRevenue = 48000m, CreatedDate = now.AddDays(-3) }
            };
            _serviceTickets = new List<ServiceDeskIncident>
            {
                new ServiceDeskIncident { IncidentId = 201, IncidentNumber = "INC-2026-0091", ClientName = "ABC Industries",
                    SiteName = "Chakan Plant", Priority = "Critical", Status = "Open", ShortDescription = "Production AHU stopped",
                    OpenedAt = now.AddMinutes(-35), SlaDueAt = now.AddMinutes(42) }
            };
            _invoices = new List<Invoice>
            {
                new Invoice { InvoiceID = 301, InvoiceNumber = "INV-2026-0831", ClientName = "Orion Labs", SiteName = "Pune",
                    InvoiceDate = now.AddDays(-45), DueDate = now.AddDays(-15), TotalAmount = 145000m, PaidAmount = 25000m,
                    BalanceDue = 120000m, PaymentStatus = "Partial" }
            };
            _quotations = new List<TenderBid>
            {
                new TenderBid { BidID = 401, QuotationNumber = "QUO-2026-0448", ClientName = "Metro Retail", SiteName = "Baner",
                    Status = "Submitted", SubmittedDate = now.AddDays(-8), DueDate = now.AddDays(7), TotalWithGST = 286000m }
            };
            _purchaseOrders = new List<PurchaseOrder>
            {
                new PurchaseOrder { POID = 501, PONumber = "PO-2026-0194", VendorName = "CoolTech Supplies", Status = "Pending Approval",
                    PODate = now.AddDays(-1), PayByDate = now.AddDays(14), TotalAmount = 68000m }
            };
            _inventory = new List<StockItem>
            {
                new StockItem { ItemID = 601, ItemName = "5 TR Compressor", Unit = "Nos", CurrentStock = 0m, ReservedStock = 0m,
                    ReorderLevel = 2m, LastPurchaseRate = 32000m, LastUpdated = now, IsActive = true }
            };
            _actionContracts = new List<AMCContract>
            {
                new AMCContract { ContractID = 701, ClientID = 5, SiteID = 5, ContractType = "AMC", ContractStatus = "Active",
                    StartDate = now.AddYears(-1), EndDate = now.AddDays(21), AnnualValue = 180000m }
            };
            _payments = new List<Payment>
            {
                new Payment { PaymentID = 801, PaymentNumber = "PAY-2026-09-00142", ClientID = 4, ClientName = "Orion Labs",
                    AmountPaid = 85000m, PaymentDate = now.AddDays(-4), CreatedDate = now.AddDays(-4), ReconciliationStatus = "Unreconciled" }
            };
            _actionInput = new ActionCenterInput
            {
                Jobs = _jobs, ServiceIncidents = _serviceTickets, Contracts = _actionContracts, Quotations = _quotations,
                Invoices = _invoices, PurchaseOrders = _purchaseOrders, InventoryItems = _inventory, Payments = _payments,
                AmcVisits = new List<ActionCenterAmcVisit>
                {
                    new ActionCenterAmcVisit { VisitId = 901, ContractId = 701, VisitNumber = 3, ScheduledDate = now.Date,
                        Status = "Scheduled", ClientName = "Pinnacle Textiles", SiteName = "Plant 2" }
                },
                PartShortages = new List<ActionCenterPartShortage>
                {
                    new ActionCenterPartShortage { PartUsedId = 902, JobId = 101, JobNumber = "JOB-1042", ItemDescription = "5 TR Compressor",
                        RequiredQuantity = 1m, AvailableQuantity = 0m, Unit = "Nos", ClientName = "ABC Industries", SiteName = "Chakan Plant",
                        ScheduledDate = now.AddMinutes(-43) }
                },
                User = SessionManager.CurrentUser, Now = now
            };
            BuildShell();
        }

        private void AddActionCenter()
        {
            _actionSnapshot = BuildActionSnapshot();
            int width = ContentWidth();
            AddActionSummary(width);

            if (_actionSnapshot.Items.Count == 0)
            {
                AddActionEmptyState(width);
                return;
            }

            AddRecommendedAction(width, _actionSnapshot.RecommendedNextAction);
            AddActionFilters(width);

            List<ActionCenterItem> filtered = FilterActions(_actionSnapshot.Items).ToList();
            Panel firstRow = new Panel { Width = width, Height = 374, BackColor = DS.BgPage, Margin = new Padding(0, 0, 0, 10) };
            int gap = 12;
            int columnWidth = (width - gap) / 2;
            Panel attention = BuildActionSection("Needs attention", "Critical, overdue and SLA-risk work.",
                filtered.Where(item => item.Bucket == ActionCenterBucket.NeedsAttention), columnWidth, 374, 4);
            Panel mine = BuildActionSection("My actions", "Work you or your role can move forward.",
                filtered.Where(item => item.Bucket == ActionCenterBucket.MyActions ||
                    (item.IsMine && item.Bucket != ActionCenterBucket.NeedsAttention && item.Bucket != ActionCenterBucket.Today &&
                     item.Bucket != ActionCenterBucket.Waiting && item.Bucket != ActionCenterBucket.Upcoming)),
                columnWidth, 374, 4);
            attention.Location = Point.Empty;
            mine.Location = new Point(columnWidth + gap, 0);
            firstRow.Controls.Add(attention);
            firstRow.Controls.Add(mine);
            _root.Controls.Add(firstRow);

            Panel secondRow = new Panel { Width = width, Height = 374, BackColor = DS.BgPage, Margin = new Padding(0, 0, 0, 10) };
            Panel today = BuildActionSection("Today's work", "Scheduled work and commitments due today.",
                filtered.Where(item => item.Bucket == ActionCenterBucket.Today || (item.DueAt.HasValue && item.DueAt.Value.Date == DateTime.Today)),
                columnWidth, 374, 4);
            Panel waitingUpcoming = BuildActionSection("Waiting & upcoming", "External responses and the next obligations.",
                filtered.Where(item => item.Bucket == ActionCenterBucket.Waiting || item.Bucket == ActionCenterBucket.Upcoming),
                columnWidth, 374, 4);
            today.Location = Point.Empty;
            waitingUpcoming.Location = new Point(columnWidth + gap, 0);
            secondRow.Controls.Add(today);
            secondRow.Controls.Add(waitingUpcoming);
            _root.Controls.Add(secondRow);
        }

        private ActionCenterSnapshot BuildActionSnapshot()
        {
            if (_actionInput != null)
            {
                _actionInput.User = SessionManager.CurrentUser;
                _actionInput.Now = DateTime.Now;
                return _actionCenterService.Build(_actionInput);
            }
            return _actionCenterService.Build(new ActionCenterInput
            {
                Jobs = _jobs,
                ServiceIncidents = _serviceTickets,
                Contracts = _actionContracts,
                Quotations = _quotations,
                Invoices = _invoices,
                PurchaseOrders = _purchaseOrders,
                InventoryItems = _inventory,
                Payments = _payments,
                User = SessionManager.CurrentUser,
                Now = DateTime.Now
            });
        }

        private void LoadActionCenterProjection()
        {
            try
            {
                _actionInput = _actionCenterRepository.GetActionable(SessionManager.CurrentUser, DateTime.Now);
            }
            catch (Exception ex)
            {
                _actionInput = null;
                AppLogger.LogError("DashboardForm.LoadActionCenterProjection", ex);
            }
        }

        private void AddActionSummary(int width)
        {
            Panel card = CardPanel(width, 108);
            card.Name = "ActionCenterSummary";
            Label workspaceIcon = ModernIconSystem.Badge(ModernIconKind.Checklist, 48, DS.Primary50, DS.Primary600, 12);
            workspaceIcon.Location = new Point(20, 25);
            Label title = new Label
            {
                Text = _actionSnapshot.WorkspaceName ?? "Operational command center",
                Location = new Point(82, 23),
                Size = new Size(Math.Max(260, width - 670), 27),
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = DS.Slate900,
                AutoEllipsis = true
            };
            Label refreshed = new Label
            {
                Text = "Live business state  •  Updated " + _actionSnapshot.GeneratedAt.ToString("hh:mm tt") + "  •  Auto-refresh every 5 minutes",
                Location = new Point(82, 53),
                Size = new Size(Math.Max(300, width - 670), 20),
                Font = new Font("Segoe UI", 8.2f),
                ForeColor = DS.Slate500
            };
            int statLeft = Math.Max(420, width - 550);
            AddActionSummaryMetric(card, statLeft, "Critical", _actionSnapshot.CriticalCount, DS.Red600);
            AddActionSummaryMetric(card, statLeft + 108, "Overdue", _actionSnapshot.OverdueCount, DS.Amber600);
            AddActionSummaryMetric(card, statLeft + 216, "Today", _actionSnapshot.TodayCount, DS.Primary600);
            AddActionSummaryMetric(card, statLeft + 324, "Waiting", _actionSnapshot.WaitingCount, DS.Teal600);
            AddActionSummaryMetric(card, statLeft + 432, "Upcoming", _actionSnapshot.UpcomingCount, DS.Green600);
            card.Controls.Add(workspaceIcon);
            card.Controls.Add(title);
            card.Controls.Add(refreshed);
            _root.Controls.Add(card);
        }

        private static void AddActionSummaryMetric(Control parent, int x, string label, int value, Color color)
        {
            Panel metric = new Panel
            {
                Location = new Point(x, 18),
                Size = new Size(98, 70),
                BackColor = Blend(color, 0.93f)
            };
            DS.Rounded(metric, 10);
            metric.Controls.Add(new Label
            {
                Text = value.ToString("N0"),
                Location = new Point(8, 9),
                Size = new Size(82, 28),
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                ForeColor = color,
                TextAlign = ContentAlignment.MiddleCenter
            });
            metric.Controls.Add(new Label
            {
                Text = label,
                Location = new Point(8, 40),
                Size = new Size(82, 18),
                Font = new Font("Segoe UI", 7.8f, FontStyle.Bold),
                ForeColor = DS.Slate600,
                TextAlign = ContentAlignment.MiddleCenter
            });
            parent.Controls.Add(metric);
        }

        private void AddRecommendedAction(int width, ActionCenterItem item)
        {
            if (item == null)
                return;

            Panel card = CardPanel(width, 150);
            card.Name = "RecommendedNextAction";
            card.BackColor = Blend(PriorityColor(item), 0.965f);
            Panel accent = new Panel { Dock = DockStyle.Left, Width = 6, BackColor = PriorityColor(item) };
            card.Controls.Add(accent);

            Label icon = ModernIconSystem.Badge(ActionIcon(item), 42, Blend(PriorityColor(item), 0.9f), PriorityColor(item), 11);
            icon.Location = new Point(22, 50);
            card.Controls.Add(icon);

            card.Controls.Add(new Label
            {
                Text = "RECOMMENDED NEXT ACTION",
                Location = new Point(78, 14),
                Size = new Size(270, 18),
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = PriorityColor(item)
            });
            card.Controls.Add(new Label
            {
                Text = item.Title,
                Location = new Point(78, 36),
                Size = new Size(Math.Max(330, width - 310), 28),
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = DS.Slate900,
                AutoEllipsis = true
            });
            card.Controls.Add(new Label
            {
                Text = ActionContext(item),
                Location = new Point(78, 67),
                Size = new Size(Math.Max(330, width - 310), 20),
                Font = new Font("Segoe UI", 9f),
                ForeColor = DS.Slate700,
                AutoEllipsis = true
            });
            card.Controls.Add(new Label
            {
                Text = item.PriorityReason,
                Location = new Point(78, 92),
                Size = new Size(Math.Max(330, width - 310), 20),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = PriorityColor(item),
                AutoEllipsis = true
            });
            card.Controls.Add(new Label
            {
                Text = DueText(item),
                Location = new Point(78, 116),
                Size = new Size(Math.Max(330, width - 310), 18),
                Font = new Font("Segoe UI", 8f),
                ForeColor = DS.Slate500,
                AutoEllipsis = true
            });
            Button action = PrimaryButton(item.SuggestedAction, Math.Max(24, width - 188), 53, 156, 44);
            action.Name = "ConfirmRecommendedActionButton";
            action.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            action.Click += async (s, e) => await OpenActionAsync(item, action);
            card.Controls.Add(action);
            _root.Controls.Add(card);
        }

        private void AddActionFilters(int width)
        {
            Panel strip = CardPanel(width, 64);
            strip.Name = "ActionCenterFilters";
            Label filterIcon = ModernIconSystem.Badge(ModernIconKind.Filter, 30, DS.Slate100, DS.Slate600, 8);
            filterIcon.Location = new Point(16, 16);
            strip.Controls.Add(new Label
            {
                Text = "Filter work",
                Location = new Point(54, 22),
                Size = new Size(76, 18),
                Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
                ForeColor = DS.Slate600
            });
            strip.Controls.Add(filterIcon);
            string[] filters = { "All", "Mine", "Critical", "Today", "Overdue", "Upcoming", "Waiting" };
            int chipX = 136;
            foreach (string option in filters)
            {
                Label chip = FilterChip(option, chipX, 15, string.Equals(_actionFilter, option, StringComparison.OrdinalIgnoreCase));
                chip.Click += (s, e) =>
                {
                    _actionFilter = option;
                    BeginInvoke((Action)BuildShell);
                };
                strip.Controls.Add(chip);
                chipX += chip.Width + 7;
            }
            Button refresh = SecondaryButton("Refresh", Math.Max(chipX + 12, width - 122), 14, 100, 36);
            refresh.Name = "ActionCenterRefresh";
            refresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            refresh.Click += async (s, e) =>
            {
                refresh.Enabled = false;
                await RefreshDashboardDataAsync();
            };
            strip.Controls.Add(refresh);
            _root.Controls.Add(strip);
        }

        private static Label FilterChip(string text, int x, int y, bool selected)
        {
            int width = Math.Max(54, TextRenderer.MeasureText(text, new Font("Segoe UI", 8f, FontStyle.Bold)).Width + 22);
            var chip = new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 34),
                BackColor = selected ? DS.Primary600 : DS.Slate50,
                ForeColor = selected ? Color.White : DS.Slate700,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            chip.MouseEnter += (s, e) => { if (!selected) chip.BackColor = DS.Primary50; };
            chip.MouseLeave += (s, e) => { if (!selected) chip.BackColor = DS.Slate50; };
            DS.Rounded(chip, 8);
            return chip;
        }

        private Panel BuildActionSection(string title, string subtitle, IEnumerable<ActionCenterItem> source, int width, int height, int take)
        {
            Panel card = CardPanel(width, height);
            List<ActionCenterItem> items = (source ?? Enumerable.Empty<ActionCenterItem>())
                .OrderByDescending(item => item.PriorityScore)
                .ThenBy(item => item.DueAt ?? DateTime.MaxValue)
                .Take(take)
                .ToList();

            ModernIconKind sectionIcon = title.IndexOf("attention", StringComparison.OrdinalIgnoreCase) >= 0 ? ModernIconKind.Alert :
                title.IndexOf("Today", StringComparison.OrdinalIgnoreCase) >= 0 ? ModernIconKind.Calendar :
                title.IndexOf("Waiting", StringComparison.OrdinalIgnoreCase) >= 0 ? ModernIconKind.Activity : ModernIconKind.Checklist;
            Color sectionColor = title.IndexOf("attention", StringComparison.OrdinalIgnoreCase) >= 0 ? DS.Red600 :
                title.IndexOf("Today", StringComparison.OrdinalIgnoreCase) >= 0 ? DS.Primary600 :
                title.IndexOf("Waiting", StringComparison.OrdinalIgnoreCase) >= 0 ? DS.Teal600 : DS.Primary600;
            Label icon = ModernIconSystem.Badge(sectionIcon, 34, Blend(sectionColor, 0.91f), sectionColor, 9);
            icon.Location = new Point(16, 14);
            card.Controls.Add(icon);

            card.Controls.Add(new Label
            {
                Text = title,
                Location = new Point(60, 13),
                Size = new Size(width - 150, 22),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = DS.Slate900,
                UseMnemonic = false
            });
            card.Controls.Add(new Label
            {
                Text = subtitle,
                Location = new Point(60, 35),
                Size = new Size(width - 150, 18),
                Font = new Font("Segoe UI", 8f),
                ForeColor = DS.Slate500,
                AutoEllipsis = true
            });
            Label count = new Label
            {
                Text = items.Count == 1 ? "1 item" : items.Count + " items",
                Location = new Point(width - 88, 20),
                Size = new Size(68, 24),
                BackColor = DS.Slate100,
                ForeColor = DS.Slate600,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            DS.Rounded(count, 8);
            card.Controls.Add(count);

            if (items.Count == 0)
            {
                card.Controls.Add(new Label
                {
                    Text = "You're clear here — nothing needs attention.",
                    Location = new Point(20, 96),
                    Size = new Size(width - 36, 24),
                    Font = new Font("Segoe UI", 8.5f),
                    ForeColor = DS.Slate500
                });
                return card;
            }

            int y = 70;
            foreach (ActionCenterItem item in items)
            {
                Panel row = BuildActionRow(item, width - 36);
                row.Location = new Point(18, y);
                card.Controls.Add(row);
                y += row.Height + 8;
            }
            return card;
        }

        private Panel BuildActionRow(ActionCenterItem item, int width)
        {
            Panel row = new Panel { Size = new Size(width, 68), BackColor = Color.White, Cursor = Cursors.Hand };
            row.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = DS.RoundedRect(new Rectangle(0, 0, row.Width - 1, row.Height - 1), 8))
                using (var pen = new Pen(DS.Slate200))
                    e.Graphics.DrawPath(pen, path);
            };
            row.MouseEnter += (s, e) => row.BackColor = DS.Slate50;
            row.MouseLeave += (s, e) => row.BackColor = Color.White;
            Panel priority = new Panel { Location = new Point(0, 0), Size = new Size(4, 68), BackColor = PriorityColor(item) };
            Label icon = ModernIconSystem.Badge(ActionIcon(item), 28, Blend(PriorityColor(item), 0.91f), PriorityColor(item), 8);
            icon.Location = new Point(12, 20);
            Label title = new Label
            {
                Text = item.Title,
                Location = new Point(50, 7),
                Size = new Size(Math.Max(120, width - 190), 19),
                Font = new Font("Segoe UI", 8.6f, FontStyle.Bold),
                ForeColor = DS.Slate900,
                AutoEllipsis = true
            };
            Label context = new Label
            {
                Text = ActionContext(item),
                Location = new Point(50, 27),
                Size = new Size(Math.Max(120, width - 190), 16),
                Font = new Font("Segoe UI", 7.8f),
                ForeColor = DS.Slate700,
                AutoEllipsis = true
            };
            Label reason = new Label
            {
                Text = item.PriorityReason,
                Location = new Point(50, 45),
                Size = new Size(Math.Max(120, width - 190), 15),
                Font = new Font("Segoe UI", 7.4f),
                ForeColor = PriorityColor(item),
                AutoEllipsis = true
            };
            Button action = SecondaryButton(item.SuggestedAction, Math.Max(16, width - 132), 17, 118, 34);
            action.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            action.Font = new Font("Segoe UI", 7.6f, FontStyle.Bold);
            action.ForeColor = PriorityColor(item);
            action.FlatAppearance.BorderColor = Blend(PriorityColor(item), 0.55f);
            action.FlatAppearance.MouseOverBackColor = Blend(PriorityColor(item), 0.92f);
            action.Click += async (s, e) => await OpenActionAsync(item, action);
            row.Controls.Add(priority);
            row.Controls.Add(icon);
            row.Controls.Add(title);
            row.Controls.Add(context);
            row.Controls.Add(reason);
            row.Controls.Add(action);
            return row;
        }

        private void AddActionEmptyState(int width)
        {
            Panel card = CardPanel(width, 210);
            card.Name = "ActionCenterEmptyState";
            card.Controls.Add(new Label
            {
                Text = "Nothing requires your attention yet.",
                Location = new Point(24, 34),
                Size = new Size(width - 48, 32),
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = DS.Slate900,
                TextAlign = ContentAlignment.MiddleCenter
            });
            card.Controls.Add(new Label
            {
                Text = "ServoERP will build this queue automatically as jobs, quotations, contracts, invoices and stock begin moving.",
                Location = new Point(60, 72),
                Size = new Size(width - 120, 38),
                Font = new Font("Segoe UI", 8.8f),
                ForeColor = DS.Slate600,
                TextAlign = ContentAlignment.MiddleCenter
            });
            var buttons = new List<Button>();
            if (SessionManager.HasPermission("Clients", "Create")) buttons.Add(EmptyAction("Add Client", 1, null));
            if (SessionManager.HasPermission("Quotations", "Create")) buttons.Add(EmptyAction("Create Quotation", 0, ShortcutNewQuotation));
            if (SessionManager.HasPermission("WorkOrders", "Create")) buttons.Add(EmptyAction("Create Job", 0, ShortcutNewJob));
            if (SessionManager.HasPermission("Contracts", "Create")) buttons.Add(EmptyAction("Create Contract", 0, ShortcutNewAMC));
            int total = buttons.Count * 142 + Math.Max(0, buttons.Count - 1) * 10;
            int x = Math.Max(24, (width - total) / 2);
            foreach (Button button in buttons)
            {
                button.Location = new Point(x, 132);
                card.Controls.Add(button);
                x += 152;
            }
            _root.Controls.Add(card);
        }

        private Button EmptyAction(string text, int navigationIndex, string shortcut)
        {
            Button button = PrimaryButton(text, 0, 0, 142, 38);
            button.Click += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(shortcut)) OnShortcut?.Invoke(shortcut);
                else OnNavigate?.Invoke(navigationIndex);
            };
            return button;
        }

        private IEnumerable<ActionCenterItem> FilterActions(IEnumerable<ActionCenterItem> source)
        {
            IEnumerable<ActionCenterItem> query = source ?? Enumerable.Empty<ActionCenterItem>();
            switch ((_actionFilter ?? "All").Trim())
            {
                case "Mine": return query.Where(item => item.IsMine);
                case "Critical": return query.Where(item => item.Priority == ActionCenterPriority.Critical);
                case "Today": return query.Where(item => item.Bucket == ActionCenterBucket.Today || (item.DueAt.HasValue && item.DueAt.Value.Date == DateTime.Today));
                case "Overdue": return query.Where(item => item.IsOverdue);
                case "Upcoming": return query.Where(item => item.Bucket == ActionCenterBucket.Upcoming);
                case "Waiting": return query.Where(item => item.IsWaiting);
                default: return query;
            }
        }

        private async Task OpenActionAsync(ActionCenterItem original, Button source)
        {
            if (_actionOpening || original == null)
                return;
            _actionOpening = true;
            source.Enabled = false;
            try
            {
                await Task.Run((Action)(() =>
                {
                    LoadActionCenterProjection();
                    if (_actionInput == null)
                        LoadData();
                }));
                ActionCenterSnapshot current = BuildActionSnapshot();
                ActionCenterItem valid = current.Items.FirstOrDefault(item => string.Equals(item.ActionId, original.ActionId, StringComparison.OrdinalIgnoreCase));
                if (valid == null)
                {
                    _actionSnapshot = current;
                    BuildShell();
                    ToastNotification.ShowToast("This item was already resolved or changed on another PC.", DS.Green600);
                    return;
                }
                SessionManager.LogAction("OPEN", "ActionCenter", valid.SourceRecordId,
                    "Opened " + valid.ActionId + " from My Work. Priority reason: " + valid.PriorityReason);
                OnOpenAction?.Invoke(valid);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("DashboardForm.ActionCenter.OpenAction", ex);
                MessageBox.Show(this, "ServoERP could not open this work item. Refresh My Work and try again.",
                    BrandingService.WindowTitle("My Work"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _actionOpening = false;
                if (source != null && !source.IsDisposed) source.Enabled = true;
            }
        }

        private static string ActionContext(ActionCenterItem item)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(item.Reference)) parts.Add(item.Reference);
            if (!string.IsNullOrWhiteSpace(item.ClientName)) parts.Add(item.ClientName);
            if (!string.IsNullOrWhiteSpace(item.SiteName)) parts.Add(item.SiteName);
            if (parts.Count == 1 && !string.IsNullOrWhiteSpace(item.Explanation)) parts.Add(item.Explanation);
            return string.Join("  •  ", parts);
        }

        private static string DueText(ActionCenterItem item)
        {
            if (item.SlaDeadline.HasValue)
                return "SLA deadline: " + item.SlaDeadline.Value.ToString("dd/MM/yyyy hh:mm tt");
            if (item.DueAt.HasValue)
                return "Due: " + item.DueAt.Value.ToString("dd/MM/yyyy hh:mm tt");
            return item.CurrentStatus;
        }

        private static Color PriorityColor(ActionCenterItem item)
        {
            if (item.Priority == ActionCenterPriority.Critical) return DS.Red600;
            if (item.Priority == ActionCenterPriority.Urgent) return DS.Amber600;
            if (item.Priority == ActionCenterPriority.Today) return DS.Primary600;
            return DS.Green600;
        }

        private static ModernIconKind ActionIcon(ActionCenterItem item)
        {
            switch ((item == null ? string.Empty : item.SourceModule ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "JOBS": return item != null && item.ActionType == "ProcureJobPart" ? ModernIconKind.Parts : ModernIconKind.Job;
                case "SERVICEDESK": return ModernIconKind.Service;
                case "INVOICES": return ModernIconKind.Invoice;
                case "PAYMENTS": return ModernIconKind.Payment;
                case "PURCHASES": return ModernIconKind.Purchase;
                case "INVENTORY": return ModernIconKind.Inventory;
                case "CONTRACTS": return ModernIconKind.Contract;
                case "QUOTATIONS": return ModernIconKind.Document;
                default: return ModernIconKind.Activity;
            }
        }
    }
}

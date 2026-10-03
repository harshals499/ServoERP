using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using ServoERP.Infrastructure;

namespace HVAC_Pro_Desktop.UI
{
    /// <summary>Human-reviewed conversion of an accepted quotation into delivery-ready records.</summary>
    public sealed class QuotationDeliveryWizardForm : ServoFormBase
    {
        private readonly QuotationDeliveryService _service = new QuotationDeliveryService();
        private readonly int _quotationId;
        private readonly QuotationDeliveryPlan _previewPlan;
        private Label _quotationContext;
        private Label _status;
        private Label _jobMetric;
        private Label _materialMetric;
        private Label _purchaseMetric;
        private Label _billingMetric;
        private DataGridView _checklistGrid;
        private DataGridView _materialGrid;
        private DataGridView _purchaseGrid;
        private DataGridView _billingGrid;
        private RichTextBox _reviewNotes;
        private Button _confirmButton;
        private QuotationDeliveryPlan _plan;
        private bool _busy;

        public QuotationDeliveryResult Result { get; private set; }

        public QuotationDeliveryWizardForm(int quotationId)
            : this(quotationId, null)
        {
        }

        internal QuotationDeliveryWizardForm(QuotationDeliveryPlan previewPlan)
            : this(previewPlan == null ? 0 : previewPlan.QuotationId, previewPlan)
        {
        }

        private QuotationDeliveryWizardForm(int quotationId, QuotationDeliveryPlan previewPlan)
        {
            _quotationId = quotationId;
            _previewPlan = previewPlan;
            BuildLayout();
            Shown += async (s, e) => await LoadPlanAsync();
        }

        private void BuildLayout()
        {
            Text = BrandingService.WindowTitle("Accepted Quote to Delivery");
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1180, 780);
            MinimumSize = new Size(980, 660);
            BackColor = DS.BgPage;
            Font = DS.Body;
            AutoScaleMode = AutoScaleMode.Dpi;

            Panel header = new Panel { Name = "DeliveryWizardDialogHeader", Tag = "dialog no-global-actions", Dock = DockStyle.Top, Height = 122, BackColor = DS.White, Padding = new Padding(24, 16, 24, 12) };
            header.Controls.Add(new Label
            {
                Text = "Accepted Quote → Delivery Wizard",
                Location = new Point(24, 14),
                Size = new Size(520, 32),
                Font = DS.H1,
                ForeColor = DS.Slate900
            });
            header.Controls.Add(new Label
            {
                Text = "Review every output before ServoERP creates the job and delivery records.",
                Location = new Point(26, 48),
                Size = new Size(650, 24),
                Font = DS.Body,
                ForeColor = DS.Slate600
            });
            _quotationContext = new Label
            {
                Text = "Loading accepted quotation…",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(26, 76),
                Size = new Size(1100, 24),
                Font = DS.SmallBold,
                ForeColor = DS.Slate800,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            header.Controls.Add(_quotationContext);

            TableLayoutPanel metrics = new TableLayoutPanel
            {
                Name = "DeliveryMetricStrip",
                Tag = "metric",
                Dock = DockStyle.Top,
                Height = 94,
                BackColor = DS.BgPage,
                Padding = new Padding(18, 12, 18, 8),
                ColumnCount = 4,
                RowCount = 1
            };
            for (int i = 0; i < 4; i++) metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _jobMetric = AddMetric(metrics, 0, "JOB", "Pending review");
            _materialMetric = AddMetric(metrics, 1, "MATERIAL PLAN", "0 line(s)");
            _purchaseMetric = AddMetric(metrics, 2, "DRAFT PURCHASE NEEDS", "0 line(s)");
            _billingMetric = AddMetric(metrics, 3, "BILLING SCHEDULE", "0 milestone(s)");

            TabControl tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = DS.Body,
                Padding = new Point(16, 8)
            };
            _checklistGrid = BuildChecklistGrid();
            _materialGrid = BuildMaterialGrid();
            _purchaseGrid = BuildPurchaseGrid();
            _billingGrid = BuildBillingGrid();
            tabs.TabPages.Add(BuildTab("1  Job & checklist", _checklistGrid));
            tabs.TabPages.Add(BuildTab("2  Material reservation", _materialGrid));
            tabs.TabPages.Add(BuildTab("3  Draft purchase needs", _purchaseGrid));
            tabs.TabPages.Add(BuildTab("4  Billing schedule", _billingGrid));
            TabPage review = new TabPage("5  Review") { BackColor = DS.White, Padding = new Padding(18) };
            _reviewNotes = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = DS.White,
                ForeColor = DS.Slate700,
                Font = DS.Body,
                DetectUrls = false,
                TabStop = false
            };
            review.Controls.Add(_reviewNotes);
            tabs.TabPages.Add(review);

            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 76, BackColor = DS.White, Padding = new Padding(24, 17, 24, 12) };
            _status = new Label
            {
                Text = "Preparing delivery plan…",
                Location = new Point(24, 26),
                Size = new Size(690, 24),
                Font = DS.Small,
                ForeColor = DS.Slate600,
                AutoEllipsis = true
            };
            footer.Controls.Add(_status);
            _confirmButton = MakeButton("Confirm & create", DS.Primary600, Color.White, 164);
            _confirmButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _confirmButton.Location = new Point(818, 17);
            _confirmButton.Enabled = false;
            _confirmButton.Click += async (s, e) => await ConfirmAsync();
            footer.Controls.Add(_confirmButton);
            Button close = MakeButton("Close", DS.White, DS.Slate700, 110);
            close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            close.Location = new Point(996, 17);
            close.Click += (s, e) => Close();
            footer.Controls.Add(close);
            footer.Resize += (s, e) =>
            {
                close.Left = footer.ClientSize.Width - close.Width - 24;
                _confirmButton.Left = close.Left - _confirmButton.Width - 12;
            };

            Controls.Add(tabs);
            Controls.Add(metrics);
            Controls.Add(footer);
            Controls.Add(header);
        }

        private async Task LoadPlanAsync()
        {
            SetBusy(true, "Building the delivery plan…");
            try
            {
                _plan = _previewPlan ?? await Task.Run(() => _service.BuildPlan(_quotationId));
                BindPlan();
                if (_plan.ExistingJobId.HasValue)
                {
                    _confirmButton.Text = "Already created";
                    _confirmButton.Enabled = false;
                    SetStatus("This quotation is already linked to " + First(_plan.ExistingJobNumber, "job #" + _plan.ExistingJobId.Value) + ".", DS.Amber600);
                }
                else if (_previewPlan != null)
                {
                    _confirmButton.Text = "Preview only";
                    _confirmButton.Enabled = false;
                    SetStatus("Visual preview — no business records will be changed.", DS.Primary700);
                }
                else
                {
                    SetStatus("Plan ready. Review each tab, then confirm to create the records.", DS.Green600);
                }
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Delivery Wizard"), "Preparing accepted quotation delivery", ex);
                SetStatus("Could not prepare the delivery plan. No records were changed.", DS.Red600);
            }
            finally
            {
                SetBusy(false, _status.Text);
            }
        }

        private void BindPlan()
        {
            _quotationContext.Text = First(_plan.QuotationNumber, "Quotation #" + _plan.QuotationId) + "  •  " +
                                     First(_plan.ClientName, "Client") + "  •  " +
                                     First(_plan.SiteName, "No site") + "  •  Accepted value " + IndiaFormatHelper.FormatCurrency(_plan.AcceptedValue);
            _jobMetric.Text = _plan.ScheduledDate.ToString("dd/MM/yyyy") + "\r\n" + _plan.ChecklistItems.Count + " checklist item(s)";
            _materialMetric.Text = _plan.Materials.Count + " line(s)\r\n" + _plan.TotalReservedQuantity.ToString("0.###") + " units reservable";
            _purchaseMetric.Text = _plan.PurchaseRequirements.Count + " line(s)\r\n" + _plan.TotalShortfallQuantity.ToString("0.###") + " units short";
            _billingMetric.Text = _plan.BillingSchedule.Count + " milestone(s)\r\n" + IndiaFormatHelper.FormatCurrency(_plan.AcceptedValue);

            _checklistGrid.DataSource = _plan.ChecklistItems.Select((item, index) => new ChecklistRow { Step = index + 1, ChecklistItem = item }).ToList();
            _materialGrid.DataSource = _plan.Materials;
            _purchaseGrid.DataSource = _plan.PurchaseRequirements;
            _billingGrid.DataSource = _plan.BillingSchedule;
            _reviewNotes.Text = BuildReviewText(_plan);
        }

        private async Task ConfirmAsync()
        {
            if (_plan == null || _busy || _previewPlan != null || _plan.ExistingJobId.HasValue) return;

            string detail = "Quotation: " + First(_plan.QuotationNumber, "#" + _plan.QuotationId) + "\r\n" +
                            "Job date: " + _plan.ScheduledDate.ToString("dd/MM/yyyy") + "\r\n" +
                            "Checklist: " + _plan.ChecklistItems.Count + " item(s)\r\n" +
                            "Material reservations: " + _plan.Materials.Count + " line(s)\r\n" +
                            "Draft purchase requirements: " + _plan.PurchaseRequirements.Count + " line(s)\r\n" +
                            "Billing: " + IndiaFormatHelper.FormatCurrency(_plan.AcceptedValue) + " planned; no invoice created.\r\n\r\n" +
                            "No supplier PO, message, or customer invoice will be sent automatically.";
            if (!ServoConfirmDialog.Show(this, "Create delivery records?", detail))
                return;

            SetBusy(true, "Creating the reviewed delivery records…");
            try
            {
                Result = await Task.Run(() => _service.Confirm(_plan));
                SetStatus("Created " + First(Result.JobNumber, "job #" + Result.JobId) + " and its reviewed delivery plan.", DS.Green600);
                _confirmButton.Text = "Created";
                _confirmButton.Enabled = false;
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Delivery Wizard"), "Creating accepted quotation delivery", ex);
                SetStatus("Delivery creation did not complete. It is safe to retry; duplicate jobs are prevented.", DS.Red600);
            }
            finally
            {
                SetBusy(false, _status.Text);
            }
        }

        private static string BuildReviewText(QuotationDeliveryPlan plan)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Ready for dispatcher confirmation");
            builder.AppendLine();
            builder.AppendLine("• Create one revenue job for " + plan.ScheduledDate.ToString("dd/MM/yyyy") + ".");
            builder.AppendLine("• Add " + plan.ChecklistItems.Count + " field checklist item(s).");
            builder.AppendLine("• Record " + plan.Materials.Count + " material reservation plan(s) without consuming stock.");
            builder.AppendLine("• Create " + plan.PurchaseRequirements.Count + " draft purchase requirement(s); no PO is sent.");
            builder.AppendLine("• Plan " + plan.BillingSchedule.Count + " billing milestone(s) totalling " + IndiaFormatHelper.FormatCurrency(plan.AcceptedValue) + "; no invoice is created.");
            if (plan.Warnings.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Review before confirming:");
                foreach (string warning in plan.Warnings) builder.AppendLine("• " + warning);
            }
            builder.AppendLine();
            builder.AppendLine("The wizard is idempotent: confirming the same quotation again returns its existing job instead of creating a duplicate.");
            return builder.ToString();
        }

        private static Label AddMetric(TableLayoutPanel host, int column, string title, string initialValue)
        {
            Panel card = new Panel { Dock = DockStyle.Fill, BackColor = DS.White, Margin = new Padding(0, 0, column == 3 ? 0 : 10, 0), Padding = new Padding(14, 8, 14, 6) };
            card.Controls.Add(new Label { Text = title, Location = new Point(14, 8), Size = new Size(235, 18), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Font = DS.SmallBold, ForeColor = DS.Slate600 });
            Label value = new Label { Text = initialValue, Location = new Point(14, 29), Size = new Size(235, 36), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Font = DS.BodyBold, ForeColor = DS.Slate900 };
            card.Controls.Add(value);
            card.Resize += (s, e) =>
            {
                value.Width = Math.Max(20, card.ClientSize.Width - 28);
                card.Controls[0].Width = value.Width;
            };
            host.Controls.Add(card, column, 0);
            return value;
        }

        private static TabPage BuildTab(string title, DataGridView grid)
        {
            TabPage page = new TabPage(title) { BackColor = DS.White, Padding = new Padding(12) };
            page.Controls.Add(grid);
            return page;
        }

        private static DataGridView BuildChecklistGrid()
        {
            DataGridView grid = BaseGrid("DeliveryChecklistGrid");
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Step", HeaderText = "Step", Width = 70 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ChecklistItem", HeaderText = "Checklist item", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            return grid;
        }

        private static DataGridView BuildMaterialGrid()
        {
            DataGridView grid = BaseGrid("DeliveryMaterialGrid");
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ItemDescription", HeaderText = "Material", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "QuantityRequired", HeaderText = "Required", Width = 90, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.###" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "QuantityAvailable", HeaderText = "Available", Width = 90, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.###" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "QuantityReserved", HeaderText = "Reserve", Width = 90, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.###" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ShortfallQuantity", HeaderText = "Shortfall", Width = 90, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.###" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Unit", HeaderText = "Unit", Width = 70 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ReservationStatus", HeaderText = "Plan", Width = 130 });
            return grid;
        }

        private static DataGridView BuildPurchaseGrid()
        {
            DataGridView grid = BaseGrid("DeliveryPurchaseRequirementsGrid");
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ItemDescription", HeaderText = "Material shortfall", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "QuantityRequired", HeaderText = "Draft qty", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.###" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Unit", HeaderText = "Unit", Width = 75 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "PreferredVendorName", HeaderText = "Preferred supplier", Width = 180 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "RequiredByDate", HeaderText = "Required by", Width = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Status", HeaderText = "Status", Width = 80 });
            return grid;
        }

        private static DataGridView BuildBillingGrid()
        {
            DataGridView grid = BaseGrid("DeliveryBillingGrid");
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "MilestoneName", HeaderText = "Milestone", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Amount", HeaderText = "Amount", Width = 150, DefaultCellStyle = new DataGridViewCellStyle { Format = "C2", FormatProvider = CultureInfo.GetCultureInfo("en-IN") } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DueDate", HeaderText = "Due date", Width = 130, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Status", HeaderText = "Status", Width = 100 });
            return grid;
        }

        private static DataGridView BaseGrid(string name)
        {
            DataGridView grid = new DataGridView
            {
                Name = name,
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                BackgroundColor = DS.White,
                BorderStyle = BorderStyle.None
            };
            GridTheme.Apply(grid, fillWidth: false, alternateRows: true, rowHeight: 40);
            return grid;
        }

        private void SetBusy(bool busy, string message)
        {
            _busy = busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            _confirmButton.Enabled = !busy && _plan != null && !_plan.ExistingJobId.HasValue && _previewPlan == null && Result == null;
            if (!string.IsNullOrWhiteSpace(message)) SetStatus(message, busy ? DS.Primary700 : _status.ForeColor);
        }

        private void SetStatus(string message, Color colour)
        {
            _status.Text = message ?? string.Empty;
            _status.ForeColor = colour;
        }

        private static Button MakeButton(string text, Color back, Color fore, int width)
        {
            Button button = new Button
            {
                Text = text,
                Size = new Size(width, 40),
                BackColor = back,
                ForeColor = fore,
                FlatStyle = FlatStyle.Flat,
                Font = DS.BodyBold,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = back == DS.White ? 1 : 0;
            button.FlatAppearance.BorderColor = DS.Border;
            return button;
        }

        private static string First(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private sealed class ChecklistRow
        {
            public int Step { get; set; }
            public string ChecklistItem { get; set; }
        }
    }
}

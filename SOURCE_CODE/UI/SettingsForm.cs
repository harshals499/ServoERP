using MessageBox = HVAC_Pro_Desktop.Services.LocalizedMessageBox;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.Services.Integrations;
using HVAC_Pro_Desktop.Services.Licensing;
using HVAC_Pro_Desktop.UI.Licensing;
using HVAC_Pro_Desktop.UI.Helpers;

namespace HVAC_Pro_Desktop.UI
{
    public partial class SettingsForm : DeferredPageControl
    {
        protected override bool EnableAutomaticLayoutScaling => false;

        protected override bool EnableMainScrollCanvas => false;

        protected override bool SuppressAutomaticChildPolish => true;

        private readonly SettingsService _svc = new SettingsService();
        private readonly HsnSacMasterService _hsnSacSvc = new HsnSacMasterService();
        private readonly NominatimGeocodingService _geoSvc = new NominatimGeocodingService();
        private readonly AuthService _authSvc = new AuthService();
        private readonly EmployeeService _employeeSvc = new EmployeeService();
        private readonly FreshStartService _freshStartSvc = new FreshStartService();
        private readonly UnitMeasurementService _unitMeasurementSvc = new UnitMeasurementService();
        private readonly OpenSourceLicenseService _openSourceLicenseSvc = new OpenSourceLicenseService();
        private readonly ModuleCatalogService _moduleCatalogSvc = new ModuleCatalogService();
        private readonly CompliancePackService _compliancePackSvc = new CompliancePackService();
        private readonly BackupService _backupSvc = new BackupService();
        private readonly LicenseService _licenseSvc = new LicenseService();
        private readonly DeviceFingerprintService _deviceFingerprintSvc = new DeviceFingerprintService();
        private readonly CloudBackupIntegrationService _cloudBackupIntegrationSvc = new CloudBackupIntegrationService();
        private readonly CardLayoutService _cardLayoutSvc = new CardLayoutService();
        private readonly SupportCenterService _supportCenterSvc = new SupportCenterService();

        private TextBox _txtCompanyName;
        private TextBox _txtAuthorisedSignatory;
        private TextBox _txtGST;
        private TextBox _txtPAN;
        private TextBox _txtTAN;
        private TextBox _txtPhone;
        private TextBox _txtEmail;
        private TextBox _txtAddress;
        private TextBox _txtOfficeLatitude;
        private TextBox _txtOfficeLongitude;
        private TextBox _txtPrefix;
        private TextBox _txtCurrency;
        private TextBox _txtFinancialYear;
        private ComboBox _cmbState;
        private ComboBox _cmbGstRegistrationType;
        private NumericUpDown _numGSTRate;
        private NumericUpDown _numMarkupPct;
        private NumericUpDown _numPayTerms;
        private NumericUpDown _numAnnualTurnover;
        private NumericUpDown _numEInvoiceThreshold;
        private CheckBox _chkEInvoiceEligible;
        private DataGridView _gridHsnSac;
        private readonly BindingSource _hsnBindingSource = new BindingSource();
        private List<HsnSacGridRow> _hsnMasterRows = new List<HsnSacGridRow>();
        private DataGridView _gridUnits;
        private Label _lblStatus;
        private Label _lblDbStatus;
        private Label _lblMoneyPreview;
        private DataGridView _gridUsers;
        private DataGridView _gridAudit;
        private DateTimePicker _dtAuditFrom;
        private DateTimePicker _dtAuditTo;
        private ComboBox _cmbAuditUser;
        private TabControl _tabs;
        private TextBox _txtHsnSearch;
        private Panel _generalCanvas;
        private TextBox _txtVersionCheckUrl;
        private readonly ToolTip _toolTip = new ToolTip { AutoPopDelay = 12000, InitialDelay = 350, ReshowDelay = 100, ShowAlways = true };
        private CheckBox _chkVersionCheckEnabled;
        private CheckBox _chkSilentAutoUpdateEnabled;
        private Label _lblInstalledVersion;
        private Label _lblLastUpdateCheckStatus;
        private Label _lblUnitSummary;
        private ComboBox _cmbDisplayFitMode;
        private ComboBox _cmbUiScale;
        private ComboBox _cmbLanguage;
        private TextBox _txtUnitCode;
        private TextBox _txtUnitShortCode;
        private TextBox _txtUnitDisplayName;
        private ComboBox _cmbUnitCategory;
        private ComboBox _cmbUnitMeasurementSystem;
        private TextBox _txtUnitAliases;
        private Panel _generalFlow;
        private Panel _settingsDirectory;
        private Panel _settingsNavigation;
        private Label _settingsSectionTitle;
        private TextBox _settingsSearch;
        private string[] _selectedSettingsCards;
        private readonly Dictionary<Panel, string> _settingsCardTitles = new Dictionary<Panel, string>();
        private readonly List<Panel> _directoryGroups = new List<Panel>();

        private sealed class SettingsDestination
        {
            public string Title;
            public string Description;
            public string[] Cards;
            public string Tab;
            public bool AdminOnly;
        }

        private sealed class SettingsDirectoryButton : Button
        {
            private bool _hovered;
            protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnPaint(PaintEventArgs e)
            {
                Color surface = _hovered || Focused ? DS.Slate50 : Color.White;
                e.Graphics.Clear(surface);
                using (Font font = new Font(LanguageManager.GetUiFontFamily(), 10.5f, FontStyle.Bold))
                using (Font descriptionFont = new Font(LanguageManager.GetUiFontFamily(), 9f))
                {
                    TextRenderer.DrawText(e.Graphics, Text, font, new Rectangle(54, 12, Math.Max(0, Width - 84), 28), DS.Slate900,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    TextRenderer.DrawText(e.Graphics, LanguageManager.Get(AccessibleDescription), descriptionFont, new Rectangle(54, 42, Math.Max(0, Width - 84), Height - 46), DS.Slate500,
                        TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                    TextRenderer.DrawText(e.Graphics, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("›"), font, new Rectangle(Width - 28, 14, 24, 28), DS.Primary600,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                using (Pen pen = new Pen(DS.Border)) e.Graphics.DrawLine(pen, 10, Height - 1, Width - 10, Height - 1);
                if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(3, 3, Width - 6, Height - 6));
            }
        }
        private Label _lblUserTotal;
        private Label _lblUserActive;
        private Label _lblUserAdmins;
        private Label _lblUserLastLogin;
        private Label _lblAuditTotal;
        private Label _lblAuditLogin;
        private Label _lblAuditWarnings;
        private Panel _auditGridCard;
        private Label _lblBackupStatus;
        private Label _lblLicenseStatus;
        private Label _lblSettingsVersionState;
        private Label _lblSettingsDbState;
        private Label _lblSettingsBackupState;
        private Label _lblSettingsLicenseState;
        private bool _reflowingSettingsCards;
        private bool _initialLoadQueued;
        private bool _settingsCardsBuilt;
        private bool _secondarySettingsCardsBuilt;
        private bool _hsnLoadQueued;
        private bool _securityLoadQueued;
        private bool _settingsPolishQueued;
        private bool _secondarySettingsCardsQueued;
        private bool _usersTabBuilt;
        private TabPage _usersTab;
        private bool _auditTabBuilt;
        private TabPage _auditTab;

        private static readonly Color HeaderBg = DS.White;
        private static readonly Color SectionBg = DS.Slate50;
        private static readonly Color SaveGreen = DS.Teal600;
        private static readonly Color InfoBlue = DS.Primary600;
        private const int GeneralCanvasWidth = 1180;

        private sealed class SectionCardState
        {
            public int BaseHeight { get; set; }
            public int ExpandedHeight { get; set; }
            public bool IsExpanded { get; set; }
        }

        private sealed class HsnSacGridRow
        {
            public int MasterID { get; set; }
            public string CodeType { get; set; }
            public string Code { get; set; }
            public string Description { get; set; }
            public string BusinessCategory { get; set; }
            public decimal TaxRate { get; set; }
            public decimal CGSTRate { get; set; }
            public decimal SGSTRate { get; set; }
            public decimal IGSTRate { get; set; }
            public bool IsDefault { get; set; }
            public bool IsActive { get; set; }
            public string Notes { get; set; }
        }

        public SettingsForm()
        {
            Dock = DockStyle.Fill;
            AutoScroll = false;
            BackColor = DS.BgPage;
            AppRuntime.LogTiming("Settings.BuildLayout.Start", 0);
            BuildLayout();
            AppRuntime.LogTiming("Settings.BuildLayout.Complete", 0);
            UIHelper.ApplyInputStyles(Controls);
            AppRuntime.LogTiming("Settings.InputStyles.Complete", 0);
        }

        public override void OnShellActivated()
        {
            EnsureInitialLoad();
            if (_tabs != null && _tabs.SelectedTab != null)
            {
                if (_usersTabBuilt && _tabs.SelectedTab == _usersTab)
                    BeginRefreshSecurityTabs();
                if (_auditTabBuilt && _tabs.SelectedTab == _auditTab)
                    RefreshAuditLog();
            }
        }

        public void EnsureInitialLoad()
        {
            if (_initialLoadQueued || DeferredLoadCompleted || IsDisposed)
                return;

            _initialLoadQueued = true;
            Action load = () =>
            {
                try
                {
                    EnsureSettingsCardsBuilt();
                    LoadSettings();
                    BeginCheckDbConnection();
                    MarkDeferredLoadCompleted();
                }
                catch (Exception ex)
                {
                    AppRuntime.LogException("SettingsForm.EnsureInitialLoad", ex);
                    global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblStatus, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Load error: {0}", ex.Message));
                    _lblStatus.ForeColor = Color.Red;
                    MarkDeferredLoadCompleted();
                }
                finally
                {
                    _initialLoadQueued = false;
                }
            };

            if (IsHandleCreated)
                BeginInvoke(load);
            else
                load();
        }

        private void EnsureSettingsCardsBuilt()
        {
            if (_settingsCardsBuilt)
                return;

            Stopwatch watch = Stopwatch.StartNew();
            AppRuntime.LogTiming("Settings.BuildCards.Start", 0);
            UiPerformanceService.WithSuspendedDrawing(_tabs, () =>
            {
                _generalCanvas.SuspendLayout();
                _generalFlow.SuspendLayout();
                try
                {
                    BuildForm(_generalFlow, includeDeferredCards: false);
                    CenterCanvas(_generalCanvas.Parent as Panel, _generalCanvas);
                }
                finally
                {
                    _generalFlow.ResumeLayout(false);
                    _generalCanvas.ResumeLayout(false);
                }
            });
            _settingsCardsBuilt = true;
            QueueDeferredSettingsPolish();
            QueueDeferredSecondarySettingsCards();
            AppRuntime.LogTiming("Settings.BuildCards.Complete", watch.ElapsedMilliseconds);
        }

        /// <summary>Builds the complete Settings surface without database loading for visual regression capture.</summary>
        internal void LoadPreviewForVisualTest()
        {
            if (_generalFlow == null || _generalFlow.IsDisposed)
                return;

            _generalCanvas.SuspendLayout();
            _generalFlow.SuspendLayout();
            try
            {
                BuildForm(_generalFlow, includeDeferredCards: true);
                _settingsCardsBuilt = true;
                _secondarySettingsCardsBuilt = true;
                CenterCanvas(_generalCanvas.Parent as Panel, _generalCanvas);
                ReflowSettingsCards();
            }
            finally
            {
                _generalFlow.ResumeLayout(true);
                _generalCanvas.ResumeLayout(true);
            }
        }

        private void QueueDeferredSecondarySettingsCards()
        {
            if (_secondarySettingsCardsBuilt || _secondarySettingsCardsQueued || IsDisposed || _generalFlow == null)
                return;

            _secondarySettingsCardsQueued = true;
            Action build = () =>
            {
                Stopwatch watch = Stopwatch.StartNew();
                try
                {
                    if (IsDisposed || _generalFlow == null || _generalFlow.IsDisposed || _secondarySettingsCardsBuilt)
                        return;

                    UiPerformanceService.WithSuspendedDrawing(_generalFlow, () =>
                    {
                        _generalFlow.SuspendLayout();
                        try
                        {
                            BuildDeferredSettingsCards(_generalFlow);
                            CenterCanvas(_generalCanvas.Parent as Panel, _generalCanvas);
                        }
                        finally
                        {
                            _generalFlow.ResumeLayout(false);
                        }
                    });
                    _secondarySettingsCardsBuilt = true;
                    QueueDeferredSettingsPolish();
                    AppRuntime.LogTiming("Settings.BuildDeferredCards.Complete", watch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    AppRuntime.LogException("SettingsForm.QueueDeferredSecondarySettingsCards", ex);
                }
                finally
                {
                    _secondarySettingsCardsQueued = false;
                }
            };

            Action queueWithDelay = () =>
            {
                var timer = new System.Windows.Forms.Timer { Interval = 900 };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    timer.Dispose();
                    if (!IsDisposed && IsHandleCreated)
                        BeginInvoke(build);
                };
                timer.Start();
            };

            if (IsHandleCreated)
                queueWithDelay();
            else
                HandleCreated += (s, e) => queueWithDelay();
        }

        /// <summary>Applies expensive shared Settings polish after first paint instead of blocking page open.</summary>
        private void QueueDeferredSettingsPolish()
        {
            if (_settingsPolishQueued || IsDisposed || _generalFlow == null)
                return;

            _settingsPolishQueued = true;
            Action polish = () =>
            {
                Stopwatch watch = Stopwatch.StartNew();
                try
                {
                    if (IsDisposed || _generalFlow == null || _generalFlow.IsDisposed)
                        return;

                    UIHelper.ApplyInputStyles(_generalFlow.Controls);
                    UIHelper.ApplyButtonAlignment(_generalFlow);
                    GlobalCardContextMenu.ApplyToTree(_generalFlow);
                    ReflowSettingsCards();
                    AppRuntime.LogTiming("Settings.DeferredPolish.Complete", watch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    AppRuntime.LogException("SettingsForm.QueueDeferredSettingsPolish", ex);
                }
                finally
                {
                    _settingsPolishQueued = false;
                }
            };

            Action queueWithDelay = () =>
            {
                var timer = new System.Windows.Forms.Timer { Interval = 1500 };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    timer.Dispose();
                    if (!IsDisposed && IsHandleCreated)
                        BeginInvoke(polish);
                };
                timer.Start();
            };

            if (IsHandleCreated)
                queueWithDelay();
            else
                HandleCreated += (s, e) => queueWithDelay();
        }

        /// <summary>Refreshes runtime-only Settings labels whenever the cached page becomes visible again.</summary>
        protected override void OnVisibleChanged(EventArgs e)
        {
            AppRuntime.LogTiming("Settings.OnVisibleChanged.Start", 0, "visible=" + Visible);
            base.OnVisibleChanged(e);
            if (Visible)
                RefreshRuntimeSettingsLabels();
            AppRuntime.LogTiming("Settings.OnVisibleChanged.Complete", 0, "visible=" + Visible);
        }

        /// <summary>Updates Settings labels that must reflect the currently running build, not saved configuration.</summary>
        private void RefreshRuntimeSettingsLabels()
        {
            if (_lblInstalledVersion != null && !_lblInstalledVersion.IsDisposed)
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblInstalledVersion, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Current version: {0}", ConfigService.GetAppVersion()));
            if (_lblLastUpdateCheckStatus != null && !_lblLastUpdateCheckStatus.IsDisposed)
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblLastUpdateCheckStatus, UpdateService.GetLastUpdateStatusDisplay());
            RefreshSettingsWorkspaceSummary();
        }

        private void BuildLayout()
        {
            Button btnSave = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Save Settings", SaveGreen, 146));
            btnSave.Location = new Point(0, 0);
            ModernIconSystem.AddButtonIcon(btnSave, ModernIconKind.Save);
            btnSave.Click += (s, e) => {
                if (LanguageManager.GetSourceText(_tabs?.SelectedTab, _tabs?.SelectedTab?.Text) == "Mobile Connection") _saveMobileConnection?.PerformClick();
                else Save();
            };
            Button btnResetDefaults = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Reset to Defaults", Color.White, 184));
            btnResetDefaults.ForeColor = DS.Slate700;
            btnResetDefaults.FlatAppearance.BorderSize = 1;
            btnResetDefaults.FlatAppearance.BorderColor = DS.Border;
            ModernIconSystem.AddButtonIcon(btnResetDefaults, ModernIconKind.Preference);
            btnResetDefaults.Click += (s, e) => ResetGeneralDefaults();
            Button btnToolbarCheckUpdates = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Check for Updates", InfoBlue, 170));
            btnToolbarCheckUpdates.Location = new Point(0, 0);
            ModernIconSystem.AddButtonIcon(btnToolbarCheckUpdates, ModernIconKind.Refresh);
            btnToolbarCheckUpdates.Click += async (s, e) => await CheckVersionNowAsync();
            Button btnFormsLibrary = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Forms Library", Color.White, 132));
            btnFormsLibrary.ForeColor = DS.Primary600;
            btnFormsLibrary.FlatAppearance.BorderSize = 1;
            btnFormsLibrary.FlatAppearance.BorderColor = DS.BorderStrong;
            ModernIconSystem.AddButtonIcon(btnFormsLibrary, ModernIconKind.Document);
            btnFormsLibrary.Click += (s, e) => FormTemplateWorkflowLauncher.Open(this, "Settings / Master Data", "Master Data", null, "company document templates import validation backup compliance settings forms library");
            _lblStatus = new Label
            {
                AutoSize = false,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9),
                ForeColor = DS.Slate500,
                Width = 220,
                Height = 22,
                TextAlign = ContentAlignment.MiddleRight
            };
            Panel header = BuildModernSettingsHeader(btnSave, btnResetDefaults, btnToolbarCheckUpdates, btnFormsLibrary);

            _tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9, FontStyle.Bold),
                Padding = new Point(18, 7),
                SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(140, 36),
                DrawMode = TabDrawMode.OwnerDrawFixed,
                Appearance = TabAppearance.FlatButtons
            };
            _tabs.DrawItem += DrawModernSettingsTab;
            TabPage generalTab = new TabPage("General") { BackColor = DS.BgPage };
            Panel body = new Panel { Name = "SettingsGeneralBody", Dock = DockStyle.Fill, AutoScroll = true, BackColor = DS.BgPage, Tag = "NO_CARD_SURFACE" };
            _generalCanvas = new Panel { Width = GeneralCanvasWidth, BackColor = DS.BgPage, Padding = new Padding(0, 0, 0, 24), Tag = "CUSTOM_INPUT_SHELL NO_INPUT_HOST NO_CARD_SURFACE" };
            _generalFlow = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                BackColor = DS.BgPage,
                Padding = new Padding(0),
                Margin = new Padding(0),
                Tag = "CUSTOM_INPUT_SHELL NO_INPUT_HOST NO_CARD_SURFACE"
            };
            _generalCanvas.Controls.Add(_generalFlow);
            body.Controls.Add(_generalCanvas);
            body.Resize += (s, e) =>
            {
                CenterCanvas(body, _generalCanvas);
                ReflowSettingsCards();
            };
            _generalFlow.Controls.Add(global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
            {
                Text = "Loading Settings...",
                Location = new Point(0, 0),
                Size = new Size(360, 32),
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 11f, FontStyle.Bold),
                ForeColor = DS.Slate600
            }));
            CenterCanvas(body, _generalCanvas);
            generalTab.Controls.Add(body);
            _tabs.TabPages.Add(generalTab);
            _tabs.TabPages.Add(CreateLazySettingsTab(global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Mobile Connection"), BuildMobileConnectionContent));
            if (IsAdminUser())
            {
                _usersTab = CreateLazySettingsTab(global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Users & Logins"), BuildUsersTabContent);
                _auditTab = CreateLazySettingsTab(global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Audit Log"), BuildAuditTabContent);
                _tabs.TabPages.Add(_usersTab);
                _tabs.TabPages.Add(_auditTab);
            }
            _tabs.SelectedIndexChanged += SettingsTabs_SelectedIndexChanged;

            Controls.Add(_tabs);
            Controls.Add(header);
        }

        private void BuildCompanyHeaderCard(Panel parent)
        {
            Panel body = AddModernSettingsCard(parent, "Company Header", "Upload your company letterhead once for generated PDFs, documents and print previews.", 310);
            body.Parent.Parent.Name = "CompanyHeaderCard";
            var preview = new PictureBox { Name = "CompanyHeaderPreview", Location = new Point(0, 0),
                Size = new Size(500, 110), SizeMode = PictureBoxSizeMode.Zoom, BackColor = DS.Slate50 };
            var status = new Label { Location = new Point(0, 118), Size = new Size(500, 42), Font = DS.Small, ForeColor = DS.Slate600 };
            Button upload = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Upload Header", InfoBlue, 146));
            upload.Name = "UploadCompanyHeader";
            upload.Location = new Point(0, 166);
            upload.Enabled = IsAdminUser();
            Action refresh = () =>
            {
                Image old = preview.Image;
                preview.Image = null;
                old?.Dispose();
                string path = DocumentBranding.GetCompanyHeaderPath();
                if (!string.IsNullOrWhiteSpace(path))
                    using (Image image = Image.FromFile(path)) preview.Image = new Bitmap(image);
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(status, string.IsNullOrWhiteSpace(path)
                    ? "No uploaded header. Select a wide PNG, JPG or BMP image, up to 10 MB."
                    : "Company header applied. Upload another image to replace it for future documents.");
            };
            upload.Click += (s, e) =>
            {
                using (var dialog = new OpenFileDialog { Title = "Select company header", Filter = "Header images|*.png;*.jpg;*.jpeg;*.bmp", CheckFileExists = true })
                {
                    if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
                    try { DocumentBranding.UploadCompanyHeader(dialog.FileName); refresh(); }
                    catch (Exception ex)
                    {
                        AppLogger.LogError("SettingsForm.UploadCompanyHeader", ex);
                        MessageBox.Show(FindForm(), global::HVAC_Pro_Desktop.Services.LanguageManager.Format("ServoERP could not apply the company header. {0}", ex.Message), global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Company Header"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            };
            body.Controls.Add(preview); body.Controls.Add(status); body.Controls.Add(upload);
            body.Resize += (s, e) => { preview.Width = Math.Max(260, body.ClientSize.Width); status.Width = preview.Width; };
            preview.Disposed += (s, e) => preview.Image?.Dispose();
            refresh();
        }

        private void BuildSettingsDirectory(Panel parent)
        {
            _directoryGroups.Clear();
            _selectedSettingsCards = null;
            _settingsDirectory = new Panel { Name = "SettingsDirectory", BackColor = DS.BgPage, Tag = "NO_CARD_SURFACE NO_DASHBOARD_RESIZE" };
            _settingsNavigation = new Panel { Name = "SettingsSectionNavigation", Height = 48, Visible = false, BackColor = DS.BgPage };
            Button back = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Back to Settings", Color.White, 160));
            back.Name = "SettingsBack";
            back.ForeColor = DS.Primary600;
            back.Click += (s, e) => ShowSettingsHome();
            _settingsNavigation.Controls.Add(back);
            _settingsSectionTitle = new Label { Location = new Point(178, 8), Size = new Size(600, 30), Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 12f, FontStyle.Bold), ForeColor = DS.Slate900 };
            _settingsNavigation.Controls.Add(_settingsSectionTitle);
            parent.Controls.Add(_settingsNavigation);
            parent.Controls.Add(_settingsDirectory);
            Label introduction = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label { Name = "SettingsIntroduction", Text = "Find and manage related settings.", Location = new Point(0, 8), Size = new Size(500, 28), Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 10f), ForeColor = DS.Slate500 });
            _settingsDirectory.Controls.Add(introduction);
            _settingsSearch = new TextBox { Name = "SettingsDirectorySearch", Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 10f), Size = new Size(290, 30), AccessibleName = global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Search settings") };
            _settingsSearch.TextChanged += (s, e) => LayoutSettingsDirectory();
            _settingsDirectory.Controls.Add(_settingsSearch);
            _toolTip.SetToolTip(_settingsSearch, LanguageManager.Get("Search settings by name or description"));
            Label searchLabel = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label { Text = "Search settings", Name = "SettingsSearchLabel", Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9f), ForeColor = DS.Slate500, Size = new Size(290, 20) });
            _settingsDirectory.Controls.Add(searchLabel);
            AddSettingsDirectoryGroup("BUSINESS", new[] {
                Destination("Company profile", "Business identity, contact details and office location", "Company Information"),
                Destination("Letterhead & documents", "Company header for PDFs and print previews", "Company Header"),
                Destination("GST & financial defaults", "Tax, invoice and payment preferences", "India Defaults"),
                Destination("HSN / SAC & units", "Codes, tax rates and units of measure", "HSN / SAC Master", "Unit Management") });
            AddSettingsDirectoryGroup("ADMINISTRATION", new[] {
                new SettingsDestination { Title = "Users & permissions", Description = "Manage staff logins and access", Tab = "Users & Logins", AdminOnly = true },
                new SettingsDestination { Title = "Audit log", Description = "Review recorded user activity", Tab = "Audit Log", AdminOnly = true },
                Destination("Backup & recovery", "Backup destinations, schedules and recovery", "Backup & Recovery", true),
                Destination("Data management", "Fresh Start and data reset tools", "Data Management", true) });
            AddSettingsDirectoryGroup("SYSTEM & SUPPORT", new[] {
                new SettingsDestination { Title = "Mobile Connection", Description = "Connect phones by QR code or office user ID and password", Tab = "Mobile Connection" },
                Destination("Display & layout", "Screen fit, interface scale and language", "Display & Layout"),
                Destination("Database & connections", "Connection setup, health and layout recovery", "System Tools"),
                new SettingsDestination { Title = "License & updates", Description = "Activation, renewal and application updates", Cards = IsAdminUser() ? new[] { "License Management", "About & Updates" } : new[] { "About & Updates" } },
                Destination("Help & diagnostics", "Support, health checks and error logs", "Help & Support", "Diagnostics & Error Log"),
                Destination("Legal & compliance", "Policies, component licenses and export pack", "Legal Agreements", "Open Source & Licenses", "Module Catalog", "Compliance Export Pack") });
            Panel shortcuts = new Panel { Name = "SettingsShortcuts", Height = 60, BackColor = Color.White, Tag = "NO_CARD_SURFACE NO_DASHBOARD_RESIZE" };
            shortcuts.Controls.Add(global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label { Text = "Frequently used", AutoSize = true, Location = new Point(18, 20), Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9f, FontStyle.Bold), ForeColor = DS.Slate900 }));
            int x = 170;
            foreach (string title in new[] { "Company profile", "Backup & recovery", "Users & permissions" })
            {
                SettingsDestination target = _directoryGroups.SelectMany(g => g.Controls.OfType<Panel>()).Select(r => r.Tag as SettingsDestination).FirstOrDefault(d => d != null && d.Title == title);
                if (target == null) continue;
                Button shortcut = MakeBtn(title, Color.White, 170);
                shortcut.Location = new Point(x, 12);
                shortcut.ForeColor = DS.Primary600;
                shortcut.Click += (s, e) => OpenSettingsDestination(target);
                shortcuts.Controls.Add(shortcut);
                x += 184;
            }
            _settingsDirectory.Controls.Add(shortcuts);
        }

        private SettingsDestination Destination(string title, string description, params string[] cards)
        {
            return new SettingsDestination { Title = title, Description = description, Cards = cards };
        }

        private SettingsDestination Destination(string title, string description, string card, bool adminOnly)
        {
            return new SettingsDestination { Title = title, Description = description, Cards = new[] { card }, AdminOnly = adminOnly };
        }

        private void AddSettingsDirectoryGroup(string title, SettingsDestination[] destinations)
        {
            Panel group = new Panel { Name = "SettingsDirectorySection", BackColor = Color.White, Padding = new Padding(18), AccessibleName = title, Tag = "NO_CARD_SURFACE NO_DASHBOARD_RESIZE" };
            group.Paint += (s, e) => { using (Pen pen = new Pen(DS.Border)) e.Graphics.DrawRectangle(pen, 0, 0, group.Width - 1, group.Height - 1); };
            group.Controls.Add(global::HVAC_Pro_Desktop.Services.LanguageManager.DataText(new Label { Text = title, UseMnemonic = false, Location = new Point(18, 20), Size = new Size(300, 24), Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9f, FontStyle.Bold), ForeColor = DS.Slate500 }));
            foreach (SettingsDestination destination in destinations)
            {
                if (destination.AdminOnly && !IsAdminUser()) continue;
                Panel row = new Panel { Name = "SettingsDirectoryRow", Tag = destination, Height = 88, BackColor = Color.White };
                Button link = global::HVAC_Pro_Desktop.Services.LanguageManager.DataText(new SettingsDirectoryButton { Name = "SettingsLink_" + destination.Title.Replace(" ", ""), Text = LanguageManager.Get(destination.Title), UseMnemonic = false, FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft, Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 10f, FontStyle.Bold), ForeColor = DS.Primary600, BackColor = Color.White, Location = new Point(4, 8), Height = 34, Cursor = Cursors.Hand, AccessibleDescription = LanguageManager.Get(destination.Description) });
                link.FlatAppearance.BorderSize = 0;
                link.Location = Point.Empty;
                link.Dock = DockStyle.Fill;
                link.Height = 88;
                link.Click += (s, e) => OpenSettingsDestination(destination);
                Label icon = ModernIconSystem.Badge(ModernIconSystem.KindForTitle(destination.Title), 32, DS.Indigo50, DS.Primary600, 6);
                icon.Location = new Point(10, 18);
                icon.Cursor = Cursors.Hand;
                icon.Click += (s, e) => link.PerformClick();
                link.Controls.Add(icon);
                row.Controls.Add(link);
                row.Resize += (s, e) => { link.MinimumSize = Size.Empty; link.SetBounds(0, 0, row.Width, row.Height); };
                group.Controls.Add(row);
            }
            _directoryGroups.Add(group);
            _settingsDirectory.Controls.Add(group);
        }

        private void OpenSettingsDestination(SettingsDestination destination)
        {
            if (destination.AdminOnly && !IsAdminUser()) return;
            if (destination.Tab != null)
            {
                TabPage tab = _tabs.TabPages.Cast<TabPage>().FirstOrDefault(t => t.Text == destination.Tab);
                if (tab != null) _tabs.SelectedTab = tab;
                return;
            }
            if (!_secondarySettingsCardsBuilt)
            {
                BuildDeferredSettingsCards(_generalFlow);
                _secondarySettingsCardsBuilt = true;
                UIHelper.ApplyInputStyles(_generalFlow.Controls);
            }
            _selectedSettingsCards = destination.Cards;
            global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_settingsSectionTitle, destination.Title);
            ReflowSettingsCards();
            Panel scroll = _generalCanvas.Parent as Panel;
            if (scroll != null) scroll.AutoScrollPosition = Point.Empty;
            if (destination.Cards.Contains("HSN / SAC Master")) BeginLoadHsnSacGrid();
        }

        private void ShowSettingsHome()
        {
            _selectedSettingsCards = null;
            ReflowSettingsCards();
            Panel scroll = _generalCanvas.Parent as Panel;
            if (scroll != null) scroll.AutoScrollPosition = Point.Empty;
        }

        private void LayoutSettingsDirectory()
        {
            if (_settingsDirectory == null) return;
            int width = Math.Max(360, _generalCanvas.Width - 4);
            _settingsDirectory.Width = width;
            _settingsSearch.Location = new Point(Math.Max(0, width - 292), 26);
            _settingsDirectory.Controls["SettingsSearchLabel"].Location = new Point(Math.Max(0, width - 292), 4);
            _settingsDirectory.Controls["SettingsIntroduction"].Width = Math.Max(120, width - 330);
            string query = _settingsSearch.Text.Trim();
            int matchingGroups = _directoryGroups.Count(g => g.Controls.OfType<Panel>().Any(r => {
                SettingsDestination d = (SettingsDestination)r.Tag;
                return query.Length == 0 || (d.Title + " " + d.Description + " " + LanguageManager.Get(d.Title) + " " + LanguageManager.Get(d.Description)).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
            }));
            int columns = Math.Max(1, Math.Min(matchingGroups, width >= 1100 ? 3 : width >= 740 ? 2 : 1));
            int groupWidth = (width - (columns - 1) * 16) / columns;
            int y = 80;
            int column = 0;
            int rowHeight = 0;
            int matches = 0;
            foreach (Panel group in _directoryGroups)
            {
                int rowY = 58;
                foreach (Panel row in group.Controls.OfType<Panel>())
                {
                    SettingsDestination destination = (SettingsDestination)row.Tag;
                    bool match = query.Length == 0 || (destination.Title + " " + destination.Description + " " + LanguageManager.Get(destination.Title) + " " + LanguageManager.Get(destination.Description)).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                    row.Visible = match;
                    if (!match) continue;
                    row.Padding = Padding.Empty;
                    row.SetBounds(14, rowY, groupWidth - 28, 88);
                    Button link = row.Controls.OfType<Button>().Single();
                    link.MinimumSize = Size.Empty;
                    link.SetBounds(0, 0, row.Width, row.Height);
                    rowY += 88;
                    matches++;
                }
                group.Visible = rowY > 58;
                if (rowY == 58) continue;
                group.SetBounds(column * (groupWidth + 16), y, groupWidth, rowY + 20);
                rowHeight = Math.Max(rowHeight, group.Height);
                if (++column == columns)
                {
                    foreach (Panel peer in _directoryGroups.Where(g => g.Top == y)) peer.Height = rowHeight;
                    y += rowHeight + 20; column = 0; rowHeight = 0;
                }
            }
            if (column > 0) y += rowHeight + 16;
            Panel shortcuts = (Panel)_settingsDirectory.Controls["SettingsShortcuts"];
            int shortcutX = width < 740 ? 16 : 170;
            int shortcutY = width < 740 ? 46 : 12;
            foreach (Button shortcut in shortcuts.Controls.OfType<Button>())
            {
                if (shortcutX + 170 > width - 16) { shortcutX = 16; shortcutY += 48; }
                shortcut.SetBounds(shortcutX, shortcutY, 170, 36);
                shortcutX += 184;
            }
            int shortcutsHeight = shortcutY + 52;
            shortcuts.SetBounds(0, y, width, shortcutsHeight);
            shortcuts.AutoScroll = false;
            shortcuts.Visible = query.Length == 0;
            Label empty = _settingsDirectory.Controls["SettingsSearchEmpty"] as Label;
            if (empty == null) { empty = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label { Name = "SettingsSearchEmpty", Text = "No settings found. Try another search.", Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 10f), ForeColor = DS.Slate500 }); _settingsDirectory.Controls.Add(empty); }
            empty.SetBounds(18, 86, width - 36, 36);
            empty.Visible = matches == 0;
            _settingsDirectory.Height = y + (shortcuts.Visible ? shortcutsHeight + 16 : 16);
            if (_selectedSettingsCards == null) { _generalFlow.Height = _settingsDirectory.Height; _generalCanvas.Height = Math.Max(_generalFlow.Height + 32, _generalCanvas.Parent.ClientSize.Height - 40); }
        }

        private void BuildForm(Panel parent, bool includeDeferredCards)
        {
            parent.Controls.Clear();
            _settingsCardTitles.Clear();
            BuildSettingsDirectory(parent);
            BuildCompanyHeaderCard(parent);

            if (IsAdminUser())
                BuildLoginAccessSection(parent);

            AppRuntime.LogTiming("Settings.BuildForm.Guides.Start", 0);
            BuildGeneralSettingsGuide(parent);
            BuildHelpSupportCard(parent);
            BuildUpdateNotificationsCard(parent);
            AppRuntime.LogTiming("Settings.BuildForm.Guides.Complete", 0);

            AppRuntime.LogTiming("Settings.BuildForm.Company.Start", 0);
            Panel companyBody = AddModernSettingsCard(parent, "Company Information", "Profile, compliance, and office location details used across the platform.", 482);
            _txtCompanyName = new TextBox();
            PlaceLabeledControl(companyBody, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Company Name *"), _txtCompanyName, 0, 0, 210);
            _txtGST = new TextBox { CharacterCasing = CharacterCasing.Upper };
            PlaceLabeledControl(companyBody, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("GSTIN"), _txtGST, 226, 0, 190);
            new ToolTip().SetToolTip(_txtGST, LanguageManager.Get("Format: 22ABCDE1234F1Z5"));
            _txtPAN = new TextBox { CharacterCasing = CharacterCasing.Upper };
            PlaceLabeledControl(companyBody, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("PAN"), _txtPAN, 432, 0, 150);
            _txtTAN = new TextBox { CharacterCasing = CharacterCasing.Upper };
            PlaceLabeledControl(companyBody, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("TAN"), _txtTAN, 0, 64, 170);
            _txtPhone = new TextBox();
            PlaceLabeledControl(companyBody, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Phone"), _txtPhone, 190, 64, 170);
            _txtEmail = new TextBox();
            PlaceLabeledControl(companyBody, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Email"), _txtEmail, 380, 64, 202);
            _txtAddress = new TextBox();
            PlaceLabeledControl(companyBody, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Address / City"), _txtAddress, 0, 128, 442);

            Button btnLocateOffice = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Locate", InfoBlue, 112));
            btnLocateOffice.Location = new Point(458, 148);
            btnLocateOffice.Click += async (s, e) => await LocateOfficeAsync();
            companyBody.Controls.Add(btnLocateOffice);

            _txtOfficeLatitude = new TextBox { ReadOnly = false };
            PlaceLabeledControl(companyBody, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Office Latitude"), _txtOfficeLatitude, 0, 192, 170);
            _txtOfficeLongitude = new TextBox { ReadOnly = false };
            PlaceLabeledControl(companyBody, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Office Longitude"), _txtOfficeLongitude, 190, 192, 170);
            _cmbState = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };
            _cmbState.Items.AddRange(IndiaStateCatalog.Names.Cast<object>().ToArray());
            PlaceLabeledControl(companyBody, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("State / UT"), _cmbState, 380, 192, 202);
            _cmbGstRegistrationType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };
            _cmbGstRegistrationType.Items.AddRange(new object[] { "Regular", "Composition", "Unregistered" });
            PlaceLabeledControl(companyBody, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("GST Registration Type"), _cmbGstRegistrationType, 0, 256, 220);
            _txtAuthorisedSignatory = new TextBox();
            PlaceLabeledControl(companyBody, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Authorised Signatory (PDF)"), _txtAuthorisedSignatory, 0, 320, 340);
            companyBody.Resize += (s, e) => LayoutCompanyInformationCard(companyBody, btnLocateOffice);
            LayoutCompanyInformationCard(companyBody, btnLocateOffice);
            AppRuntime.LogTiming("Settings.BuildForm.Company.Complete", 0);

            AppRuntime.LogTiming("Settings.BuildForm.Display.Start", 0);
            Panel displayBody = AddModernSettingsCard(parent, "Display & Layout", "Customize how dense data is displayed across the system.", 480);
            _cmbDisplayFitMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };
            _cmbDisplayFitMode.Items.AddRange(new object[]
            {
                "Auto detect laptop screens",
                "IdeaPad / compact laptop",
                "Standard desktop"
            });
            // Persisted mode uses SelectedIndex below; localize presentation without mutating Items.
            _cmbDisplayFitMode.FormattingEnabled = true;
            _cmbDisplayFitMode.Format += (s, e) => e.Value = LanguageManager.Get(Convert.ToString(e.ListItem, CultureInfo.InvariantCulture));
            PlaceLabeledControl(displayBody, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Display fit mode"), _cmbDisplayFitMode, 0, 0, 330);
            _cmbUiScale = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };
            foreach (int option in LayoutScaler.GetUiScaleOptions())
                _cmbUiScale.Items.Add(option.ToString() + "%");
            PlaceLabeledControl(displayBody, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Global UI scale"), _cmbUiScale, 0, 74, 160);
            Button btnSaveDisplayFit = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Save Display", InfoBlue, 130));
            btnSaveDisplayFit.Location = new Point(356, 20);
            btnSaveDisplayFit.Click += (s, e) =>
            {
                SaveDisplayFitSetting();
                SaveUiScaleSetting();
                LayoutScaler.ApplyDisplayFit(FindForm());
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_lblStatus, "Display settings saved. Reopen ServoERP to apply global UI scale everywhere.");
                _lblStatus.ForeColor = SaveGreen;
            };
            displayBody.Controls.Add(btnSaveDisplayFit);
            Label displayHelp = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
            {
                Text = "Use global UI scale for all pages. 90% fits more cards on small screens; 110% or 125% improves readability on large displays.",
                Location = new Point(190, 78),
                Size = new Size(340, 54),
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9f),
                ForeColor = DS.Slate600
            });
            displayHelp.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            displayBody.Controls.Add(displayHelp);
            Label displayFitHelp = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
            {
                Text = "Display fit controls responsive layout rules; UI scale controls font, spacing, card, and control sizing.",
                Location = new Point(0, 148),
                Size = new Size(530, 50),
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9f),
                ForeColor = DS.Slate600
            });
            displayFitHelp.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            displayBody.Controls.Add(displayFitHelp);
            Label currentScreen = global::HVAC_Pro_Desktop.Services.LanguageManager.DataText(new Label
            {
                Text = BuildDisplayFitScreenSummary(),
                Location = new Point(0, 210),
                Size = new Size(530, 50),
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 8.75f, FontStyle.Bold),
                ForeColor = SaveGreen
            });
            currentScreen.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            displayBody.Controls.Add(currentScreen);

            _cmbLanguage = new ComboBox
            {
                Name = "ApplicationLanguage",
                DropDownStyle = ComboBoxStyle.DropDownList,
                AccessibleName = LanguageManager.Get("Application language")
            };
            _cmbLanguage.Items.AddRange(new object[] { LanguageManager.English, LanguageManager.Marathi });
            _cmbLanguage.SelectedItem = LanguageManager.CurrentLanguage;
            if (_cmbLanguage.SelectedIndex < 0) _cmbLanguage.SelectedIndex = 0;
            PlaceLabeledControl(displayBody, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Application language"), _cmbLanguage, 0, 280, 220);
            Button saveLanguage = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Save Language", InfoBlue, 180));
            saveLanguage.Name = "SaveApplicationLanguage";
            saveLanguage.Location = new Point(248, 300);
            saveLanguage.Click += (s, e) => SaveApplicationLanguage();
            displayBody.Controls.Add(saveLanguage);
            displayBody.Controls.Add(global::HVAC_Pro_Desktop.Services.LanguageManager.DataText(new Label
            {
                Text = LanguageManager.Get("Choose English or Marathi. Your business data and number formats stay unchanged."),
                Location = new Point(0, 348), Size = new Size(530, 56),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            }));

            AppRuntime.LogTiming("Settings.BuildForm.Display.Complete", 0);

            if (includeDeferredCards)
            {
                AppRuntime.LogTiming("Settings.BuildForm.ComplianceCards.Start", 0);
                BuildLegalAgreementsCard(parent);
                BuildOpenSourceLicensesCard(parent);
                BuildModuleCatalogCard(parent);
                BuildCompliancePackCard(parent);
                AppRuntime.LogTiming("Settings.BuildForm.ComplianceCards.Complete", 0);
            }

            AppRuntime.LogTiming("Settings.BuildForm.Defaults.Start", 0);
            Panel defaultsBody = AddModernSettingsCard(parent, "India Defaults", "Set default financial and taxation preferences.", 360);
            _txtPrefix = new TextBox { CharacterCasing = CharacterCasing.Upper };
            PlaceLabeledControl(defaultsBody, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Invoice Prefix"), _txtPrefix, 0, 0, 150);
            _numGSTRate = MakeDecimalBox(Point.Empty, 0, 0m, 28m, 18m, 2);
            PlaceLabeledControl(defaultsBody, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Default GST %"), _numGSTRate, 170, 0, 150);
            _numPayTerms = MakeDecimalBox(Point.Empty, 0, 0m, 365m, 30m, 0);
            PlaceLabeledControl(defaultsBody, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Payment Terms (days)"), _numPayTerms, 340, 0, 150);
            _numMarkupPct = MakeDecimalBox(Point.Empty, 0, 0m, 200m, 25m, 2);
            PlaceLabeledControl(defaultsBody, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Default Markup %"), _numMarkupPct, 0, 72, 150);
            _numAnnualTurnover = MakeDecimalBox(Point.Empty, 0, 0m, 9999999999m, 0m, 2, 1000m);
            _numAnnualTurnover.ValueChanged += (s, e) => RefreshIndiaDefaultsPreview();
            PlaceLabeledControl(defaultsBody, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Annual Turnover"), _numAnnualTurnover, 170, 72, 150);
            _numEInvoiceThreshold = MakeDecimalBox(Point.Empty, 0, 0m, 9999999999m, 50000000m, 2, 1000m);
            _numEInvoiceThreshold.ValueChanged += (s, e) => RefreshIndiaDefaultsPreview();
            PlaceLabeledControl(defaultsBody, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("E-Invoice Threshold"), _numEInvoiceThreshold, 340, 72, 150);
            _txtCurrency = new TextBox { ReadOnly = false, Text = "INR (\u20B9)" };
            PlaceLabeledControl(defaultsBody, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Currency"), _txtCurrency, 0, 144, 150);
            _txtFinancialYear = new TextBox { ReadOnly = false };
            PlaceLabeledControl(defaultsBody, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Financial Year"), _txtFinancialYear, 170, 144, 320);
            _chkEInvoiceEligible = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new CheckBox
            {
                Location = new Point(0, 222),
                Width = 180,
                Text = "E-Invoice eligible",
                Enabled = false,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9, FontStyle.Bold),
                ForeColor = DS.Slate700,
                BackColor = Color.White
            });
            defaultsBody.Controls.Add(_chkEInvoiceEligible);
            _toolTip.SetToolTip(_chkEInvoiceEligible, LanguageManager.Get("Calculated automatically from annual turnover and configured e-invoice threshold."));
            _lblMoneyPreview = new Label
            {
                Location = new Point(190, 220),
                Width = 330,
                Height = 42,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9),
                ForeColor = DS.Slate500
            };
            defaultsBody.Controls.Add(_lblMoneyPreview);
            defaultsBody.Resize += (s, e) => LayoutIndiaDefaultsCard(defaultsBody);
            LayoutIndiaDefaultsCard(defaultsBody);
            AppRuntime.LogTiming("Settings.BuildForm.Defaults.Complete", 0);

            AppRuntime.LogTiming("Settings.BuildForm.Hsn.Start", 0);
            Panel hsnBody = AddModernSettingsCard(parent, "HSN / SAC Master", "Manage HSN / SAC codes and tax rates.", 384);
            _gridHsnSac = BuildHsnSacGrid();
            Panel hsnGridHost = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(526, 258),
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            _gridHsnSac.Location = Point.Empty;
            _gridHsnSac.Size = new Size(hsnGridHost.Width, hsnGridHost.Height + SystemInformation.HorizontalScrollBarHeight + 2);
            hsnGridHost.Controls.Add(_gridHsnSac);
            hsnBody.Controls.Add(hsnGridHost);

            _txtHsnSearch = new TextBox { Width = 220, Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9f) };
            _txtHsnSearch.TextChanged += (s, e) => ApplyHsnSacFilter();
            _txtHsnSearch.Location = new Point(0, 272);
            hsnBody.Controls.Add(_txtHsnSearch);

            Button btnAddRow = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Add HSN/SAC Row", InfoBlue, 150));
            btnAddRow.Location = new Point(236, 272);
            btnAddRow.Click += (s, e) =>
            {
                _hsnMasterRows.Add(new HsnSacGridRow
                {
                    MasterID = 0,
                    CodeType = "HSN",
                    TaxRate = 18m,
                    CGSTRate = 9m,
                    SGSTRate = 9m,
                    IGSTRate = 18m,
                    IsActive = true
                });
                ApplyHsnSacFilter();
            };
            hsnBody.Controls.Add(btnAddRow);
            hsnBody.Resize += (s, e) =>
            {
                btnAddRow.Top = Math.Max(210, hsnBody.ClientSize.Height - btnAddRow.Height - 2);
                btnAddRow.Left = Math.Max(236, hsnBody.ClientSize.Width - btnAddRow.Width);
                _txtHsnSearch.Top = btnAddRow.Top;
                _txtHsnSearch.Width = Math.Min(260, Math.Max(160, btnAddRow.Left - 12));
                hsnGridHost.Width = Math.Max(260, hsnBody.ClientSize.Width);
                hsnGridHost.Height = Math.Max(150, btnAddRow.Top - 14);
                _gridHsnSac.Width = hsnGridHost.Width;
                _gridHsnSac.Height = hsnGridHost.Height + SystemInformation.HorizontalScrollBarHeight + 2;
                LayoutHsnSacColumns(_gridHsnSac);
            };
            AppRuntime.LogTiming("Settings.BuildForm.Hsn.Complete", 0);

            Panel unitBody = AddModernSettingsCard(parent, "Unit Management", "Add and review global units used across inventory, quotations, invoices, and jobs.", 560);
            BuildUnitManagementCard(unitBody);

            Panel systemBody = AddModernSettingsCard(parent, "System Tools", "Database connection checks, schema repair, setup, and saved dashboard-layout recovery tools.", 438);
            systemBody.Controls.Add(global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
            {
                Text = "Database Health",
                Location = new Point(0, 0),
                Size = new Size(180, 20),
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9f, FontStyle.Bold),
                ForeColor = DS.Slate700
            }));
            _lblDbStatus = new Label { Location = new Point(0, 24), Size = new Size(520, 44), Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9), ForeColor = DS.Slate700 };
            systemBody.Controls.Add(_lblDbStatus);
            Button btnTest = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Test Connection", InfoBlue, 140));
            btnTest.Location = new Point(0, 80);
            btnTest.Click += (s, e) => CheckDbConnection();
            systemBody.Controls.Add(btnTest);
            Button btnSetup = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Connection Setup", SaveGreen, 156));
            btnSetup.Location = new Point(154, 80);
            btnSetup.Click += (s, e) => OpenConnectionSetup();
            systemBody.Controls.Add(btnSetup);
            Button btnRepair = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Repair Database", Color.White, 150));
            btnRepair.ForeColor = DS.Primary600;
            btnRepair.FlatAppearance.BorderSize = 1;
            btnRepair.FlatAppearance.BorderColor = DS.BorderStrong;
            btnRepair.Location = new Point(324, 80);
            btnRepair.Click += async (s, e) => await RepairDatabaseSchemaAsync(btnRepair);
            systemBody.Controls.Add(btnRepair);
            Label layoutTitle = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
            {
                Text = "Layout Recovery",
                Location = new Point(0, 132),
                Size = new Size(180, 20),
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9f, FontStyle.Bold),
                ForeColor = DS.Slate700
            });
            systemBody.Controls.Add(layoutTitle);
            Label layoutHelp = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
            {
                Text = "Reset saved card layouts for specific workspaces when dashboards look broken, cramped, or out of place.",
                Location = new Point(0, 154),
                Size = new Size(530, 34),
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 8.7f),
                ForeColor = DS.Slate500
            });
            systemBody.Controls.Add(layoutHelp);
            Panel resetArea = new Panel { Location = new Point(0, 196), Size = new Size(526, 164), BackColor = Color.White, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom };
            systemBody.Controls.Add(resetArea);
            BuildLayoutResetSection(resetArea);
            systemBody.Resize += (s, e) =>
            {
                int width = Math.Max(320, systemBody.ClientSize.Width);
                _lblDbStatus.Width = width;
                bool stackActions = width < 500;
                btnTest.Top = 80;
                btnTest.Left = 0;
                btnSetup.Top = stackActions ? btnTest.Bottom + 10 : 80;
                btnSetup.Left = stackActions ? 0 : btnTest.Right + 14;
                btnRepair.Top = stackActions ? btnSetup.Bottom + 10 : 80;
                btnRepair.Left = stackActions ? 0 : btnSetup.Right + 14;
                layoutTitle.Top = stackActions ? btnRepair.Bottom + 24 : 132;
                layoutHelp.Top = layoutTitle.Bottom + 4;
                layoutHelp.Width = width;
                resetArea.Top = layoutHelp.Bottom + 8;
                resetArea.Width = width;
                resetArea.Height = Math.Max(150, systemBody.ClientSize.Height - resetArea.Top);
            };

            if (IsAdminUser())
            {
                Panel licenseBody = AddModernSettingsCard(parent, "License Management", "Activation, renewal, device status, and frozen-mode recovery.", 300);
                BuildLicenseSection(licenseBody);

                Panel backupBody = AddModernSettingsCard(parent, "Backup & Recovery", "Configure client-owned network, local, and external-drive SQL backups.", 300);
                BuildBackupRestoreSection(backupBody);

                Panel dataBody = AddModernSettingsCard(parent, "Data Management", "Fresh Start clears transactional records, master data, and settings.", 300);
                BuildFreshStartSection(dataBody);
            }

            Panel diagnosticsBody = AddModernSettingsCard(parent, "Diagnostics & Error Log", "Review local ServoERP exception logs for support and troubleshooting.", 250);
            BuildDiagnosticsErrorLogSection(diagnosticsBody);
        }

        private void BuildDeferredSettingsCards(Panel parent)
        {
            if (_secondarySettingsCardsBuilt || parent == null || parent.IsDisposed)
                return;

            AppRuntime.LogTiming("Settings.BuildForm.ComplianceCards.Start", 0);
            BuildLegalAgreementsCard(parent);
            BuildOpenSourceLicensesCard(parent);
            BuildModuleCatalogCard(parent);
            BuildCompliancePackCard(parent);
            AppRuntime.LogTiming("Settings.BuildForm.ComplianceCards.Complete", 0);
        }

        /// <summary>Builds Settings actions for viewing and maintaining ServoERP error logs.</summary>
        private void BuildDiagnosticsErrorLogSection(Panel parent)
        {
            string logsFolder = ServoERP.Infrastructure.ExceptionLogger.LogFolderPath;

            var label = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
            {
                Text = "Error log location:",
                Location = new Point(0, 0),
                Size = new Size(160, 20),
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 8.5f, FontStyle.Bold),
                ForeColor = DS.Slate500
            });
            parent.Controls.Add(label);

            var txtLogLocation = new TextBox
            {
                Text = logsFolder,
                ReadOnly = true,
                Location = new Point(0, 24),
                Size = new Size(560, 34),
                BorderStyle = BorderStyle.None,
                BackColor = DS.Slate100,
                ForeColor = DS.Slate700
            };
            parent.Controls.Add(txtLogLocation);

            Button btnOpenFolder = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Open Log Folder", InfoBlue, 150));
            btnOpenFolder.Location = new Point(0, 82);
            btnOpenFolder.Click += (s, e) => OpenErrorLogFolder(logsFolder);
            parent.Controls.Add(btnOpenFolder);

            Button btnViewCurrent = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("View Current Log", SaveGreen, 150));
            btnViewCurrent.Location = new Point(166, 82);
            btnViewCurrent.Click += (s, e) => ViewCurrentErrorLog();
            parent.Controls.Add(btnViewCurrent);

            Button btnClearOld = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Clear Old Logs", Color.White, 140));
            btnClearOld.ForeColor = DS.Slate700;
            btnClearOld.FlatAppearance.BorderSize = 1;
            btnClearOld.FlatAppearance.BorderColor = DS.Border;
            btnClearOld.Location = new Point(332, 82);
            btnClearOld.Click += (s, e) => ClearOldErrorLogs(logsFolder);
            parent.Controls.Add(btnClearOld);
        }

        /// <summary>Opens the folder that contains monthly exception logs.</summary>
        private void OpenErrorLogFolder(string logsFolder)
        {
            try
            {
                Directory.CreateDirectory(logsFolder);
                Process.Start("explorer.exe", logsFolder);
            }
            catch (Exception ex)
            {
                ShowError( "Could not open the error log folder.", ex);
            }
        }

        /// <summary>Opens the current monthly exception log in Notepad when it exists.</summary>
        private void ViewCurrentErrorLog()
        {
            try
            {
                string logPath = ServoERP.Infrastructure.ExceptionLogger.CurrentLogPath();
                if (string.IsNullOrWhiteSpace(logPath))
                {
                    RunOnUI(() =>
                        MessageBox.Show(global::HVAC_Pro_Desktop.Services.LanguageManager.Get("No errors logged this month."), BrandingService.WindowTitle("Diagnostics"), MessageBoxButtons.OK, MessageBoxIcon.Information));
                    return;
                }

                Process.Start("notepad.exe", logPath);
            }
            catch (Exception ex)
            {
                ShowError( "Could not open the current error log.", ex);
            }
        }

        /// <summary>Deletes monthly exception logs older than 90 days after confirmation.</summary>
        private void ClearOldErrorLogs(string logsFolder)
        {
            try
            {
                bool confirm = ServoERP.Infrastructure.ServoConfirmDialog.Show(
                    this,
                    "Clear old ServoERP logs",
                    "ServoERP will delete local .log files older than 90 days from the diagnostics log folder. Current logs and business data are not touched.");
                if (!confirm)
                    return;

                int deleted = 0;
                if (Directory.Exists(logsFolder))
                {
                    DateTime cutoff = DateTime.Now.AddDays(-90);
                    foreach (string file in Directory.GetFiles(logsFolder, "*.log"))
                    {
                        if (File.GetLastWriteTime(file) >= cutoff)
                            continue;

                        File.Delete(file);
                        deleted++;
                    }
                }

                RunOnUI(() =>
                    MessageBox.Show(global::HVAC_Pro_Desktop.Services.LanguageManager.Format("{0} old log file(s) deleted.", deleted), BrandingService.WindowTitle("Clear Old Logs"), MessageBoxButtons.OK, MessageBoxIcon.Information));
            }
            catch (Exception ex)
            {
                ShowError( "Could not clear old error logs.", ex);
            }
        }

        private void BuildGeneralSettingsGuide(Panel parent)
        {
            Panel body = AddModernSettingsCard(parent, "Settings Control Center", "Keep release health, protection, and daily admin actions visible before diving into each settings card.", 318);

            Label intro = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
            {
                Text = "This workspace is strongest when it answers three questions quickly: is the build healthy, is the client protected, and what should the admin do next?",
                Location = new Point(0, 0),
                Size = new Size(520, 38),
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9f),
                ForeColor = DS.Slate600
            });
            body.Controls.Add(intro);

            FlowLayoutPanel summaryFlow = new FlowLayoutPanel
            {
                Location = new Point(0, 52),
                Size = new Size(530, 88),
                BackColor = Color.White,
                WrapContents = true
            };
            _lblSettingsVersionState = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(AddSummaryCard(summaryFlow, "Release", ConfigService.GetAppVersion(), DS.Primary600));
            _lblSettingsDbState = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(AddSummaryCard(summaryFlow, "Database", "Checking", SaveGreen));
            _lblSettingsBackupState = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(AddSummaryCard(summaryFlow, "Backup", "Pending", DS.Amber600));
            _lblSettingsLicenseState = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(AddSummaryCard(summaryFlow, "License", "Review", DS.Red600));
            body.Controls.Add(summaryFlow);

            Label playbook = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
            {
                Text = "Suggested order: company profile, India defaults, display fit, backups, then risk controls and updates.",
                Location = new Point(0, 150),
                Size = new Size(530, 18),
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 8.7f, FontStyle.Bold),
                ForeColor = DS.Slate500
            });
            body.Controls.Add(playbook);

            FlowLayoutPanel actionFlow = new FlowLayoutPanel
            {
                Location = new Point(0, 182),
                Size = new Size(530, 80),
                BackColor = Color.White,
                WrapContents = true
            };
            Button btnOpenCompany = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Review Company", Color.White, 146));
            btnOpenCompany.ForeColor = DS.Primary600;
            btnOpenCompany.FlatAppearance.BorderColor = DS.Border;
            btnOpenCompany.FlatAppearance.BorderSize = 1;
            btnOpenCompany.Click += (s, e) => _txtCompanyName.Focus();
            Button btnCheckDatabase = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Check Database", InfoBlue, 142));
            btnCheckDatabase.Click += (s, e) => CheckDbConnection();
            Button btnBackupNow = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Backup Now", SaveGreen, 122));
            btnBackupNow.Click += async (s, e) => await CreateBackupAsync();
            Button btnOpenUsers = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Users & Logins", Color.White, 138));
            btnOpenUsers.ForeColor = DS.Slate700;
            btnOpenUsers.FlatAppearance.BorderColor = DS.Border;
            btnOpenUsers.FlatAppearance.BorderSize = 1;
            btnOpenUsers.Click += (s, e) =>
            {
                if (_usersTab != null)
                    _tabs.SelectedTab = _usersTab;
            };
            Button btnCheckUpdates = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Check Updates", Color.White, 132));
            btnCheckUpdates.ForeColor = DS.Primary600;
            btnCheckUpdates.FlatAppearance.BorderColor = DS.Border;
            btnCheckUpdates.FlatAppearance.BorderSize = 1;
            btnCheckUpdates.Click += async (s, e) => await CheckVersionNowAsync();
            actionFlow.Controls.AddRange(new Control[] { btnOpenCompany, btnCheckDatabase, btnBackupNow, btnOpenUsers, btnCheckUpdates });
            body.Controls.Add(actionFlow);
        }

        private Panel BuildSettingsGuideRow(string title, string text, Color accent)
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 8), BackColor = Color.FromArgb(248, 250, 252), Padding = new Padding(10, 7, 10, 6) };
            DS.Rounded(panel, 8);
            Label titleLabel = global::HVAC_Pro_Desktop.Services.LanguageManager.DataText(new Label { Text = title, Dock = DockStyle.Top, Height = 20, Font = DS.SmallBold, ForeColor = accent });
            Label bodyLabel = global::HVAC_Pro_Desktop.Services.LanguageManager.DataText(new Label { Text = text, Dock = DockStyle.Fill, Font = DS.Caption, ForeColor = DS.Slate600 });
            panel.Controls.Add(bodyLabel);
            panel.Controls.Add(titleLabel);
            return panel;
        }

        private void BuildHelpSupportCard(Panel parent)
        {
            Panel body = AddModernSettingsCard(parent, "Help & Support", "Open knowledge base, health checks, diagnostics, update tools, and support brief from Settings.", 280);

            Label icon = ModernIconSystem.Badge(ModernIconKind.Service, 46, DS.Primary50, DS.Primary600, 12);
            icon.Location = new Point(0, 4);
            body.Controls.Add(icon);

            Label title = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
            {
                Text = "Need help with ServoERP?",
                Location = new Point(64, 4),
                Size = new Size(420, 28),
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 12f, FontStyle.Bold),
                ForeColor = DS.Slate900
            });
            body.Controls.Add(title);

            Label text = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
            {
                Text = "Use this for guided help, database checks, diagnostics export, update verification, and support handover information.",
                Location = new Point(64, 38),
                Size = new Size(430, 56),
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9f),
                ForeColor = DS.Slate600
            });
            body.Controls.Add(text);

            Button open = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Open Help & Support", InfoBlue, 178));
            open.Name = "btnSettingsHelpSupport";
            open.UseMnemonic = false;
            ModernIconSystem.AddButtonIcon(open, ModernIconKind.Service);
            open.Location = new Point(64, 112);
            open.Click += (s, e) => OpenHelpSupportFromSettings();
            body.Controls.Add(open);

            Button logs = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("View Error Logs", Color.White, 150));
            logs.Name = "btnSettingsViewErrorLogs";
            logs.UseMnemonic = false;
            logs.Location = new Point(open.Right + 12, 112);
            logs.Click += (s, e) => CrashProtectionService.SafeShowDialog(this, "Open error log viewer", () => new ErrorLogViewerForm());
            body.Controls.Add(logs);

            body.Resize += (s, e) =>
            {
                title.Width = Math.Max(220, body.ClientSize.Width - title.Left - 12);
                text.Width = Math.Max(220, body.ClientSize.Width - text.Left - 12);
                if (logs.Right > body.ClientSize.Width - 12)
                    logs.Location = new Point(64, open.Bottom + 10);
            };
        }

        private void OpenHelpSupportFromSettings()
        {
            MainForm main = FindForm() as MainForm;
            if (main != null)
            {
                main.ShowSupportCenterDrawer();
                return;
            }

            CrashProtectionService.SafeShowDialog(this, "Open Help & Support", () => new SupportCenterDialog());
        }

        private void BuildLoginAccessSection(Panel parent)
        {
            Panel cardBody = AddModernSettingsCard(parent, "User Logins", "Create and manage staff logins from Settings.", 300);

            cardBody.Controls.Add(global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
            {
                Text = "Add new usernames, reset passwords, assign roles, and deactivate access.",
                Location = new Point(0, 0),
                Width = 420,
                Height = 58,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 12, FontStyle.Bold),
                ForeColor = DS.Slate900
            }));
            cardBody.Controls.Add(global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
            {
                Text = "Use this for admin-controlled staff access without leaving Settings.",
                Location = new Point(0, 70),
                Width = 420,
                Height = 44,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9),
                ForeColor = DS.Slate500
            }));

            Button btnCreateLogin = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Create Login", SaveGreen, 120));
            btnCreateLogin.Location = new Point(0, 142);
            btnCreateLogin.Click += (s, e) =>
            {
                OpenUserManagementTab();
                AddUser();
            };

            Button btnManageLogins = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Open Logins", InfoBlue, 120));
            btnManageLogins.Location = new Point(136, 142);
            btnManageLogins.Click += (s, e) => OpenUserManagementTab();

            cardBody.Controls.Add(btnCreateLogin);
            cardBody.Controls.Add(btnManageLogins);
        }

        private void BuildUpdateNotificationsCard(Panel parent)
        {
            Panel updatesBody = AddModernSettingsCard(parent, "About & Updates", "GitHub Releases powered updates and installed version information.", 370);
            _txtVersionCheckUrl = new TextBox
            {
                ReadOnly = true,
                TabStop = false,
                BackColor = DS.Slate50,
                ForeColor = DS.Slate700
            };
            PlaceLabeledControl(updatesBody, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("GitHub Releases Repository"), _txtVersionCheckUrl, 0, 0, 444);
            Button btnCopy = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Copy", Color.White, 72));
            btnCopy.ForeColor = DS.Slate700;
            btnCopy.FlatAppearance.BorderSize = 1;
            btnCopy.FlatAppearance.BorderColor = DS.Border;
            btnCopy.Location = new Point(456, 20);
            btnCopy.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnCopy.Click += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(_txtVersionCheckUrl.Text))
                    UIHelper.TrySetClipboardText(this, _txtVersionCheckUrl.Text.Trim(), BrandingService.WindowTitle("Settings"));
            };
            updatesBody.Controls.Add(btnCopy);
            updatesBody.Resize += (s, e) =>
            {
                btnCopy.Left = Math.Max(0, updatesBody.ClientSize.Width - btnCopy.Width - 2);
                _txtVersionCheckUrl.Width = Math.Max(180, btnCopy.Left - 14);
            };
            _chkVersionCheckEnabled = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new CheckBox
            {
                Text = "Check for updates automatically",
                Location = new Point(0, 78),
                Width = 300,
                Height = 26,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9f),
                ForeColor = DS.Slate700,
                BackColor = Color.White
            });
            updatesBody.Controls.Add(_chkVersionCheckEnabled);

            _chkSilentAutoUpdateEnabled = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new CheckBox
            {
                Text = "Automatically download updates and install when ServoERP closes",
                Location = new Point(0, 106),
                Width = 300,
                Height = 26,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9f),
                ForeColor = DS.Slate700,
                BackColor = Color.White
            });
            updatesBody.Controls.Add(_chkSilentAutoUpdateEnabled);

            _lblInstalledVersion = global::HVAC_Pro_Desktop.Services.LanguageManager.DataText(new Label
            {
                Text = global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Current version: {0}", ConfigService.GetAppVersion()),
                Location = new Point(0, 150),
                Width = 320,
                Height = 22,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9f, FontStyle.Bold),
                ForeColor = SaveGreen
            });
            updatesBody.Controls.Add(_lblInstalledVersion);

            _lblLastUpdateCheckStatus = global::HVAC_Pro_Desktop.Services.LanguageManager.DataText(new Label
            {
                Text = UpdateService.GetLastUpdateStatusDisplay(),
                Location = new Point(0, 182),
                Width = 520,
                Height = 48,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 8.8f),
                ForeColor = DS.Slate600
            });
            updatesBody.Controls.Add(_lblLastUpdateCheckStatus);

            Button btnCheckNow = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Check for Updates", InfoBlue, 170));
            btnCheckNow.Location = new Point(0, 246);
            btnCheckNow.Click += async (s, e) => await CheckVersionNowAsync();
            updatesBody.Controls.Add(btnCheckNow);

            Button btnReleaseNotes = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Release Notes", InfoBlue, 150));
            btnReleaseNotes.Name = "ViewInstalledReleaseNotes";
            btnReleaseNotes.Location = new Point(182, 246);
            btnReleaseNotes.Click += (sender, args) =>
            {
                using (var dialog = new ReleaseNotesDialog(UpdateService.GetCurrentAssemblyVersion(), UpdateService.GetInstalledReleaseNotesText()))
                    dialog.ShowDialog(FindForm());
            };
            updatesBody.Controls.Add(btnReleaseNotes);
        }

        private void BuildUnitManagementCard(Panel parent)
        {
            _txtUnitCode = new TextBox { CharacterCasing = CharacterCasing.Upper };
            _txtUnitShortCode = new TextBox { CharacterCasing = CharacterCasing.Upper };
            _txtUnitDisplayName = new TextBox();
            _cmbUnitCategory = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };
            _cmbUnitMeasurementSystem = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _txtUnitAliases = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                AcceptsReturn = false
            };

            _cmbUnitMeasurementSystem.Items.AddRange(new object[] { "", "Metric", "Imperial", "Mixed", "Count", "Service" });
            _cmbUnitMeasurementSystem.SelectedIndex = 0;

            PlaceLabeledControl(parent, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Unit Code *"), _txtUnitCode, 0, 0, 110);
            PlaceLabeledControl(parent, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Short Code"), _txtUnitShortCode, 126, 0, 110);
            PlaceLabeledControl(parent, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Display Name *"), _txtUnitDisplayName, 252, 0, 268);
            PlaceLabeledControl(parent, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Category"), _cmbUnitCategory, 0, 74, 250);
            PlaceLabeledControl(parent, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Measurement System"), _cmbUnitMeasurementSystem, 266, 74, 254);
            PlaceLabeledControl(parent, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Aliases"), _txtUnitAliases, 0, 148, 520, 58);

            Label aliasHelp = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
            {
                Text = "Enter aliases separated by commas, for example: meter, metres, mtr",
                Location = new Point(0, 202),
                Size = new Size(520, 18),
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 8.5f),
                ForeColor = DS.Slate500
            });
            parent.Controls.Add(aliasHelp);

            Button btnAddUnit = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Add Unit", SaveGreen, 120));
            btnAddUnit.Location = new Point(0, 234);
            btnAddUnit.Click += (s, e) => SaveUnitFromSettings();
            parent.Controls.Add(btnAddUnit);

            Button btnRefreshUnits = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Refresh Units", InfoBlue, 126));
            btnRefreshUnits.Location = new Point(136, 234);
            btnRefreshUnits.Click += (s, e) => RefreshUnitManagementCard();
            parent.Controls.Add(btnRefreshUnits);

            _lblUnitSummary = new Label
            {
                Location = new Point(278, 238),
                Size = new Size(242, 20),
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 8.8f, FontStyle.Bold),
                ForeColor = DS.Slate600,
                TextAlign = ContentAlignment.MiddleRight
            };
            parent.Controls.Add(_lblUnitSummary);

            Label gridLabel = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
            {
                Text = "Available Units",
                Location = new Point(0, 274),
                Size = new Size(220, 20),
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9f, FontStyle.Bold),
                ForeColor = DS.Slate700
            });
            parent.Controls.Add(gridLabel);

            Panel gridHost = new Panel
            {
                Location = new Point(0, 302),
                Size = new Size(520, 156),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            parent.Controls.Add(gridHost);

            _gridUnits = new DataGridView
            {
                Location = Point.Empty,
                Size = new Size(520, 156),
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                MultiSelect = false
            };
            StyleDataGrid(_gridUnits);
            gridHost.Controls.Add(_gridUnits);

            Action layout = () =>
            {
                int width = Math.Max(320, parent.ClientSize.Width);
                int gap = 16;
                bool narrow = width < 470;
                int actionsTop;
                int summaryTop;
                int summaryHeight;

                if (narrow)
                {
                    int shortWidth = Math.Max(110, Math.Min(140, (width - gap) / 2));
                    int codeWidth = Math.Max(110, width - shortWidth - gap);
                    SetLabeledControlBounds(parent, "Unit Code *", _txtUnitCode, 0, 0, codeWidth);
                    SetLabeledControlBounds(parent, "Short Code", _txtUnitShortCode, codeWidth + gap, 0, shortWidth);
                    SetLabeledControlBounds(parent, "Display Name *", _txtUnitDisplayName, 0, 74, width);
                    SetLabeledControlBounds(parent, "Category", _cmbUnitCategory, 0, 148, width);
                    SetLabeledControlBounds(parent, "Measurement System", _cmbUnitMeasurementSystem, 0, 222, width);
                    SetLabeledControlBounds(parent, "Aliases", _txtUnitAliases, 0, 296, width, 58);
                    aliasHelp.Top = 376;
                    aliasHelp.Width = width;
                    btnAddUnit.Top = 410;
                    btnAddUnit.Left = 0;
                    btnRefreshUnits.Top = 410;
                    btnRefreshUnits.Left = Math.Min(width - btnRefreshUnits.Width, btnAddUnit.Right + 12);
                    actionsTop = btnAddUnit.Top;
                    summaryTop = btnAddUnit.Bottom + 10;
                    summaryHeight = 20;
                    _lblUnitSummary.TextAlign = ContentAlignment.MiddleLeft;
                    _lblUnitSummary.Left = 0;
                    _lblUnitSummary.Top = summaryTop;
                    _lblUnitSummary.Width = width;
                    gridLabel.Top = _lblUnitSummary.Bottom + 12;
                }
                else
                {
                    int leftWidth = Math.Max(150, (width - gap) / 2);
                    int rightX = leftWidth + gap;
                    int rightWidth = Math.Max(150, width - rightX);
                    SetLabeledControlBounds(parent, "Unit Code *", _txtUnitCode, 0, 0, 110);
                    SetLabeledControlBounds(parent, "Short Code", _txtUnitShortCode, 126, 0, 110);
                    SetLabeledControlBounds(parent, "Display Name *", _txtUnitDisplayName, 252, 0, Math.Max(160, width - 252));
                    SetLabeledControlBounds(parent, "Category", _cmbUnitCategory, 0, 74, leftWidth);
                    SetLabeledControlBounds(parent, "Measurement System", _cmbUnitMeasurementSystem, rightX, 74, rightWidth);
                    SetLabeledControlBounds(parent, "Aliases", _txtUnitAliases, 0, 148, width, 58);
                    aliasHelp.Top = 226;
                    aliasHelp.Width = width;
                    btnAddUnit.Top = 258;
                    btnAddUnit.Left = 0;
                    btnRefreshUnits.Top = 258;
                    btnRefreshUnits.Left = btnAddUnit.Right + 16;
                    actionsTop = btnAddUnit.Top;
                    summaryTop = actionsTop + 4;
                    summaryHeight = 22;
                    _lblUnitSummary.TextAlign = ContentAlignment.MiddleRight;
                    _lblUnitSummary.Top = summaryTop;
                    _lblUnitSummary.Width = Math.Min(260, Math.Max(180, width - btnRefreshUnits.Right - 16));
                    _lblUnitSummary.Left = Math.Max(btnRefreshUnits.Right + 12, width - _lblUnitSummary.Width);
                    gridLabel.Top = btnAddUnit.Bottom + 18;
                }

                aliasHelp.Left = 0;
                _lblUnitSummary.Height = summaryHeight;
                gridLabel.Left = 0;
                gridLabel.Width = Math.Min(240, width);
                aliasHelp.Width = width;
                gridHost.Top = gridLabel.Bottom + 8;
                gridHost.Left = 0;
                gridHost.Width = width;
                gridHost.Height = Math.Max(150, parent.ClientSize.Height - gridHost.Top);
                _gridUnits.Width = gridHost.ClientSize.Width;
                _gridUnits.Height = gridHost.ClientSize.Height;

                gridHost.SendToBack();
                _gridUnits.SendToBack();
                gridLabel.BringToFront();
                _lblUnitSummary.BringToFront();
                btnAddUnit.BringToFront();
                btnRefreshUnits.BringToFront();
                aliasHelp.BringToFront();
                _txtUnitCode.BringToFront();
                _txtUnitShortCode.BringToFront();
                _txtUnitDisplayName.BringToFront();
                _cmbUnitCategory.BringToFront();
                _cmbUnitMeasurementSystem.BringToFront();
                _txtUnitAliases.BringToFront();
            };
            parent.Resize += (s, e) => layout();
            layout();
        }

        private void BuildLegalAgreementsCard(Panel parent)
        {
            Panel legalBody = AddModernSettingsCard(parent, "Legal Agreements", "View EULA, Privacy Policy, Data Processing Policy, and Disclaimer.", 220);
            Label help = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
            {
                Text = "Review the legal agreements accepted during first launch. This viewer is read-only and does not change acceptance status.",
                Location = new Point(0, 0),
                Size = new Size(528, 48),
                Font = DS.Small,
                ForeColor = DS.Slate600
            });
            legalBody.Controls.Add(help);

            Button viewLegal = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("View Legal Agreements", InfoBlue, 190));
            viewLegal.Location = new Point(0, 72);
            viewLegal.Click += (s, e) =>
            {
                using (var form = new LegalAgreementForm(true))
                    form.ShowDialog(FindForm());
            };
            legalBody.Controls.Add(viewLegal);
        }

        private void BuildOpenSourceLicensesCard(Panel parent)
        {
            Panel body = AddModernSettingsCard(parent, "Open Source & Licenses", "Review third-party components, license notes, and export audit disclosure.", 240);
            Label help = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
            {
                Text = "Enterprise clients often ask which open-source components are bundled. Use this disclosure for procurement, compliance, and IT review.",
                Location = new Point(0, 0),
                Size = new Size(528, 58),
                Font = DS.Small,
                ForeColor = DS.Slate600
            });
            body.Controls.Add(help);

            Button view = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("View Open Source", InfoBlue, 170));
            view.Location = new Point(0, 82);
            view.Click += (s, e) =>
            {
                using (var form = new OpenSourceLicenseForm())
                    form.ShowDialog(FindForm());
            };
            body.Controls.Add(view);

            Button export = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Export Disclosure", SaveGreen, 170));
            export.Location = new Point(186, 82);
            export.Click += (s, e) =>
            {
                try
                {
            string path = _openSourceLicenseSvc.ExportDisclosureReport();
                    global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblStatus, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Open-source disclosure exported: {0}", path));
                    _lblStatus.ForeColor = SaveGreen;
                    System.Diagnostics.Process.Start("notepad.exe", path);
                }
                catch (Exception ex)
                {
                    global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblStatus, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Disclosure export failed: {0}", ex.Message));
                    _lblStatus.ForeColor = Color.Red;
                }
            };
            body.Controls.Add(export);
        }

        private void BuildModuleCatalogCard(Panel parent)
        {
            Panel body = AddModernSettingsCard(parent, "Module Catalog", "Review installed modules and extension-ready roadmap ideas.", 240);
            Label help = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
            {
                Text = "Use this catalog as the client-facing module map: what is installed today, what pattern inspired it, and what can be extended next.",
                Location = new Point(0, 0),
                Size = new Size(528, 58),
                Font = DS.Small,
                ForeColor = DS.Slate600
            });
            body.Controls.Add(help);

            Button view = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("View Catalog", InfoBlue, 150));
            view.Location = new Point(0, 82);
            view.Click += (s, e) =>
            {
                using (var form = new ModuleCatalogForm())
                    form.ShowDialog(FindForm());
            };
            body.Controls.Add(view);

            Button export = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Export Catalog", SaveGreen, 150));
            export.Location = new Point(166, 82);
            export.Click += (s, e) =>
            {
                try
                {
            string path = _moduleCatalogSvc.ExportReport();
                    global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblStatus, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Module catalog exported: {0}", path));
                    _lblStatus.ForeColor = SaveGreen;
                    System.Diagnostics.Process.Start("notepad.exe", path);
                }
                catch (Exception ex)
                {
                    global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblStatus, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Catalog export failed: {0}", ex.Message));
                    _lblStatus.ForeColor = Color.Red;
                }
            };
            body.Controls.Add(export);
        }

        private void BuildCompliancePackCard(Panel parent)
        {
            Panel body = AddModernSettingsCard(parent, "Compliance Export Pack", "Generate a local legal, license, module, and readiness ZIP.", 240);
            Label help = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
            {
                Text = "Create a local handover pack for client IT, procurement, and audit review. No passwords, license keys, or database records are uploaded.",
                Location = new Point(0, 0),
                Size = new Size(528, 58),
                Font = DS.Small,
                ForeColor = DS.Slate600
            });
            body.Controls.Add(help);

            Button view = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Open Exporter", InfoBlue, 150));
            view.Location = new Point(0, 82);
            view.Click += (s, e) =>
            {
                using (var form = new CompliancePackForm())
                    form.ShowDialog(FindForm());
            };
            body.Controls.Add(view);

            Button export = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Generate Pack", SaveGreen, 150));
            export.Location = new Point(166, 82);
            export.Click += (s, e) =>
            {
                try
                {
            string path = _compliancePackSvc.ExportPack();
                    global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblStatus, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Compliance pack created: {0}", path));
                    _lblStatus.ForeColor = SaveGreen;
                    System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\"");
                }
                catch (Exception ex)
                {
                    global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblStatus, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Compliance pack failed: {0}", ex.Message));
                    _lblStatus.ForeColor = Color.Red;
                }
            };
            body.Controls.Add(export);
        }

        private void LoadSettings()
        {
            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                AppRuntime.LogTiming("Settings.LoadSettings.Start", 0);
                IndiaCompanySettings settings = _svc.GetIndiaCompanySettings();
                AppRuntime.LogTiming("Settings.LoadSettings.CompanyLoaded", watch.ElapsedMilliseconds);
                _txtCompanyName.Text = settings.CompanyName;
                _txtAuthorisedSignatory.Text = settings.AuthorisedSignatoryName;
                _txtGST.Text = settings.GSTIN;
                _txtPAN.Text = settings.PAN;
                _txtTAN.Text = settings.TAN;
                _txtPhone.Text = settings.Phone;
                _txtEmail.Text = settings.Email;
                _txtAddress.Text = settings.Address;
                _txtOfficeLatitude.Text = settings.OfficeLatitude.HasValue ? settings.OfficeLatitude.Value.ToString("0.0000000", CultureInfo.InvariantCulture) : string.Empty;
                _txtOfficeLongitude.Text = settings.OfficeLongitude.HasValue ? settings.OfficeLongitude.Value.ToString("0.0000000", CultureInfo.InvariantCulture) : string.Empty;
                _txtPrefix.Text = settings.InvoicePrefix;
                _txtCurrency.Text = settings.CurrencyCode + " (" + settings.CurrencySymbol + ")";
                _txtFinancialYear.Text = settings.FinancialYearPattern;
                _numGSTRate.Value = Clamp(_numGSTRate, settings.DefaultGSTRate);
                _numPayTerms.Value = Clamp(_numPayTerms, settings.DefaultPaymentTermsDays);
                _numMarkupPct.Value = Clamp(_numMarkupPct, ParseDecimal(_svc.Get("DefaultMarkupPct", "25"), 25m));
                _numAnnualTurnover.Value = Clamp(_numAnnualTurnover, settings.AnnualTurnover);
                _numEInvoiceThreshold.Value = Clamp(_numEInvoiceThreshold, settings.EInvoiceThresholdAmount);
                SelectCombo(_cmbState, settings.CompanyState, "Maharashtra");
                SelectCombo(_cmbGstRegistrationType, settings.GSTRegistrationType, "Regular");
                if (_txtVersionCheckUrl != null)
                {
                    _txtVersionCheckUrl.Text = UpdateService.GetGitHubRepositoryUrl();
                }
                if (_chkVersionCheckEnabled != null)
                    _chkVersionCheckEnabled.Checked = ConfigService.IsVersionCheckEnabled();
                if (_chkSilentAutoUpdateEnabled != null)
                    _chkSilentAutoUpdateEnabled.Checked = ConfigService.IsSilentAutoUpdateEnabled();
                RefreshUnitManagementCard();
                RefreshRuntimeSettingsLabels();
                LoadDisplayFitSetting();
                LoadUiScaleSetting();
                AppRuntime.LogTiming("Settings.LoadSettings.RuntimeLoaded", watch.ElapsedMilliseconds);

                RefreshIndiaDefaultsPreview();
                BeginLoadHsnSacGrid();
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("SettingsForm.LoadSettings", ex);
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblStatus, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Load error: {0}", ex.Message));
                _lblStatus.ForeColor = Color.Red;
            }
        }

        private void RefreshUnitManagementCard()
        {
            if (_gridUnits == null)
                return;

            List<UnitMeasurement> units = _unitMeasurementSvc.GetUnits().ToList();
            _gridUnits.DataSource = units
                .Where(unit => unit != null)
                .Select(unit => new
                {
                    Code = unit.UnitCode,
                    Short = string.IsNullOrWhiteSpace(unit.ShortCode) ? unit.UnitCode : unit.ShortCode,
                    Name = unit.DisplayName,
                    Category = unit.Category,
                    System = unit.MeasurementSystem
                })
                .ToList();

            if (_gridUnits.Columns.Count > 0)
            {
                if (_gridUnits.Columns["Code"] != null) _gridUnits.Columns["Code"].HeaderText = global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Code");
                if (_gridUnits.Columns["Short"] != null) _gridUnits.Columns["Short"].HeaderText = global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Short");
                if (_gridUnits.Columns["Name"] != null) _gridUnits.Columns["Name"].HeaderText = global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Display Name");
                if (_gridUnits.Columns["Category"] != null) _gridUnits.Columns["Category"].HeaderText = global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Category");
                if (_gridUnits.Columns["System"] != null) _gridUnits.Columns["System"].HeaderText = global::HVAC_Pro_Desktop.Services.LanguageManager.Get("System");
            }

            if (_lblUnitSummary != null)
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblUnitSummary, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("{0} global units", units.Count.ToString("N0")));

            if (_cmbUnitCategory != null)
            {
                string selected = _cmbUnitCategory.Text;
                string[] defaults =
                {
                    "Length", "Area", "Volume", "Weight and Mass", "Pressure", "Temperature",
                    "Energy and Power", "Electrical", "Airflow and Velocity", "Refrigerant and Gas",
                    "Concentration and Purity", "Count and Packaging", "Length of run", "Time",
                    "Service billing", "Consumable dispensing"
                };

                List<string> categories = units
                    .Select(unit => (unit.Category ?? string.Empty).Trim())
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .Concat(defaults)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(text => text)
                    .ToList();

                _cmbUnitCategory.BeginUpdate();
                try
                {
                    _cmbUnitCategory.Items.Clear();
                    foreach (string category in categories)
                        _cmbUnitCategory.Items.Add(category);
                    _cmbUnitCategory.Text = selected;
                }
                finally
                {
                    _cmbUnitCategory.EndUpdate();
                }
            }
        }

        private void SaveUnitFromSettings()
        {
            string[] aliases = (_txtUnitAliases?.Text ?? string.Empty)
                .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(text => text.Trim())
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            string message;
            bool saved = _unitMeasurementSvc.TryAddUnit(
                (_txtUnitCode?.Text ?? string.Empty).Trim(),
                (_txtUnitShortCode?.Text ?? string.Empty).Trim(),
                (_txtUnitDisplayName?.Text ?? string.Empty).Trim(),
                (_cmbUnitCategory?.Text ?? string.Empty).Trim(),
                (_cmbUnitMeasurementSystem?.Text ?? string.Empty).Trim(),
                aliases,
                out message);

            if (!saved)
            {
                MessageBox.Show(message, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Add Unit"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblStatus, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Unit save failed: {0}", message));
                _lblStatus.ForeColor = Color.Red;
                return;
            }

            global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblStatus, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Unit added to the global list: {0}", ((_txtUnitShortCode?.Text ?? string.Empty).Trim().Length > 0 ? _txtUnitShortCode.Text.Trim() : _txtUnitCode.Text.Trim())));
            _lblStatus.ForeColor = SaveGreen;
            ClearUnitEntry();
            RefreshUnitManagementCard();
        }

        private void ClearUnitEntry()
        {
            if (_txtUnitCode != null) _txtUnitCode.Clear();
            if (_txtUnitShortCode != null) _txtUnitShortCode.Clear();
            if (_txtUnitDisplayName != null) _txtUnitDisplayName.Clear();
            if (_cmbUnitCategory != null) _cmbUnitCategory.Text = string.Empty;
            if (_cmbUnitMeasurementSystem != null) _cmbUnitMeasurementSystem.SelectedIndex = 0;
            if (_txtUnitAliases != null) _txtUnitAliases.Clear();
        }

        private Control BuildUsersTabContent()
        {
            Panel page = new Panel { Dock = DockStyle.Fill, BackColor = DS.BgPage, Padding = new Padding(22, 18, 22, 22) };
            FlowLayoutPanel summary = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 92, WrapContents = false, BackColor = DS.BgPage };
            _lblUserTotal = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(AddSummaryCard(summary, "Total Users", "0", InfoBlue));
            _lblUserActive = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(AddSummaryCard(summary, "Active Users", "0", SaveGreen));
            _lblUserAdmins = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(AddSummaryCard(summary, "Admin Users", "0", DS.Primary700));
            _lblUserLastLogin = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(AddSummaryCard(summary, "Last Login", "-", DS.Slate700));

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = DS.BgPage, Padding = new Padding(0, 12, 0, 12) };
            FlowLayoutPanel toolbarInner = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = DS.BgPage, WrapContents = true };
            Button btnAdd = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Add User", SaveGreen, 110));
            Button btnEdit = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Edit User", InfoBlue, 110));
            Button btnReset = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Reset Password", Color.FromArgb(211, 84, 0), 128));
            Button btnDeactivate = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Deactivate", Color.FromArgb(220, 38, 38), 110));
            btnAdd.Margin = btnEdit.Margin = btnReset.Margin = btnDeactivate.Margin = new Padding(0, 0, 10, 8);
            btnAdd.Click += (s, e) => AddUser();
            btnEdit.Click += (s, e) => EditSelectedUser();
            btnReset.Click += (s, e) => ResetSelectedUserPassword();
            btnDeactivate.Click += (s, e) => ToggleSelectedUserActive(false);
            toolbarInner.Controls.AddRange(new Control[] { btnAdd, btnEdit, btnReset, btnDeactivate });
            toolbar.Controls.Add(toolbarInner);

            Panel gridCard = BuildPlainCard();
            gridCard.Dock = DockStyle.Fill;
            gridCard.Padding = new Padding(14);
            _gridUsers = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            StyleDataGrid(_gridUsers);
            gridCard.Controls.Add(_gridUsers);

            page.Controls.Add(gridCard);
            page.Controls.Add(toolbar);
            page.Controls.Add(summary);
            return page;
        }

        private TabPage BuildUsersTab()
        {
            TabPage tab = new TabPage("Users & Logins") { BackColor = DS.BgPage };
            tab.Controls.Add(BuildUsersTabContent());
            return tab;
        }

        private LazyTabPage CreateLazySettingsTab(string title, Func<Control> builder)
        {
            LazyTabPage tab = new LazyTabPage(title, builder);
            tab.BackColor = DS.BgPage;
            return tab;
        }

        private Control BuildAuditTabContent()
        {
            Panel page = new Panel { Dock = DockStyle.Fill, BackColor = DS.BgPage, Padding = new Padding(22, 18, 22, 22) };
            FlowLayoutPanel summary = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 92, WrapContents = false, BackColor = DS.BgPage };
            _lblAuditTotal = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(AddSummaryCard(summary, "Total Events", "0", InfoBlue));
            _lblAuditLogin = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(AddSummaryCard(summary, "Login Events", "0", SaveGreen));
            _lblAuditWarnings = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(AddSummaryCard(summary, "Failed / Warning", "0", DS.Red600));

            Panel toolbar = BuildPlainCard();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 70;
            toolbar.Padding = new Padding(14, 12, 14, 12);
            FlowLayoutPanel toolbarInner = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, BackColor = Color.White };
            toolbarInner.Controls.Add(global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(FilterLabel("From")));
            _dtAuditFrom = new DateTimePicker { Width = 130, Format = DateTimePickerFormat.Short, Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9), Margin = new Padding(0, 0, 18, 8) };
            toolbarInner.Controls.Add(_dtAuditFrom);
            toolbarInner.Controls.Add(global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(FilterLabel("To")));
            _dtAuditTo = new DateTimePicker { Width = 130, Format = DateTimePickerFormat.Short, Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9), Margin = new Padding(0, 0, 18, 8) };
            toolbarInner.Controls.Add(_dtAuditTo);
            toolbarInner.Controls.Add(global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(FilterLabel("User")));
            _cmbAuditUser = new ComboBox { Width = 180, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9), Margin = new Padding(0, 0, 18, 8) };
            toolbarInner.Controls.Add(_cmbAuditUser);
            Button btnRefresh = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Load Audit", InfoBlue, 104));
            btnRefresh.Margin = new Padding(0, 0, 0, 8);
            btnRefresh.Click += (s, e) =>
            {
                EnsureAuditGrid();
                RefreshAuditLog();
            };
            toolbarInner.Controls.Add(btnRefresh);
            toolbar.Controls.Add(toolbarInner);

            _auditGridCard = BuildPlainCard();
            _auditGridCard.Dock = DockStyle.Fill;
            _auditGridCard.Padding = new Padding(14);
            _auditGridCard.Controls.Add(global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
            {
                Text = "Click Load Audit to view the latest 100 audit events.",
                Dock = DockStyle.Top,
                Height = 28,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9f, FontStyle.Bold),
                ForeColor = DS.Slate600
            }));

            page.Controls.Add(_auditGridCard);
            page.Controls.Add(toolbar);
            page.Controls.Add(summary);
            _dtAuditFrom.Value = DateTime.Today.AddDays(-30);
            _dtAuditTo.Value = DateTime.Today;
            return page;
        }

        private TabPage BuildAuditTab()
        {
            TabPage tab = new TabPage("Audit Log") { BackColor = DS.BgPage };
            tab.Controls.Add(BuildAuditTabContent());
            return tab;
        }

        private void EnsureAuditGrid()
        {
            if (_gridAudit != null || _auditGridCard == null)
                return;

            _auditGridCard.Controls.Clear();
            _gridAudit = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            StyleDataGrid(_gridAudit);
            _auditGridCard.Controls.Add(_gridAudit);
        }

        private bool IsAdminUser()
        {
            return SessionManager.CurrentUser != null && string.Equals(SessionManager.CurrentUser.RoleName, "Admin", StringComparison.OrdinalIgnoreCase);
        }

        private void SettingsTabs_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_tabs == null || _tabs.SelectedTab == null)
                return;

            LazyTabPage lazyTab = _tabs.SelectedTab as LazyTabPage;
            if (lazyTab != null)
                lazyTab.EnsureBuilt();

            if (string.Equals(LanguageManager.GetSourceText(_tabs.SelectedTab, _tabs.SelectedTab.Text), "Users & Logins", StringComparison.OrdinalIgnoreCase))
            {
                EnsureUsersTabBuilt();
                BeginRefreshSecurityTabs();
            }
            else if (string.Equals(LanguageManager.GetSourceText(_tabs.SelectedTab, _tabs.SelectedTab.Text), "Audit Log", StringComparison.OrdinalIgnoreCase))
            {
                EnsureAuditTabBuilt();
                RefreshAuditLog();
            }
        }

        private void EnsureUsersTabBuilt()
        {
            if (_usersTabBuilt || _tabs == null || _usersTab == null)
                return;

            LazyTabPage lazyUsersTab = _usersTab as LazyTabPage;
            if (lazyUsersTab != null)
                lazyUsersTab.EnsureBuilt();
            _usersTabBuilt = true;
        }

        private void EnsureAuditTabBuilt()
        {
            if (_auditTabBuilt || _tabs == null || _auditTab == null)
                return;

            LazyTabPage lazyAuditTab = _auditTab as LazyTabPage;
            if (lazyAuditTab != null)
                lazyAuditTab.EnsureBuilt();
            _auditTabBuilt = true;
        }

        private void OpenUserManagementTab()
        {
            if (_tabs == null)
                return;

            EnsureUsersTabBuilt();

            foreach (TabPage tab in _tabs.TabPages)
            {
                if (string.Equals(LanguageManager.GetSourceText(tab, tab.Text), "Users & Logins", StringComparison.OrdinalIgnoreCase))
                {
                    _tabs.SelectedTab = tab;
                    break;
                }
            }
        }

        private void RefreshSecurityTabs()
        {
            if (!IsAdminUser())
                return;

            RefreshUsers();
            RefreshAuditLog();
        }

        /// <summary>Loads security summaries after Settings is visible so user SQL does not block first paint.</summary>
        private async void BeginRefreshSecurityTabs()
        {
            if (!IsAdminUser() || _gridUsers == null || _securityLoadQueued)
                return;

            _securityLoadQueued = true;
            try
            {
                AppRuntime.LogTiming("Settings.SecurityUsers.Start", 0);
                List<ManagedUserDto> users = await Task.Run(() => _authSvc.GetUsers());
                RunOnUI(() =>
                {
                    _securityLoadQueued = false;
                    if (IsDisposed || _gridUsers == null || _gridUsers.IsDisposed)
                        return;
                    BindUsers(users ?? new List<ManagedUserDto>());
                    AppRuntime.LogTiming("Settings.SecurityUsers.Complete", 0);
                });
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("SettingsForm.BeginRefreshSecurityTabs", ex);
                RunOnUI(() =>
                {
                    _securityLoadQueued = false;
                    if (IsDisposed || _gridUsers == null || _gridUsers.IsDisposed)
                        return;
                    global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblStatus, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("User login summary could not be loaded: {0}", ex.Message));
                    _lblStatus.ForeColor = Color.Red;
                });
                ShowError("Failed to load user login summary. Please try again.", ex);
            }
        }

        private void RefreshUsers()
        {
            if (_gridUsers == null)
                return;

            BindUsers(_authSvc.GetUsers());
        }

        /// <summary>Binds loaded user rows to the Users & Logins tab.</summary>
        private void BindUsers(List<ManagedUserDto> users)
        {
            if (_gridUsers == null)
                return;

            users = users ?? new List<ManagedUserDto>();
            _gridUsers.DataSource = users
                .Select(u => new
                {
                    u.UserId,
                    u.Username,
                    DisplayName = u.DisplayName,
                    Role = u.RoleName,
                    Active = u.IsActive ? "Yes" : "No",
                    LastLogin = u.LastLoginDate.HasValue ? u.LastLoginDate.Value.ToString("dd/MM/yyyy HH:mm") : "-"
                })
                .ToList();
            if (_gridUsers.Columns["UserId"] != null)
                _gridUsers.Columns["UserId"].Visible = false;
            if (_gridUsers.Columns["DisplayName"] != null)
                _gridUsers.Columns["DisplayName"].HeaderText = global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Display Name");
            if (_gridUsers.Columns["LastLogin"] != null)
                _gridUsers.Columns["LastLogin"].HeaderText = global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Last Login");

            if (_lblUserTotal != null)
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblUserTotal, users.Count.ToString());
            if (_lblUserActive != null)
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblUserActive, users.Count(u => u.IsActive).ToString());
            if (_lblUserAdmins != null)
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblUserAdmins, users.Count(u => string.Equals(u.RoleName, "Admin", StringComparison.OrdinalIgnoreCase)).ToString());
            if (_lblUserLastLogin != null)
            {
                DateTime? latest = users.Where(u => u.LastLoginDate.HasValue).Select(u => u.LastLoginDate.Value).DefaultIfEmpty(DateTime.MinValue).Max();
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblUserLastLogin, (latest.HasValue && latest.Value != DateTime.MinValue ? latest.Value.ToString("dd/MM HH:mm") : global::HVAC_Pro_Desktop.Services.LanguageManager.Get("-")));
            }

            if (_cmbAuditUser != null)
            {
                string selected = _cmbAuditUser.SelectedItem?.ToString() ?? "All Users";
                _cmbAuditUser.Items.Clear();
                _cmbAuditUser.Items.Add("All Users");
                foreach (string username in users.Select(u => u.Username).Distinct().OrderBy(x => x))
                    _cmbAuditUser.Items.Add(username);
                _cmbAuditUser.SelectedItem = _cmbAuditUser.Items.Contains(selected) ? selected : "All Users";
            }
        }

        private void RefreshAuditLog()
        {
            if (_gridAudit == null)
                return;

            string username = _cmbAuditUser != null && _cmbAuditUser.SelectedItem != null && _cmbAuditUser.SelectedItem.ToString() != "All Users"
                ? _cmbAuditUser.SelectedItem.ToString()
                : string.Empty;
            var table = _authSvc.GetAuditLog(_dtAuditFrom.Value.Date, _dtAuditTo.Value.Date, username);
            _gridAudit.DataSource = table;
            if (_gridAudit.Columns["LogDate"] != null)
                _gridAudit.Columns["LogDate"].HeaderText = global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Log Date");
            if (_gridAudit.Columns["ModuleKey"] != null)
                _gridAudit.Columns["ModuleKey"].HeaderText = global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Module");
            if (_gridAudit.Columns["LogDate"] != null)
                _gridAudit.Columns["LogDate"].Width = 140;
            if (_gridAudit.Columns["Username"] != null)
                _gridAudit.Columns["Username"].Width = 120;
            if (_gridAudit.Columns["Action"] != null)
                _gridAudit.Columns["Action"].Width = 90;
            if (_gridAudit.Columns["ModuleKey"] != null)
                _gridAudit.Columns["ModuleKey"].Width = 110;
            if (_gridAudit.Columns["Description"] != null)
                _gridAudit.Columns["Description"].Width = 520;

            if (_lblAuditTotal != null)
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblAuditTotal, table.Rows.Count.ToString());
            if (_lblAuditLogin != null)
            {
                int loginCount = 0;
                foreach (System.Data.DataRow row in table.Rows)
                {
                    if (Convert.ToString(row["Action"]).IndexOf("LOGIN", StringComparison.OrdinalIgnoreCase) >= 0)
                        loginCount++;
                }
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblAuditLogin, loginCount.ToString());
            }
            if (_lblAuditWarnings != null)
            {
                int warningCount = 0;
                foreach (System.Data.DataRow row in table.Rows)
                {
                    string action = Convert.ToString(row["Action"]);
                    string description = Convert.ToString(row["Description"]);
                    if (action.IndexOf("FAIL", StringComparison.OrdinalIgnoreCase) >= 0
                        || action.IndexOf("WARN", StringComparison.OrdinalIgnoreCase) >= 0
                        || description.IndexOf("fail", StringComparison.OrdinalIgnoreCase) >= 0
                        || description.IndexOf("warning", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        warningCount++;
                    }
                }
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblAuditWarnings, warningCount.ToString());
            }
        }

        private ManagedUserDto GetSelectedUser()
        {
            if (_gridUsers?.CurrentRow == null)
                return null;

            object userIdObj = _gridUsers.CurrentRow.Cells["UserId"]?.Value;
            if (userIdObj == null || userIdObj == DBNull.Value)
                return null;

            int userId = Convert.ToInt32(userIdObj);
            return _authSvc.GetUsers().FirstOrDefault(u => u.UserId == userId);
        }

        private void AddUser()
        {
            if (!ShowUserEditor(null, out string username, out string displayName, out int roleId, out bool isActive, out int? employeeId))
                return;

            var result = _authSvc.CreateUser(username, displayName, roleId, isActive, employeeId);
            if (!result.Success)
            {
                MessageBox.Show(result.ErrorMessage, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Add User"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            RefreshUsers();
            MessageBox.Show(global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Temp password: {0}\r\n\r\nShare this with the user.", result.TempPassword), global::HVAC_Pro_Desktop.Services.LanguageManager.Get("User Created"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void EditSelectedUser()
        {
            ManagedUserDto user = GetSelectedUser();
            if (user == null)
                return;

            if (!ShowUserEditor(user, out string username, out string displayName, out int roleId, out bool isActive, out int? employeeId))
                return;

            if (!_authSvc.UpdateUser(user.UserId, username, displayName, roleId, isActive, employeeId))
            {
                MessageBox.Show(global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Unable to update user."), global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Edit User"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            RefreshUsers();
        }

        private void ResetSelectedUserPassword()
        {
            ManagedUserDto user = GetSelectedUser();
            if (user == null)
                return;

            var result = _authSvc.ResetPassword(user.UserId);
            if (!result.Success)
            {
                MessageBox.Show(result.ErrorMessage, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Reset Password"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            MessageBox.Show(global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Temp password: {0}\r\n\r\nShare this with the user.", result.TempPassword), global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Password Reset"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshUsers();
        }

        private void ToggleSelectedUserActive(bool isActive)
        {
            ManagedUserDto user = GetSelectedUser();
            if (user == null)
                return;

            string accountLabel = string.IsNullOrWhiteSpace(user.DisplayName)
                ? (string.IsNullOrWhiteSpace(user.Username) ? "this user" : user.Username)
                : user.DisplayName;
            string action = isActive ? "Activate" : "Deactivate";
            string impact = isActive
                ? "The user will be able to sign in again using their existing credentials."
                : "The user will no longer be able to sign in. Existing audit history and business records stay unchanged.";
            if (!ServoERP.Infrastructure.ServoConfirmDialog.Show(
                    this,
                    action + " " + accountLabel + "?",
                    impact))
                return;

            if (!_authSvc.SetUserActive(user.UserId, isActive))
            {
                MessageBox.Show(global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Unable to change active state. You cannot deactivate your own account."), global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Deactivate User"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            RefreshUsers();
        }

        private bool ShowUserEditor(ManagedUserDto user, out string username, out string displayName, out int roleId, out bool isActive, out int? employeeId)
        {
            username = null;
            displayName = null;
            roleId = 0;
            isActive = true;
            employeeId = null;
            string tempUsername = null;
            string tempDisplayName = null;
            int tempRoleId = 0;
            bool tempIsActive = true;
            int? tempEmployeeId = null;

            using (Form dialog = ServoModalForm.Create((user == null ? global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Add User") : global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Edit User")), 360, 292))
            {
                TextBox txtUsername = new TextBox { Location = new Point(24, 34), Width = 300, Text = user?.Username ?? string.Empty };
                TextBox txtDisplayName = new TextBox { Location = new Point(24, 84), Width = 300, Text = user?.DisplayName ?? string.Empty };
                ComboBox cmbRole = new ComboBox { Location = new Point(24, 134), Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
                var roles = _authSvc.GetRoles();
                cmbRole.DataSource = roles;
                cmbRole.DisplayMember = "RoleName";
                cmbRole.ValueMember = "RoleId";
                if (user != null)
                    cmbRole.SelectedValue = user.RoleId;
                ComboBox cmbEmployee = new ComboBox { Location = new Point(24, 184), Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
                var employees = new List<Employee> { new Employee { EmployeeID = 0, EmployeeCode = "—", Name = "Not linked" } };
                try { employees.AddRange(_employeeSvc.GetAll().Where(e => e != null).OrderBy(e => e.Name)); }
                catch (Exception ex) { AppLogger.LogError("SettingsForm.ShowUserEditor.Employees", ex); }
                cmbEmployee.DataSource = employees;
                cmbEmployee.DisplayMember = "Name";
                cmbEmployee.ValueMember = "EmployeeID";
                if (user != null && user.EmployeeId.HasValue)
                    cmbEmployee.SelectedValue = user.EmployeeId.Value;
                CheckBox chkActive = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new CheckBox { Location = new Point(24, 222), Text = "User is active", Checked = user == null || user.IsActive });

                Button btnOk = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Save", SaveGreen, 90));
                btnOk.Location = new Point(234, 242);
                btnOk.Click += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(txtUsername.Text) || string.IsNullOrWhiteSpace(txtDisplayName.Text) || cmbRole.SelectedValue == null)
                    {
                        MessageBox.Show(global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Username, display name, and role are required."), dialog.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    tempUsername = txtUsername.Text.Trim();
                    tempDisplayName = txtDisplayName.Text.Trim();
                    tempRoleId = Convert.ToInt32(cmbRole.SelectedValue);
                    int selectedEmployeeId = cmbEmployee.SelectedValue == null ? 0 : Convert.ToInt32(cmbEmployee.SelectedValue);
                    tempEmployeeId = selectedEmployeeId > 0 ? (int?)selectedEmployeeId : null;
                    tempIsActive = chkActive.Checked;
                    dialog.DialogResult = DialogResult.OK;
                    dialog.Close();
                };

                dialog.Controls.AddRange(new Control[]
                {
                    global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label { Text = "Username", Location = new Point(24, 14), AutoSize = true }),
                    txtUsername,
                    global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label { Text = "Display Name", Location = new Point(24, 64), AutoSize = true }),
                    txtDisplayName,
                    global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label { Text = "Role", Location = new Point(24, 114), AutoSize = true }),
                    cmbRole,
                    global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label { Text = "Linked Employee (for My Work assignment)", Location = new Point(24, 164), AutoSize = true }),
                    cmbEmployee,
                    chkActive,
                    btnOk
                });

                bool ok = dialog.ShowDialog(this) == DialogResult.OK;
                if (ok)
                {
                    username = tempUsername;
                    displayName = tempDisplayName;
                    roleId = tempRoleId;
                    isActive = tempIsActive;
                    employeeId = tempEmployeeId;
                }

                return ok;
            }
        }

        private void Save()
        {
            try
            {
                var settings = new IndiaCompanySettings
                {
                    CompanyName = _txtCompanyName.Text.Trim(),
                    AuthorisedSignatoryName = _txtAuthorisedSignatory.Text.Trim(),
                    GSTIN = _txtGST.Text,
                    PAN = _txtPAN.Text,
                    TAN = _txtTAN.Text,
                    Phone = _txtPhone.Text.Trim(),
                    Email = _txtEmail.Text.Trim(),
                    Address = _txtAddress.Text.Trim(),
                    CompanyState = _cmbState.SelectedItem?.ToString() ?? "Maharashtra",
                    GSTRegistrationType = _cmbGstRegistrationType.SelectedItem?.ToString() ?? "Regular",
                    InvoicePrefix = _txtPrefix.Text,
                    DefaultGSTRate = _numGSTRate.Value,
                    DefaultPaymentTermsDays = (int)_numPayTerms.Value,
                    AnnualTurnover = _numAnnualTurnover.Value,
                    EInvoiceThresholdAmount = _numEInvoiceThreshold.Value,
                    DefaultPlaceOfSupply = _cmbState.SelectedItem?.ToString() ?? "Maharashtra",
                    OfficeLatitude = ParseNullableDouble(_txtOfficeLatitude.Text),
                    OfficeLongitude = ParseNullableDouble(_txtOfficeLongitude.Text)
                };

                _svc.SaveIndiaCompanySettings(settings);
                ConfigService.Set("Company", "CompanyName", settings.CompanyName);
                ConfigService.Set("Company", "CompanyAddress", settings.Address);
                ConfigService.Set("Company", "CompanyPhone", settings.Phone);
                ConfigService.Set("Company", "CompanyEmail", settings.Email);
                ConfigService.Set("Company", "CompanyGSTIN", settings.GSTIN);
                ConfigService.Set("Company", "CompanyPAN", settings.PAN);
                ConfigService.Set("Company", "CompanyState", settings.CompanyState);
                _svc.Set("DefaultMarkupPct", _numMarkupPct.Value.ToString("0.##"));
                _hsnSacSvc.SaveAll(CollectHsnSacRows());
                SaveDisplayFitSetting();
                SaveUiScaleSetting();
                if (_txtVersionCheckUrl != null)
                {
                    ConfigService.Set("App", "GitHubRepositoryUrl", ConfigService.ProductionVersionCheckUrl);
                    ConfigService.Set("App", "VersionCheckUrl", ConfigService.ProductionVersionCheckUrl);
                    _txtVersionCheckUrl.Text = UpdateService.GetGitHubRepositoryUrl();
                    ConfigService.Set("App", "VersionCheckEnabled", _chkVersionCheckEnabled != null && _chkVersionCheckEnabled.Checked ? "true" : "false");
                    UpdateService.SetSilentAutoUpdatePreference(_chkSilentAutoUpdateEnabled != null && _chkSilentAutoUpdateEnabled.Checked);
                    ConfigService.Set("App", "VersionCheckIntervalHours", ConfigService.GetVersionCheckIntervalHours().ToString());
                }
                RefreshIndiaDefaultsPreview();

                global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_lblStatus, "India settings and HSN/SAC master saved.");
                _lblStatus.ForeColor = SaveGreen;
            }
            catch (Exception ex)
            {
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblStatus, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Error: {0}", ex.Message));
                _lblStatus.ForeColor = Color.Red;
            }
        }

        private void SaveApplicationLanguage()
        {
            if (_cmbLanguage?.SelectedItem == null) return;
            try
            {
                string language = _cmbLanguage.SelectedItem.ToString();
                LanguagePreferenceService.Save(language);
                LanguageManager.SetLanguage(language);
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblStatus, LanguageManager.Get("Language saved. Reopen ServoERP to apply the language to all screens."));
                _lblStatus.ForeColor = SaveGreen;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Settings.SaveApplicationLanguage");
                MessageBox.Show(this, LanguageManager.Get("Unable to save the language preference. Please try again."),
                    LanguageManager.Get("Settings"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadDisplayFitSetting()
        {
            if (_cmbDisplayFitMode == null)
                return;

            string mode = LayoutScaler.GetDisplayFitMode();
            if (mode == LayoutScaler.DisplayFitIdeaPad)
                _cmbDisplayFitMode.SelectedIndex = 1;
            else if (mode == LayoutScaler.DisplayFitStandard)
                _cmbDisplayFitMode.SelectedIndex = 2;
            else
                _cmbDisplayFitMode.SelectedIndex = 0;
        }

        private void SaveDisplayFitSetting()
        {
            if (_cmbDisplayFitMode == null)
                return;

            string mode = LayoutScaler.DisplayFitAuto;
            if (_cmbDisplayFitMode.SelectedIndex == 1)
                mode = LayoutScaler.DisplayFitIdeaPad;
            else if (_cmbDisplayFitMode.SelectedIndex == 2)
                mode = LayoutScaler.DisplayFitStandard;

            LayoutScaler.SetDisplayFitMode(mode);
        }

        private void LoadUiScaleSetting()
        {
            if (_cmbUiScale == null)
                return;

            string selected = LayoutScaler.GetUiScalePercent().ToString(CultureInfo.InvariantCulture) + "%";
            int index = _cmbUiScale.Items.IndexOf(selected);
            _cmbUiScale.SelectedIndex = index >= 0 ? index : Math.Max(0, _cmbUiScale.Items.IndexOf("100%"));
        }

        private void SaveUiScaleSetting()
        {
            if (_cmbUiScale == null || _cmbUiScale.SelectedItem == null)
                return;

            string text = _cmbUiScale.SelectedItem.ToString().Replace("%", "").Trim();
            int percent;
            if (int.TryParse(text, out percent))
                LayoutScaler.SetUiScalePercent(percent);
        }

        private string BuildDisplayFitScreenSummary()
        {
            Rectangle workArea = Screen.PrimaryScreen != null
                ? Screen.PrimaryScreen.WorkingArea
                : SystemInformation.WorkingArea;
            return string.Format(CultureInfo.CurrentCulture, LanguageManager.Get("Detected working area: {0} x {1} px. Recommended for IdeaPad laptops: IdeaPad / compact laptop."), workArea.Width, workArea.Height);
        }

        /// <summary>Checks SQL Server health on a worker thread so opening Settings never freezes the shell.</summary>
        private async void BeginCheckDbConnection()
        {
            if (_lblDbStatus == null || _lblDbStatus.IsDisposed)
                return;

            global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_lblDbStatus, "Database: checking office SQL Server...");
            _lblDbStatus.ForeColor = DS.Slate600;

            try
            {
                AppRuntime.LogTiming("Settings.CheckDbConnection.Start", 0);
                DatabaseConnectionTestResult result = await DatabaseConnectionFactory.TestDatabaseConnectionAsync();
                RunOnUI(() =>
                {
                    if (IsDisposed || _lblDbStatus == null || _lblDbStatus.IsDisposed)
                        return;
                    if (result == null)
                    {
                        global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_lblDbStatus, "Database: status unavailable.");
                        _lblDbStatus.ForeColor = Color.Red;
                        return;
                    }

                    AppRuntime.LogTiming("Settings.CheckDbConnection.Complete", 0);
                    global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblDbStatus, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Database: {0}", result.Message));
                    _lblDbStatus.ForeColor = result.Success ? SaveGreen : Color.Red;
                    RefreshSettingsWorkspaceSummary();
                });
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("SettingsForm.BeginCheckDbConnection", ex);
                RunOnUI(() =>
                {
                    if (IsDisposed || _lblDbStatus == null || _lblDbStatus.IsDisposed)
                        return;
                    global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblDbStatus, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Database: NOT connected - {0}", ex.Message));
                    _lblDbStatus.ForeColor = Color.Red;
                    RefreshSettingsWorkspaceSummary();
                });
                ShowError("Failed to check office SQL Server connection. Please try again.", ex);
            }
        }

        /// <summary>Runs an immediate SQL Server health check for explicit Settings actions.</summary>
        private void CheckDbConnection()
        {
            try
            {
                AppRuntime.LogTiming("Settings.CheckDbConnection.Start", 0);
                DatabaseConnectionTestResult result = DatabaseConnectionFactory.TestDatabaseConnectionAsync()
                    .GetAwaiter()
                    .GetResult();
                AppRuntime.LogTiming("Settings.CheckDbConnection.Complete", 0);

                global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblDbStatus, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Database: {0}", result.Message));
                _lblDbStatus.ForeColor = result.Success ? SaveGreen : Color.Red;
                RefreshSettingsWorkspaceSummary();
            }
            catch (Exception ex)
            {
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblDbStatus, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Database: NOT connected - {0}", ex.Message));
                _lblDbStatus.ForeColor = Color.Red;
                RefreshSettingsWorkspaceSummary();
            }
        }

        private async Task RepairDatabaseSchemaAsync(Button sourceButton)
        {
            if (_lblDbStatus == null || _lblDbStatus.IsDisposed)
                return;

            bool wasEnabled = sourceButton == null || sourceButton.Enabled;
            if (sourceButton != null)
                sourceButton.Enabled = false;

            global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_lblDbStatus, "Database: repairing schema and sync metadata...");
            _lblDbStatus.ForeColor = DS.Slate600;
            Cursor = Cursors.WaitCursor;

            try
            {
                SupportToolResult result = await Task.Run(() => _supportCenterSvc.RepairDatabaseSchema());
                if (IsDisposed || _lblDbStatus == null || _lblDbStatus.IsDisposed)
                    return;

                global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblDbStatus, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Database: {0}", result.Message));
                _lblDbStatus.ForeColor = result.Success ? SaveGreen : Color.Red;
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblStatus, result.Title);
                _lblStatus.ForeColor = result.Success ? SaveGreen : Color.Red;
                RefreshSettingsWorkspaceSummary();

                MessageBox.Show(
                    FindForm(),
                    result.Message + (string.IsNullOrWhiteSpace(result.Detail) ? string.Empty : Environment.NewLine + Environment.NewLine + result.Detail),
                    BrandingService.WindowTitle(result.Title),
                    MessageBoxButtons.OK,
                    result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("SettingsForm.RepairDatabaseSchema", ex);
                if (_lblDbStatus != null && !_lblDbStatus.IsDisposed)
                {
                    global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblDbStatus, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Database: repair failed - {0}", ex.Message));
                    _lblDbStatus.ForeColor = Color.Red;
                }
                RefreshSettingsWorkspaceSummary();
                ShowError("Failed to repair database schema. Please try again.", ex);
            }
            finally
            {
                Cursor = Cursors.Default;
                if (sourceButton != null && wasEnabled && !sourceButton.IsDisposed)
                    sourceButton.Enabled = true;
            }
        }

        private void OpenConnectionSetup()
        {
            try
            {
                using (var form = new ConnectionSetupForm())
                {
                    if (form.ShowDialog(FindForm()) == DialogResult.OK)
                    {
                        global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_lblStatus, "Connection settings saved. Please restart the app to use the new database connection.");
                        _lblStatus.ForeColor = SaveGreen;
                        AppRuntime.LogConnection("Connection setup saved from Settings.");
                        CheckDbConnection();
                    }
                    else
                    {
                        AppRuntime.LogConnection("Connection setup cancelled from Settings.");
                    }
                }
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("Settings.OpenConnectionSetup", ex);
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblStatus, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Connection setup error: {0}", ex.Message));
                _lblStatus.ForeColor = Color.Red;
            }
        }

        private void RefreshIndiaDefaultsPreview()
        {
            _chkEInvoiceEligible.Checked = _numAnnualTurnover.Value >= _numEInvoiceThreshold.Value;
            _txtFinancialYear.Text = IndiaFinancialYearHelper.GetFinancialYearDisplay(DateTime.Today);
            global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblMoneyPreview, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Money preview: {0}  |  FY: {1}  |  Dates: {2}", IndiaFormatHelper.FormatCurrency(_numAnnualTurnover.Value), IndiaFinancialYearHelper.GetFinancialYearCode(DateTime.Today), IndiaFormatHelper.FormatDate(DateTime.Today)));
        }

        private DataGridView BuildHsnSacGrid()
        {
            var grid = new DataGridView
            {
                Width = 526,
                Height = 258,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 8.5f),
                ScrollBars = ScrollBars.None
            };
            StyleDataGrid(grid);
            grid.ScrollBars = ScrollBars.None;
            grid.AutoGenerateColumns = false;

            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "MasterID", DataPropertyName = "MasterID", Visible = false });
            grid.Columns.Add(new DataGridViewComboBoxColumn { Name = "CodeType", DataPropertyName = "CodeType", HeaderText = global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Type"), DataSource = new[] { "HSN", "SAC" }, FillWeight = 50, Visible = false });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Code", DataPropertyName = "Code", HeaderText = global::HVAC_Pro_Desktop.Services.LanguageManager.Get("HSN / SAC"), FillWeight = 78, MinimumWidth = 82 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", DataPropertyName = "Description", HeaderText = global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Description"), FillWeight = 190, MinimumWidth = 130 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "BusinessCategory", DataPropertyName = "BusinessCategory", HeaderText = global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Category"), FillWeight = 120, Visible = false });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "TaxRate", DataPropertyName = "TaxRate", HeaderText = global::HVAC_Pro_Desktop.Services.LanguageManager.Get("GST %"), FillWeight = 52, MinimumWidth = 58 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CGSTRate", DataPropertyName = "CGSTRate", HeaderText = global::HVAC_Pro_Desktop.Services.LanguageManager.Get("CGST %"), FillWeight = 52, MinimumWidth = 58 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SGSTRate", DataPropertyName = "SGSTRate", HeaderText = global::HVAC_Pro_Desktop.Services.LanguageManager.Get("SGST %"), FillWeight = 52, MinimumWidth = 58 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "IGSTRate", DataPropertyName = "IGSTRate", HeaderText = global::HVAC_Pro_Desktop.Services.LanguageManager.Get("IGST %"), FillWeight = 52, MinimumWidth = 58, Visible = false });
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsDefault", DataPropertyName = "IsDefault", HeaderText = global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Default"), FillWeight = 50, Visible = false });
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsActive", DataPropertyName = "IsActive", HeaderText = global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Active"), FillWeight = 45, MinimumWidth = 54, Visible = false });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Notes", DataPropertyName = "Notes", HeaderText = global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Notes"), FillWeight = 140, Visible = false });
            _hsnBindingSource.DataSource = typeof(HsnSacGridRow);
            grid.DataSource = _hsnBindingSource;
            grid.Resize += (s, e) => LayoutHsnSacColumns(grid);
            LayoutHsnSacColumns(grid);
            return grid;
        }

        private void LayoutHsnSacColumns(DataGridView grid)
        {
            if (grid == null || grid.Columns.Count == 0)
                return;

            int taxWidth = 58;
            int codeWidth = 76;
            int available = Math.Max(320, grid.ClientSize.Width - 34);
            int descriptionWidth = Math.Max(128, available - codeWidth - (taxWidth * 3));
            if (grid.Columns["Code"] != null) grid.Columns["Code"].Width = codeWidth;
            if (grid.Columns["Description"] != null) grid.Columns["Description"].Width = descriptionWidth;
            if (grid.Columns["TaxRate"] != null) grid.Columns["TaxRate"].Width = taxWidth;
            if (grid.Columns["CGSTRate"] != null) grid.Columns["CGSTRate"].Width = taxWidth;
            if (grid.Columns["SGSTRate"] != null) grid.Columns["SGSTRate"].Width = taxWidth;
        }

        private void LoadHsnSacGrid(IEnumerable<HsnSacMasterEntry> rows)
        {
            _hsnMasterRows = (rows ?? Enumerable.Empty<HsnSacMasterEntry>())
                .Select(entry => new HsnSacGridRow
                {
                    MasterID = entry.MasterID,
                    CodeType = entry.CodeType,
                    Code = entry.Code,
                    Description = entry.Description,
                    BusinessCategory = entry.BusinessCategory,
                    TaxRate = entry.TaxRate,
                    CGSTRate = entry.CGSTRate,
                    SGSTRate = entry.SGSTRate,
                    IGSTRate = entry.IGSTRate,
                    IsDefault = entry.IsDefault,
                    IsActive = entry.IsActive,
                    Notes = entry.Notes
                })
                .ToList();
            ApplyHsnSacFilter();
        }

        private void ApplyHsnSacFilter()
        {
            string search = (_txtHsnSearch == null ? string.Empty : _txtHsnSearch.Text ?? string.Empty).Trim();
            IEnumerable<HsnSacGridRow> rows = _hsnMasterRows ?? Enumerable.Empty<HsnSacGridRow>();
            if (!string.IsNullOrWhiteSpace(search))
            {
                rows = rows.Where(row =>
                    (row.Code ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (row.Description ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (row.CodeType ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            _hsnBindingSource.DataSource = new BindingList<HsnSacGridRow>(rows.ToList());
        }

        /// <summary>Loads HSN/SAC rows after Settings is visible so master-data SQL does not block first paint.</summary>
        private async void BeginLoadHsnSacGrid()
        {
            if (_gridHsnSac == null || _gridHsnSac.IsDisposed || _hsnLoadQueued)
                return;

            _hsnLoadQueued = true;
            try
            {
                AppRuntime.LogTiming("Settings.HsnSac.Start", 0);
                IEnumerable<HsnSacMasterEntry> entries = await Task.Run(() => _hsnSacSvc.GetAll());
                RunOnUI(() =>
                {
                    _hsnLoadQueued = false;
                    if (IsDisposed || _gridHsnSac == null || _gridHsnSac.IsDisposed)
                        return;
                    LoadHsnSacGrid(entries);
                    AppRuntime.LogTiming("Settings.HsnSac.Complete", 0);
                });
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("SettingsForm.BeginLoadHsnSacGrid", ex);
                RunOnUI(() =>
                {
                    _hsnLoadQueued = false;
                    if (IsDisposed || _gridHsnSac == null || _gridHsnSac.IsDisposed)
                        return;
                    global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblStatus, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("HSN/SAC master could not be loaded: {0}", ex.Message));
                    _lblStatus.ForeColor = Color.Red;
                });
                ShowError("Failed to load HSN/SAC master. Please try again.", ex);
            }
        }

        private List<HsnSacMasterEntry> CollectHsnSacRows()
        {
            var entries = new List<HsnSacMasterEntry>();
            foreach (HsnSacGridRow row in _hsnMasterRows ?? new List<HsnSacGridRow>())
            {
                string code = row.Code ?? string.Empty;
                string description = row.Description ?? string.Empty;
                if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(description))
                    continue;

                entries.Add(new HsnSacMasterEntry
                {
                    MasterID = row.MasterID,
                    CodeType = row.CodeType,
                    Code = code,
                    Description = description,
                    BusinessCategory = row.BusinessCategory,
                    TaxRate = row.TaxRate,
                    CGSTRate = row.CGSTRate,
                    SGSTRate = row.SGSTRate,
                    IGSTRate = row.IGSTRate,
                    IsDefault = row.IsDefault,
                    IsActive = row.IsActive,
                    Notes = row.Notes
                });
            }
            return entries;
        }

        private TextBox Field(Panel parent, string label, ref int y, int width = 380, bool uppercase = false)
        {
            parent.Controls.Add(MakeLbl(label, new Point(0, y + 3)));
            var txt = new TextBox
            {
                Location = new Point(210, y),
                Width = width,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9),
                BorderStyle = BorderStyle.None
            };
            if (uppercase)
                txt.CharacterCasing = CharacterCasing.Upper;
            parent.Controls.Add(txt);
            y += 32;
            return txt;
        }

        private void Section(Panel parent, string text, ref int y)
        {
            y += 6;
            parent.Controls.Add(global::HVAC_Pro_Desktop.Services.LanguageManager.DataText(new Label
            {
                Text = text,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 8, FontStyle.Bold),
                ForeColor = InfoBlue,
                Location = new Point(0, y),
                Width = 960,
                Height = 22,
                BackColor = SectionBg,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 0, 0)
            }));
            y += 28;
        }

        private Label MakeLbl(string text, Point location)
        {
            return global::HVAC_Pro_Desktop.Services.LanguageManager.DataText(new Label
            {
                Text = text,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 8, FontStyle.Bold),
                ForeColor = Color.Gray,
                Location = location,
                Width = 206,
                TextAlign = ContentAlignment.MiddleRight
            });
        }

        private NumericUpDown MakeDecimalBox(Point location, int width, decimal minimum, decimal maximum, decimal value, int decimals, decimal increment = 1m)
        {
            return new NumericUpDown
            {
                Location = location,
                Width = width,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9),
                Minimum = minimum,
                Maximum = maximum,
                Value = value,
                DecimalPlaces = decimals,
                Increment = increment,
                ThousandsSeparator = true
            };
        }

        private Button MakeBtn(string text, Color bg, int width)
        {
            var button = global::HVAC_Pro_Desktop.Services.LanguageManager.DataText(new Button
            {
                Text = text,
                Width = width,
                Height = 34,
                BackColor = bg,
                ForeColor = bg == Color.White ? DS.Slate700 : Color.White,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            });
            button.FlatAppearance.BorderSize = bg == Color.White ? 1 : 0;
            button.FlatAppearance.BorderColor = DS.Border;
            button.FlatAppearance.MouseOverBackColor = bg == Color.White ? DS.Slate50 : ControlPaint.Light(bg);
            button.FlatAppearance.MouseDownBackColor = bg == Color.White ? DS.Slate100 : ControlPaint.Dark(bg);
            DS.Rounded(button, 8);
            return button;
        }

        private Panel BuildModernSettingsHeader(params Button[] actions)
        {
            Panel avatar = new Panel
            {
                BackColor = DS.Primary600,
                Size = new Size(38, 38)
            };
            DS.Rounded(avatar, 19);
            avatar.Controls.Add(global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
            {
                Text = "AD",
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            }));
            Label user = global::HVAC_Pro_Desktop.Services.LanguageManager.DataText(new Label
            {
                Text = (SessionManager.CurrentUser == null ? global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Administrator\r\nAdmin") : (SessionManager.CurrentUser.DisplayName + "\r\n" + SessionManager.CurrentUser.RoleName)),
                Size = new Size(150, 42),
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 8.5f, FontStyle.Bold),
                ForeColor = DS.Slate900
            });
            Panel meta = new Panel
            {
                Name = "SettingsHeaderMetaPanel",
                Size = new Size(196, 42),
                BackColor = Color.Transparent
            };
            avatar.Location = new Point(0, 2);
            user.Location = new Point(46, 0);
            meta.Controls.Add(avatar);
            meta.Controls.Add(user);

            SharedPageHeaderModel model = SharedPageHeader.CreateWorkspaceEditor(
                "SettingsHeader",
                "Settings",
                "Configure company profile, compliance, backups, users, and system preferences.",
                (actions ?? new Button[0]).Cast<Control>().ToList(),
                SharedPageHeader.CreateSearchCommand("SettingsGlobalSearch", 280, global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Search"), "Ctrl + K", () => SharedUiPrimitives.OpenGlobalSearch(this)),
                null,
                null,
                meta);
            model.Dock = DockStyle.Top;
            model.DefaultHeight = 94;
            model.CompactHeight = 132;
            model.BackColor = Color.White;
            return SharedPageHeader.Build(model).Header;
        }

        private Panel BuildModernActionBar(params Button[] buttons)
        {
            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = DS.BgPage, Padding = new Padding(28, 10, 28, 10) };
            foreach (Button button in buttons)
            {
                button.Height = 36;
                button.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                toolbar.Controls.Add(button);
            }

            Action layoutActions = () =>
            {
                int right = toolbar.ClientSize.Width - toolbar.Padding.Right;
                foreach (Button button in buttons)
                {
                    right -= button.Width;
                    button.Left = Math.Max(0, right);
                    button.Top = toolbar.Padding.Top;
                    right -= 10;
                }
                _lblStatus.Location = new Point(toolbar.Padding.Left, toolbar.Padding.Top + 7);
                _lblStatus.Width = Math.Max(120, right - toolbar.Padding.Left - 8);
            };
            toolbar.Resize += (s, e) => layoutActions();
            layoutActions();

            _lblStatus.Margin = Padding.Empty;
            _lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            toolbar.Controls.Add(_lblStatus);
            foreach (Button button in buttons)
                button.BringToFront();
            return toolbar;
        }

        private void DrawModernSettingsTab(object sender, DrawItemEventArgs e)
        {
            TabControl tabs = sender as TabControl;
            if (tabs == null || e.Index < 0)
                return;

            bool selected = e.Index == tabs.SelectedIndex;
            Rectangle bounds = e.Bounds;
            using (SolidBrush back = new SolidBrush(DS.BgPage))
                e.Graphics.FillRectangle(back, bounds);
            Color textColor = selected ? InfoBlue : DS.Slate600;
            TextRenderer.DrawText(
                e.Graphics,
                tabs.TabPages[e.Index].Text,
                new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9f, selected ? FontStyle.Bold : FontStyle.Regular),
                bounds,
                textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            if (selected)
            {
                using (Pen pen = new Pen(InfoBlue, 2))
                    e.Graphics.DrawLine(pen, bounds.Left + 18, bounds.Bottom - 3, bounds.Right - 18, bounds.Bottom - 3);
            }
        }

        private Panel AddModernSettingsCard(Panel parent, string title, string subtitle, int height)
        {
            Panel wrapper = new Panel
            {
                BackColor = Color.White,
                Padding = new Padding(22, 18, 22, 14)
            };
            Panel body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            wrapper.AutoScroll = false;
            wrapper.Size = new Size(560, height);
            wrapper.Margin = Padding.Empty;
            wrapper.Tag = "settings-card";
            _settingsCardTitles[wrapper] = title;
            wrapper.Visible = _selectedSettingsCards != null && _selectedSettingsCards.Contains(title);
            wrapper.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.BorderStrong))
                    e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, wrapper.Width - 1), Math.Max(0, wrapper.Height - 1));
            };
            body.AutoScroll = false;
            body.Controls.Add(global::HVAC_Pro_Desktop.Services.LanguageManager.DataText(new Label
            {
                Text = LanguageManager.Get(title),
                Location = new Point(0, 0),
                Size = new Size(500, 24),
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 12f, FontStyle.Bold),
                ForeColor = DS.Slate900,
                BackColor = Color.White,
                UseMnemonic = false
            }));
            body.Controls.Add(global::HVAC_Pro_Desktop.Services.LanguageManager.DataText(new Label
            {
                Text = LanguageManager.Get(subtitle),
                Location = new Point(0, 27),
                Size = new Size(500, 38),
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 8.7f),
                ForeColor = DS.Slate500,
                BackColor = Color.White,
                UseMnemonic = false
            }));
            Panel content = new Panel
            {
                Location = new Point(0, 76),
                Size = new Size(530, Math.Max(90, height - 108)),
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                AutoScroll = false
            };
            content.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, content.ClientSize.Width - 1), Math.Max(0, content.ClientSize.Height - 1));
            };
            body.Resize += (s, e) =>
            {
                int availableWidth = Math.Max(280, body.ClientSize.Width);
                content.Location = new Point(0, 76);
                content.Width = Math.Max(260, availableWidth);
                content.Height = Math.Max(90, body.ClientSize.Height - content.Top);
                foreach (Label label in body.Controls.OfType<Label>())
                {
                    label.Width = Math.Max(220, availableWidth - label.Left - 8);
                }
            };
            body.Controls.Add(content);
            wrapper.Controls.Add(body);
            parent.Controls.Add(wrapper);
            return content;
        }

        private Panel BuildPlainCard()
        {
            Panel card = new Panel { BackColor = Color.White };
            DS.Rounded(card, 12);
            return card;
        }

        private Label AddSummaryCard(FlowLayoutPanel parent, string title, string value, Color accent)
        {
            Panel card = new Panel
            {
                BackColor = Color.White,
                Size = new Size(178, 74),
                Margin = new Padding(0, 0, 14, 12),
                Padding = new Padding(14, 10, 14, 10)
            };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = DS.RoundedRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 10))
                using (Pen pen = new Pen(DS.Slate200))
                    e.Graphics.DrawPath(pen, path);
            };
            DS.Rounded(card, 10);
            Panel inner = card;
            inner.Controls.Add(global::HVAC_Pro_Desktop.Services.LanguageManager.DataText(new Label
            {
                Text = title,
                Location = new Point(36, 0),
                Size = new Size(110, 18),
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 8.2f, FontStyle.Bold),
                ForeColor = DS.Slate500
            }));
            Label icon = ModernIconSystem.Badge(ModernIconSystem.KindForTitle(title), 26, DS.Indigo50, accent, 8);
            icon.Location = new Point(0, 2);
            inner.Controls.Add(icon);
            Label valueLabel = global::HVAC_Pro_Desktop.Services.LanguageManager.DataText(new Label
            {
                Text = value,
                Location = new Point(36, 24),
                Size = new Size(110, 28),
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 14f, FontStyle.Bold),
                ForeColor = accent,
                AutoEllipsis = true
            });
            inner.Controls.Add(valueLabel);
            parent.Controls.Add(card);
            return valueLabel;
        }

        private Label FilterLabel(string text)
        {
            return global::HVAC_Pro_Desktop.Services.LanguageManager.DataText(new Label
            {
                Text = text,
                Width = 42,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = DS.Slate500,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 8.5f, FontStyle.Bold),
                Margin = new Padding(0, 0, 4, 8)
            });
        }

        private void ReflowSettingsCards()
        {
            if (_generalFlow == null || _generalCanvas == null || _settingsDirectory == null)
                return;
            if (_reflowingSettingsCards)
                return;

            _reflowingSettingsCards = true;
            try
            {
                int viewportWidth = _generalCanvas.Parent == null ? GeneralCanvasWidth : _generalCanvas.Parent.ClientSize.Width;
                int canvasWidth = _selectedSettingsCards == null
                    ? Math.Min(1600, Math.Max(360, viewportWidth - 48))
                    : Math.Min(GeneralCanvasWidth, Math.Max(360, viewportWidth - 48));
                _generalCanvas.Left = Math.Max(24, (viewportWidth - canvasWidth) / 2);
                if (_generalCanvas.Width != canvasWidth)
                    _generalCanvas.Width = canvasWidth;
                if (_generalFlow.Width != canvasWidth)
                    _generalFlow.Width = canvasWidth;
                bool home = _selectedSettingsCards == null;
                _settingsDirectory.Visible = home;
                _settingsNavigation.Visible = !home;
                _settingsNavigation.Width = canvasWidth;
                int y = 56;
                foreach (var entry in _settingsCardTitles)
                {
                    bool selected = !home && _selectedSettingsCards.Contains(entry.Value);
                    entry.Key.Visible = selected;
                    if (!selected) continue;
                    entry.Key.Width = canvasWidth - 4;
                    entry.Key.Location = new Point(0, y);
                    y += entry.Key.Height + 14;
                }
                if (home) LayoutSettingsDirectory();
                else
                {
                    _generalFlow.Height = y;
                    _generalCanvas.Height = Math.Max(_generalCanvas.Parent.ClientSize.Height - 40, y + 32);
                }
            }
            finally
            {
                _reflowingSettingsCards = false;
            }
        }

        private void ResetGeneralDefaults()
        {
            _txtPrefix.Text = "INV";
            _numGSTRate.Value = Clamp(_numGSTRate, 18m);
            _numPayTerms.Value = Clamp(_numPayTerms, 30m);
            _numMarkupPct.Value = Clamp(_numMarkupPct, 25m);
            _numEInvoiceThreshold.Value = Clamp(_numEInvoiceThreshold, 50000000m);
            if (_cmbDisplayFitMode != null)
                _cmbDisplayFitMode.SelectedIndex = 0;
            RefreshIndiaDefaultsPreview();
            global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_lblStatus, "Defaults restored in the form. Click Save Settings to persist them.");
            _lblStatus.ForeColor = SaveGreen;
        }

        private void LayoutCompanyInformationCard(Panel parent, Button locateButton)
        {
            if (parent == null)
                return;

            int gap = 14;
            int width = Math.Max(320, parent.ClientSize.Width);
            int col = Math.Max(142, (width - gap) / 2);
            int rightX = col + gap;
            int full = width;

            SetLabeledControlBounds(parent, "Company Name *", _txtCompanyName, 0, 0, col);
            SetLabeledControlBounds(parent, "GSTIN", _txtGST, rightX, 0, col);
            SetLabeledControlBounds(parent, "PAN", _txtPAN, 0, 52, col);
            SetLabeledControlBounds(parent, "TAN", _txtTAN, rightX, 52, col);
            SetLabeledControlBounds(parent, "Phone", _txtPhone, 0, 104, col);
            SetLabeledControlBounds(parent, "Email", _txtEmail, rightX, 104, col);

            int locateWidth = locateButton == null ? 0 : locateButton.Width;
            SetLabeledControlBounds(parent, "Address / City", _txtAddress, 0, 156, Math.Max(170, full - locateWidth - gap));
            if (locateButton != null)
                locateButton.Location = new Point(Math.Max(0, full - locateButton.Width), 176);

            SetLabeledControlBounds(parent, "Office Latitude", _txtOfficeLatitude, 0, 208, col);
            SetLabeledControlBounds(parent, "Office Longitude", _txtOfficeLongitude, rightX, 208, col);
            SetLabeledControlBounds(parent, "State / UT", _cmbState, 0, 260, col);
            SetLabeledControlBounds(parent, "GST Registration Type", _cmbGstRegistrationType, rightX, 260, col);
            SetLabeledControlBounds(parent, "Authorised Signatory (PDF)", _txtAuthorisedSignatory, 0, 312, Math.Max(170, full));
        }

        private void LayoutIndiaDefaultsCard(Panel parent)
        {
            if (parent == null)
                return;

            int gap = 14;
            int width = Math.Max(320, parent.ClientSize.Width);
            int col = Math.Max(142, (width - gap) / 2);
            int rightX = col + gap;

            SetLabeledControlBounds(parent, "Invoice Prefix", _txtPrefix, 0, 0, col);
            SetLabeledControlBounds(parent, "Default GST %", _numGSTRate, rightX, 0, col);
            SetLabeledControlBounds(parent, "Payment Terms (days)", _numPayTerms, 0, 58, col);
            SetLabeledControlBounds(parent, "Default Markup %", _numMarkupPct, rightX, 58, col);
            SetLabeledControlBounds(parent, "Annual Turnover", _numAnnualTurnover, 0, 116, col);
            SetLabeledControlBounds(parent, "E-Invoice Threshold", _numEInvoiceThreshold, rightX, 116, col);
            SetLabeledControlBounds(parent, "Currency", _txtCurrency, 0, 174, col);
            SetLabeledControlBounds(parent, "Financial Year", _txtFinancialYear, rightX, 174, col);
            _chkEInvoiceEligible.Location = new Point(0, 236);
            _chkEInvoiceEligible.Width = col;
            _lblMoneyPreview.Location = new Point(rightX, 232);
            _lblMoneyPreview.Size = new Size(col, 54);
        }

        private void CenterCanvas(Panel viewport, Panel canvas)
        {
            if (viewport == null || canvas == null)
                return;

            canvas.Left = 20;
            canvas.Top = 20;
            ReflowSettingsCards();
        }

        private Panel AddSectionCard(Panel parent, ref int y, string title, string subtitle, int height, Color? shadowColor = null)
        {
            Panel shadow = new Panel
            {
                Location = new Point(0, y),
                Size = new Size(GeneralCanvasWidth, height),
                BackColor = shadowColor ?? Color.FromArgb(226, 232, 240),
                Padding = new Padding(0, 0, 2, 2)
            };

            Panel card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };
            DS.Rounded(card, 10);

            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Color.White,
                Padding = new Padding(18, 12, 18, 8)
            };
            header.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Slate200))
                    e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };

            Label titleLabel = global::HVAC_Pro_Desktop.Services.LanguageManager.DataText(new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 11, FontStyle.Bold),
                ForeColor = DS.Slate900,
                TextAlign = ContentAlignment.MiddleLeft
            });
            Label overflowLabel = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
            {
                Dock = DockStyle.Right,
                Width = 76,
                Text = "Scroll",
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 8.5f),
                ForeColor = DS.Slate500,
                TextAlign = ContentAlignment.MiddleRight,
                Visible = false
            });
            Button btnExtend = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Button
            {
                Dock = DockStyle.Right,
                Width = 76,
                Height = 24,
                Text = "Extend",
                BackColor = Color.White,
                ForeColor = DS.Slate700,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 8.5f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            });
            btnExtend.FlatAppearance.BorderColor = DS.Border;
            btnExtend.FlatAppearance.BorderSize = 1;
            btnExtend.FlatAppearance.MouseOverBackColor = DS.Slate100;
            btnExtend.Margin = new Padding(0);
            Panel headerRight = new Panel { Dock = DockStyle.Right, Width = 160, BackColor = Color.White };
            headerRight.Controls.Add(btnExtend);
            headerRight.Controls.Add(overflowLabel);
            header.Controls.Add(headerRight);
            header.Controls.Add(titleLabel);

            Panel content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(24, 14, 24, 22),
                AutoScroll = true
            };

            Panel body = new Panel
            {
                Dock = DockStyle.Top,
                Height = Math.Max(120, height - 92),
                BackColor = Color.White
            };
            body.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            content.Resize += (s, e) =>
            {
                int scrollWidth = content.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0;
                body.Width = Math.Max(120, content.ClientSize.Width - content.Padding.Horizontal - scrollWidth);
            };

            content.Controls.Add(body);
            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                content.Controls.Add(global::HVAC_Pro_Desktop.Services.LanguageManager.DataText(new Label
                {
                    Text = subtitle,
                    Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9),
                    ForeColor = DS.Slate500,
                    Dock = DockStyle.Top,
                    Height = 34
                }));
            }
            card.Controls.Add(content);
            card.Controls.Add(header);
            shadow.Controls.Add(card);
            shadow.Tag = new SectionCardState
            {
                BaseHeight = height,
                ExpandedHeight = Math.Min(760, height + Math.Max(160, height / 2)),
                IsExpanded = false
            };
            btnExtend.Click += (s, e) => ToggleSectionCard(parent, shadow, btnExtend);
            content.Layout += (s, e) => QueueSectionOverflowHint(content, overflowLabel);
            content.Resize += (s, e) => QueueSectionOverflowHint(content, overflowLabel);
            content.ControlAdded += (s, e) => QueueSectionOverflowHint(content, overflowLabel);
            content.ControlRemoved += (s, e) => QueueSectionOverflowHint(content, overflowLabel);
            parent.Controls.Add(shadow);
            y += height + 16;
            return body;
        }

        private void ToggleSectionCard(Panel parent, Panel shadow, Button button)
        {
            SectionCardState state = shadow.Tag as SectionCardState;
            if (state == null)
                return;

            state.IsExpanded = !state.IsExpanded;
            shadow.Height = state.IsExpanded ? state.ExpandedHeight : state.BaseHeight;
            global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(button, state.IsExpanded ? "Collapse" : "Extend");
            ReflowSectionCards(parent);
        }

        private void ReflowSectionCards(Panel parent)
        {
            int y = 0;
            List<Panel> cards = parent.Controls
                .OfType<Panel>()
                .Where(panel => panel.Tag is SectionCardState)
                .OrderBy(panel => panel.Top)
                .ToList();

            foreach (Panel card in cards)
            {
                card.Location = new Point(0, y);
                y += card.Height + 16;
            }

            parent.Height = y + 8;
        }

        private void QueueSectionOverflowHint(Panel content, Label overflowLabel)
        {
            if (IsDisposed)
                return;

            try
            {
                if (IsHandleCreated)
                    BeginInvoke((Action)(() => UpdateSectionOverflowHint(content, overflowLabel)));
                else
                    UpdateSectionOverflowHint(content, overflowLabel);
            }
            catch
            {
            }
        }

        private static void UpdateSectionOverflowHint(Panel content, Label overflowLabel)
        {
            bool overflow = content.VerticalScroll.Visible
                || content.HorizontalScroll.Visible
                || content.DisplayRectangle.Height > content.ClientSize.Height + 4
                || content.DisplayRectangle.Width > content.ClientSize.Width + 4;
            overflowLabel.Visible = overflow;
        }

        private void BuildLayoutResetSection(Panel parent)
        {
            parent.Controls.Clear();
            string[] pageKeys =
            {
                "Dashboard",
                "QuotationAnalysis",
                "InvoiceAnalysis",
                "JobAnalysis",
                "InventoryAnalysis",
                "PurchaseAnalysis"
            };

            parent.Controls.Add(global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
            {
                Text = "WORKSPACE LAYOUTS",
                Location = new Point(0, 0),
                Size = new Size(360, 20),
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 8.5f, FontStyle.Bold),
                ForeColor = DS.Primary700,
                BackColor = Color.White,
            }));
            parent.Controls.Add(global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
            {
                Text = "Restore a dashboard or analysis page to its original arrangement.",
                Location = new Point(0, 28),
                Size = new Size(480, 22),
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 8.8f),
                ForeColor = DS.Slate600,
                BackColor = Color.White,
                AutoEllipsis = true
            }));
            ComboBox pageSelector = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(0, 58),
                Size = new Size(248, 28),
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9f)
            };
            pageSelector.Items.AddRange(pageKeys.Select(key => (object)key.Replace("Analysis", " Analysis")).ToArray());
            pageSelector.SelectedIndex = 0;
            parent.Controls.Add(pageSelector);

            Button resetSelected = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Reset Selected", InfoBlue, 142));
            resetSelected.Location = new Point(260, 56);
            resetSelected.Click += (s, e) =>
            {
                int index = pageSelector.SelectedIndex;
                if (index >= 0 && index < pageKeys.Length)
                    ResetLayout(pageKeys[index]);
            };
            parent.Controls.Add(resetSelected);

            Button resetAll = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Reset All Layouts", DS.Slate600, 160));
            resetAll.Location = new Point(0, 106);
            resetAll.Click += (s, e) =>
            {
                foreach (string pageKey in pageKeys)
                    ResetLayout(pageKey);
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_lblStatus, "All card layouts reset to default.");
            };
            parent.Controls.Add(resetAll);
            parent.Resize += (s, e) =>
            {
                int width = Math.Max(300, parent.ClientSize.Width);
                resetSelected.Left = Math.Max(0, width - resetSelected.Width);
                pageSelector.Width = Math.Max(150, resetSelected.Left - 12);
            };
            parent.Height = 154;
        }

        private void BuildFreshStartSection(Panel parent)
        {
            parent.Controls.Add(global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
            {
                Text = "Warning: this permanently removes transactional records, clients, employees, vendors, sites, contracts, salary, and settings. Users, roles, and permissions stay intact.",
                Location = new Point(0, 0),
                Width = Math.Max(240, parent.ClientSize.Width),
                Height = 54,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9),
                ForeColor = Color.FromArgb(127, 29, 29),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            }));

            Button button = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Fresh Start", Color.FromArgb(220, 38, 38), 132));
            button.Location = new Point(0, 62);
            button.Click += (s, e) => RunFreshStart();
            parent.Controls.Add(button);

            parent.Controls.Add(global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
            {
                Text = "Type CONFIRM in the next dialog to unlock the delete action.",
                Location = new Point(0, 112),
                Width = Math.Max(240, parent.ClientSize.Width),
                Height = 24,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9),
                ForeColor = DS.Slate500,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            }));
        }

        private void BuildBackupRestoreSection(Panel parent)
        {
            _lblBackupStatus = global::HVAC_Pro_Desktop.Services.LanguageManager.DataText(new Label
            {
                Text = BuildBackupSummary(),
                Location = new Point(0, 0),
                Width = Math.Max(260, parent.ClientSize.Width),
                Height = 58,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9),
                ForeColor = DS.Slate700,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            });
            parent.Controls.Add(_lblBackupStatus);

            Button btnBackupNow = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Backup Now", SaveGreen, 124));
            btnBackupNow.Location = new Point(0, 78);
            btnBackupNow.Click += async (s, e) => await CreateBackupAsync();
            parent.Controls.Add(btnBackupNow);

            Button btnBackupSettings = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Backup Settings", InfoBlue, 142));
            btnBackupSettings.Location = new Point(140, 78);
            btnBackupSettings.Click += (s, e) => OpenBackupSettings();
            parent.Controls.Add(btnBackupSettings);

            Button btnSharedStorage = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Shared Storage", InfoBlue, 142));
            btnSharedStorage.Location = new Point(298, 78);
            btnSharedStorage.Click += (s, e) => OpenSharedStorageSettings();
            parent.Controls.Add(btnSharedStorage);

            Button btnRestoreFile = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Restore File", Color.FromArgb(220, 38, 38), 124));
            btnRestoreFile.Location = new Point(456, 78);
            btnRestoreFile.Click += async (s, e) => await RestoreFromFileAsync();
            parent.Controls.Add(btnRestoreFile);

            Button btnOpenFolder = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Open Folder", InfoBlue, 124));
            btnOpenFolder.Location = new Point(596, 78);
            btnOpenFolder.Click += (s, e) => OpenBackupFolder();
            parent.Controls.Add(btnOpenFolder);

            parent.Resize += (s, e) =>
            {
                int gap = 10;
                int buttonWidth = Math.Max(92, (parent.ClientSize.Width - (gap * 4)) / 5);
                btnBackupNow.SetBounds(0, 78, buttonWidth, 34);
                btnBackupSettings.SetBounds(btnBackupNow.Right + gap, 78, buttonWidth, 34);
                btnSharedStorage.SetBounds(btnBackupSettings.Right + gap, 78, buttonWidth, 34);
                btnRestoreFile.SetBounds(btnSharedStorage.Right + gap, 78, buttonWidth, 34);
                btnOpenFolder.SetBounds(btnRestoreFile.Right + gap, 78, buttonWidth, 34);
            };

            parent.Controls.Add(global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
            {
                Text = "Backups stay on the client network, local PC, or external drive. Restore first creates a safety backup and then restores the selected .bak.",
                Location = new Point(0, 128),
                Width = Math.Max(260, parent.ClientSize.Width),
                Height = 58,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 8.8f),
                ForeColor = DS.Slate500,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            }));
        }

        private void BuildLicenseSection(Panel parent)
        {
            _lblLicenseStatus = global::HVAC_Pro_Desktop.Services.LanguageManager.DataText(new Label
            {
                Text = BuildLicenseSummary(),
                Location = new Point(0, 0),
                Width = Math.Max(260, parent.ClientSize.Width),
                Height = 82,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9),
                ForeColor = DS.Slate700,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            });
            parent.Controls.Add(_lblLicenseStatus);

            Button activate = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Activate / Renew", SaveGreen, 144));
            activate.Location = new Point(0, 104);
            activate.Click += (s, e) => OpenLicenseActivation();
            parent.Controls.Add(activate);

            Button refresh = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Refresh Status", InfoBlue, 132));
            refresh.Location = new Point(160, 104);
            refresh.Click += (s, e) => RefreshLicenseStatus();
            parent.Controls.Add(refresh);

            Button copyFingerprint = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(MakeBtn("Copy Device ID", Color.White, 132));
            copyFingerprint.ForeColor = DS.Slate700;
            copyFingerprint.FlatAppearance.BorderColor = DS.Border;
            copyFingerprint.FlatAppearance.BorderSize = 1;
            copyFingerprint.Location = new Point(308, 104);
            copyFingerprint.Click += (s, e) => CopyLicenseDeviceFingerprint();
            parent.Controls.Add(copyFingerprint);
            parent.Resize += (s, e) =>
            {
                int gap = 10;
                int buttonWidth = Math.Max(104, (parent.ClientSize.Width - (gap * 2)) / 3);
                activate.SetBounds(0, 104, buttonWidth, 34);
                refresh.SetBounds(activate.Right + gap, 104, buttonWidth, 34);
                copyFingerprint.SetBounds(refresh.Right + gap, 104, buttonWidth, 34);
            };

            parent.Controls.Add(global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
            {
                Text = "Frozen Mode allows login, read-only data access, reports export, backup export, and license renewal only.",
                Location = new Point(0, 158),
                Width = Math.Max(260, parent.ClientSize.Width),
                Height = 48,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 8.8f),
                ForeColor = DS.Slate500,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            }));
        }

        private string BuildLicenseSummary()
        {
            LicenseValidationResult result = _licenseSvc.ValidateCurrentLicense();
            LicenseSnapshot s = result.Snapshot;
            if (s == null || string.IsNullOrWhiteSpace(s.LicenseKey))
                return "License: activation required.";

            string displayPlan = LicensePlanCatalog.GetDisplayName(s);
            string price = BuildLicensePriceText(s);
            string companyCode = string.IsNullOrWhiteSpace(s.CompanyCode) ? string.Empty : " | Code: " + s.CompanyCode;
            string mode = s.OnlineValidationRequired ? "Online subscription" : "Offline/local license";
            return "License: " + s.Status
                + "\r\nPlan: " + displayPlan + " | Company: " + s.CompanyName + companyCode
                + "\r\nExpires: " + s.ExpiryDateUtc.ToLocalTime().ToString("dd MMM yyyy")
                + " | Devices: " + Math.Max(1, s.ActivatedDeviceCount) + "/" + LicensePlanCatalog.GetDisplayMaxDevices(s) + " | Grace: " + s.GracePeriodDays + " day(s)"
                + "\r\nMode: " + mode
                + "\r\n" + price + (string.IsNullOrWhiteSpace(price) ? string.Empty : " | ") + (s.StatusMessage ?? result.Message);
        }

        private static string BuildLicensePriceText(LicenseSnapshot s)
        {
            if (s == null)
                return string.Empty;

            string currency = string.IsNullOrWhiteSpace(s.Currency) ? "INR" : s.Currency;
            decimal price = LicensePlanCatalog.GetDisplayAnnualPrice(s);
            if (price <= 0)
                return string.Empty;
            decimal renewal = price;
            string offer = s.IsLaunchOffer ? " launch offer" : string.Empty;
            return "Price: " + FormatLicenseMoney(currency, price) + "/year"
                + " | Renewal: " + FormatLicenseMoney(currency, renewal) + "/year" + offer;
        }

        private static string FormatLicenseMoney(string currency, decimal amount)
        {
            string symbol = string.Equals(currency, "INR", StringComparison.OrdinalIgnoreCase) ? "₹" : currency + " ";
            return symbol + amount.ToString("N0", CultureInfo.GetCultureInfo("en-IN"));
        }

        private void RefreshLicenseStatus()
        {
            if (_lblLicenseStatus != null)
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblLicenseStatus, BuildLicenseSummary());
            global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_lblStatus, "License status refreshed.");
            _lblStatus.ForeColor = SaveGreen;
            RefreshSettingsWorkspaceSummary();
        }

        private void OpenLicenseActivation()
        {
            using (var dialog = new LicenseActivationForm())
            {
                dialog.ShowDialog(this);
            }

            RefreshLicenseStatus();
        }

        private void CopyLicenseDeviceFingerprint()
        {
            string fingerprint = _deviceFingerprintSvc.GetFingerprintHash();
            if (UIHelper.TrySetClipboardText(this, fingerprint, BrandingService.WindowTitle("License")))
            {
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_lblStatus, "Device fingerprint copied for license issuance.");
                _lblStatus.ForeColor = SaveGreen;
            }
        }

        private string BuildBackupSummary()
        {
            try
            {
            var latest = _backupSvc.GetBackupLog(50).FirstOrDefault(r => r.Success);
                if (latest == null)
                    return "No successful backups found. Local fallback folder: " + BackupService.BackupRoot;

                return "Latest backup: " + latest.BackupTime.ToString("dd MMM yyyy HH:mm", CultureInfo.CurrentCulture) + " | " + latest.Destination + " | " + latest.FileSizeKB.ToString("N0") + " KB";
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SettingsForm.BuildBackupSummary", ex);
                return "Backup status could not be loaded: " + ex.Message;
            }
        }

        private async Task CreateBackupAsync()
        {
            SetBackupStatus("Creating database backup...", DS.Slate700);
            try
            {
            BackupResult result = await Task.Run(() => _backupSvc.RunBackup(BackupTrigger.Manual));
                SetBackupStatus(result.Success ? BuildBackupSummary() : "Backup failed: " + result.Message, result.Success ? SaveGreen : Color.Red);
                ToastNotification.ShowToast(result.Success ? "Backup completed - saved to " + FriendlyBackupDestination(result.DestinationUsed) : "Backup failed - please check settings", result.Success ? SaveGreen : Color.Red);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SettingsForm.CreateBackupAsync", ex);
                SetBackupStatus("Backup failed: " + ex.Message, Color.Red);
            }
        }

        private void OpenBackupSettings()
        {
            using (var form = new BackupSettingsForm())
                form.ShowDialog(FindForm());

            if (_lblBackupStatus != null)
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblBackupStatus, BuildBackupSummary());
            RefreshSettingsWorkspaceSummary();
        }

        private void OpenSharedStorageSettings()
        {
            using (var form = new SharedStorageSettingsForm())
                form.ShowDialog(FindForm());

            if (_lblBackupStatus != null)
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblBackupStatus, BuildBackupSummary());
            RefreshSettingsWorkspaceSummary();
        }

        private void RefreshSettingsWorkspaceSummary()
        {
            if (_lblSettingsVersionState == null)
                return;

            global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblSettingsVersionState, ConfigService.GetAppVersion());

            string dbStatus = _lblDbStatus == null ? string.Empty : _lblDbStatus.Text;
            if (dbStatus.IndexOf("NOT connected", StringComparison.OrdinalIgnoreCase) >= 0)
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_lblSettingsDbState, "Issue");
            else if (dbStatus.IndexOf("checking", StringComparison.OrdinalIgnoreCase) >= 0)
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_lblSettingsDbState, "Checking");
            else if (!string.IsNullOrWhiteSpace(dbStatus))
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_lblSettingsDbState, "Ready");
            else
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_lblSettingsDbState, "Pending");

            string backupSummary = _lblBackupStatus != null && !string.IsNullOrWhiteSpace(_lblBackupStatus.Text)
                ? _lblBackupStatus.Text
                : BuildBackupSummary();
            global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblSettingsBackupState, (backupSummary.IndexOf("No successful backups", StringComparison.OrdinalIgnoreCase) >= 0 ? global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Pending") : (backupSummary.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0 ? "Issue" : "Ready")));

            string licenseSummary = _lblLicenseStatus != null && !string.IsNullOrWhiteSpace(_lblLicenseStatus.Text)
                ? _lblLicenseStatus.Text
                : BuildLicenseSummary();
            global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_lblSettingsLicenseState, licenseSummary.IndexOf("activation required", StringComparison.OrdinalIgnoreCase) >= 0
                ? "Action"
                : "Active");

        }

        private async Task CreateCloudBackupAsync()
        {
            SetBackupStatus("Creating cloud backup...", DS.Slate700);
            try
            {
            IntegrationOperationResult result = await _cloudBackupIntegrationSvc.CreateAndUploadBackupAsync(System.Threading.CancellationToken.None);
                SetBackupStatus(result.Success ? "Cloud backup complete: " + result.ReferenceId : "Cloud backup failed: " + result.Message, result.Success ? SaveGreen : Color.Red);
                MessageBox.Show(
                    (result.Success ? global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Cloud backup completed:\r\n{0}", result.LocalPath) : global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Cloud backup failed:\r\n{0}", result.Message)),
                    (result.Success ? global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Cloud Backup Complete") : global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Cloud Backup Failed")),
                    MessageBoxButtons.OK,
                    result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SettingsForm.CreateCloudBackupAsync", ex);
                SetBackupStatus("Cloud backup failed: " + ex.Message, Color.Red);
            }
        }

        private async Task RestoreFromFileAsync()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select ServoERP SQL backup";
                dialog.Filter = "SQL Server Backup (*.bak)|*.bak";
                dialog.InitialDirectory = Directory.Exists(BackupService.BackupRoot) ? BackupService.BackupRoot : @"C:\";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                await RestoreBackupAsync(dialog.FileName);
            }
        }

        private async Task RestoreBackupAsync(string backupPath)
        {
            string fileName = Path.GetFileName(backupPath);
            bool confirm = ServoERP.Infrastructure.ServoConfirmDialog.Show(
                this,
                "Restore database from backup?",
                "Backup file: " + fileName + "\r\n\r\nCurrent data will be replaced. ServoERP will create a safety backup first.");
            if (!confirm)
                return;

            SetBackupStatus("Restoring database from " + fileName + "...", DS.Slate700);
            try
            {
            BackupResult result = await Task.Run(() => _backupSvc.RestoreDatabaseBackup(backupPath, true));
                if (result.Success)
                {
                    MainForm mainForm = FindForm() as MainForm;
                    mainForm?.ClearCachedPagesExceptCurrent();
                    SetBackupStatus("Restore complete. Reopen ServoERP before continuing work.", SaveGreen);
                    MessageBox.Show(global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Restore completed.\r\n\r\nClose and reopen ServoERP before continuing work."), global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Restore Complete"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    SetBackupStatus("Restore failed: " + result.Message, Color.Red);
                    MessageBox.Show(global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Restore failed:\r\n{0}", result.Message), global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Restore Failed"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SettingsForm.RestoreBackupAsync", ex);
                SetBackupStatus("Restore failed: " + ex.Message, Color.Red);
            }
        }

        private void OpenBackupFolder()
        {
            try
            {
                Directory.CreateDirectory(BackupService.BackupRoot);
                System.Diagnostics.Process.Start("explorer.exe", BackupService.BackupRoot);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SettingsForm.OpenBackupFolder", ex);
                SetBackupStatus("Could not open backup folder: " + ex.Message, Color.Red);
            }
        }

        private void SetBackupStatus(string text, Color color)
        {
            if (_lblBackupStatus != null)
            {
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblBackupStatus, text);
                _lblBackupStatus.ForeColor = color;
            }

            global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblStatus, text);
            _lblStatus.ForeColor = color;
        }

        private static string FriendlyBackupDestination(string destination)
        {
            if (string.Equals(destination, "Network", StringComparison.OrdinalIgnoreCase))
                return "Network Server";
            if (string.Equals(destination, "Local", StringComparison.OrdinalIgnoreCase))
                return "Local Folder";
            if (string.Equals(destination, "ExternalDrive", StringComparison.OrdinalIgnoreCase))
                return "External Drive";
            return "backup destination";
        }

        private void RunFreshStart()
        {
            using (Form dialog = ServoModalForm.Create(global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Fresh Start Confirmation"), 520, 280))
            {
                var prompt = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
                {
                    Text = "All data including master data will be deleted. This cannot be undone.\r\n\r\nType CONFIRM to proceed.",
                    Location = new Point(18, 18),
                    Size = new Size(484, 130)
                });
                var confirmBox = new TextBox { Location = new Point(18, 164), Width = 484 };
                var btnCancel = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Button { Text = "Cancel", Location = new Point(318, 214), Width = 88, DialogResult = DialogResult.Cancel });
                var btnDelete = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Button { Text = "Delete", Location = new Point(414, 214), Width = 88, Enabled = false, BackColor = Color.FromArgb(220, 38, 38), ForeColor = Color.White, FlatStyle = FlatStyle.Flat });
                btnDelete.FlatAppearance.BorderSize = 0;
                _toolTip.SetToolTip(btnDelete, LanguageManager.Get("Type CONFIRM to unlock this destructive action."));
                confirmBox.TextChanged += (s, e) => btnDelete.Enabled = string.Equals(confirmBox.Text, "CONFIRM", StringComparison.Ordinal);
                btnDelete.Click += (s, e) => dialog.DialogResult = DialogResult.OK;
                dialog.Controls.AddRange(new Control[] { prompt, confirmBox, btnCancel, btnDelete });
                dialog.AcceptButton = btnDelete;
                dialog.CancelButton = btnCancel;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
            }

            try
            {
                FreshStartResult result = _freshStartSvc.RunFreshStart();
                MainForm mainForm = FindForm() as MainForm;
                mainForm?.ClearCachedPagesExceptCurrent();
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_lblStatus, "Fresh Start complete.");
                _lblStatus.ForeColor = SaveGreen;
                MessageBox.Show(
                    global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Fresh Start complete. The following data was cleared:\r\n- Jobs\r\n- Quotations\r\n- Invoices\r\n- Payments\r\n- Purchases\r\n- Attendance records\r\n- SLA Logs\r\n- Clients\r\n- Employees\r\n- Vendors\r\n- Sites\r\n- Contracts\r\n- Salary\r\n- Settings\r\n\r\nApp is ready for a new client."),
                    global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Fresh Start Complete"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SettingsForm.RunFreshStart", ex);
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblStatus, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Fresh Start failed: {0}", ex.Message));
                _lblStatus.ForeColor = Color.Red;
                MessageBox.Show(global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Fresh Start failed. No data was removed."), global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Fresh Start Failed"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ResetLayout(string pageKey)
        {
            int userId = CardLayoutService.ResolveCurrentUserId();
            System.Threading.Tasks.Task.Run(() => _cardLayoutSvc.ResetPageLayout(userId, pageKey));
            MainForm mainForm = FindForm() as MainForm;
            if (mainForm != null)
                mainForm.ReloadPageByKey(pageKey);
            global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblStatus, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("{0} layout reset.", pageKey));
            _lblStatus.ForeColor = SaveGreen;
        }

        private void PlaceLabeledControl(Panel parent, string label, Control control, int x, int y, int width, int height = 34)
        {
            parent.Controls.Add(global::HVAC_Pro_Desktop.Services.LanguageManager.DataText(new Label
            {
                Text = label,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 8.5f, FontStyle.Bold),
                ForeColor = DS.Slate500,
                Location = new Point(x, y),
                Width = width
            }));

            StyleInputControl(control);
            control.Location = new Point(x, y + 20);
            control.Size = new Size(width, height);
            parent.Controls.Add(control);
            parent.Height = Math.Max(parent.Height, y + height + 36);
        }

        private void SetLabeledControlBounds(Panel parent, string label, Control control, int x, int y, int width, int height = 34)
        {
            if (parent == null || control == null)
                return;

            Label labelControl = parent.Controls
                .OfType<Label>()
                .FirstOrDefault(l => string.Equals(LanguageManager.GetSourceText(l, l.Text), label, StringComparison.Ordinal));
            if (labelControl != null)
            {
                labelControl.Location = new Point(x, y);
                labelControl.Width = width;
                labelControl.Height = 18;
            }

            control.SetBounds(x, y + 20, width, height);
        }

        private void StyleInputControl(Control control)
        {
            control.Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9.5f);

            if (control is TextBox textBox)
            {
                textBox.BorderStyle = BorderStyle.FixedSingle;
                textBox.BackColor = textBox.ReadOnly ? DS.Slate100 : Color.White;
            }
            else if (control is ComboBox comboBox)
            {
                comboBox.FlatStyle = FlatStyle.Standard;
                comboBox.BackColor = Color.White;
            }
            else if (control is NumericUpDown numeric)
            {
                numeric.BackColor = Color.White;
                numeric.BorderStyle = BorderStyle.FixedSingle;
                numeric.ThousandsSeparator = true;
            }
        }

        private void StyleDataGrid(DataGridView grid)
        {
            DS.StyleGrid(grid);
            grid.RowTemplate.Height = 38;
            grid.ColumnHeadersHeight = 42;
        }

        private TextBox MakeReadOnlyField(Point location, int width)
        {
            return new TextBox
            {
                Location = location,
                Width = width,
                ReadOnly = true,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9),
                BorderStyle = BorderStyle.None,
                BackColor = Color.White
            };
        }

        private static decimal Clamp(NumericUpDown box, decimal value)
        {
            return Math.Max(box.Minimum, Math.Min(box.Maximum, value));
        }

        private static decimal ParseDecimal(string value, decimal fallback)
        {
            return decimal.TryParse(value, out decimal parsed) ? parsed : fallback;
        }

        private static double? ParseNullableDouble(string value)
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                ? (double?)parsed
                : null;
        }

        private static void SelectCombo(ComboBox combo, string value, string fallback)
        {
            string target = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (string.Equals(combo.Items[i].ToString(), target, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }

            if (combo.Items.Count > 0)
                combo.SelectedIndex = 0;
        }

        private static string ToCell(object value)
        {
            return value == null || value == DBNull.Value ? string.Empty : value.ToString().Trim();
        }

        private static int ToInt(object value)
        {
            return value != null && int.TryParse(value.ToString(), out int parsed) ? parsed : 0;
        }

        private static decimal ToDecimal(object value, decimal defaultValue)
        {
            return value != null && decimal.TryParse(value.ToString(), out decimal parsed) ? parsed : defaultValue;
        }

        private static bool ToBool(object value)
        {
            return value != null && value != DBNull.Value && Convert.ToBoolean(value);
        }

        private async Task LocateOfficeAsync()
        {
            try
            {
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_lblStatus, "Locating office address...");
                _lblStatus.ForeColor = InfoBlue;
                GeocodeResult result = await Task.Run(() => _geoSvc.LocateAddress(_txtAddress.Text));
                _txtOfficeLatitude.Text = result.Latitude.ToString("0.0000000", CultureInfo.InvariantCulture);
                _txtOfficeLongitude.Text = result.Longitude.ToString("0.0000000", CultureInfo.InvariantCulture);
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_lblStatus, "Office coordinates updated from OpenStreetMap.");
                _lblStatus.ForeColor = SaveGreen;
            }
            catch (Exception ex)
            {
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblStatus, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Locate error: {0}", ex.Message));
                _lblStatus.ForeColor = Color.Red;
            }
        }

        private async Task CheckVersionNowAsync()
        {
            try
            {
                ConfigService.Set("App", "GitHubRepositoryUrl", ConfigService.ProductionVersionCheckUrl);
                ConfigService.Set("App", "VersionCheckUrl", ConfigService.ProductionVersionCheckUrl);
                if (_txtVersionCheckUrl != null)
                    _txtVersionCheckUrl.Text = UpdateService.GetGitHubRepositoryUrl();
                ConfigService.Set("App", "VersionCheckEnabled", _chkVersionCheckEnabled == null || _chkVersionCheckEnabled.Checked ? "true" : "false");
                UpdateService.SetSilentAutoUpdatePreference(_chkSilentAutoUpdateEnabled != null && _chkSilentAutoUpdateEnabled.Checked);

                if (_chkVersionCheckEnabled != null && !_chkVersionCheckEnabled.Checked)
                {
                    if (_lblLastUpdateCheckStatus != null)
                        global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_lblLastUpdateCheckStatus, "Update checks are turned off in Settings.");
                    MessageBox.Show(
                        global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Update notification settings were saved, but automatic checks are turned off.\r\nTurn on \"Check for updates automatically\" to show update banners at startup."),
                        global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Update notifications"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                UpdateCheckResult result = await UpdateService.CheckForUpdatesAsync();
                if (_lblLastUpdateCheckStatus != null)
                    global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblLastUpdateCheckStatus, UpdateService.GetLastUpdateStatusDisplay());

                if (result.IsUpdateAvailable)
                {
                    if (!result.CanApplyUpdate)
                    {
                        MessageBox.Show(
                            global::HVAC_Pro_Desktop.Services.LanguageManager.Format("{0}\r\n\r\nServoERP did not open a browser or start an external download.", result.StatusMessage),
                            global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Update unavailable"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }

                    bool install = ServoERP.Infrastructure.ServoConfirmDialog.Show(
                        this,
                        "Install ServoERP update?",
                        "ServoERP v" + result.LatestVersion + " is available from GitHub Releases. Current version: v" + result.CurrentVersion + ".\r\n\r\nServoERP will download the update, back up configuration, restart, and apply it. Save your work before continuing.");
                    if (install)
                        await InstallUpdateAsync(result);
                }
                else
                {
                    MessageBox.Show(
                        result.StatusMessage,
                        global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Check for updates"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SettingsForm.CheckVersionNowAsync", ex);
                if (_lblLastUpdateCheckStatus != null)
                    global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_lblLastUpdateCheckStatus, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Update check failed. ServoERP will continue normally. {0}", ex.Message));
                MessageBox.Show(
                    global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Update check failed. ServoERP will continue normally.\r\n\r\n{0}", ex.Message),
                    global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Check for updates"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private Task InstallUpdateAsync(UpdateCheckResult result)
        {
            if (result == null || !result.IsUpdateAvailable)
                return Task.CompletedTask;

            using (var progressForm = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Form
            {
                Text = "Downloading ServoERP update",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                Size = new Size(420, 150),
                BackColor = DS.BgPage,
                Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9f)
            }))
            using (var cancelSource = new CancellationTokenSource())
            {
                var status = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label
                {
                    Text = "Downloading update package...",
                    Dock = DockStyle.Top,
                    Height = 36,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(18, 8, 18, 0),
                    ForeColor = DS.Slate800
                });
                var progress = new ProgressBar
                {
                    Dock = DockStyle.Top,
                    Height = 20,
                    Minimum = 0,
                    Maximum = 100
                };
                var cancel = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(DS.GhostBtn("Cancel", 90, 30));
                cancel.Dock = DockStyle.Bottom;
                cancel.Click += (s, e) => cancelSource.Cancel();
                progressForm.Controls.Add(cancel);
                progressForm.Controls.Add(progress);
                progressForm.Controls.Add(status);

                var progressReporter = new Progress<int>(value =>
                {
                    progress.Value = Math.Max(0, Math.Min(100, value));
                    global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(status, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Downloading update package... {0}%", progress.Value));
                });

                progressForm.Shown += async (s, e) =>
                {
                    try
                    {
                        await UpdateService.DownloadUpdatePackageAsync(result, progressReporter, cancelSource.Token);
                        progressForm.Close();
                        UpdateService.ApplyUpdateAndRestart(result);
                    }
                    catch (OperationCanceledException)
                    {
                        progressForm.Close();
                    }
                    catch (Exception ex)
                    {
                        progressForm.Close();
                        AppLogger.LogError("SettingsForm.InstallUpdate", ex);
                        MessageBox.Show(
                            global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Automatic update could not complete.\r\n\r\n{0}\r\n\r\nPlease try again in a few minutes.", ex.Message),
                            global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Install update"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                };

                progressForm.ShowDialog(this);
            }

            return Task.CompletedTask;
        }
    }
}

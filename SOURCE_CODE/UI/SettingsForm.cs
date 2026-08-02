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
using HVAC_Pro_Desktop.AI;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.Services.Integrations;
using HVAC_Pro_Desktop.Services.Licensing;
using HVAC_Pro_Desktop.UI.Licensing;
using HVAC_Pro_Desktop.UI.Helpers;

namespace HVAC_Pro_Desktop.UI
{
    public class SettingsForm : DeferredPageControl
    {
        protected override bool EnableAutomaticLayoutScaling => false;

        protected override bool EnableMainScrollCanvas => false;

        protected override bool SuppressAutomaticChildPolish => true;

        private readonly SettingsService _svc = new SettingsService();
        private readonly HsnSacMasterService _hsnSacSvc = new HsnSacMasterService();
        private readonly NominatimGeocodingService _geoSvc = new NominatimGeocodingService();
        private readonly AuthService _authSvc = new AuthService();
        private readonly FreshStartService _freshStartSvc = new FreshStartService();
        private readonly UnitMeasurementService _unitMeasurementSvc = new UnitMeasurementService();
        private readonly OpenSourceLicenseService _openSourceLicenseSvc = new OpenSourceLicenseService();
        private readonly ModuleCatalogService _moduleCatalogSvc = new ModuleCatalogService();
        private readonly CompliancePackService _compliancePackSvc = new CompliancePackService();
        private readonly BackupService _backupSvc = new BackupService();
        private readonly AiAssistantService _aiAssistantSvc = new AiAssistantService();
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
        private TextBox _txtUnitCode;
        private TextBox _txtUnitShortCode;
        private TextBox _txtUnitDisplayName;
        private ComboBox _cmbUnitCategory;
        private ComboBox _cmbUnitMeasurementSystem;
        private TextBox _txtUnitAliases;
        private CheckBox _chkAiEnabled;
        private ComboBox _cmbAiProvider;
        private TextBox _txtAiEndpoint;
        private TextBox _txtAiModel;
        private NumericUpDown _numAiMaxTokens;
        private NumericUpDown _numAiTemperature;
        private Panel _generalFlow;
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
        private Label _lblSettingsAssistantState;
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
        private const int GeneralCanvasWidth = 1720;

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
                    _lblStatus.Text = "Load error: " + ex.Message;
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
                _lblInstalledVersion.Text = "Current version: " + ConfigService.GetAppVersion();
            if (_lblLastUpdateCheckStatus != null && !_lblLastUpdateCheckStatus.IsDisposed)
                _lblLastUpdateCheckStatus.Text = UpdateService.GetLastUpdateStatusDisplay();
            RefreshSettingsWorkspaceSummary();
        }

        private void BuildLayout()
        {
            Button btnSave = MakeBtn("Save Settings", SaveGreen, 146);
            btnSave.Location = new Point(0, 0);
            ModernIconSystem.AddButtonIcon(btnSave, ModernIconKind.Save);
            btnSave.Click += (s, e) => Save();
            Button btnResetDefaults = MakeBtn("Reset to Defaults", Color.White, 184);
            btnResetDefaults.ForeColor = DS.Slate700;
            btnResetDefaults.FlatAppearance.BorderSize = 1;
            btnResetDefaults.FlatAppearance.BorderColor = DS.Border;
            ModernIconSystem.AddButtonIcon(btnResetDefaults, ModernIconKind.Preference);
            btnResetDefaults.Click += (s, e) => ResetGeneralDefaults();
            Button btnToolbarCheckUpdates = MakeBtn("Check for Updates", InfoBlue, 170);
            btnToolbarCheckUpdates.Location = new Point(0, 0);
            ModernIconSystem.AddButtonIcon(btnToolbarCheckUpdates, ModernIconKind.Refresh);
            btnToolbarCheckUpdates.Click += async (s, e) => await CheckVersionNowAsync();
            Button btnFormsLibrary = MakeBtn("Forms Library", Color.White, 132);
            btnFormsLibrary.ForeColor = DS.Primary600;
            btnFormsLibrary.FlatAppearance.BorderSize = 1;
            btnFormsLibrary.FlatAppearance.BorderColor = DS.BorderStrong;
            ModernIconSystem.AddButtonIcon(btnFormsLibrary, ModernIconKind.Document);
            btnFormsLibrary.Click += (s, e) => FormTemplateWorkflowLauncher.Open(this, "Settings / Master Data", "Master Data", null, "company document templates import validation backup compliance settings forms library");
            _lblStatus = new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 9),
                ForeColor = DS.Slate500,
                Width = 220,
                Height = 22,
                TextAlign = ContentAlignment.MiddleRight
            };
            Panel header = BuildModernSettingsHeader(btnSave, btnResetDefaults, btnToolbarCheckUpdates, btnFormsLibrary);

            _tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
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
            _generalFlow.Controls.Add(new Label
            {
                Text = "Loading Settings...",
                Location = new Point(0, 0),
                Size = new Size(360, 32),
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = DS.Slate600
            });
            CenterCanvas(body, _generalCanvas);
            generalTab.Controls.Add(body);
            _tabs.TabPages.Add(generalTab);
            if (IsAdminUser())
            {
                _usersTab = CreateLazySettingsTab("Users & Logins", BuildUsersTabContent);
                _auditTab = CreateLazySettingsTab("Audit Log", BuildAuditTabContent);
                _tabs.TabPages.Add(_usersTab);
                _tabs.TabPages.Add(_auditTab);
            }
            _tabs.SelectedIndexChanged += SettingsTabs_SelectedIndexChanged;

            Controls.Add(_tabs);
            Controls.Add(header);
        }

        private void BuildForm(Panel parent, bool includeDeferredCards)
        {
            parent.Controls.Clear();

            if (IsAdminUser())
                BuildLoginAccessSection(parent);

            AppRuntime.LogTiming("Settings.BuildForm.Guides.Start", 0);
            BuildGeneralSettingsGuide(parent);
            BuildHelpSupportCard(parent);
            BuildUpdateNotificationsCard(parent);
            if (includeDeferredCards)
            {
                BuildAgentSimulationCard(parent);
                BuildDevTeamDashboardCard(parent);
            }
            AppRuntime.LogTiming("Settings.BuildForm.Guides.Complete", 0);

            AppRuntime.LogTiming("Settings.BuildForm.Company.Start", 0);
            Panel companyBody = AddModernSettingsCard(parent, "Company Information", "Profile, compliance, and office location details used across the platform.", 482);
            _txtCompanyName = new TextBox();
            PlaceLabeledControl(companyBody, "Company Name *", _txtCompanyName, 0, 0, 210);
            _txtGST = new TextBox { CharacterCasing = CharacterCasing.Upper };
            PlaceLabeledControl(companyBody, "GSTIN", _txtGST, 226, 0, 190);
            new ToolTip().SetToolTip(_txtGST, "Format: 22ABCDE1234F1Z5");
            _txtPAN = new TextBox { CharacterCasing = CharacterCasing.Upper };
            PlaceLabeledControl(companyBody, "PAN", _txtPAN, 432, 0, 150);
            _txtTAN = new TextBox { CharacterCasing = CharacterCasing.Upper };
            PlaceLabeledControl(companyBody, "TAN", _txtTAN, 0, 64, 170);
            _txtPhone = new TextBox();
            PlaceLabeledControl(companyBody, "Phone", _txtPhone, 190, 64, 170);
            _txtEmail = new TextBox();
            PlaceLabeledControl(companyBody, "Email", _txtEmail, 380, 64, 202);
            _txtAddress = new TextBox();
            PlaceLabeledControl(companyBody, "Address / City", _txtAddress, 0, 128, 442);

            Button btnLocateOffice = MakeBtn("Locate", InfoBlue, 112);
            btnLocateOffice.Location = new Point(458, 148);
            btnLocateOffice.Click += async (s, e) => await LocateOfficeAsync();
            companyBody.Controls.Add(btnLocateOffice);

            _txtOfficeLatitude = new TextBox { ReadOnly = false };
            PlaceLabeledControl(companyBody, "Office Latitude", _txtOfficeLatitude, 0, 192, 170);
            _txtOfficeLongitude = new TextBox { ReadOnly = false };
            PlaceLabeledControl(companyBody, "Office Longitude", _txtOfficeLongitude, 190, 192, 170);
            _cmbState = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };
            _cmbState.Items.AddRange(IndiaStateCatalog.Names.Cast<object>().ToArray());
            PlaceLabeledControl(companyBody, "State / UT", _cmbState, 380, 192, 202);
            _cmbGstRegistrationType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };
            _cmbGstRegistrationType.Items.AddRange(new object[] { "Regular", "Composition", "Unregistered" });
            PlaceLabeledControl(companyBody, "GST Registration Type", _cmbGstRegistrationType, 0, 256, 220);
            _txtAuthorisedSignatory = new TextBox();
            PlaceLabeledControl(companyBody, "Authorised Signatory (PDF)", _txtAuthorisedSignatory, 0, 320, 340);
            companyBody.Resize += (s, e) => LayoutCompanyInformationCard(companyBody, btnLocateOffice);
            LayoutCompanyInformationCard(companyBody, btnLocateOffice);
            AppRuntime.LogTiming("Settings.BuildForm.Company.Complete", 0);

            AppRuntime.LogTiming("Settings.BuildForm.Display.Start", 0);
            Panel displayBody = AddModernSettingsCard(parent, "Display & Layout", "Customize how dense data is displayed across the system.", 340);
            _cmbDisplayFitMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };
            _cmbDisplayFitMode.Items.AddRange(new object[]
            {
                "Auto detect laptop screens",
                "IdeaPad / compact laptop",
                "Standard desktop"
            });
            PlaceLabeledControl(displayBody, "Display fit mode", _cmbDisplayFitMode, 0, 0, 330);
            _cmbUiScale = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };
            foreach (int option in LayoutScaler.GetUiScaleOptions())
                _cmbUiScale.Items.Add(option.ToString() + "%");
            PlaceLabeledControl(displayBody, "Global UI scale", _cmbUiScale, 0, 74, 160);
            Button btnSaveDisplayFit = MakeBtn("Save Display", InfoBlue, 130);
            btnSaveDisplayFit.Location = new Point(356, 20);
            btnSaveDisplayFit.Click += (s, e) =>
            {
                SaveDisplayFitSetting();
                SaveUiScaleSetting();
                LayoutScaler.ApplyDisplayFit(FindForm());
                _lblStatus.Text = "Display settings saved. Reopen ServoERP to apply global UI scale everywhere.";
                _lblStatus.ForeColor = SaveGreen;
            };
            displayBody.Controls.Add(btnSaveDisplayFit);
            Label displayHelp = new Label
            {
                Text = "Use global UI scale for all pages. 90% fits more cards on small screens; 110% or 125% improves readability on large displays.",
                Location = new Point(190, 78),
                Size = new Size(340, 54),
                Font = new Font("Segoe UI", 9f),
                ForeColor = DS.Slate600
            };
            displayHelp.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            displayBody.Controls.Add(displayHelp);
            Label displayFitHelp = new Label
            {
                Text = "Display fit controls responsive layout rules; UI scale controls font, spacing, card, and control sizing.",
                Location = new Point(0, 148),
                Size = new Size(530, 50),
                Font = new Font("Segoe UI", 9f),
                ForeColor = DS.Slate600
            };
            displayFitHelp.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            displayBody.Controls.Add(displayFitHelp);
            Label currentScreen = new Label
            {
                Text = BuildDisplayFitScreenSummary(),
                Location = new Point(0, 210),
                Size = new Size(530, 50),
                Font = new Font("Segoe UI", 8.75f, FontStyle.Bold),
                ForeColor = SaveGreen
            };
            currentScreen.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            displayBody.Controls.Add(currentScreen);

            AppRuntime.LogTiming("Settings.BuildForm.Display.Complete", 0);

            if (includeDeferredCards)
            {
                BuildLocalAiCard(parent);
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
            PlaceLabeledControl(defaultsBody, "Invoice Prefix", _txtPrefix, 0, 0, 150);
            _numGSTRate = MakeDecimalBox(Point.Empty, 0, 0m, 28m, 18m, 2);
            PlaceLabeledControl(defaultsBody, "Default GST %", _numGSTRate, 170, 0, 150);
            _numPayTerms = MakeDecimalBox(Point.Empty, 0, 0m, 365m, 30m, 0);
            PlaceLabeledControl(defaultsBody, "Payment Terms (days)", _numPayTerms, 340, 0, 150);
            _numMarkupPct = MakeDecimalBox(Point.Empty, 0, 0m, 200m, 25m, 2);
            PlaceLabeledControl(defaultsBody, "Default Markup %", _numMarkupPct, 0, 72, 150);
            _numAnnualTurnover = MakeDecimalBox(Point.Empty, 0, 0m, 9999999999m, 0m, 2, 1000m);
            _numAnnualTurnover.ValueChanged += (s, e) => RefreshIndiaDefaultsPreview();
            PlaceLabeledControl(defaultsBody, "Annual Turnover", _numAnnualTurnover, 170, 72, 150);
            _numEInvoiceThreshold = MakeDecimalBox(Point.Empty, 0, 0m, 9999999999m, 50000000m, 2, 1000m);
            _numEInvoiceThreshold.ValueChanged += (s, e) => RefreshIndiaDefaultsPreview();
            PlaceLabeledControl(defaultsBody, "E-Invoice Threshold", _numEInvoiceThreshold, 340, 72, 150);
            _txtCurrency = new TextBox { ReadOnly = false, Text = "INR (\u20B9)" };
            PlaceLabeledControl(defaultsBody, "Currency", _txtCurrency, 0, 144, 150);
            _txtFinancialYear = new TextBox { ReadOnly = false };
            PlaceLabeledControl(defaultsBody, "Financial Year", _txtFinancialYear, 170, 144, 320);
            _chkEInvoiceEligible = new CheckBox
            {
                Location = new Point(0, 222),
                Width = 180,
                Text = "E-Invoice eligible",
                Enabled = false,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = DS.Slate700,
                BackColor = Color.White
            };
            defaultsBody.Controls.Add(_chkEInvoiceEligible);
            _toolTip.SetToolTip(_chkEInvoiceEligible, "Calculated automatically from annual turnover and configured e-invoice threshold.");
            _lblMoneyPreview = new Label
            {
                Location = new Point(190, 220),
                Width = 330,
                Height = 42,
                Font = new Font("Segoe UI", 9),
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

            _txtHsnSearch = new TextBox { Width = 220, Font = new Font("Segoe UI", 9f) };
            _txtHsnSearch.TextChanged += (s, e) => ApplyHsnSacFilter();
            _txtHsnSearch.Location = new Point(0, 272);
            hsnBody.Controls.Add(_txtHsnSearch);

            Button btnAddRow = MakeBtn("Add HSN/SAC Row", InfoBlue, 150);
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
            systemBody.Controls.Add(new Label
            {
                Text = "Database Health",
                Location = new Point(0, 0),
                Size = new Size(180, 20),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = DS.Slate700
            });
            _lblDbStatus = new Label { Location = new Point(0, 24), Size = new Size(520, 44), Font = new Font("Segoe UI", 9), ForeColor = DS.Slate700 };
            systemBody.Controls.Add(_lblDbStatus);
            Button btnTest = MakeBtn("Test Connection", InfoBlue, 140);
            btnTest.Location = new Point(0, 80);
            btnTest.Click += (s, e) => CheckDbConnection();
            systemBody.Controls.Add(btnTest);
            Button btnSetup = MakeBtn("Connection Setup", SaveGreen, 156);
            btnSetup.Location = new Point(154, 80);
            btnSetup.Click += (s, e) => OpenConnectionSetup();
            systemBody.Controls.Add(btnSetup);
            Button btnRepair = MakeBtn("Repair Database", Color.White, 150);
            btnRepair.ForeColor = DS.Primary600;
            btnRepair.FlatAppearance.BorderSize = 1;
            btnRepair.FlatAppearance.BorderColor = DS.BorderStrong;
            btnRepair.Location = new Point(324, 80);
            btnRepair.Click += async (s, e) => await RepairDatabaseSchemaAsync(btnRepair);
            systemBody.Controls.Add(btnRepair);
            Label layoutTitle = new Label
            {
                Text = "Layout Recovery",
                Location = new Point(0, 132),
                Size = new Size(180, 20),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = DS.Slate700
            };
            systemBody.Controls.Add(layoutTitle);
            Label layoutHelp = new Label
            {
                Text = "Reset saved card layouts for specific workspaces when dashboards look broken, cramped, or out of place.",
                Location = new Point(0, 154),
                Size = new Size(530, 34),
                Font = new Font("Segoe UI", 8.7f),
                ForeColor = DS.Slate500
            };
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

            BuildAgentSimulationCard(parent);
            BuildDevTeamDashboardCard(parent);
            BuildLocalAiCard(parent);
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

            var label = new Label
            {
                Text = "Error log location:",
                Location = new Point(0, 0),
                Size = new Size(160, 20),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = DS.Slate500
            };
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

            Button btnOpenFolder = MakeBtn("Open Log Folder", InfoBlue, 150);
            btnOpenFolder.Location = new Point(0, 82);
            btnOpenFolder.Click += (s, e) => OpenErrorLogFolder(logsFolder);
            parent.Controls.Add(btnOpenFolder);

            Button btnViewCurrent = MakeBtn("View Current Log", SaveGreen, 150);
            btnViewCurrent.Location = new Point(166, 82);
            btnViewCurrent.Click += (s, e) => ViewCurrentErrorLog();
            parent.Controls.Add(btnViewCurrent);

            Button btnClearOld = MakeBtn("Clear Old Logs", Color.White, 140);
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
                        MessageBox.Show("No errors logged this month.", BrandingService.WindowTitle("Diagnostics"), MessageBoxButtons.OK, MessageBoxIcon.Information));
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
                    MessageBox.Show(deleted + " old log file(s) deleted.", BrandingService.WindowTitle("Clear Old Logs"), MessageBoxButtons.OK, MessageBoxIcon.Information));
            }
            catch (Exception ex)
            {
                ShowError( "Could not clear old error logs.", ex);
            }
        }

        private void BuildGeneralSettingsGuide(Panel parent)
        {
            Panel body = AddModernSettingsCard(parent, "Settings Control Center", "Keep release health, protection, and daily admin actions visible before diving into each settings card.", 318);

            Label intro = new Label
            {
                Text = "This workspace is strongest when it answers three questions quickly: is the build healthy, is the client protected, and what should the admin do next?",
                Location = new Point(0, 0),
                Size = new Size(520, 38),
                Font = new Font("Segoe UI", 9f),
                ForeColor = DS.Slate600
            };
            body.Controls.Add(intro);

            FlowLayoutPanel summaryFlow = new FlowLayoutPanel
            {
                Location = new Point(0, 52),
                Size = new Size(530, 88),
                BackColor = Color.White,
                WrapContents = true
            };
            _lblSettingsVersionState = AddSummaryCard(summaryFlow, "Release", ConfigService.GetAppVersion(), DS.Primary600);
            _lblSettingsDbState = AddSummaryCard(summaryFlow, "Database", "Checking", SaveGreen);
            _lblSettingsBackupState = AddSummaryCard(summaryFlow, "Backup", "Pending", DS.Amber600);
            _lblSettingsLicenseState = AddSummaryCard(summaryFlow, "License", "Review", DS.Red600);
            _lblSettingsAssistantState = AddSummaryCard(summaryFlow, "Assistant", "Disabled", DS.Teal600);
            body.Controls.Add(summaryFlow);

            Label playbook = new Label
            {
                Text = "Suggested order: company profile, India defaults, display fit, backups, then risk controls and updates.",
                Location = new Point(0, 150),
                Size = new Size(530, 18),
                Font = new Font("Segoe UI", 8.7f, FontStyle.Bold),
                ForeColor = DS.Slate500
            };
            body.Controls.Add(playbook);

            FlowLayoutPanel actionFlow = new FlowLayoutPanel
            {
                Location = new Point(0, 182),
                Size = new Size(530, 80),
                BackColor = Color.White,
                WrapContents = true
            };
            Button btnOpenCompany = MakeBtn("Review Company", Color.White, 146);
            btnOpenCompany.ForeColor = DS.Primary600;
            btnOpenCompany.FlatAppearance.BorderColor = DS.Border;
            btnOpenCompany.FlatAppearance.BorderSize = 1;
            btnOpenCompany.Click += (s, e) => _txtCompanyName.Focus();
            Button btnCheckDatabase = MakeBtn("Check Database", InfoBlue, 142);
            btnCheckDatabase.Click += (s, e) => CheckDbConnection();
            Button btnBackupNow = MakeBtn("Backup Now", SaveGreen, 122);
            btnBackupNow.Click += async (s, e) => await CreateBackupAsync();
            Button btnOpenUsers = MakeBtn("Users & Logins", Color.White, 138);
            btnOpenUsers.ForeColor = DS.Slate700;
            btnOpenUsers.FlatAppearance.BorderColor = DS.Border;
            btnOpenUsers.FlatAppearance.BorderSize = 1;
            btnOpenUsers.Click += (s, e) =>
            {
                if (_usersTab != null)
                    _tabs.SelectedTab = _usersTab;
            };
            Button btnCheckUpdates = MakeBtn("Check Updates", Color.White, 132);
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
            Label titleLabel = new Label { Text = title, Dock = DockStyle.Top, Height = 20, Font = DS.SmallBold, ForeColor = accent };
            Label bodyLabel = new Label { Text = text, Dock = DockStyle.Fill, Font = DS.Caption, ForeColor = DS.Slate600 };
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

            Label title = new Label
            {
                Text = "Need help with ServoERP?",
                Location = new Point(64, 4),
                Size = new Size(420, 28),
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = DS.Slate900
            };
            body.Controls.Add(title);

            Label text = new Label
            {
                Text = "Use this for guided help, database checks, diagnostics export, update verification, and support handover information.",
                Location = new Point(64, 38),
                Size = new Size(430, 56),
                Font = new Font("Segoe UI", 9f),
                ForeColor = DS.Slate600
            };
            body.Controls.Add(text);

            Button open = MakeBtn("Open Help & Support", InfoBlue, 178);
            open.Name = "btnSettingsHelpSupport";
            open.UseMnemonic = false;
            ModernIconSystem.AddButtonIcon(open, ModernIconKind.Service);
            open.Location = new Point(64, 112);
            open.Click += (s, e) => OpenHelpSupportFromSettings();
            body.Controls.Add(open);

            Button logs = MakeBtn("View Error Logs", Color.White, 150);
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

        private void BuildAgentSimulationCard(Panel parent)
        {
            Panel body = AddModernSettingsCard(parent, "Agent Simulation", "Run isolated [AGENT] QA data, PDFs, report, pause/resume, and cleanup.", 260);
            Label summary = new Label
            {
                Text = "Tracks exact IDs in AgentState.json. Real records are not touched.",
                Location = new Point(0, 2),
                Size = new Size(520, 54),
                Font = new Font("Segoe UI", 9f),
                ForeColor = DS.Slate600
            };
            body.Controls.Add(summary);

            Button run = MakeBtn("Run Agent Simulation", InfoBlue, 184);
            run.Location = new Point(0, 76);
            run.Name = "btnRunAgentSimulation";
            ModernIconSystem.AddButtonIcon(run, ModernIconKind.Service);
            run.Click += (s, e) => OpenAgentSimulationPanel(true);
            body.Controls.Add(run);

            Button openReport = MakeBtn("Open Latest Report", Color.White, 160);
            openReport.ForeColor = DS.Primary600;
            openReport.FlatAppearance.BorderColor = DS.Border;
            openReport.FlatAppearance.BorderSize = 1;
            openReport.Location = new Point(202, 76);
            openReport.Click += (s, e) => OpenLatestAgentReport();
            body.Controls.Add(openReport);
        }

        private void BuildDevTeamDashboardCard(Panel parent)
        {
            Panel body = AddModernSettingsCard(parent, "ServoERP Brain - Dev Team", "Local AI dev team (Ollama-powered) that audits and improves this app.", 200);
            Label summary = new Label
            {
                Text = "Runs fully offline via the local Ollama models. Submit a task, watch the 8 agents work, and review the final report.",
                Location = new Point(0, 2),
                Size = new Size(520, 54),
                Font = new Font("Segoe UI", 9f),
                ForeColor = DS.Slate600
            };
            body.Controls.Add(summary);

            Button open = MakeBtn("Open Dev Team Dashboard", InfoBlue, 200);
            open.Location = new Point(0, 76);
            open.Name = "btnOpenDevTeamDashboard";
            open.Click += (s, e) => OpenDevTeamDashboard();
            body.Controls.Add(open);
        }

        private void OpenDevTeamDashboard()
        {
            MainForm main = FindForm() as MainForm;
            if (main != null)
            {
                main.ShowDevTeamDashboard();
                return;
            }

            using (var dashboard = new DevTeamDashboardForm())
                dashboard.ShowDialog(FindForm());
        }

        private void OpenAgentSimulationPanel(bool start)
        {
            if (start)
                AgentSimulationService.Instance.StartOrResume();

            using (var panel = new AgentSimulationPanel())
                panel.ShowDialog(FindForm());
        }

        private void OpenLatestAgentReport()
        {
            try
            {
                string path = AgentSimulationService.Instance.BuildLatestReport();
                if (File.Exists(path))
                    System.Diagnostics.Process.Start("notepad.exe", path);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SettingsForm.OpenLatestAgentReport", ex);
                MessageBox.Show("Unable to open agent report:\r\n" + ex.Message, "Agent Simulation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BuildLoginAccessSection(Panel parent)
        {
            Panel cardBody = AddModernSettingsCard(parent, "User Logins", "Create and manage staff logins from Settings.", 300);

            cardBody.Controls.Add(new Label
            {
                Text = "Add new usernames, reset passwords, assign roles, and deactivate access.",
                Location = new Point(0, 0),
                Width = 420,
                Height = 58,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = DS.Slate900
            });
            cardBody.Controls.Add(new Label
            {
                Text = "Use this for admin-controlled staff access without leaving Settings.",
                Location = new Point(0, 70),
                Width = 420,
                Height = 44,
                Font = new Font("Segoe UI", 9),
                ForeColor = DS.Slate500
            });

            Button btnCreateLogin = MakeBtn("Create Login", SaveGreen, 120);
            btnCreateLogin.Location = new Point(0, 142);
            btnCreateLogin.Click += (s, e) =>
            {
                OpenUserManagementTab();
                AddUser();
            };

            Button btnManageLogins = MakeBtn("Open Logins", InfoBlue, 120);
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
            PlaceLabeledControl(updatesBody, "GitHub Releases Repository", _txtVersionCheckUrl, 0, 0, 444);
            Button btnCopy = MakeBtn("Copy", Color.White, 72);
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
            _chkVersionCheckEnabled = new CheckBox
            {
                Text = "Check for updates automatically",
                Location = new Point(0, 78),
                Width = 300,
                Height = 26,
                Font = new Font("Segoe UI", 9f),
                ForeColor = DS.Slate700,
                BackColor = Color.White
            };
            updatesBody.Controls.Add(_chkVersionCheckEnabled);

            _chkSilentAutoUpdateEnabled = new CheckBox
            {
                Text = "Download updates automatically",
                Location = new Point(0, 106),
                Width = 300,
                Height = 26,
                Font = new Font("Segoe UI", 9f),
                ForeColor = DS.Slate700,
                BackColor = Color.White
            };
            updatesBody.Controls.Add(_chkSilentAutoUpdateEnabled);

            _lblInstalledVersion = new Label
            {
                Text = "Current version: " + ConfigService.GetAppVersion(),
                Location = new Point(0, 150),
                Width = 320,
                Height = 22,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = SaveGreen
            };
            updatesBody.Controls.Add(_lblInstalledVersion);

            _lblLastUpdateCheckStatus = new Label
            {
                Text = UpdateService.GetLastUpdateStatusDisplay(),
                Location = new Point(0, 182),
                Width = 520,
                Height = 48,
                Font = new Font("Segoe UI", 8.8f),
                ForeColor = DS.Slate600
            };
            updatesBody.Controls.Add(_lblLastUpdateCheckStatus);

            Button btnCheckNow = MakeBtn("Check for Updates", InfoBlue, 170);
            btnCheckNow.Location = new Point(0, 246);
            btnCheckNow.Click += async (s, e) => await CheckVersionNowAsync();
            updatesBody.Controls.Add(btnCheckNow);
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

            PlaceLabeledControl(parent, "Unit Code *", _txtUnitCode, 0, 0, 110);
            PlaceLabeledControl(parent, "Short Code", _txtUnitShortCode, 126, 0, 110);
            PlaceLabeledControl(parent, "Display Name *", _txtUnitDisplayName, 252, 0, 268);
            PlaceLabeledControl(parent, "Category", _cmbUnitCategory, 0, 74, 250);
            PlaceLabeledControl(parent, "Measurement System", _cmbUnitMeasurementSystem, 266, 74, 254);
            PlaceLabeledControl(parent, "Aliases", _txtUnitAliases, 0, 148, 520, 58);

            Label aliasHelp = new Label
            {
                Text = "Enter aliases separated by commas, for example: meter, metres, mtr",
                Location = new Point(0, 202),
                Size = new Size(520, 18),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = DS.Slate500
            };
            parent.Controls.Add(aliasHelp);

            Button btnAddUnit = MakeBtn("Add Unit", SaveGreen, 120);
            btnAddUnit.Location = new Point(0, 234);
            btnAddUnit.Click += (s, e) => SaveUnitFromSettings();
            parent.Controls.Add(btnAddUnit);

            Button btnRefreshUnits = MakeBtn("Refresh Units", InfoBlue, 126);
            btnRefreshUnits.Location = new Point(136, 234);
            btnRefreshUnits.Click += (s, e) => RefreshUnitManagementCard();
            parent.Controls.Add(btnRefreshUnits);

            _lblUnitSummary = new Label
            {
                Location = new Point(278, 238),
                Size = new Size(242, 20),
                Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
                ForeColor = DS.Slate600,
                TextAlign = ContentAlignment.MiddleRight
            };
            parent.Controls.Add(_lblUnitSummary);

            Label gridLabel = new Label
            {
                Text = "Available Units",
                Location = new Point(0, 274),
                Size = new Size(220, 20),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = DS.Slate700
            };
            parent.Controls.Add(gridLabel);

            Panel gridHost = new Panel
            {
                Location = new Point(0, 302),
                Size = new Size(520, 156),
                BackColor = Color.White,
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

        private void BuildLocalAiCard(Panel parent)
        {
            Panel aiBody = AddModernSettingsCard(parent, "ServoERP Assistant", "Built-in ERP helper. No server, model setup, or API key is required.", 410);

            _chkAiEnabled = new CheckBox
            {
                Text = "Enable ServoERP Assistant",
                Location = new Point(0, 0),
                Size = new Size(240, 26),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = DS.Slate700,
                BackColor = Color.White
            };
            aiBody.Controls.Add(_chkAiEnabled);

            _cmbAiProvider = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };
            _cmbAiProvider.Items.AddRange(new object[] { "Built-in" });
            PlaceLabeledControl(aiBody, "Provider", _cmbAiProvider, 0, 52, 190);
            _cmbAiProvider.Enabled = true;

            _txtAiEndpoint = new TextBox { Visible = false };
            _txtAiModel = new TextBox { Visible = false };
            _numAiMaxTokens = MakeDecimalBox(Point.Empty, 0, 64m, 4096m, 700m, 0, 50m);
            _numAiTemperature = MakeDecimalBox(Point.Empty, 0, 0m, 2m, 0.2m, 2, 0.05m);
            _numAiMaxTokens.Visible = false;
            _numAiTemperature.Visible = false;
            aiBody.Controls.Add(_txtAiEndpoint);
            aiBody.Controls.Add(_txtAiModel);
            aiBody.Controls.Add(_numAiMaxTokens);
            aiBody.Controls.Add(_numAiTemperature);

            Label help = new Label
            {
                Text = "No API keys, endpoints, or model setup are needed. The assistant uses built-in ServoERP rules, module context, and preview-only actions.",
                Location = new Point(0, 124),
                Size = new Size(528, 48),
                Font = DS.Small,
                ForeColor = DS.Slate600
            };
            aiBody.Controls.Add(help);

            Button openCopilot = MakeBtn("Open AI Copilot", InfoBlue, 150);
            openCopilot.Location = new Point(0, 190);
            openCopilot.Click += (s, e) =>
            {
                MainForm shell = FindForm() as MainForm;
                if (shell != null)
                    shell.ShowAiCopilot();
            };
            aiBody.Controls.Add(openCopilot);

            Button test = MakeBtn("Check Assistant", InfoBlue, 138);
            test.Location = new Point(164, 190);
            test.Click += async (s, e) => await TestLocalAiAsync();
            aiBody.Controls.Add(test);
        }

        private void BuildLegalAgreementsCard(Panel parent)
        {
            Panel legalBody = AddModernSettingsCard(parent, "Legal Agreements", "View EULA, Privacy Policy, Data Processing Policy, and Disclaimer.", 220);
            Label help = new Label
            {
                Text = "Review the legal agreements accepted during first launch. This viewer is read-only and does not change acceptance status.",
                Location = new Point(0, 0),
                Size = new Size(528, 48),
                Font = DS.Small,
                ForeColor = DS.Slate600
            };
            legalBody.Controls.Add(help);

            Button viewLegal = MakeBtn("View Legal Agreements", InfoBlue, 190);
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
            Label help = new Label
            {
                Text = "Enterprise clients often ask which open-source components are bundled. Use this disclosure for procurement, compliance, and IT review.",
                Location = new Point(0, 0),
                Size = new Size(528, 58),
                Font = DS.Small,
                ForeColor = DS.Slate600
            };
            body.Controls.Add(help);

            Button view = MakeBtn("View Open Source", InfoBlue, 170);
            view.Location = new Point(0, 82);
            view.Click += (s, e) =>
            {
                using (var form = new OpenSourceLicenseForm())
                    form.ShowDialog(FindForm());
            };
            body.Controls.Add(view);

            Button export = MakeBtn("Export Disclosure", SaveGreen, 170);
            export.Location = new Point(186, 82);
            export.Click += (s, e) =>
            {
                try
                {
            string path = _openSourceLicenseSvc.ExportDisclosureReport();
                    _lblStatus.Text = "Open-source disclosure exported: " + path;
                    _lblStatus.ForeColor = SaveGreen;
                    System.Diagnostics.Process.Start("notepad.exe", path);
                }
                catch (Exception ex)
                {
                    _lblStatus.Text = "Disclosure export failed: " + ex.Message;
                    _lblStatus.ForeColor = Color.Red;
                }
            };
            body.Controls.Add(export);
        }

        private void BuildModuleCatalogCard(Panel parent)
        {
            Panel body = AddModernSettingsCard(parent, "Module Catalog", "Review installed modules and extension-ready roadmap ideas.", 240);
            Label help = new Label
            {
                Text = "Use this catalog as the client-facing module map: what is installed today, what pattern inspired it, and what can be extended next.",
                Location = new Point(0, 0),
                Size = new Size(528, 58),
                Font = DS.Small,
                ForeColor = DS.Slate600
            };
            body.Controls.Add(help);

            Button view = MakeBtn("View Catalog", InfoBlue, 150);
            view.Location = new Point(0, 82);
            view.Click += (s, e) =>
            {
                using (var form = new ModuleCatalogForm())
                    form.ShowDialog(FindForm());
            };
            body.Controls.Add(view);

            Button export = MakeBtn("Export Catalog", SaveGreen, 150);
            export.Location = new Point(166, 82);
            export.Click += (s, e) =>
            {
                try
                {
            string path = _moduleCatalogSvc.ExportReport();
                    _lblStatus.Text = "Module catalog exported: " + path;
                    _lblStatus.ForeColor = SaveGreen;
                    System.Diagnostics.Process.Start("notepad.exe", path);
                }
                catch (Exception ex)
                {
                    _lblStatus.Text = "Catalog export failed: " + ex.Message;
                    _lblStatus.ForeColor = Color.Red;
                }
            };
            body.Controls.Add(export);
        }

        private void BuildCompliancePackCard(Panel parent)
        {
            Panel body = AddModernSettingsCard(parent, "Compliance Export Pack", "Generate a local legal, license, module, and readiness ZIP.", 240);
            Label help = new Label
            {
                Text = "Create a local handover pack for client IT, procurement, and audit review. No passwords, license keys, or database records are uploaded.",
                Location = new Point(0, 0),
                Size = new Size(528, 58),
                Font = DS.Small,
                ForeColor = DS.Slate600
            };
            body.Controls.Add(help);

            Button view = MakeBtn("Open Exporter", InfoBlue, 150);
            view.Location = new Point(0, 82);
            view.Click += (s, e) =>
            {
                using (var form = new CompliancePackForm())
                    form.ShowDialog(FindForm());
            };
            body.Controls.Add(view);

            Button export = MakeBtn("Generate Pack", SaveGreen, 150);
            export.Location = new Point(166, 82);
            export.Click += (s, e) =>
            {
                try
                {
            string path = _compliancePackSvc.ExportPack();
                    _lblStatus.Text = "Compliance pack created: " + path;
                    _lblStatus.ForeColor = SaveGreen;
                    System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\"");
                }
                catch (Exception ex)
                {
                    _lblStatus.Text = "Compliance pack failed: " + ex.Message;
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
                LoadAiSettings();
                AppRuntime.LogTiming("Settings.LoadSettings.RuntimeLoaded", watch.ElapsedMilliseconds);

                RefreshIndiaDefaultsPreview();
                BeginLoadHsnSacGrid();
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("SettingsForm.LoadSettings", ex);
                _lblStatus.Text = "Load error: " + ex.Message;
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
                if (_gridUnits.Columns["Code"] != null) _gridUnits.Columns["Code"].HeaderText = "Code";
                if (_gridUnits.Columns["Short"] != null) _gridUnits.Columns["Short"].HeaderText = "Short";
                if (_gridUnits.Columns["Name"] != null) _gridUnits.Columns["Name"].HeaderText = "Display Name";
                if (_gridUnits.Columns["Category"] != null) _gridUnits.Columns["Category"].HeaderText = "Category";
                if (_gridUnits.Columns["System"] != null) _gridUnits.Columns["System"].HeaderText = "System";
            }

            if (_lblUnitSummary != null)
                _lblUnitSummary.Text = units.Count.ToString("N0") + " global units";

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
                MessageBox.Show(message, "Add Unit", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _lblStatus.Text = "Unit save failed: " + message;
                _lblStatus.ForeColor = Color.Red;
                return;
            }

            _lblStatus.Text = "Unit added to the global list: " + ((_txtUnitShortCode?.Text ?? string.Empty).Trim().Length > 0 ? _txtUnitShortCode.Text.Trim() : _txtUnitCode.Text.Trim());
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
            _lblUserTotal = AddSummaryCard(summary, "Total Users", "0", InfoBlue);
            _lblUserActive = AddSummaryCard(summary, "Active Users", "0", SaveGreen);
            _lblUserAdmins = AddSummaryCard(summary, "Admin Users", "0", DS.Primary700);
            _lblUserLastLogin = AddSummaryCard(summary, "Last Login", "-", DS.Slate700);

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = DS.BgPage, Padding = new Padding(0, 12, 0, 12) };
            FlowLayoutPanel toolbarInner = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = DS.BgPage, WrapContents = true };
            Button btnAdd = MakeBtn("Add User", SaveGreen, 110);
            Button btnEdit = MakeBtn("Edit User", InfoBlue, 110);
            Button btnReset = MakeBtn("Reset Password", Color.FromArgb(211, 84, 0), 128);
            Button btnDeactivate = MakeBtn("Deactivate", Color.FromArgb(220, 38, 38), 110);
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
            _lblAuditTotal = AddSummaryCard(summary, "Total Events", "0", InfoBlue);
            _lblAuditLogin = AddSummaryCard(summary, "Login Events", "0", SaveGreen);
            _lblAuditWarnings = AddSummaryCard(summary, "Failed / Warning", "0", DS.Red600);

            Panel toolbar = BuildPlainCard();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 70;
            toolbar.Padding = new Padding(14, 12, 14, 12);
            FlowLayoutPanel toolbarInner = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, BackColor = Color.White };
            toolbarInner.Controls.Add(FilterLabel("From"));
            _dtAuditFrom = new DateTimePicker { Width = 130, Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 9), Margin = new Padding(0, 0, 18, 8) };
            toolbarInner.Controls.Add(_dtAuditFrom);
            toolbarInner.Controls.Add(FilterLabel("To"));
            _dtAuditTo = new DateTimePicker { Width = 130, Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 9), Margin = new Padding(0, 0, 18, 8) };
            toolbarInner.Controls.Add(_dtAuditTo);
            toolbarInner.Controls.Add(FilterLabel("User"));
            _cmbAuditUser = new ComboBox { Width = 180, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9), Margin = new Padding(0, 0, 18, 8) };
            toolbarInner.Controls.Add(_cmbAuditUser);
            Button btnRefresh = MakeBtn("Load Audit", InfoBlue, 104);
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
            _auditGridCard.Controls.Add(new Label
            {
                Text = "Click Load Audit to view the latest 100 audit events.",
                Dock = DockStyle.Top,
                Height = 28,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = DS.Slate600
            });

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
            return SessionManager.CurrentUser != null;
        }

        private void SettingsTabs_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_tabs == null || _tabs.SelectedTab == null)
                return;

            LazyTabPage lazyTab = _tabs.SelectedTab as LazyTabPage;
            if (lazyTab != null)
                lazyTab.EnsureBuilt();

            if (string.Equals(_tabs.SelectedTab.Text, "Users & Logins", StringComparison.OrdinalIgnoreCase))
            {
                EnsureUsersTabBuilt();
                BeginRefreshSecurityTabs();
            }
            else if (string.Equals(_tabs.SelectedTab.Text, "Audit Log", StringComparison.OrdinalIgnoreCase))
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
                if (string.Equals(tab.Text, "Users & Logins", StringComparison.OrdinalIgnoreCase))
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
                    _lblStatus.Text = "User login summary could not be loaded: " + ex.Message;
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
                _gridUsers.Columns["DisplayName"].HeaderText = "Display Name";
            if (_gridUsers.Columns["LastLogin"] != null)
                _gridUsers.Columns["LastLogin"].HeaderText = "Last Login";

            if (_lblUserTotal != null)
                _lblUserTotal.Text = users.Count.ToString();
            if (_lblUserActive != null)
                _lblUserActive.Text = users.Count(u => u.IsActive).ToString();
            if (_lblUserAdmins != null)
                _lblUserAdmins.Text = users.Count(u => string.Equals(u.RoleName, "Admin", StringComparison.OrdinalIgnoreCase)).ToString();
            if (_lblUserLastLogin != null)
            {
                DateTime? latest = users.Where(u => u.LastLoginDate.HasValue).Select(u => u.LastLoginDate.Value).DefaultIfEmpty(DateTime.MinValue).Max();
                _lblUserLastLogin.Text = latest.HasValue && latest.Value != DateTime.MinValue ? latest.Value.ToString("dd/MM HH:mm") : "-";
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
                _gridAudit.Columns["LogDate"].HeaderText = "Log Date";
            if (_gridAudit.Columns["ModuleKey"] != null)
                _gridAudit.Columns["ModuleKey"].HeaderText = "Module";
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
                _lblAuditTotal.Text = table.Rows.Count.ToString();
            if (_lblAuditLogin != null)
            {
                int loginCount = 0;
                foreach (System.Data.DataRow row in table.Rows)
                {
                    if (Convert.ToString(row["Action"]).IndexOf("LOGIN", StringComparison.OrdinalIgnoreCase) >= 0)
                        loginCount++;
                }
                _lblAuditLogin.Text = loginCount.ToString();
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
                _lblAuditWarnings.Text = warningCount.ToString();
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
            if (!ShowUserEditor(null, out string username, out string displayName, out int roleId, out bool isActive))
                return;

            var result = _authSvc.CreateUser(username, displayName, roleId, isActive);
            if (!result.Success)
            {
                MessageBox.Show(result.ErrorMessage, "Add User", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            RefreshUsers();
            MessageBox.Show("Temp password: " + result.TempPassword + "\r\n\r\nShare this with the user.", "User Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void EditSelectedUser()
        {
            ManagedUserDto user = GetSelectedUser();
            if (user == null)
                return;

            if (!ShowUserEditor(user, out string username, out string displayName, out int roleId, out bool isActive))
                return;

            if (!_authSvc.UpdateUser(user.UserId, username, displayName, roleId, isActive))
            {
                MessageBox.Show("Unable to update user.", "Edit User", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                MessageBox.Show(result.ErrorMessage, "Reset Password", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            MessageBox.Show("Temp password: " + result.TempPassword + "\r\n\r\nShare this with the user.", "Password Reset", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                MessageBox.Show("Unable to change active state. You cannot deactivate your own account.", "Deactivate User", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            RefreshUsers();
        }

        private bool ShowUserEditor(ManagedUserDto user, out string username, out string displayName, out int roleId, out bool isActive)
        {
            username = null;
            displayName = null;
            roleId = 0;
            isActive = true;
            string tempUsername = null;
            string tempDisplayName = null;
            int tempRoleId = 0;
            bool tempIsActive = true;

            using (Form dialog = ServoModalForm.Create(user == null ? "Add User" : "Edit User", 360, 230))
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
                CheckBox chkActive = new CheckBox { Location = new Point(24, 170), Text = "User is active", Checked = user == null || user.IsActive };

                Button btnOk = MakeBtn("Save", SaveGreen, 90);
                btnOk.Location = new Point(234, 188);
                btnOk.Click += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(txtUsername.Text) || string.IsNullOrWhiteSpace(txtDisplayName.Text) || cmbRole.SelectedValue == null)
                    {
                        MessageBox.Show("Username, display name, and role are required.", dialog.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    tempUsername = txtUsername.Text.Trim();
                    tempDisplayName = txtDisplayName.Text.Trim();
                    tempRoleId = Convert.ToInt32(cmbRole.SelectedValue);
                    tempIsActive = chkActive.Checked;
                    dialog.DialogResult = DialogResult.OK;
                    dialog.Close();
                };

                dialog.Controls.AddRange(new Control[]
                {
                    new Label { Text = "Username", Location = new Point(24, 14), AutoSize = true },
                    txtUsername,
                    new Label { Text = "Display Name", Location = new Point(24, 64), AutoSize = true },
                    txtDisplayName,
                    new Label { Text = "Role", Location = new Point(24, 114), AutoSize = true },
                    cmbRole,
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
                _svc.Set("DefaultMarkupPct", _numMarkupPct.Value.ToString("0.##"));
                _hsnSacSvc.SaveAll(CollectHsnSacRows());
                SaveDisplayFitSetting();
                SaveUiScaleSetting();
                SaveAiSettings();
                if (_txtVersionCheckUrl != null)
                {
                    ConfigService.Set("App", "GitHubRepositoryUrl", ConfigService.ProductionVersionCheckUrl);
                    ConfigService.Set("App", "VersionCheckUrl", ConfigService.ProductionVersionCheckUrl);
                    _txtVersionCheckUrl.Text = UpdateService.GetGitHubRepositoryUrl();
                    ConfigService.Set("App", "VersionCheckEnabled", _chkVersionCheckEnabled != null && _chkVersionCheckEnabled.Checked ? "true" : "false");
                    ConfigService.Set("App", "SilentAutoUpdateEnabled", _chkSilentAutoUpdateEnabled != null && _chkSilentAutoUpdateEnabled.Checked ? "true" : "false");
                    ConfigService.Set("App", "SilentAutoUpdateApplyImmediately", "false");
                    ConfigService.Set("App", "SilentAutoUpdateApplyOnExit", "false");
                    ConfigService.Set("App", "VersionCheckIntervalHours", ConfigService.GetVersionCheckIntervalHours().ToString());
                }
                RefreshIndiaDefaultsPreview();

                _lblStatus.Text = "India settings and HSN/SAC master saved.";
                _lblStatus.ForeColor = SaveGreen;
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "Error: " + ex.Message;
                _lblStatus.ForeColor = Color.Red;
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

        private void LoadAiSettings()
        {
            if (_chkAiEnabled == null)
                return;

            AiProviderConfig config = AiProviderConfig.Load();
            _chkAiEnabled.Checked = config.Enabled;
            SelectCombo(_cmbAiProvider, config.Provider, "Built-in");
            _txtAiEndpoint.Text = config.EndpointUrl;
            _txtAiModel.Text = config.ModelName;
            _numAiMaxTokens.Value = Clamp(_numAiMaxTokens, config.MaxTokens);
            _numAiTemperature.Value = Clamp(_numAiTemperature, config.Temperature);
            RefreshSettingsWorkspaceSummary();
        }

        private void SaveAiSettings()
        {
            if (_chkAiEnabled == null)
                return;

            var config = new AiProviderConfig
            {
                Enabled = _chkAiEnabled.Checked,
                Provider = "Built-in",
                EndpointUrl = "",
                ModelName = "ServoERP Bot",
                MaxTokens = (int)_numAiMaxTokens.Value,
                Temperature = _numAiTemperature.Value
            };
            config.Save();
        }

        private async Task TestLocalAiAsync()
        {
            try
            {
                SaveAiSettings();
                _lblStatus.Text = "Checking assistant...";
                _lblStatus.ForeColor = InfoBlue;
            bool ok = await _aiAssistantSvc.IsLocalAiReachableAsync(CancellationToken.None);
                _lblStatus.Text = ok
                    ? "ServoERP Assistant is ready."
                    : "ServoERP Assistant is disabled.";
                _lblStatus.ForeColor = ok ? SaveGreen : DS.Amber600;
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "Assistant check failed: " + ex.Message;
                _lblStatus.ForeColor = Color.Red;
            }
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
            return "Detected working area: " + workArea.Width + " x " + workArea.Height + " px. Recommended for IdeaPad laptops: IdeaPad / compact laptop.";
        }

        /// <summary>Checks SQL Server health on a worker thread so opening Settings never freezes the shell.</summary>
        private async void BeginCheckDbConnection()
        {
            if (_lblDbStatus == null || _lblDbStatus.IsDisposed)
                return;

            _lblDbStatus.Text = "Database: checking office SQL Server...";
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
                        _lblDbStatus.Text = "Database: status unavailable.";
                        _lblDbStatus.ForeColor = Color.Red;
                        return;
                    }

                    AppRuntime.LogTiming("Settings.CheckDbConnection.Complete", 0);
                    _lblDbStatus.Text = "Database: " + result.Message;
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
                    _lblDbStatus.Text = "Database: NOT connected - " + ex.Message;
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

                _lblDbStatus.Text = "Database: " + result.Message;
                _lblDbStatus.ForeColor = result.Success ? SaveGreen : Color.Red;
                RefreshSettingsWorkspaceSummary();
            }
            catch (Exception ex)
            {
                _lblDbStatus.Text = "Database: NOT connected - " + ex.Message;
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

            _lblDbStatus.Text = "Database: repairing schema and sync metadata...";
            _lblDbStatus.ForeColor = DS.Slate600;
            Cursor = Cursors.WaitCursor;

            try
            {
                SupportToolResult result = await Task.Run(() => _supportCenterSvc.RepairDatabaseSchema());
                if (IsDisposed || _lblDbStatus == null || _lblDbStatus.IsDisposed)
                    return;

                _lblDbStatus.Text = "Database: " + result.Message;
                _lblDbStatus.ForeColor = result.Success ? SaveGreen : Color.Red;
                _lblStatus.Text = result.Title;
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
                    _lblDbStatus.Text = "Database: repair failed - " + ex.Message;
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
                        _lblStatus.Text = "Connection settings saved. Please restart the app to use the new database connection.";
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
                _lblStatus.Text = "Connection setup error: " + ex.Message;
                _lblStatus.ForeColor = Color.Red;
            }
        }

        private void RefreshIndiaDefaultsPreview()
        {
            _chkEInvoiceEligible.Checked = _numAnnualTurnover.Value >= _numEInvoiceThreshold.Value;
            _txtFinancialYear.Text = IndiaFinancialYearHelper.GetFinancialYearDisplay(DateTime.Today);
            _lblMoneyPreview.Text =
                "Money preview: " + IndiaFormatHelper.FormatCurrency(_numAnnualTurnover.Value)
                + "  |  FY: " + IndiaFinancialYearHelper.GetFinancialYearCode(DateTime.Today)
                + "  |  Dates: " + IndiaFormatHelper.FormatDate(DateTime.Today);
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
                Font = new Font("Segoe UI", 8.5f),
                ScrollBars = ScrollBars.None
            };
            StyleDataGrid(grid);
            grid.ScrollBars = ScrollBars.None;
            grid.AutoGenerateColumns = false;

            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "MasterID", DataPropertyName = "MasterID", Visible = false });
            grid.Columns.Add(new DataGridViewComboBoxColumn { Name = "CodeType", DataPropertyName = "CodeType", HeaderText = "Type", DataSource = new[] { "HSN", "SAC" }, FillWeight = 50, Visible = false });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Code", DataPropertyName = "Code", HeaderText = "HSN / SAC", FillWeight = 78, MinimumWidth = 82 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", DataPropertyName = "Description", HeaderText = "Description", FillWeight = 190, MinimumWidth = 130 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "BusinessCategory", DataPropertyName = "BusinessCategory", HeaderText = "Category", FillWeight = 120, Visible = false });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "TaxRate", DataPropertyName = "TaxRate", HeaderText = "GST %", FillWeight = 52, MinimumWidth = 58 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CGSTRate", DataPropertyName = "CGSTRate", HeaderText = "CGST %", FillWeight = 52, MinimumWidth = 58 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SGSTRate", DataPropertyName = "SGSTRate", HeaderText = "SGST %", FillWeight = 52, MinimumWidth = 58 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "IGSTRate", DataPropertyName = "IGSTRate", HeaderText = "IGST %", FillWeight = 52, MinimumWidth = 58, Visible = false });
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsDefault", DataPropertyName = "IsDefault", HeaderText = "Default", FillWeight = 50, Visible = false });
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsActive", DataPropertyName = "IsActive", HeaderText = "Active", FillWeight = 45, MinimumWidth = 54, Visible = false });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Notes", DataPropertyName = "Notes", HeaderText = "Notes", FillWeight = 140, Visible = false });
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
                    _lblStatus.Text = "HSN/SAC master could not be loaded: " + ex.Message;
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
                Font = new Font("Segoe UI", 9),
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
            parent.Controls.Add(new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = InfoBlue,
                Location = new Point(0, y),
                Width = 960,
                Height = 22,
                BackColor = SectionBg,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 0, 0)
            });
            y += 28;
        }

        private Label MakeLbl(string text, Point location)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = Color.Gray,
                Location = location,
                Width = 206,
                TextAlign = ContentAlignment.MiddleRight
            };
        }

        private NumericUpDown MakeDecimalBox(Point location, int width, decimal minimum, decimal maximum, decimal value, int decimals, decimal increment = 1m)
        {
            return new NumericUpDown
            {
                Location = location,
                Width = width,
                Font = new Font("Segoe UI", 9),
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
            var button = new Button
            {
                Text = text,
                Width = width,
                Height = 34,
                BackColor = bg,
                ForeColor = bg == Color.White ? DS.Slate700 : Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
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
            avatar.Controls.Add(new Label
            {
                Text = "AD",
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            });
            Label user = new Label
            {
                Text = SessionManager.CurrentUser == null ? "Administrator\r\nAdmin" : (SessionManager.CurrentUser.DisplayName + "\r\n" + SessionManager.CurrentUser.RoleName),
                Size = new Size(150, 42),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = DS.Slate900
            };
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
                SharedPageHeader.CreateSearchCommand("SettingsGlobalSearch", 280, "Search", "Ctrl + K", () => SharedUiPrimitives.OpenGlobalSearch(this)),
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
                new Font("Segoe UI", 9f, selected ? FontStyle.Bold : FontStyle.Regular),
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
            Panel body;
            Panel wrapper = DS.MakeCard(out body, 14, new Padding(22, 22, 22, 18));
            wrapper.AutoScroll = false;
            wrapper.Size = new Size(560, height);
            wrapper.Margin = new Padding(0, 0, 14, 14);
            wrapper.Tag = "settings-card";
            body.AutoScroll = false;
            body.Controls.Add(new Label
            {
                Text = title,
                Location = new Point(60, 2),
                Size = new Size(410, 24),
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = DS.Slate900,
                UseMnemonic = false
            });
            body.Controls.Add(new Label
            {
                Text = subtitle,
                Location = new Point(60, 29),
                Size = new Size(420, 38),
                Font = new Font("Segoe UI", 8.7f),
                ForeColor = DS.Slate500,
                UseMnemonic = false
            });
            Panel icon = ModernIconSystem.EmptyStateIcon(ModernIconSystem.KindForTitle(title), 44, DS.Indigo50, DS.Primary600);
            icon.Location = new Point(0, 2);
            body.Controls.Add(icon);
            Panel content = new Panel
            {
                Location = new Point(0, 76),
                Size = new Size(530, Math.Max(90, height - 108)),
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                AutoScroll = false
            };
            body.Resize += (s, e) =>
            {
                int availableWidth = Math.Max(280, body.ClientSize.Width);
                content.Location = new Point(0, 76);
                content.Width = Math.Max(260, availableWidth);
                content.Height = Math.Max(90, body.ClientSize.Height - content.Top);
                foreach (Label label in body.Controls.OfType<Label>())
                {
                    if (label.Left >= 60)
                        label.Width = Math.Max(220, availableWidth - label.Left - 8);
                }
            };
            body.Controls.Add(content);
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
            inner.Controls.Add(new Label
            {
                Text = title,
                Location = new Point(36, 0),
                Size = new Size(110, 18),
                Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
                ForeColor = DS.Slate500
            });
            Label icon = ModernIconSystem.Badge(ModernIconSystem.KindForTitle(title), 26, DS.Indigo50, accent, 8);
            icon.Location = new Point(0, 2);
            inner.Controls.Add(icon);
            Label valueLabel = new Label
            {
                Text = value,
                Location = new Point(36, 24),
                Size = new Size(110, 28),
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = accent,
                AutoEllipsis = true
            };
            inner.Controls.Add(valueLabel);
            parent.Controls.Add(card);
            return valueLabel;
        }

        private Label FilterLabel(string text)
        {
            return new Label
            {
                Text = text,
                Width = 42,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = DS.Slate500,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Margin = new Padding(0, 0, 4, 8)
            };
        }

        private void ReflowSettingsCards()
        {
            if (_generalFlow == null || _generalCanvas == null)
                return;
            if (_reflowingSettingsCards)
                return;

            _reflowingSettingsCards = true;
            try
            {
                int viewportWidth = _generalCanvas.Parent == null ? GeneralCanvasWidth : _generalCanvas.Parent.ClientSize.Width;
                int canvasWidth = Math.Min(GeneralCanvasWidth, Math.Max(420, viewportWidth - 42));
                if (_generalCanvas.Width != canvasWidth)
                    _generalCanvas.Width = canvasWidth;
                if (_generalFlow.Width != canvasWidth)
                    _generalFlow.Width = canvasWidth;
                int columns = canvasWidth >= 1560 ? 3 : (canvasWidth >= 980 ? 2 : 1);
                int gap = 14;
                int cardWidth = columns == 1 ? canvasWidth - 4 : (canvasWidth - (gap * (columns - 1))) / columns;
                int[] columnHeights = new int[columns];
                Panel[] cards = _generalFlow.Controls
                    .OfType<Panel>()
                    .Where(p => Equals(p.Tag, "settings-card"))
                    .ToArray();

                _generalFlow.SuspendLayout();
                foreach (Panel card in cards)
                {
                    int targetWidth = Math.Max(360, cardWidth);
                    if (card.Width != targetWidth)
                        card.Width = targetWidth;
                    if (card.Margin != Padding.Empty)
                        card.Margin = Padding.Empty;

                    int column = 0;
                    for (int i = 1; i < columns; i++)
                    {
                        if (columnHeights[i] < columnHeights[column])
                            column = i;
                    }

                    Point targetLocation = new Point(column * (card.Width + gap), columnHeights[column]);
                    if (card.Location != targetLocation)
                        card.Location = targetLocation;
                    columnHeights[column] += card.Height + gap;
                }
                _generalFlow.ResumeLayout(false);

                int contentHeight = columnHeights.Length == 0 ? 0 : columnHeights.Max();
                if (contentHeight > 0)
                    contentHeight -= gap;
                if (_generalFlow.Height != contentHeight)
                    _generalFlow.Height = contentHeight;
                int canvasHeight = Math.Max(_generalCanvas.Parent == null ? 0 : _generalCanvas.Parent.ClientSize.Height - 40, _generalFlow.Height + 32);
                if (_generalCanvas.Height != canvasHeight)
                    _generalCanvas.Height = canvasHeight;
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
            _lblStatus.Text = "Defaults restored in the form. Click Save Settings to persist them.";
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

            Label titleLabel = new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = DS.Slate900,
                TextAlign = ContentAlignment.MiddleLeft
            };
            Label overflowLabel = new Label
            {
                Dock = DockStyle.Right,
                Width = 76,
                Text = "Scroll",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = DS.Slate500,
                TextAlign = ContentAlignment.MiddleRight,
                Visible = false
            };
            Button btnExtend = new Button
            {
                Dock = DockStyle.Right,
                Width = 76,
                Height = 24,
                Text = "Extend",
                BackColor = Color.White,
                ForeColor = DS.Slate700,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
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
                content.Controls.Add(new Label
                {
                    Text = subtitle,
                    Font = new Font("Segoe UI", 9),
                    ForeColor = DS.Slate500,
                    Dock = DockStyle.Top,
                    Height = 34
                });
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
            button.Text = state.IsExpanded ? "Collapse" : "Extend";
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

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Location = new Point(0, 0),
                Size = new Size(Math.Max(320, parent.ClientSize.Width), 108),
                BackColor = Color.White,
                WrapContents = true,
                AutoScroll = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            parent.Controls.Add(actions);

            foreach (string pageKey in pageKeys)
            {
                Button button = MakeBtn("Reset " + pageKey.Replace("Analysis", " Analysis"), InfoBlue, 174);
                button.Margin = new Padding(0, 0, 14, 12);
                button.Click += (s, e) => ResetLayout(pageKey);
                actions.Controls.Add(button);
            }

            Button resetAll = MakeBtn("Reset all layouts", SaveGreen, 180);
            resetAll.Location = new Point(0, actions.Bottom + 6);
            resetAll.Click += (s, e) =>
            {
                foreach (string pageKey in pageKeys)
                    ResetLayout(pageKey);
                _lblStatus.Text = "All card layouts reset to default.";
            };
            parent.Controls.Add(resetAll);
            Action layout = () =>
            {
                actions.Width = Math.Max(320, parent.ClientSize.Width);
                int rows = 1;
                int runningWidth = 0;
                foreach (Control control in actions.Controls)
                {
                    int nextWidth = control.Width + control.Margin.Horizontal;
                    if (runningWidth > 0 && runningWidth + nextWidth > actions.Width)
                    {
                        rows++;
                        runningWidth = 0;
                    }
                    runningWidth += nextWidth;
                }

                actions.Height = Math.Max(46, rows * 46);
                resetAll.Top = actions.Bottom + 6;
                parent.Height = resetAll.Bottom + 4;
            };
            parent.Resize += (s, e) => layout();
            layout();
        }

        private void BuildFreshStartSection(Panel parent)
        {
            parent.Controls.Add(new Label
            {
                Text = "Warning: this permanently removes transactional records, clients, employees, vendors, sites, contracts, salary, and settings. Users, roles, and permissions stay intact.",
                Location = new Point(0, 0),
                Width = Math.Max(240, parent.ClientSize.Width),
                Height = 54,
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(127, 29, 29),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            });

            Button button = MakeBtn("Fresh Start", Color.FromArgb(220, 38, 38), 132);
            button.Location = new Point(0, 62);
            button.Click += (s, e) => RunFreshStart();
            parent.Controls.Add(button);

            parent.Controls.Add(new Label
            {
                Text = "Type CONFIRM in the next dialog to unlock the delete action.",
                Location = new Point(0, 112),
                Width = Math.Max(240, parent.ClientSize.Width),
                Height = 24,
                Font = new Font("Segoe UI", 9),
                ForeColor = DS.Slate500,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            });
        }

        private void BuildBackupRestoreSection(Panel parent)
        {
            _lblBackupStatus = new Label
            {
                Text = BuildBackupSummary(),
                Location = new Point(0, 0),
                Width = Math.Max(260, parent.ClientSize.Width),
                Height = 58,
                Font = new Font("Segoe UI", 9),
                ForeColor = DS.Slate700,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            parent.Controls.Add(_lblBackupStatus);

            Button btnBackupNow = MakeBtn("Backup Now", SaveGreen, 124);
            btnBackupNow.Location = new Point(0, 78);
            btnBackupNow.Click += async (s, e) => await CreateBackupAsync();
            parent.Controls.Add(btnBackupNow);

            Button btnBackupSettings = MakeBtn("Backup Settings", InfoBlue, 142);
            btnBackupSettings.Location = new Point(140, 78);
            btnBackupSettings.Click += (s, e) => OpenBackupSettings();
            parent.Controls.Add(btnBackupSettings);

            Button btnSharedStorage = MakeBtn("Shared Storage", InfoBlue, 142);
            btnSharedStorage.Location = new Point(298, 78);
            btnSharedStorage.Click += (s, e) => OpenSharedStorageSettings();
            parent.Controls.Add(btnSharedStorage);

            Button btnRestoreFile = MakeBtn("Restore File", Color.FromArgb(220, 38, 38), 124);
            btnRestoreFile.Location = new Point(456, 78);
            btnRestoreFile.Click += async (s, e) => await RestoreFromFileAsync();
            parent.Controls.Add(btnRestoreFile);

            Button btnOpenFolder = MakeBtn("Open Folder", InfoBlue, 124);
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

            parent.Controls.Add(new Label
            {
                Text = "Backups stay on the client network, local PC, or external drive. Restore first creates a safety backup and then restores the selected .bak.",
                Location = new Point(0, 128),
                Width = Math.Max(260, parent.ClientSize.Width),
                Height = 58,
                Font = new Font("Segoe UI", 8.8f),
                ForeColor = DS.Slate500,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            });
        }

        private void BuildLicenseSection(Panel parent)
        {
            _lblLicenseStatus = new Label
            {
                Text = BuildLicenseSummary(),
                Location = new Point(0, 0),
                Width = Math.Max(260, parent.ClientSize.Width),
                Height = 82,
                Font = new Font("Segoe UI", 9),
                ForeColor = DS.Slate700,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            parent.Controls.Add(_lblLicenseStatus);

            Button activate = MakeBtn("Activate / Renew", SaveGreen, 144);
            activate.Location = new Point(0, 104);
            activate.Click += (s, e) => OpenLicenseActivation();
            parent.Controls.Add(activate);

            Button refresh = MakeBtn("Refresh Status", InfoBlue, 132);
            refresh.Location = new Point(160, 104);
            refresh.Click += (s, e) => RefreshLicenseStatus();
            parent.Controls.Add(refresh);

            Button copyFingerprint = MakeBtn("Copy Device ID", Color.White, 132);
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

            parent.Controls.Add(new Label
            {
                Text = "Frozen Mode allows login, read-only data access, reports export, backup export, and license renewal only.",
                Location = new Point(0, 158),
                Width = Math.Max(260, parent.ClientSize.Width),
                Height = 48,
                Font = new Font("Segoe UI", 8.8f),
                ForeColor = DS.Slate500,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            });
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
                _lblLicenseStatus.Text = BuildLicenseSummary();
            _lblStatus.Text = "License status refreshed.";
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
                _lblStatus.Text = "Device fingerprint copied for license issuance.";
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
                _lblBackupStatus.Text = BuildBackupSummary();
            RefreshSettingsWorkspaceSummary();
        }

        private void OpenSharedStorageSettings()
        {
            using (var form = new SharedStorageSettingsForm())
                form.ShowDialog(FindForm());

            if (_lblBackupStatus != null)
                _lblBackupStatus.Text = BuildBackupSummary();
            RefreshSettingsWorkspaceSummary();
        }

        private void RefreshSettingsWorkspaceSummary()
        {
            if (_lblSettingsVersionState == null)
                return;

            _lblSettingsVersionState.Text = ConfigService.GetAppVersion();

            string dbStatus = _lblDbStatus == null ? string.Empty : _lblDbStatus.Text;
            if (dbStatus.IndexOf("NOT connected", StringComparison.OrdinalIgnoreCase) >= 0)
                _lblSettingsDbState.Text = "Issue";
            else if (dbStatus.IndexOf("checking", StringComparison.OrdinalIgnoreCase) >= 0)
                _lblSettingsDbState.Text = "Checking";
            else if (!string.IsNullOrWhiteSpace(dbStatus))
                _lblSettingsDbState.Text = "Ready";
            else
                _lblSettingsDbState.Text = "Pending";

            string backupSummary = _lblBackupStatus != null && !string.IsNullOrWhiteSpace(_lblBackupStatus.Text)
                ? _lblBackupStatus.Text
                : BuildBackupSummary();
            _lblSettingsBackupState.Text = backupSummary.IndexOf("No successful backups", StringComparison.OrdinalIgnoreCase) >= 0
                ? "Pending"
                : (backupSummary.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0 ? "Issue" : "Ready");

            string licenseSummary = _lblLicenseStatus != null && !string.IsNullOrWhiteSpace(_lblLicenseStatus.Text)
                ? _lblLicenseStatus.Text
                : BuildLicenseSummary();
            _lblSettingsLicenseState.Text = licenseSummary.IndexOf("activation required", StringComparison.OrdinalIgnoreCase) >= 0
                ? "Action"
                : "Active";

            bool assistantEnabled = _chkAiEnabled != null && _chkAiEnabled.Checked;
            _lblSettingsAssistantState.Text = assistantEnabled ? "Enabled" : "Disabled";
        }

        private async Task CreateCloudBackupAsync()
        {
            SetBackupStatus("Creating cloud backup...", DS.Slate700);
            try
            {
            IntegrationOperationResult result = await _cloudBackupIntegrationSvc.CreateAndUploadBackupAsync(System.Threading.CancellationToken.None);
                SetBackupStatus(result.Success ? "Cloud backup complete: " + result.ReferenceId : "Cloud backup failed: " + result.Message, result.Success ? SaveGreen : Color.Red);
                MessageBox.Show(
                    result.Success ? "Cloud backup completed:\r\n" + result.LocalPath : "Cloud backup failed:\r\n" + result.Message,
                    result.Success ? "Cloud Backup Complete" : "Cloud Backup Failed",
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
                    MessageBox.Show("Restore completed.\r\n\r\nClose and reopen ServoERP before continuing work.", "Restore Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    SetBackupStatus("Restore failed: " + result.Message, Color.Red);
                    MessageBox.Show("Restore failed:\r\n" + result.Message, "Restore Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                _lblBackupStatus.Text = text;
                _lblBackupStatus.ForeColor = color;
            }

            _lblStatus.Text = text;
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
            using (Form dialog = ServoModalForm.Create("Fresh Start Confirmation", 520, 280))
            {
                var prompt = new Label
                {
                    Text = "All data including master data will be deleted. This cannot be undone.\r\n\r\nType CONFIRM to proceed.",
                    Location = new Point(18, 18),
                    Size = new Size(484, 130)
                };
                var confirmBox = new TextBox { Location = new Point(18, 164), Width = 484 };
                var btnCancel = new Button { Text = "Cancel", Location = new Point(318, 214), Width = 88, DialogResult = DialogResult.Cancel };
                var btnDelete = new Button { Text = "Delete", Location = new Point(414, 214), Width = 88, Enabled = false, BackColor = Color.FromArgb(220, 38, 38), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                btnDelete.FlatAppearance.BorderSize = 0;
                _toolTip.SetToolTip(btnDelete, "Type CONFIRM to unlock this destructive action.");
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
                _lblStatus.Text = "Fresh Start complete.";
                _lblStatus.ForeColor = SaveGreen;
                MessageBox.Show(
                    "Fresh Start complete. The following data was cleared:\r\n- Jobs\r\n- Quotations\r\n- Invoices\r\n- Payments\r\n- Purchases\r\n- Attendance records\r\n- SLA Logs\r\n- Clients\r\n- Employees\r\n- Vendors\r\n- Sites\r\n- Contracts\r\n- Salary\r\n- Settings\r\n\r\nApp is ready for a new client.",
                    "Fresh Start Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SettingsForm.RunFreshStart", ex);
                _lblStatus.Text = "Fresh Start failed: " + ex.Message;
                _lblStatus.ForeColor = Color.Red;
                MessageBox.Show("Fresh Start failed. No data was removed.", "Fresh Start Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ResetLayout(string pageKey)
        {
            int userId = CardLayoutService.ResolveCurrentUserId();
            System.Threading.Tasks.Task.Run(() => _cardLayoutSvc.ResetPageLayout(userId, pageKey));
            MainForm mainForm = FindForm() as MainForm;
            if (mainForm != null)
                mainForm.ReloadPageByKey(pageKey);
            _lblStatus.Text = pageKey + " layout reset.";
            _lblStatus.ForeColor = SaveGreen;
        }

        private void PlaceLabeledControl(Panel parent, string label, Control control, int x, int y, int width, int height = 34)
        {
            parent.Controls.Add(new Label
            {
                Text = label,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = DS.Slate500,
                Location = new Point(x, y),
                Width = width
            });

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
                .FirstOrDefault(l => string.Equals(l.Text, label, StringComparison.Ordinal));
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
            control.Font = new Font("Segoe UI", 9.5f);

            if (control is TextBox textBox)
            {
                textBox.BorderStyle = BorderStyle.None;
                textBox.BackColor = textBox.ReadOnly ? DS.Slate100 : Color.White;
            }
            else if (control is ComboBox comboBox)
            {
                comboBox.FlatStyle = FlatStyle.Flat;
                comboBox.BackColor = Color.White;
            }
            else if (control is NumericUpDown numeric)
            {
                numeric.BackColor = Color.White;
                numeric.BorderStyle = BorderStyle.None;
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
                Font = new Font("Segoe UI", 9),
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
                _lblStatus.Text = "Locating office address...";
                _lblStatus.ForeColor = InfoBlue;
                GeocodeResult result = await Task.Run(() => _geoSvc.LocateAddress(_txtAddress.Text));
                _txtOfficeLatitude.Text = result.Latitude.ToString("0.0000000", CultureInfo.InvariantCulture);
                _txtOfficeLongitude.Text = result.Longitude.ToString("0.0000000", CultureInfo.InvariantCulture);
                _lblStatus.Text = "Office coordinates updated from OpenStreetMap.";
                _lblStatus.ForeColor = SaveGreen;
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "Locate error: " + ex.Message;
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
                ConfigService.Set("App", "SilentAutoUpdateEnabled", _chkSilentAutoUpdateEnabled != null && _chkSilentAutoUpdateEnabled.Checked ? "true" : "false");
                ConfigService.Set("App", "SilentAutoUpdateApplyImmediately", "false");
                ConfigService.Set("App", "SilentAutoUpdateApplyOnExit", "false");

                if (_chkVersionCheckEnabled != null && !_chkVersionCheckEnabled.Checked)
                {
                    if (_lblLastUpdateCheckStatus != null)
                        _lblLastUpdateCheckStatus.Text = "Update checks are turned off in Settings.";
                    MessageBox.Show(
                        "Update notification settings were saved, but automatic checks are turned off.\r\nTurn on \"Check for updates automatically\" to show update banners at startup.",
                        "Update notifications",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                UpdateCheckResult result = await UpdateService.CheckForUpdatesAsync();
                if (_lblLastUpdateCheckStatus != null)
                    _lblLastUpdateCheckStatus.Text = UpdateService.GetLastUpdateStatusDisplay();

                if (result.IsUpdateAvailable)
                {
                    if (!result.CanApplyUpdate)
                    {
                        bool openInstaller = ServoERP.Infrastructure.ServoConfirmDialog.Show(
                            this,
                            "Open latest ServoERP installer?",
                            result.StatusMessage + "\r\n\r\nThis opens the installer download page in your browser.");
                        if (openInstaller && !string.IsNullOrWhiteSpace(result.DownloadUrl))
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = result.DownloadUrl,
                                UseShellExecute = true
                            });
                        }
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
                        "Check for updates",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SettingsForm.CheckVersionNowAsync", ex);
                if (_lblLastUpdateCheckStatus != null)
                    _lblLastUpdateCheckStatus.Text = "Update check failed. ServoERP will continue normally. " + ex.Message;
                MessageBox.Show(
                    "Update check failed. ServoERP will continue normally.\r\n\r\n" + ex.Message,
                    "Check for updates",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private Task InstallUpdateAsync(UpdateCheckResult result)
        {
            if (result == null || !result.IsUpdateAvailable)
                return Task.CompletedTask;

            using (var progressForm = new Form
            {
                Text = "Downloading ServoERP update",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                Size = new Size(420, 150),
                BackColor = DS.BgPage,
                Font = new Font("Segoe UI", 9f)
            })
            using (var cancelSource = new CancellationTokenSource())
            {
                var status = new Label
                {
                    Text = "Downloading update package...",
                    Dock = DockStyle.Top,
                    Height = 36,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(18, 8, 18, 0),
                    ForeColor = DS.Slate800
                };
                var progress = new ProgressBar
                {
                    Dock = DockStyle.Top,
                    Height = 20,
                    Minimum = 0,
                    Maximum = 100
                };
                var cancel = DS.GhostBtn("Cancel", 90, 30);
                cancel.Dock = DockStyle.Bottom;
                cancel.Click += (s, e) => cancelSource.Cancel();
                progressForm.Controls.Add(cancel);
                progressForm.Controls.Add(progress);
                progressForm.Controls.Add(status);

                var progressReporter = new Progress<int>(value =>
                {
                    progress.Value = Math.Max(0, Math.Min(100, value));
                    status.Text = "Downloading update package... " + progress.Value + "%";
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
                            "Automatic update could not complete.\r\n\r\n" + ex.Message + "\r\n\r\nPlease try again in a few minutes.",
                            "Install update",
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




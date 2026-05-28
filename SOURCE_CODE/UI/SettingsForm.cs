using System;
using System.Collections.Generic;
using System.Data.SqlClient;
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

        private readonly SettingsService _svc = new SettingsService();
        private readonly HsnSacMasterService _hsnSacSvc = new HsnSacMasterService();
        private readonly NominatimGeocodingService _geoSvc = new NominatimGeocodingService();
        private readonly AuthService _authSvc = new AuthService();
        private readonly FreshStartService _freshStartSvc = new FreshStartService();

        private TextBox _txtCompanyName;
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
        private Label _lblStatus;
        private Label _lblDbStatus;
        private Label _lblMoneyPreview;
        private DataGridView _gridUsers;
        private DataGridView _gridAudit;
        private DateTimePicker _dtAuditFrom;
        private DateTimePicker _dtAuditTo;
        private ComboBox _cmbAuditUser;
        private TabControl _tabs;
        private Panel _generalCanvas;
        private TextBox _txtVersionCheckUrl;
        private readonly ToolTip _toolTip = new ToolTip { AutoPopDelay = 12000, InitialDelay = 350, ReshowDelay = 100, ShowAlways = true };
        private CheckBox _chkVersionCheckEnabled;
        private Label _lblInstalledVersion;
        private ComboBox _cmbDisplayFitMode;
        private ComboBox _cmbUiScale;
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
        private Label _lblAuditLatest;
        private Label _lblBackupStatus;
        private Label _lblLicenseStatus;

        private static readonly Color HeaderBg = DS.White;
        private static readonly Color SectionBg = DS.Slate50;
        private static readonly Color SaveGreen = DS.Teal600;
        private static readonly Color InfoBlue = DS.Primary600;
        private const int GeneralCanvasWidth = 1540;

        private sealed class SectionCardState
        {
            public int BaseHeight { get; set; }
            public int ExpandedHeight { get; set; }
            public bool IsExpanded { get; set; }
        }

        public SettingsForm()
        {
            Dock = DockStyle.Fill;
            BackColor = DS.BgPage;
            BuildLayout();
            UIHelper.ApplyInputStyles(Controls);
            EnableDeferredLoad(
                () =>
                {
                    LoadSettings();
                    CheckDbConnection();
                },
                ex =>
                {
                    _lblStatus.Text = "Load error: " + ex.Message;
                    _lblStatus.ForeColor = Color.Red;
                });
        }

        private void BuildLayout()
        {
            Panel header = BuildModernSettingsHeader();

            Button btnSave = MakeBtn("Save Settings", SaveGreen, 146);
            btnSave.Location = new Point(0, 0);
            ModernIconSystem.AddButtonIcon(btnSave, ModernIconKind.Save);
            btnSave.Click += (s, e) => Save();
            Button btnResetDefaults = MakeBtn("Reset to Defaults", Color.White, 148);
            btnResetDefaults.ForeColor = DS.Slate700;
            btnResetDefaults.FlatAppearance.BorderSize = 1;
            btnResetDefaults.FlatAppearance.BorderColor = DS.Slate300;
            ModernIconSystem.AddButtonIcon(btnResetDefaults, ModernIconKind.Preference);
            btnResetDefaults.Click += (s, e) => ResetGeneralDefaults();
            Button btnToolbarCheckUpdates = MakeBtn("Check Update Notification", InfoBlue, 190);
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
                Location = new Point(162, 4),
                Width = 760,
                Height = 22,
                TextAlign = ContentAlignment.MiddleLeft
            };
            Panel toolbar = BuildModernActionBar(btnSave, btnResetDefaults, btnToolbarCheckUpdates, btnFormsLibrary);

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
            Panel body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = DS.BgPage };
            _generalCanvas = new Panel { Width = GeneralCanvasWidth, BackColor = DS.BgPage, Padding = new Padding(0, 0, 0, 24) };
            _generalFlow = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                BackColor = DS.BgPage,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            _generalCanvas.Controls.Add(_generalFlow);
            body.Controls.Add(_generalCanvas);
            body.Resize += (s, e) =>
            {
                CenterCanvas(body, _generalCanvas);
                ReflowSettingsCards();
            };
            BuildForm(_generalFlow);
            CenterCanvas(body, _generalCanvas);
            generalTab.Controls.Add(body);
            _tabs.TabPages.Add(generalTab);
            if (IsAdminUser())
            {
                _tabs.TabPages.Add(BuildUsersTab());
                _tabs.TabPages.Add(BuildAuditTab());
            }

            Controls.Add(_tabs);
            Controls.Add(toolbar);
            Controls.Add(header);
        }

        private void BuildForm(Panel parent)
        {
            parent.Controls.Clear();

            if (IsAdminUser())
                BuildLoginAccessSection(parent);

            BuildUpdateNotificationsCard(parent);

            Panel companyBody = AddModernSettingsCard(parent, "Company Information", "Profile, compliance, and office location details used across the platform.", 430);
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

            _txtOfficeLatitude = new TextBox { ReadOnly = true };
            PlaceLabeledControl(companyBody, "Office Latitude", _txtOfficeLatitude, 0, 192, 170);
            _txtOfficeLongitude = new TextBox { ReadOnly = true };
            PlaceLabeledControl(companyBody, "Office Longitude", _txtOfficeLongitude, 190, 192, 170);
            _cmbState = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbState.Items.AddRange(IndiaStateCatalog.Names.Cast<object>().ToArray());
            PlaceLabeledControl(companyBody, "State / UT", _cmbState, 380, 192, 202);
            _cmbGstRegistrationType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbGstRegistrationType.Items.AddRange(new object[] { "Regular", "Composition", "Unregistered" });
            PlaceLabeledControl(companyBody, "GST Registration Type", _cmbGstRegistrationType, 0, 256, 220);
            companyBody.Resize += (s, e) => LayoutCompanyInformationCard(companyBody, btnLocateOffice);
            LayoutCompanyInformationCard(companyBody, btnLocateOffice);

            Panel displayBody = AddModernSettingsCard(parent, "Display & Layout", "Customize how dense data is displayed across the system.", 340);
            _cmbDisplayFitMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbDisplayFitMode.Items.AddRange(new object[]
            {
                "Auto detect laptop screens",
                "IdeaPad / compact laptop",
                "Standard desktop"
            });
            PlaceLabeledControl(displayBody, "Display fit mode", _cmbDisplayFitMode, 0, 0, 330);
            _cmbUiScale = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
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

            BuildLocalAiCard(parent);

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
            _txtCurrency = new TextBox { ReadOnly = true, Text = "INR (\u20B9)" };
            PlaceLabeledControl(defaultsBody, "Currency", _txtCurrency, 0, 144, 150);
            _txtFinancialYear = new TextBox { ReadOnly = true };
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

            Button btnAddRow = MakeBtn("Add HSN/SAC Row", InfoBlue, 150);
            btnAddRow.Location = new Point(0, 272);
            btnAddRow.Click += (s, e) => _gridHsnSac.Rows.Add(0, "HSN", "", "", "", 18m, 9m, 9m, 18m, false, true, "");
            hsnBody.Controls.Add(btnAddRow);
            hsnBody.Resize += (s, e) =>
            {
                btnAddRow.Top = Math.Max(210, hsnBody.ClientSize.Height - btnAddRow.Height - 2);
                hsnGridHost.Width = Math.Max(260, hsnBody.ClientSize.Width);
                hsnGridHost.Height = Math.Max(150, btnAddRow.Top - 14);
                _gridHsnSac.Width = hsnGridHost.Width;
                _gridHsnSac.Height = hsnGridHost.Height + SystemInformation.HorizontalScrollBarHeight + 2;
                LayoutHsnSacColumns(_gridHsnSac);
            };

            Panel systemBody = AddModernSettingsCard(parent, "System Tools", "Database connection and saved card layout controls.", 330);
            _lblDbStatus = new Label { Location = new Point(0, 0), Size = new Size(520, 24), Font = new Font("Segoe UI", 9), ForeColor = DS.Slate700 };
            systemBody.Controls.Add(_lblDbStatus);
            Button btnTest = MakeBtn("Test Connection", InfoBlue, 140);
            btnTest.Location = new Point(0, 42);
            btnTest.Click += (s, e) => CheckDbConnection();
            systemBody.Controls.Add(btnTest);
            Button btnSetup = MakeBtn("Connection Setup", SaveGreen, 156);
            btnSetup.Location = new Point(154, 42);
            btnSetup.Click += (s, e) => OpenConnectionSetup();
            systemBody.Controls.Add(btnSetup);
            Panel resetArea = new Panel { Location = new Point(0, 88), Size = new Size(526, 112), BackColor = Color.White, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            systemBody.Controls.Add(resetArea);
            BuildLayoutResetSection(resetArea);

            if (IsAdminUser())
            {
                Panel licenseBody = AddModernSettingsCard(parent, "License Management", "Activation, renewal, device status, and frozen-mode recovery.", 300);
                BuildLicenseSection(licenseBody);

                Panel backupBody = AddModernSettingsCard(parent, "Backup & Restore", "Create recoverable SQL backups and restore safely after corruption or mistakes.", 300);
                BuildBackupRestoreSection(backupBody);

                Panel dataBody = AddModernSettingsCard(parent, "Data Management", "Fresh Start clears transactional records, master data, and settings.", 300);
                BuildFreshStartSection(dataBody);
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
            Panel updatesBody = AddModernSettingsCard(parent, "Update Notifications", "Configure update checks and version information.", 300);
            _txtVersionCheckUrl = new TextBox();
            PlaceLabeledControl(updatesBody, "Version File URL", _txtVersionCheckUrl, 0, 0, 444);
            Button btnCopy = MakeBtn("Copy", Color.White, 72);
            btnCopy.ForeColor = DS.Slate700;
            btnCopy.FlatAppearance.BorderSize = 1;
            btnCopy.FlatAppearance.BorderColor = DS.Slate300;
            btnCopy.Location = new Point(456, 20);
            btnCopy.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnCopy.Click += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(_txtVersionCheckUrl.Text))
                    Clipboard.SetText(_txtVersionCheckUrl.Text.Trim());
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

            _lblInstalledVersion = new Label
            {
                Text = "Installed version: " + ConfigService.GetAppVersion(),
                Location = new Point(0, 122),
                Width = 320,
                Height = 22,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = SaveGreen
            };
            updatesBody.Controls.Add(_lblInstalledVersion);

            Button btnCheckNow = MakeBtn("Check Update Notification", InfoBlue, 190);
            btnCheckNow.Location = new Point(0, 160);
            btnCheckNow.Click += async (s, e) => await CheckVersionNowAsync();
            updatesBody.Controls.Add(btnCheckNow);
        }

        private void BuildLocalAiCard(Panel parent)
        {
            Panel aiBody = AddModernSettingsCard(parent, "Local AI Assistant", "Configure the inbuilt ServoERP Copilot. Ollama on localhost is used by default.", 410);

            _chkAiEnabled = new CheckBox
            {
                Text = "Enable Local AI Assistant",
                Location = new Point(0, 0),
                Size = new Size(240, 26),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = DS.Slate700,
                BackColor = Color.White
            };
            aiBody.Controls.Add(_chkAiEnabled);

            _cmbAiProvider = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbAiProvider.Items.AddRange(new object[] { "Ollama", "OpenAI-compatible future" });
            PlaceLabeledControl(aiBody, "Provider", _cmbAiProvider, 0, 52, 190);

            _txtAiEndpoint = new TextBox();
            PlaceLabeledControl(aiBody, "Endpoint URL", _txtAiEndpoint, 210, 52, 318);

            _txtAiModel = new TextBox();
            PlaceLabeledControl(aiBody, "Model name", _txtAiModel, 0, 124, 190);

            _numAiMaxTokens = MakeDecimalBox(Point.Empty, 0, 64m, 4096m, 700m, 0, 50m);
            PlaceLabeledControl(aiBody, "Max tokens", _numAiMaxTokens, 210, 124, 150);

            _numAiTemperature = MakeDecimalBox(Point.Empty, 0, 0m, 2m, 0.2m, 2, 0.05m);
            PlaceLabeledControl(aiBody, "Temperature", _numAiTemperature, 378, 124, 150);

            Label help = new Label
            {
                Text = "No API keys are stored here. Keep provider as Ollama for local-only AI. Install Ollama, then pull llama3.1 or qwen2.5 before using Copilot.",
                Location = new Point(0, 204),
                Size = new Size(528, 48),
                Font = DS.Small,
                ForeColor = DS.Slate600
            };
            aiBody.Controls.Add(help);

            Button test = MakeBtn("Test Local AI", InfoBlue, 126);
            test.Location = new Point(0, 266);
            test.Click += async (s, e) => await TestLocalAiAsync();
            aiBody.Controls.Add(test);
        }

        private void LoadSettings()
        {
            try
            {
                IndiaCompanySettings settings = _svc.GetIndiaCompanySettings();
                _txtCompanyName.Text = settings.CompanyName;
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
                    _txtVersionCheckUrl.Text = ConfigService.GetVersionCheckUrl();
                if (_chkVersionCheckEnabled != null)
                    _chkVersionCheckEnabled.Checked = ConfigService.IsVersionCheckEnabled();
                if (_lblInstalledVersion != null)
                    _lblInstalledVersion.Text = "Installed version: " + ConfigService.GetAppVersion();
                LoadDisplayFitSetting();
                LoadUiScaleSetting();
                LoadAiSettings();

                LoadHsnSacGrid(_hsnSacSvc.GetAll());
                RefreshIndiaDefaultsPreview();
                RefreshSecurityTabs();
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "Load error: " + ex.Message;
                _lblStatus.ForeColor = Color.Red;
            }
        }

        private TabPage BuildUsersTab()
        {
            TabPage tab = new TabPage("Users & Logins") { BackColor = DS.BgPage };

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
            tab.Controls.Add(page);
            return tab;
        }

        private TabPage BuildAuditTab()
        {
            TabPage tab = new TabPage("Audit Log") { BackColor = DS.BgPage };

            Panel page = new Panel { Dock = DockStyle.Fill, BackColor = DS.BgPage, Padding = new Padding(22, 18, 22, 22) };
            FlowLayoutPanel summary = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 92, WrapContents = false, BackColor = DS.BgPage };
            _lblAuditTotal = AddSummaryCard(summary, "Total Events", "0", InfoBlue);
            _lblAuditLogin = AddSummaryCard(summary, "Login Events", "0", SaveGreen);
            _lblAuditWarnings = AddSummaryCard(summary, "Failed / Warning", "0", DS.Red600);
            _lblAuditLatest = AddSummaryCard(summary, "Latest Activity", "-", DS.Slate700);

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
            Button btnRefresh = MakeBtn("Refresh", InfoBlue, 92);
            btnRefresh.Margin = new Padding(0, 0, 0, 8);
            btnRefresh.Click += (s, e) => RefreshAuditLog();
            toolbarInner.Controls.Add(btnRefresh);
            toolbar.Controls.Add(toolbarInner);

            Panel gridCard = BuildPlainCard();
            gridCard.Dock = DockStyle.Fill;
            gridCard.Padding = new Padding(14);
            _gridAudit = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            StyleDataGrid(_gridAudit);
            gridCard.Controls.Add(_gridAudit);

            page.Controls.Add(gridCard);
            page.Controls.Add(toolbar);
            page.Controls.Add(summary);
            tab.Controls.Add(page);
            _dtAuditFrom.Value = DateTime.Today.AddDays(-30);
            _dtAuditTo.Value = DateTime.Today;
            return tab;
        }

        private bool IsAdminUser()
        {
            return SessionManager.CurrentUser != null
                && string.Equals(SessionManager.CurrentUser.RoleName, "Admin", StringComparison.OrdinalIgnoreCase);
        }

        private void OpenUserManagementTab()
        {
            if (_tabs == null)
                return;

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

        private void RefreshUsers()
        {
            if (_gridUsers == null)
                return;

            var users = _authSvc.GetUsers();
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
            if (_lblAuditLatest != null)
                _lblAuditLatest.Text = table.Rows.Count > 0 ? Convert.ToDateTime(table.Rows[0]["LogDate"]).ToString("dd/MM HH:mm") : "-";
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

            using (Form dialog = new Form())
            {
                dialog.AutoScaleMode = AutoScaleMode.Dpi;
                dialog.Text = user == null ? "Add User" : "Edit User";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.ClientSize = new Size(360, 230);
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.Font = new Font("Segoe UI", 9);

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
                    ConfigService.Set("App", "VersionCheckUrl", _txtVersionCheckUrl.Text.Trim());
                    ConfigService.Set("App", "VersionCheckEnabled", _chkVersionCheckEnabled != null && _chkVersionCheckEnabled.Checked ? "true" : "false");
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
            SelectCombo(_cmbAiProvider, config.Provider, "Ollama");
            _txtAiEndpoint.Text = config.EndpointUrl;
            _txtAiModel.Text = config.ModelName;
            _numAiMaxTokens.Value = Clamp(_numAiMaxTokens, config.MaxTokens);
            _numAiTemperature.Value = Clamp(_numAiTemperature, config.Temperature);
        }

        private void SaveAiSettings()
        {
            if (_chkAiEnabled == null)
                return;

            var config = new AiProviderConfig
            {
                Enabled = _chkAiEnabled.Checked,
                Provider = _cmbAiProvider.SelectedItem == null ? "Ollama" : _cmbAiProvider.SelectedItem.ToString(),
                EndpointUrl = _txtAiEndpoint.Text.Trim(),
                ModelName = _txtAiModel.Text.Trim(),
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
                _lblStatus.Text = "Testing Local AI connection...";
                _lblStatus.ForeColor = InfoBlue;
                bool ok = await new AiAssistantService().IsLocalAiReachableAsync(CancellationToken.None);
                _lblStatus.Text = ok
                    ? "Local AI connected."
                    : "Local AI is not running. Please install/start Ollama and pull a model like llama3.1 or qwen2.5.";
                _lblStatus.ForeColor = ok ? SaveGreen : DS.Amber600;
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "Local AI test failed: " + ex.Message;
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

        private void CheckDbConnection()
        {
            try
            {
                var db = new DatabaseManager();
                using (SqlConnection conn = db.GetConnection())
                {
                    conn.Open();
                }

                _lblDbStatus.Text = "Database: Connected (" + db.ResolvedServer + ")";
                _lblDbStatus.ForeColor = SaveGreen;
            }
            catch (Exception ex)
            {
                _lblDbStatus.Text = "Database: NOT connected - " + ex.Message;
                _lblDbStatus.ForeColor = Color.Red;
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

            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "MasterID", Visible = false });
            grid.Columns.Add(new DataGridViewComboBoxColumn { Name = "CodeType", HeaderText = "Type", DataSource = new[] { "HSN", "SAC" }, FillWeight = 50, Visible = false });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Code", HeaderText = "HSN / SAC", FillWeight = 78, MinimumWidth = 82 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "Description", FillWeight = 190, MinimumWidth = 130 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "BusinessCategory", HeaderText = "Category", FillWeight = 120, Visible = false });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "TaxRate", HeaderText = "GST %", FillWeight = 52, MinimumWidth = 58 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CGSTRate", HeaderText = "CGST %", FillWeight = 52, MinimumWidth = 58 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SGSTRate", HeaderText = "SGST %", FillWeight = 52, MinimumWidth = 58 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "IGSTRate", HeaderText = "IGST %", FillWeight = 52, MinimumWidth = 58, Visible = false });
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsDefault", HeaderText = "Default", FillWeight = 50, Visible = false });
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsActive", HeaderText = "Active", FillWeight = 45, MinimumWidth = 54, Visible = false });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Notes", HeaderText = "Notes", FillWeight = 140, Visible = false });
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
            _gridHsnSac.Rows.Clear();
            foreach (HsnSacMasterEntry entry in rows ?? Enumerable.Empty<HsnSacMasterEntry>())
            {
                _gridHsnSac.Rows.Add(
                    entry.MasterID,
                    entry.CodeType,
                    entry.Code,
                    entry.Description,
                    entry.BusinessCategory,
                    entry.TaxRate,
                    entry.CGSTRate,
                    entry.SGSTRate,
                    entry.IGSTRate,
                    entry.IsDefault,
                    entry.IsActive,
                    entry.Notes);
            }
        }

        private List<HsnSacMasterEntry> CollectHsnSacRows()
        {
            var entries = new List<HsnSacMasterEntry>();
            foreach (DataGridViewRow row in _gridHsnSac.Rows)
            {
                if (row.IsNewRow)
                    continue;

                string code = ToCell(row.Cells["Code"].Value);
                string description = ToCell(row.Cells["Description"].Value);
                if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(description))
                    continue;

                entries.Add(new HsnSacMasterEntry
                {
                    MasterID = ToInt(row.Cells["MasterID"].Value),
                    CodeType = ToCell(row.Cells["CodeType"].Value),
                    Code = code,
                    Description = description,
                    BusinessCategory = ToCell(row.Cells["BusinessCategory"].Value),
                    TaxRate = ToDecimal(row.Cells["TaxRate"].Value, 18m),
                    CGSTRate = ToDecimal(row.Cells["CGSTRate"].Value, 9m),
                    SGSTRate = ToDecimal(row.Cells["SGSTRate"].Value, 9m),
                    IGSTRate = ToDecimal(row.Cells["IGSTRate"].Value, 18m),
                    IsDefault = ToBool(row.Cells["IsDefault"].Value),
                    IsActive = row.Cells["IsActive"].Value == null || row.Cells["IsActive"].Value == DBNull.Value
                        ? true
                        : ToBool(row.Cells["IsActive"].Value),
                    Notes = ToCell(row.Cells["Notes"].Value)
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
                BorderStyle = BorderStyle.FixedSingle
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
            button.FlatAppearance.BorderColor = DS.Slate300;
            button.FlatAppearance.MouseOverBackColor = bg == Color.White ? DS.Slate50 : ControlPaint.Light(bg);
            button.FlatAppearance.MouseDownBackColor = bg == Color.White ? DS.Slate100 : ControlPaint.Dark(bg);
            DS.Rounded(button, 8);
            return button;
        }

        private Panel BuildModernSettingsHeader()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 74, BackColor = Color.White, Padding = new Padding(28, 12, 28, 10) };
            header.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Slate200))
                    e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };

            Label title = new Label
            {
                Text = "Settings",
                Location = new Point(28, 14),
                Size = new Size(260, 26),
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = DS.Slate900
            };
            Label breadcrumb = new Label
            {
                Text = "Home  >  Settings",
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(header.Width - 360, 19),
                Size = new Size(150, 22),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = DS.Slate500,
                TextAlign = ContentAlignment.MiddleRight
            };
            Panel avatar = new Panel
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
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
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Size = new Size(150, 42),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = DS.Slate900
            };
            header.Resize += (s, e) =>
            {
                user.Location = new Point(header.ClientSize.Width - 178, 16);
                avatar.Location = new Point(user.Left - 48, 16);
                breadcrumb.Location = new Point(avatar.Left - 176, 21);
            };
            header.Controls.Add(title);
            header.Controls.Add(breadcrumb);
            header.Controls.Add(avatar);
            header.Controls.Add(user);
            return header;
        }

        private Panel BuildModernActionBar(params Button[] buttons)
        {
            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = DS.BgPage, Padding = new Padding(28, 10, 28, 10) };
            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 560,
                BackColor = DS.BgPage,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            foreach (Button button in buttons.Reverse())
            {
                button.Height = 36;
                button.Margin = new Padding(10, 0, 0, 0);
                flow.Controls.Add(button);
            }
            _lblStatus.Dock = DockStyle.Fill;
            _lblStatus.Margin = Padding.Empty;
            _lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            toolbar.Controls.Add(flow);
            toolbar.Controls.Add(_lblStatus);
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
            wrapper.Size = new Size(560, height);
            wrapper.Margin = new Padding(0, 0, 14, 14);
            wrapper.Tag = "settings-card";
            body.Controls.Add(new Label
            {
                Text = title,
                Location = new Point(60, 2),
                Size = new Size(410, 24),
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = DS.Slate900
            });
            body.Controls.Add(new Label
            {
                Text = subtitle,
                Location = new Point(60, 29),
                Size = new Size(420, 38),
                Font = new Font("Segoe UI", 8.7f),
                ForeColor = DS.Slate500
            });
            Panel icon = ModernIconSystem.EmptyStateIcon(ModernIconSystem.KindForTitle(title), 44, DS.Indigo50, DS.Primary600);
            icon.Location = new Point(0, 2);
            body.Controls.Add(icon);
            Panel content = new Panel
            {
                Location = new Point(0, 76),
                Size = new Size(530, Math.Max(90, height - 108)),
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            body.Resize += (s, e) =>
            {
                int availableWidth = Math.Max(280, body.ClientSize.Width);
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

            int viewportWidth = _generalCanvas.Parent == null ? GeneralCanvasWidth : _generalCanvas.Parent.ClientSize.Width;
            int canvasWidth = Math.Min(GeneralCanvasWidth, Math.Max(420, viewportWidth - 42));
            _generalCanvas.Width = canvasWidth;
            _generalFlow.Width = canvasWidth;
            int columns = canvasWidth >= 1220 ? 3 : (canvasWidth >= 820 ? 2 : 1);
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
                card.Width = Math.Max(390, cardWidth);
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
            _generalFlow.ResumeLayout(false);

            int contentHeight = columnHeights.Length == 0 ? 0 : columnHeights.Max();
            if (contentHeight > 0)
                contentHeight -= gap;
            _generalFlow.Height = contentHeight;
            _generalCanvas.Height = Math.Max(_generalCanvas.Parent == null ? 0 : _generalCanvas.Parent.ClientSize.Height - 40, _generalFlow.Height + 32);
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
            btnExtend.FlatAppearance.BorderColor = DS.Slate200;
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
            string[] pageKeys =
            {
                "Dashboard",
                "QuotationAnalysis",
                "InvoiceAnalysis",
                "JobAnalysis",
                "InventoryAnalysis",
                "PurchaseAnalysis"
            };

            int x = 0;
            int y = 0;
            int availableWidth = Math.Max(300, parent.ClientSize.Width);
            foreach (string pageKey in pageKeys)
            {
                Button button = MakeBtn("Reset " + pageKey.Replace("Analysis", " Analysis"), InfoBlue, 174);
                if (x > 0 && x + button.Width > availableWidth)
                {
                    x = 0;
                    y += 40;
                }
                button.Location = new Point(x, y);
                button.Click += (s, e) => ResetLayout(pageKey);
                parent.Controls.Add(button);
                x += 188;
            }

            Button resetAll = MakeBtn("Reset all layouts", SaveGreen, 180);
            resetAll.Location = new Point(0, y + 42);
            resetAll.Click += (s, e) =>
            {
                foreach (string pageKey in pageKeys)
                    ResetLayout(pageKey);
                _lblStatus.Text = "All card layouts reset to default.";
            };
            parent.Controls.Add(resetAll);
            parent.Height = resetAll.Bottom + 4;
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

            Button btnRestoreFile = MakeBtn("Restore File", Color.FromArgb(220, 38, 38), 124);
            btnRestoreFile.Location = new Point(140, 78);
            btnRestoreFile.Click += async (s, e) => await RestoreFromFileAsync();
            parent.Controls.Add(btnRestoreFile);

            Button btnOpenFolder = MakeBtn("Open Folder", InfoBlue, 124);
            btnOpenFolder.Location = new Point(280, 78);
            btnOpenFolder.Click += (s, e) => OpenBackupFolder();
            parent.Controls.Add(btnOpenFolder);

            Button btnCloudBackup = MakeBtn("Cloud Backup", Color.FromArgb(79, 70, 229), 124);
            btnCloudBackup.Location = new Point(420, 78);
            btnCloudBackup.Click += async (s, e) => await CreateCloudBackupAsync();
            parent.Controls.Add(btnCloudBackup);
            parent.Resize += (s, e) =>
            {
                int gap = 10;
                int buttonWidth = Math.Max(92, (parent.ClientSize.Width - (gap * 3)) / 4);
                btnBackupNow.SetBounds(0, 78, buttonWidth, 34);
                btnRestoreFile.SetBounds(btnBackupNow.Right + gap, 78, buttonWidth, 34);
                btnOpenFolder.SetBounds(btnRestoreFile.Right + gap, 78, buttonWidth, 34);
                btnCloudBackup.SetBounds(btnOpenFolder.Right + gap, 78, buttonWidth, 34);
            };

            parent.Controls.Add(new Label
            {
                Text = "Restore first creates a safety backup, disconnects active database sessions, restores the selected .bak, and returns the database to multi-user mode.",
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
            copyFingerprint.FlatAppearance.BorderColor = DS.Slate300;
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
            LicenseValidationResult result = new LicenseService().ValidateCurrentLicense();
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
            string fingerprint = new DeviceFingerprintService().GetFingerprintHash();
            Clipboard.SetText(fingerprint);
            _lblStatus.Text = "Device fingerprint copied for license issuance.";
            _lblStatus.ForeColor = SaveGreen;
        }

        private string BuildBackupSummary()
        {
            try
            {
                var latest = new BackupService().GetBackups().FirstOrDefault();
                if (latest == null)
                    return "No backups found. Backups are stored in " + BackupService.BackupRoot;

                return "Latest backup: " + latest.Name + " | " + latest.LastWriteTime.ToString("dd MMM yyyy HH:mm", CultureInfo.CurrentCulture);
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
                BackupResult result = await Task.Run(() => new BackupService().CreateDatabaseBackup("Manual backup from Settings"));
                SetBackupStatus(result.Success ? BuildBackupSummary() : "Backup failed: " + result.Message, result.Success ? SaveGreen : Color.Red);
                MessageBox.Show(result.Success ? "Backup created:\r\n" + result.BackupPath : "Backup failed:\r\n" + result.Message, result.Success ? "Backup Complete" : "Backup Failed", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SettingsForm.CreateBackupAsync", ex);
                SetBackupStatus("Backup failed: " + ex.Message, Color.Red);
            }
        }

        private async Task CreateCloudBackupAsync()
        {
            SetBackupStatus("Creating cloud backup...", DS.Slate700);
            try
            {
                IntegrationOperationResult result = await new CloudBackupIntegrationService().CreateAndUploadBackupAsync(System.Threading.CancellationToken.None);
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
            DialogResult confirm = MessageBox.Show(
                "Restore database from this backup?\r\n\r\n" + fileName + "\r\n\r\nCurrent data will be replaced. A safety backup will be created first.",
                "Confirm Restore",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (confirm != DialogResult.Yes)
                return;

            SetBackupStatus("Restoring database from " + fileName + "...", DS.Slate700);
            try
            {
                BackupResult result = await Task.Run(() => new BackupService().RestoreDatabaseBackup(backupPath, true));
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

        private void RunFreshStart()
        {
            using (Form dialog = new Form())
            {
                dialog.AutoScaleMode = AutoScaleMode.Dpi;
                dialog.Text = "Fresh Start Confirmation";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.ClientSize = new Size(520, 280);
                dialog.Font = new Font("Segoe UI", 9);

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
            System.Threading.Tasks.Task.Run(() => new CardLayoutService().ResetPageLayout(userId, pageKey));
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
                textBox.BorderStyle = BorderStyle.FixedSingle;
                textBox.BackColor = textBox.ReadOnly ? DS.Slate100 : Color.White;
            }
            else if (control is ComboBox comboBox)
            {
                comboBox.FlatStyle = FlatStyle.System;
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
                Font = new Font("Segoe UI", 9),
                BorderStyle = BorderStyle.FixedSingle,
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
                string url = _txtVersionCheckUrl == null ? string.Empty : _txtVersionCheckUrl.Text.Trim();
                if (string.IsNullOrWhiteSpace(url))
                {
                    MessageBox.Show(
                        "Version check URL is not configured.\r\nEnter the servoerp.in version endpoint above to enable update checks.",
                        "Check for updates",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                ConfigService.Set("App", "VersionCheckUrl", url);
                ConfigService.Set("App", "VersionCheckEnabled", _chkVersionCheckEnabled == null || _chkVersionCheckEnabled.Checked ? "true" : "false");

                if (_chkVersionCheckEnabled != null && !_chkVersionCheckEnabled.Checked)
                {
                    MessageBox.Show(
                        "Update notification settings were saved, but automatic checks are turned off.\r\nTurn on \"Check for updates automatically\" to show update banners at startup.",
                        "Update notifications",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                UpdateCheckResult result = await UpdateService.CheckForUpdatesAsync();

                if (result.IsUpdateAvailable)
                {
                    DialogResult install = MessageBox.Show(
                        "Version " + result.LatestVersion + " is available.\r\nCurrent version: " + result.CurrentVersion + ".\r\n\r\nInstall the update now?",
                        "Update available",
                        MessageBoxButtons.OKCancel,
                        MessageBoxIcon.Information);
                    if (install == DialogResult.OK)
                        await InstallUpdateAsync(result);
                }
                else
                {
                    MessageBox.Show(
                        BrandingService.AppName + " is up to date.\r\nCurrent version: " + result.CurrentVersion + ".",
                        "Check for updates",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SettingsForm.CheckVersionNowAsync", ex);
                MessageBox.Show(
                    BrandingService.AppName + " is up to date.\r\nCurrent version: " + ConfigService.GetAppVersion() + ".",
                    "Check for updates",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
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
                        string packagePath = await UpdateService.DownloadUpdatePackageAsync(result, progressReporter, cancelSource.Token);
                        progressForm.Close();
                        UpdateService.StartPackageUpdater(packagePath);
                        Application.Exit();
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


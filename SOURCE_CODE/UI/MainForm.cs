using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.AI;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.Services.Licensing;
using HVAC_Pro_Desktop.UI.Helpers;

namespace HVAC_Pro_Desktop.UI
{
    public partial class MainForm : BaseForm
    {
        private Panel _sidebar;
        private Panel _sidebarScroll;
        private Panel _content;
        private Panel _userStrip;
        private Panel _pnlUpdateBanner;
        private Panel _pnlLicenseBanner;
        private Label _lblUpdateMessage;
        private Label _lblLicenseMessage;
        private Button _btnDownloadUpdate;
        private Button _btnSupportCenter;
        private Button _btnAiCopilot;
        private AiAssistantForm _aiAssistantForm;
        private bool _hideUpdateBannerForSession;
        private UpdateCheckResult _latestUpdateResult;
        private readonly Dictionary<int, UserControl> _pageCache = new Dictionary<int, UserControl>();
        private readonly LinkedList<int> _pageUsage = new LinkedList<int>();
        private readonly Dictionary<int, Button> _navButtons = new Dictionary<int, Button>();
        private UserControl _transientPage;
        private int _currentIndex = -1;
        private readonly PersistentLayoutMemoryService _layoutMemory = new PersistentLayoutMemoryService();
        private const int MaxCachedPages = 19;
        private const int ClientsPageIndex = 1;
        private const int JobsPageIndex = 15;
        private const int RetiredServiceDeskPageIndex = 16;
        private const int MasterDataPageIndex = 17;
        private const int WhatsAppHubPageIndex = 18;

        private static readonly Color SbBg = Color.FromArgb(13, 42, 170);
        private static readonly Color SbBgDeep = Color.FromArgb(28, 93, 245);
        private static readonly Color SbActive = Color.FromArgb(55, 105, 255);
        private static readonly Color SbHover = Color.FromArgb(35, 86, 224);
        private static readonly Color SbText = Color.FromArgb(230, 238, 255);
        private static readonly Color SbMutedText = Color.FromArgb(166, 190, 255);
        private static readonly Color SbActiveText = Color.White;
        private static readonly Color SbAccent = DS.Primary500;
        private static readonly Color Border = Color.FromArgb(28, 80, 220);
        // Sidebar is intentionally word-based. Do not convert it to an icon-only rail.
        private const int SbWidth = 200;
        private const int CompactSbWidth = 155;
        private bool _compactShell;

        private static readonly (string label, string icon)[] NavItems =
        {
            ("Dashboard", "D"),
            ("Clients", "C"),
            ("Contracts", "A"),
            ("Invoices", "I"),
            ("Payments", "P"),
            ("SLA Dashboard", "S"),
            ("Quotations", "Q"),
            ("Reports", "R"),
            ("Settings", "T"),
            ("Vendors", "V"),
            ("Purchases", "B"),
            ("Inventory", "N"),
            ("Employees", "E"),
            ("Payroll", "Y"),
            ("Dispatch Center", "D"),
            ("Jobs", "J"),
            ("Retired", "K"),
            ("Master Data", "M"),
            ("WhatsApp Hub", "W"),
        };

        private static readonly int[] DashboardItems = { 0 };
        private static readonly int[] SalesItems = { 6, 3, 10, 4 };
        private static readonly int[] OperationsItems = { 14, 11, 1, 9, 15 };
        private static readonly int[] HrPayrollItems = { 12, 13 };
        private static readonly int[] DataComplianceItems = { 17, 2 };
        private static readonly int[] ReportsItems = { 7 };
        private static readonly int[] SettingsSupportItems = { 8, 18 };

        public MainForm()
        {
            InitializeComponent();
            AdjustForScreenSize();
        }

        private void InitializeComponent()
        {
            RefreshWindowTitle();
            AutoScaleMode = AutoScaleMode.Dpi;
            _compactShell = IsCompactWorkArea();
            Size = _compactShell ? new Size(1180, 700) : new Size(1440, 860);
            MinimumSize = new Size(1024, 600);
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            BackColor = DS.BgPage;
            Font = new Font("Segoe UI", _compactShell ? 8.25f : 9f);
            try
            {
                var embeddedIcon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (embeddedIcon != null)
                    this.Icon = embeddedIcon;
                else
                {
                    string iconPath = System.IO.Path.Combine(
                        System.IO.Path.GetDirectoryName(Application.ExecutablePath), "app.ico");
                    if (System.IO.File.Exists(iconPath))
                        this.Icon = new System.Drawing.Icon(iconPath);
                }
            }
            catch { }
            BuildLayout();
            UIHelper.ApplyInputStyles(Controls);
            UIHelper.ApplyGlobalScrollAndResize(this);
            Activated += (s, e) => { RefreshWindowTitle(); EnsureSessionOrClose(); };
            Shown += (s, e) =>
            {
                ApplyResponsiveShell();
                ApplyShellLayoutMemory();
                EnsureSessionOrClose();
                ApplyRBAC();
                AppWarmupService.StartBackgroundWarmup();
                BeginVersionCheck();
                RefreshLicenseBanner();
            };
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyResponsiveShell();
            AdjustForScreenSize();
        }

        private void BuildLayout()
        {
            _content = new Panel { Dock = DockStyle.None, BackColor = DS.BgPage };
            Controls.Add(_content);

            _sidebar = new Panel { Dock = DockStyle.Left, Width = GetSidebarWidth(), BackColor = SbBg };
            _sidebar.Paint += (s, e) =>
            {
                Rectangle bounds = _sidebar.ClientRectangle;
                if (bounds.Width > 0 && bounds.Height > 0)
                {
                    using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(bounds, SbBg, SbBgDeep, 75f))
                        e.Graphics.FillRectangle(brush, bounds);
                }
            };
            _sidebarScroll = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = SbBg,
                AutoScroll = true
            };
            _userStrip = BuildUserStrip();
            _sidebar.Controls.Add(_sidebarScroll);
            BuildSidebar();
            Controls.Add(_sidebar);

            BuildUpdateBanner();
            BuildLicenseBanner();
            BuildAiCopilotLauncher();
            BuildSupportLauncher();
        }

        private void AdjustForScreenSize()
        {
            Rectangle screen = Screen.FromControl(this).Bounds;
            bool small = LayoutScaler.IsLaptopFitModeEnabled(this) || screen.Width < 1400 || ClientSize.Width < 1220 || ClientSize.Height < 720;
            _compactShell = small;

            if (_sidebar != null)
                _sidebar.Width = small ? CompactSbWidth : SbWidth;

            if (_content != null)
            {
                _content.Dock = DockStyle.None;
                _content.Left = _sidebar == null ? 0 : _sidebar.Width;
                _content.Top = 0;
                _content.Width = Math.Max(0, ClientSize.Width - _content.Left);
                _content.Height = ClientSize.Height;
                _content.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            }

            foreach (Button button in _navButtons.Values)
            {
                button.Height = small ? 28 : 32;
                button.Font = new Font(button.Font.FontFamily, small ? 8.3f : 9.4f, button.Font.Style);
                button.Padding = new Padding(small ? 4 : 8, 0, 0, 0);
            }

            MinimumSize = new Size(1024, 600);
            LayoutAiCopilotLauncher();
            LayoutSupportLauncher();
        }

        private void BuildAiCopilotLauncher()
        {
            _btnAiCopilot = DS.PrimaryBtn("AI Copilot", 128, 38);
            _btnAiCopilot.UseMnemonic = false;
            _btnAiCopilot.Name = "btnAiCopilot";
            _btnAiCopilot.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            ModernIconSystem.AddButtonIcon(_btnAiCopilot, ModernIconKind.Service);
            _btnAiCopilot.Click += (s, e) => OpenAiCopilot();
            Controls.Add(_btnAiCopilot);
            _btnAiCopilot.BringToFront();
            LayoutAiCopilotLauncher();
        }

        private void BuildSupportLauncher()
        {
            _btnSupportCenter = DS.PrimaryBtn("Help & Support", 148, 38);
            _btnSupportCenter.UseMnemonic = false;
            _btnSupportCenter.Name = "btnHelpSupport";
            _btnSupportCenter.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            ModernIconSystem.AddButtonIcon(_btnSupportCenter, ModernIconKind.Service);
            _btnSupportCenter.Click += (s, e) =>
            {
                _btnSupportCenter.Visible = false;
                try
                {
                    using (var dialog = new SupportCenterDialog())
                        dialog.ShowDialog(this);
                }
                finally
                {
                    _btnSupportCenter.Visible = true;
                    LayoutSupportLauncher();
                }
            };
            Controls.Add(_btnSupportCenter);
            _btnSupportCenter.BringToFront();
            LayoutSupportLauncher();
        }

        private void LayoutSupportLauncher()
        {
            if (_btnSupportCenter == null)
                return;

            int bottomOffset = 22;
            if (_pnlUpdateBanner != null && _pnlUpdateBanner.Visible)
                bottomOffset += _pnlUpdateBanner.Height;
            if (_pnlLicenseBanner != null && _pnlLicenseBanner.Visible)
                bottomOffset += _pnlLicenseBanner.Height;

            _btnSupportCenter.Left = Math.Max((_sidebar == null ? 0 : _sidebar.Width) + 18, ClientSize.Width - _btnSupportCenter.Width - 24);
            _btnSupportCenter.Top = Math.Max(20, ClientSize.Height - _btnSupportCenter.Height - bottomOffset);
            _btnSupportCenter.BringToFront();
        }

        private void LayoutAiCopilotLauncher()
        {
            if (_btnAiCopilot == null)
                return;

            int bottomOffset = 68;
            if (_pnlUpdateBanner != null && _pnlUpdateBanner.Visible)
                bottomOffset += _pnlUpdateBanner.Height;
            if (_pnlLicenseBanner != null && _pnlLicenseBanner.Visible)
                bottomOffset += _pnlLicenseBanner.Height;

            _btnAiCopilot.Left = Math.Max((_sidebar == null ? 0 : _sidebar.Width) + 18, ClientSize.Width - _btnAiCopilot.Width - 24);
            _btnAiCopilot.Top = Math.Max(20, ClientSize.Height - _btnAiCopilot.Height - bottomOffset);
            _btnAiCopilot.BringToFront();
        }

        private void OpenAiCopilot()
        {
            try
            {
                if (_aiAssistantForm == null || _aiAssistantForm.IsDisposed)
                {
                    _aiAssistantForm = new AiAssistantForm(GetCurrentAiModuleName);
                    _aiAssistantForm.Owner = this;
                    _aiAssistantForm.FormClosed += (s, e) => _aiAssistantForm = null;
                }

                Rectangle work = Screen.FromControl(this).WorkingArea;
                int width = Math.Min(460, Math.Max(390, work.Width / 3));
                _aiAssistantForm.Width = width;
                _aiAssistantForm.Height = Math.Min(work.Height - 40, Math.Max(600, Height - 60));
                _aiAssistantForm.Left = Math.Max(work.Left, work.Right - _aiAssistantForm.Width - 18);
                _aiAssistantForm.Top = Math.Max(work.Top + 18, Top + 28);
                _aiAssistantForm.Show(this);
                _aiAssistantForm.BringToFront();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("MainForm.OpenAiCopilot", ex);
                MessageBox.Show(this, "Unable to open ServoERP Copilot: " + ex.Message, BrandingService.WindowTitle("AI Copilot"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string GetCurrentAiModuleName()
        {
            if (_currentIndex >= 0 && _currentIndex < NavItems.Length)
                return NavItems[_currentIndex].label;
            return "Dashboard";
        }

        private void BuildUpdateBanner()
        {
            _pnlUpdateBanner = new Panel
            {
                Name = "pnlUpdateBanner",
                Dock = DockStyle.Bottom,
                Height = 38,
                BackColor = DS.Amber50,
                Visible = false
            };
            _pnlUpdateBanner.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(253, 230, 138)))
                    e.Graphics.DrawLine(pen, 0, 0, _pnlUpdateBanner.Width, 0);
            };

            Panel indicatorWrap = new Panel { Dock = DockStyle.Left, Width = 30, BackColor = Color.Transparent };
            Panel dot = new Panel { Size = new Size(8, 8), BackColor = DS.Amber500 };
            dot.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (Brush brush = new SolidBrush(dot.BackColor))
                    e.Graphics.FillEllipse(brush, 0, 0, dot.Width - 1, dot.Height - 1);
            };
            indicatorWrap.Controls.Add(dot);
            indicatorWrap.Resize += (s, e) =>
            {
                dot.Location = new Point(14, Math.Max(0, (indicatorWrap.Height - dot.Height) / 2));
            };

            FlowLayoutPanel actionFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 312,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 4, 12, 0)
            };

            _btnDownloadUpdate = new Button
            {
                Text = "Install update",
                Width = 138,
                Height = 24,
                FlatStyle = FlatStyle.Flat,
                BackColor = DS.Primary600,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Margin = new Padding(0, 2, 8, 0),
                Visible = false
            };
            _btnDownloadUpdate.FlatAppearance.BorderSize = 0;
            _btnDownloadUpdate.Click += (s, e) => DownloadAndInstallUpdate();

            Button btnRemindLater = new Button
            {
                Text = "Remind me later",
                Width = 122,
                Height = 24,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(120, 53, 15),
                Font = new Font("Segoe UI", 8.5f),
                Margin = new Padding(0, 2, 8, 0)
            };
            btnRemindLater.FlatAppearance.BorderColor = Color.FromArgb(253, 230, 138);
            btnRemindLater.FlatAppearance.BorderSize = 1;
            btnRemindLater.Click += (s, e) =>
            {
                try
                {
                    ConfigService.Set("App", "UpdateDismissedUntil", DateTime.Now.AddHours(24).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                    _pnlUpdateBanner.Visible = false;
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("UpdateBanner.RemindLater", ex);
                }
            };

            Button btnClose = new Button
            {
                Text = "x",
                Width = 28,
                Height = 24,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = DS.Slate500,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Margin = new Padding(0, 2, 0, 0)
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) =>
            {
                _hideUpdateBannerForSession = true;
                _pnlUpdateBanner.Visible = false;
            };

            actionFlow.Controls.Add(_btnDownloadUpdate);
            actionFlow.Controls.Add(btnRemindLater);
            actionFlow.Controls.Add(btnClose);

            _lblUpdateMessage = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(120, 53, 15),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0)
            };

            _pnlUpdateBanner.Controls.Add(_lblUpdateMessage);
            _pnlUpdateBanner.Controls.Add(actionFlow);
            _pnlUpdateBanner.Controls.Add(indicatorWrap);
            Controls.Add(_pnlUpdateBanner);
            _pnlUpdateBanner.BringToFront();
        }

        private void BuildLicenseBanner()
        {
            _pnlLicenseBanner = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.FromArgb(254, 242, 242),
                Visible = false
            };
            _lblLicenseMessage = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(153, 27, 27),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0)
            };
            Button renew = new Button
            {
                Text = "Renew license",
                Dock = DockStyle.Right,
                Width = 132,
                FlatStyle = FlatStyle.Flat,
                BackColor = DS.Primary600,
                ForeColor = Color.White
            };
            renew.FlatAppearance.BorderSize = 0;
            renew.Click += (s, e) => NavigateTo("Settings");
            _pnlLicenseBanner.Controls.Add(_lblLicenseMessage);
            _pnlLicenseBanner.Controls.Add(renew);
            Controls.Add(_pnlLicenseBanner);
            _pnlLicenseBanner.BringToFront();
        }

        private void RefreshLicenseBanner()
        {
            if (_pnlLicenseBanner == null || _lblLicenseMessage == null)
                return;

            LicenseValidationResult result = new LicenseService().ValidateCurrentLicense();
            if (result.IsFrozen)
            {
                _lblLicenseMessage.Text = "Your ServoERP license has expired. Renew to continue business operations.";
                _pnlLicenseBanner.Visible = true;
                return;
            }

            LicenseSnapshot snapshot = result.Snapshot;
            if (snapshot != null && snapshot.Status == LicenseStatus.Warning)
            {
                _lblLicenseMessage.Text = snapshot.StatusMessage;
                _pnlLicenseBanner.BackColor = DS.Amber50;
                _lblLicenseMessage.ForeColor = Color.FromArgb(120, 53, 15);
                _pnlLicenseBanner.Visible = true;
                return;
            }

            _pnlLicenseBanner.Visible = false;
        }

        private void BuildSidebar()
        {
            _sidebar.Controls.Clear();
            _navButtons.Clear();
            if (_sidebar != null)
                _sidebar.Width = GetSidebarWidth();
            _userStrip = BuildUserStrip();

            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 8, BackColor = Color.Transparent };
            footer.Controls.Add(new Label
            {
                Text = string.Empty,
                Font = new Font("Segoe UI", 7),
                ForeColor = SbMutedText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            });

            Panel bottomRail = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = footer.Height + _userStrip.Height,
                BackColor = Color.Transparent
            };
            bottomRail.Controls.Add(footer);
            bottomRail.Controls.Add(_userStrip);

            _sidebar.Controls.Add(bottomRail);
            _sidebar.Controls.Add(_sidebarScroll);

            _sidebarScroll.Controls.Clear();
            _sidebarScroll.Controls.Add(new Panel { Dock = DockStyle.Top, Height = _compactShell ? 4 : 6, BackColor = Color.Transparent });
            AddNavGroup("SETTINGS & SUPPORT", SettingsSupportItems, false);
            AddNavGroup("REPORTS", ReportsItems, true);
            AddNavGroup("DATA & COMPLIANCE", DataComplianceItems, true);
            AddNavGroup("HR & PAYROLL", HrPayrollItems, true);
            AddNavGroup("OPERATIONS", OperationsItems, true);
            AddNavGroup("SALES", SalesItems, true);
            AddNavGroup("DASHBOARD", DashboardItems, false);
            _sidebarScroll.Controls.Add(MakeSidebarBrandHeader());
            _sidebarScroll.AutoScrollPosition = new Point(0, 0);
        }

        private Panel MakeSidebarBrandHeader()
        {
            int headerHeight = _compactShell ? 48 : 56;
            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = headerHeight,
                BackColor = Color.Transparent,
                Padding = new Padding(_compactShell ? 12 : 18, _compactShell ? 10 : 14, 12, 6)
            };

            PictureBox logo = new PictureBox
            {
                Size = new Size(_compactShell ? 26 : 32, _compactShell ? 26 : 32),
                Location = new Point(header.Padding.Left, _compactShell ? 10 : 12),
                SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Color.Transparent
            };
            Image sidebarLogo = LoadSidebarLogoImage();
            logo.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                Rectangle circle = new Rectangle(0, 0, logo.Width - 1, logo.Height - 1);
                using (Brush brush = new SolidBrush(Color.FromArgb(52, 91, 224)))
                    e.Graphics.FillEllipse(brush, circle);
                if (sidebarLogo != null)
                {
                    int drawSize = Math.Max(18, logo.Width - 8);
                    Rectangle target = new Rectangle((logo.Width - drawSize) / 2, (logo.Height - drawSize) / 2, drawSize, drawSize);
                    e.Graphics.DrawImage(sidebarLogo, target);
                }
            };

            Label name = new Label
            {
                Text = BrandingService.AppName,
                Location = new Point(logo.Right + 10, _compactShell ? 9 : 12),
                Size = new Size(Math.Max(60, GetSidebarWidth() - logo.Right - 24), _compactShell ? 28 : 30),
                Font = new Font("Segoe UI", _compactShell ? 10f : 11.5f, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                BackColor = Color.Transparent
            };

            header.Controls.Add(logo);
            header.Controls.Add(name);
            return header;
        }

        private Image LoadSidebarLogoImage()
        {
            try
            {
                Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (icon != null)
                    return icon.ToBitmap();
            }
            catch
            {
            }

            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates =
            {
                System.IO.Path.Combine(basePath, "Resources", "Branding", "app_logo_sidebar.png"),
                System.IO.Path.Combine(basePath, "app_logo_sidebar.png"),
                System.IO.Path.Combine(@"C:\HVAC_PRO_MSE\SOURCE_CODE\Resources\Branding", "app_logo_sidebar.png")
            };

            foreach (string path in candidates)
            {
                try
                {
                    if (!System.IO.File.Exists(path))
                        continue;

                    using (var stream = System.IO.File.OpenRead(path))
                    using (var image = Image.FromStream(stream))
                        return new Bitmap(image);
                }
                catch
                {
                }
            }

            Bitmap fallback = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(fallback))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (Brush brush = new SolidBrush(SbActive))
                    g.FillEllipse(brush, 0, 0, 31, 31);
                using (Font font = new Font("Segoe UI", 10f, FontStyle.Bold))
                using (Brush brush = new SolidBrush(Color.White))
                    g.DrawString("SE", font, brush, new PointF(5, 7));
            }
            return fallback;
        }

        private Button MakeUploadDocumentButton()
        {
            var button = new Button
            {
                Text = "Upload Company PDF",
                Dock = DockStyle.Top,
                Height = _compactShell ? 32 : 38,
                BackColor = DS.White,
                ForeColor = DS.Slate700,
                Font = new Font("Segoe UI", _compactShell ? 8f : 8.5f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                Padding = new Padding(10, 0, 10, 0),
                TabStop = false
            };
            button.FlatAppearance.BorderColor = DS.Border;
            button.FlatAppearance.BorderSize = 1;
            button.Click += (s, e) => UploadCompanyQuotationDocument();
            DS.Rounded(button, 10);
            return button;
        }

        private void UploadCompanyQuotationDocument()
        {
            try
            {
                string path = DocumentTemplateService.UploadQuotationTemplateWithDialog(this);
                if (string.IsNullOrWhiteSpace(path))
                    return;
                MessageBox.Show("Company quotation PDF uploaded.\n\n" + path, BrandingService.WindowTitle("Documents"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Documents"), "Uploading company quotation PDF", ex);
            }
        }

        private Button MakeConnectButton()
        {
            var button = new Button
            {
                Text = "Quick Action",
                Dock = DockStyle.Top,
                Height = _compactShell ? 32 : 36,
                BackColor = SbActive,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", _compactShell ? 8f : 8.5f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                Padding = new Padding(10, 0, 10, 0),
                TabStop = false
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(35, 104, 148);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(17, 57, 84);
            button.Click += (s, e) => NavigateTo(JobsPageIndex);
            DS.Rounded(button, 6);
            return button;
        }

        private void AddNavGroup(string label, int[] itemIndexes, bool showTopLine)
        {
            var visibleItems = new List<int>();
            foreach (int itemIndex in itemIndexes)
            {
                if (CanViewNavItem(itemIndex))
                    visibleItems.Add(itemIndex);
            }

            if (visibleItems.Count == 0)
                return;

            for (int i = visibleItems.Count - 1; i >= 0; i--)
                _sidebarScroll.Controls.Add(MakeNavBtn(visibleItems[i]));
            _sidebarScroll.Controls.Add(MakeDivider(label, showTopLine));
        }

        private Control MakeDivider(string text, bool showTopLine)
        {
            int height = _compactShell ? 24 : 28;
            Panel wrapper = new Panel
            {
                Dock = DockStyle.Top,
                Height = height,
                BackColor = Color.Transparent,
            };
            if (showTopLine)
            {
                Panel line = new Panel
                {
                    Height = 1,
                    Width = Math.Max(1, GetSidebarWidth() - (_compactShell ? 34 : 46)),
                    Location = new Point(_compactShell ? 17 : 23, 2),
                    BackColor = Color.FromArgb(54, 103, 222)
                };
                wrapper.Controls.Add(line);
            }

            Label label = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", _compactShell ? 7.5f : 8.2f, FontStyle.Bold),
                ForeColor = SbMutedText,
                Location = new Point(_compactShell ? 16 : 22, showTopLine ? 7 : 4),
                Size = new Size(Math.Max(80, GetSidebarWidth() - (_compactShell ? 28 : 40)), 18),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                AutoEllipsis = true,
                UseMnemonic = false
            };
            wrapper.Controls.Add(label);
            return wrapper;
        }

        private Button MakeNavBtn(int index)
        {
            var (label, icon) = NavItems[index];
            var btn = new Button
            {
                Text = string.Empty,
                AccessibleName = label,
                Dock = DockStyle.Top,
                Height = _compactShell ? 28 : 32,
                BackColor = Color.Transparent,
                ForeColor = SbText,
                Font = new Font("Segoe UI", _compactShell ? 8.3f : 9.4f),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Tag = index,
                TabStop = false,
                Margin = new Padding(0)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.BorderColor = Color.FromArgb(0, 0, 0, 0);
            btn.FlatAppearance.MouseOverBackColor = SbHover;
            btn.FlatAppearance.MouseDownBackColor = SbActive;
            btn.Paint += (s, e) => PaintSidebarNavButton((Button)s, e);
            btn.Click += (s, e) => NavigateTo((int)((Button)s).Tag);
            _navButtons[index] = btn;
            return btn;
        }

        private void PaintSidebarNavButton(Button btn, PaintEventArgs e)
        {
            int index = btn.Tag is int ? (int)btn.Tag : -1;
            string label = index >= 0 && index < NavItems.Length ? NavItems[index].label : btn.AccessibleName ?? string.Empty;
            bool active = btn.BackColor.ToArgb() == SbActive.ToArgb();
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Rectangle pill = new Rectangle(_compactShell ? 8 : 10, 3, btn.Width - (_compactShell ? 16 : 20), btn.Height - 6);
            if (active)
            {
                using (var path = DS.RoundedRect(pill, 8))
                using (Brush brush = new SolidBrush(SbActive))
                    e.Graphics.FillPath(brush, path);
            }
            else if (btn.ClientRectangle.Contains(btn.PointToClient(Cursor.Position)))
            {
                using (var path = DS.RoundedRect(pill, 8))
                using (Brush brush = new SolidBrush(SbHover))
                    e.Graphics.FillPath(brush, path);
            }

            Color fg = active ? Color.White : SbText;
            int iconX = _compactShell ? 17 : 20;
            int textX = _compactShell ? 43 : 51;
            Rectangle iconRect = new Rectangle(iconX, 0, 20, btn.Height);
            Rectangle textRect = new Rectangle(textX, 0, btn.Width - textX - 12, btn.Height);
            TextRenderer.DrawText(
                e.Graphics,
                GetNavGlyph(index),
                new Font("Segoe MDL2 Assets", _compactShell ? 9.6f : 11f),
                iconRect,
                Color.FromArgb(active ? 255 : 210, fg),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(
                e.Graphics,
                label,
                new Font("Segoe UI", _compactShell ? 8.15f : 9.5f, active ? FontStyle.Bold : FontStyle.Regular),
                textRect,
                fg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private string GetNavGlyph(int index)
        {
            switch (index)
            {
                case 0: return "\uE80F"; // Dashboard
                case 1: return "\uE716"; // Clients
                case 2: return "\uE8A5"; // Contracts
                case 3: return "\uE8A5"; // Invoices
                case 4: return "\uE8C7"; // Payments
                case 6: return "\uE721"; // Quotations
                case 7: return "\uE9D2"; // Reports
                case 8: return "\uE713"; // Settings
                case 9: return "\uE716"; // Vendors
                case 10: return "\uE7BF"; // Purchases
                case 11: return "\uE8F1"; // Inventory
                case 12: return "\uE77B"; // Employees
                case 13: return "\uE8C7"; // Payroll
                case 14: return "\uE8A5"; // Dispatch
                case 15: return "\uE8F1"; // Jobs
                case 16: return "\uE90F"; // Retired
                case 17: return "\uE8A5"; // Master Data
                case 18: return "\uE717"; // WhatsApp Hub
                default: return "\uE10F";
            }
        }

        public void ApplyRBAC()
        {
            BuildSidebar();

            int target = CanViewNavItem(_currentIndex) ? _currentIndex : GetFirstAllowedPage();
            if (target < 0)
            {
                _content.Controls.Clear();
                _content.Controls.Add(BuildNoAccessPage());
                return;
            }

            NavigateTo(target);
        }

        public void NavigateTo(string pageKey)
        {
            try
            {
                NavigateTo(MapPageKey(pageKey));
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("NavigateTo(" + pageKey + ")", ex);
            }
        }

        public void ReloadPageByKey(string pageKey)
        {
            int index = MapPageKey(pageKey);

            if (_pageCache.TryGetValue(index, out UserControl cachedPage))
            {
                _pageCache.Remove(index);
                LinkedListNode<int> node = _pageUsage.Find(index);
                if (node != null)
                    _pageUsage.Remove(node);

                if (cachedPage != null)
                {
                    if (_content.Controls.Contains(cachedPage))
                        _content.Controls.Remove(cachedPage);
                    cachedPage.Dispose();
                }
            }

            if (_currentIndex == index)
                NavigateTo(index);
        }

        public void ClearCachedPagesExceptCurrent()
        {
            var keys = new List<int>(_pageCache.Keys);
            foreach (int index in keys)
            {
                if (index == _currentIndex)
                    continue;

                if (!_pageCache.TryGetValue(index, out UserControl cachedPage))
                    continue;

                _pageCache.Remove(index);
                LinkedListNode<int> node = _pageUsage.Find(index);
                if (node != null)
                    _pageUsage.Remove(node);

                cachedPage?.Dispose();
            }
        }

        public void NavigateTo(int index)
        {
            EnsureSessionOrClose();
            if (!SessionManager.IsLoggedIn || !CanViewNavItem(index))
                return;

            if (_currentIndex == index && _pageCache.ContainsKey(index))
                return;

            UpdateNavState(index);

            try
            {
                _content.SuspendLayout();
                ClearTransientPage();

                UserControl page;
                if (!_pageCache.TryGetValue(index, out page) || page == null || page.IsDisposed)
                {
                    page = CreatePage(index);
                    page.Dock = DockStyle.Fill;
                    page.Visible = false;
                    DS.ApplyTheme(page);
                    LayoutScaler.ApplyGlobalScale(page);
                    if (!(page is DeferredPageControl))
                        LayoutScaler.ScaleControl(page);
                    LayoutScaler.ApplyDisplayFit(page);
                    UIHelper.ApplyGlobalScrollAndResize(page);
                    ResizeCursorHelper.Apply(page);
                    _pageCache[index] = page;
                    _content.Controls.Add(page);
                }

                foreach (Control control in _content.Controls)
                    control.Visible = false;

                page.Visible = true;
                page.BringToFront();
                if (page is PurchaseForm purchasePage)
                    purchasePage.ApplyNavigationRequest();
                if (page is ContractManagementForm contractPage)
                    contractPage.ApplyNavigationRequest();
                if (page is ReportForm reportPage)
                    reportPage.ApplyNavigationRequest();
                TouchPage(index);
                TrimPageCache(index);
                _currentIndex = index;
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("NavigateTo(" + index + ")", ex);
                _content.Controls.Clear();
                _pageCache.Remove(index);
                _content.Controls.Add(BuildErrorPage(index, ex));
            }
            finally
            {
                _content.ResumeLayout();
            }
        }

        public void NavigateToJobDetail(int jobId)
        {
            EnsureSessionOrClose();
            if (!SessionManager.IsLoggedIn || !CanViewNavItem(JobsPageIndex) || jobId <= 0)
                return;

            UpdateNavState(JobsPageIndex);

            try
            {
                _content.SuspendLayout();
                ClearTransientPage();

                foreach (Control control in _content.Controls)
                    control.Visible = false;

                var page = new JobDetailPage
                {
                    Dock = DockStyle.Fill,
                    JobId = jobId,
                    OnBackToJobs = NavigateBackToJobs
                };
                page.LoadJob();
                DS.ApplyTheme(page);
                LayoutScaler.ApplyGlobalScale(page);
                LayoutScaler.ScaleControl(page);
                LayoutScaler.ApplyDisplayFit(page);
                UIHelper.ApplyGlobalScrollAndResize(page);
                ResizeCursorHelper.Apply(page);

                _transientPage = page;
                _content.Controls.Add(page);
                page.Visible = true;
                page.BringToFront();
                TouchPage(JobsPageIndex);
                _currentIndex = -1;
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("NavigateToJobDetail(" + jobId + ")", ex);
                _content.Controls.Clear();
                ClearTransientPage();
                _content.Controls.Add(BuildErrorPage(JobsPageIndex, ex));
            }
            finally
            {
                _content.ResumeLayout();
            }
        }

        public void NavigateToClientDetail(int clientId)
        {
            NavigateToClientDetail(clientId, 0);
        }

        public void NavigateToClientSite(int siteId)
        {
            EnsureSessionOrClose();
            if (!SessionManager.IsLoggedIn || !CanViewNavItem(ClientsPageIndex) || siteId <= 0)
                return;

            try
            {
                var site = new SiteService().GetById(siteId);
                if (site == null || site.ClientID <= 0)
                {
                    NavigateTo(ClientsPageIndex);
                    return;
                }

                NavigateToClientDetail(site.ClientID, siteId);
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("NavigateToClientSite(" + siteId + ")", ex);
                NavigateTo(ClientsPageIndex);
            }
        }

        public void NavigateToClientDetail(int clientId, int highlightSiteId)
        {
            EnsureSessionOrClose();
            if (!SessionManager.IsLoggedIn || !CanViewNavItem(ClientsPageIndex) || clientId <= 0)
                return;

            UpdateNavState(ClientsPageIndex);

            try
            {
                _content.SuspendLayout();
                ClearTransientPage();

                foreach (Control control in _content.Controls)
                    control.Visible = false;

                var page = new ClientDetailPage
                {
                    Dock = DockStyle.Fill,
                    ClientId = clientId,
                    HighlightSiteId = highlightSiteId,
                    OnBackToClients = NavigateBackToClients
                };
                page.LoadClient();
                DS.ApplyTheme(page);
                LayoutScaler.ApplyGlobalScale(page);
                LayoutScaler.ScaleControl(page);
                LayoutScaler.ApplyDisplayFit(page);
                UIHelper.ApplyGlobalScrollAndResize(page);
                ResizeCursorHelper.Apply(page);

                _transientPage = page;
                _content.Controls.Add(page);
                page.Visible = true;
                page.BringToFront();
                TouchPage(ClientsPageIndex);
                _currentIndex = -1;
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("NavigateToClientDetail(" + clientId + ")", ex);
                _content.Controls.Clear();
                ClearTransientPage();
                _content.Controls.Add(BuildErrorPage(ClientsPageIndex, ex));
            }
            finally
            {
                _content.ResumeLayout();
            }
        }

        private void NavigateBackToClients(int selectedClientId)
        {
            if (_pageCache.TryGetValue(ClientsPageIndex, out UserControl cachedPage) && cachedPage is ClientManagementForm clients)
                clients.SelectClientFromNavigation(selectedClientId);

            _currentIndex = -1;
            NavigateTo(ClientsPageIndex);

            if (_pageCache.TryGetValue(ClientsPageIndex, out cachedPage) && cachedPage is ClientManagementForm selectedClients)
                selectedClients.SelectClientFromNavigation(selectedClientId);
        }

        private void NavigateBackToJobs(int selectedJobId)
        {
            if (_pageCache.TryGetValue(JobsPageIndex, out UserControl cachedPage) && cachedPage is JobManagementForm jobs)
                jobs.SelectJobFromNavigation(selectedJobId);

            _currentIndex = -1;
            NavigateTo(JobsPageIndex);

            if (_pageCache.TryGetValue(JobsPageIndex, out cachedPage) && cachedPage is JobManagementForm selectedJobs)
                selectedJobs.SelectJobFromNavigation(selectedJobId);
        }

        private void ClearTransientPage()
        {
            if (_transientPage == null)
                return;

            if (_content.Controls.Contains(_transientPage))
                _content.Controls.Remove(_transientPage);
            _transientPage.Dispose();
            _transientPage = null;
        }

        private UserControl CreatePage(int index)
        {
            UserControl page;
            switch (index)
            {
                case 1: page = new ClientManagementForm(); break;
                case 2: page = new ContractManagementForm(); break;
                case 3: page = new InvoiceForm(); break;
                case 4: page = new PaymentForm(); break;
                case 5: page = new SLADashboardForm(); break;
                case 6: page = new TenderBidForm(); break;
                case 7: page = new ReportForm(); break;
                case 8: page = new SettingsForm(); break;
                case 9: page = new VendorForm(); break;
                case 10: page = new PurchaseForm(); break;
                case 11: page = new InventoryForm(); break;
                case 12: page = new EmployeeForm(); break;
                case 13: page = new PayrollForm(); break;
                case 14: page = new GeoIntelligenceForm(); break;
                case 15: page = new JobManagementForm(); break;
                case 17: page = new MasterDataForm(); break;
                case 18: page = new WhatsAppHubForm(); break;
                default:
                    var dash = new DashboardForm();
                    dash.OnNavigate = NavigateTo;
                    page = dash;
                    break;
            }

            if (page is ClientManagementForm cmf)
            {
                cmf.OnNavigate = NavigateTo;
                cmf.OnOpenClientDetail = NavigateToClientDetail;
            }
            if (page is TenderBidForm tbf) tbf.OnNavigate = NavigateTo;
            if (page is GeoIntelligenceForm gif)
            {
                gif.OnNavigate = NavigateTo;
                gif.OnOpenClientSite = NavigateToClientSite;
                gif.OnOpenJobDetail = NavigateToJobDetail;
            }
            if (page is JobManagementForm jmf) jmf.OnOpenJobDetail = NavigateToJobDetail;

            return page;
        }

        private void TouchPage(int index)
        {
            var node = _pageUsage.Find(index);
            if (node != null)
                _pageUsage.Remove(node);
            _pageUsage.AddLast(index);
        }

        private void TrimPageCache(int activeIndex)
        {
            while (_pageCache.Count > MaxCachedPages && _pageUsage.Count > 0)
            {
                int candidate = _pageUsage.First.Value;
                _pageUsage.RemoveFirst();

                if (candidate == activeIndex || candidate == 0)
                {
                    _pageUsage.AddLast(candidate);
                    if (_pageUsage.Count == 1)
                        break;
                    continue;
                }

                if (_pageCache.TryGetValue(candidate, out var page))
                {
                    _pageCache.Remove(candidate);
                    if (page != null)
                    {
                        if (_content.Controls.Contains(page))
                            _content.Controls.Remove(page);
                        page.Dispose();
                    }
                }
            }
        }

        private Control BuildErrorPage(int index, Exception ex)
        {
            string title = index >= 0 && index < NavItems.Length ? NavItems[index].label : "page";

            var wrap = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = DS.BgPage,
                Padding = new Padding(32)
            };

            var card = new Panel
            {
                Size = new Size(640, 220),
                BackColor = DS.BgCard,
                Location = new Point(40, 40)
            };
            card.Paint += (s, e) =>
            {
                using (var pen = new Pen(Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };

            card.Controls.Add(new Label
            {
                Text = "This page could not be opened",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = DS.Slate900,
                Location = new Point(22, 20),
                AutoSize = true
            });

            card.Controls.Add(new Label
            {
                Text = title + " hit an error, so the app stayed open and logged the issue instead of crashing.",
                Font = new Font("Segoe UI", 10),
                ForeColor = DS.Slate700,
                Location = new Point(22, 58),
                Size = new Size(580, 40)
            });

            card.Controls.Add(new Label
            {
                Text = ex.Message,
                Font = new Font("Segoe UI", 9),
                ForeColor = DS.Red500,
                Location = new Point(22, 108),
                Size = new Size(580, 42)
            });

            card.Controls.Add(new Label
            {
                Text = string.IsNullOrWhiteSpace(AppRuntime.LastLogPath)
                    ? "Crash log saved in the app logs folder."
                    : "Crash log: " + AppRuntime.LastLogPath,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = DS.Slate500,
                Location = new Point(22, 162),
                Size = new Size(590, 24)
            });

            var btnRetry = new Button
            {
                Text = "Retry",
                Width = 100,
                Height = 34,
                Location = new Point(22, 186),
                BackColor = SbAccent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnRetry.FlatAppearance.BorderSize = 0;
            DS.StyleButton(btnRetry);
            btnRetry.Click += (s, e) => NavigateTo(index);
            card.Controls.Add(btnRetry);

            wrap.Controls.Add(card);
            return wrap;
        }

        private void EnsureSessionOrClose()
        {
            if (SessionManager.IsLoggedIn)
                return;

            BeginInvoke((Action)(() =>
            {
                if (!IsDisposed)
                    Close();
            }));
        }

        private Panel BuildUserStrip()
        {
            var wrap = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = _compactShell ? 108 : 118,
                BackColor = Color.Transparent,
                Padding = new Padding(14, _compactShell ? 10 : 14, 14, 8),
                TabStop = false
            };

            var avatar = new Panel
            {
                Location = new Point(_compactShell ? 14 : 20, _compactShell ? 14 : 18),
                Size = new Size(_compactShell ? 34 : 40, _compactShell ? 34 : 40),
                BackColor = Color.Transparent,
                TabStop = false
            };
            avatar.SizeChanged += (s, e) =>
            {
                try
                {
                    avatar.Region = new Region(new Rectangle(0, 0, avatar.Width, avatar.Height));
                }
                catch { }
            };
            avatar.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (Brush brush = new SolidBrush(Color.FromArgb(25, 55, 185)))
                    e.Graphics.FillEllipse(brush, 0, 0, avatar.Width - 1, avatar.Height - 1);
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (Brush textBrush = new SolidBrush(Color.White))
                using (Font avatarFont = new Font("Segoe UI", _compactShell ? 9.5f : 10.5f, FontStyle.Bold))
                {
                    e.Graphics.DrawString(GetUserInitials(), avatarFont, textBrush, new RectangleF(0, 0, avatar.Width - 1, avatar.Height - 1), sf);
                }
            };

            var lblName = new Label
            {
                Text = SessionManager.CurrentUser?.DisplayName ?? "Guest",
                Location = new Point(_compactShell ? 58 : 72, _compactShell ? 10 : 14),
                Size = new Size(_compactShell ? 82 : 104, 18),
                Font = new Font("Segoe UI", _compactShell ? 8.25f : 9f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoEllipsis = true
            };
            var lblRole = new Label
            {
                Text = SessionManager.CurrentUser?.RoleName ?? "No Role",
                Location = new Point(_compactShell ? 58 : 72, _compactShell ? 32 : 38),
                Size = new Size(_compactShell ? 74 : 82, 18),
                Font = new Font("Segoe UI", _compactShell ? 7.5f : 8f),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(22, 180, 92),
                TextAlign = ContentAlignment.MiddleCenter
            };
            DS.Rounded(lblRole, 9);
            var linkChange = new LinkLabel
            {
                Text = "Change Password",
                Location = new Point(_compactShell ? 14 : 20, _compactShell ? 82 : 92),
                Size = new Size(_compactShell ? 110 : 124, 18),
                Font = new Font("Segoe UI", _compactShell ? 7.5f : 8.25f),
                LinkColor = Color.FromArgb(218, 244, 252),
                ActiveLinkColor = Color.White,
                BackColor = Color.Transparent,
                TabStop = false
            };
            linkChange.Click += (s, e) =>
            {
                if (!SessionManager.IsLoggedIn)
                    return;

                using (var dialog = new ChangePasswordForm(SessionManager.CurrentUser.UserId, false))
                    dialog.ShowDialog(this);
            };
            var linkSearch = new LinkLabel
            {
                Text = "Search",
                Location = new Point(_compactShell ? 14 : 20, _compactShell ? 60 : 70),
                Size = new Size(48, 18),
                Font = new Font("Segoe UI", _compactShell ? 7.5f : 8.25f),
                LinkColor = Color.FromArgb(218, 244, 252),
                ActiveLinkColor = Color.White,
                BackColor = Color.Transparent,
                TabStop = false
            };
            linkSearch.Click += (s, e) =>
            {
                using (var dialog = new GlobalSearchDialog(NavigateTo))
                    dialog.ShowDialog(this);
            };
            var linkAlerts = new LinkLabel
            {
                Text = "Alerts",
                Location = new Point(_compactShell ? 66 : 78, _compactShell ? 60 : 70),
                Size = new Size(48, 18),
                Font = new Font("Segoe UI", _compactShell ? 7.5f : 8.25f),
                LinkColor = Color.FromArgb(254, 240, 138),
                ActiveLinkColor = Color.White,
                BackColor = Color.Transparent,
                TabStop = false
            };
            linkAlerts.Click += (s, e) =>
            {
                using (var dialog = new NotificationCenterDialog(NavigateTo))
                    dialog.ShowDialog(this);
            };
            var linkLogout = new LinkLabel
            {
                Text = "Logout",
                Location = new Point(_compactShell ? 110 : 148, _compactShell ? 60 : 70),
                Size = new Size(_compactShell ? 42 : 44, 18),
                Font = new Font("Segoe UI", _compactShell ? 7.5f : 8.25f),
                LinkColor = Color.FromArgb(248, 113, 113),
                ActiveLinkColor = Color.FromArgb(254, 202, 202),
                BackColor = Color.Transparent,
                TabStop = false
            };
            linkLogout.Click += (s, e) =>
            {
                try
                {
                    new AuthService().Logout();
                }
                catch (Exception ex)
                {
                    AppRuntime.LogException("Logout", ex);
                }
                Close();
            };

            wrap.Controls.Add(avatar);
            wrap.Controls.Add(lblName);
            wrap.Controls.Add(lblRole);
            wrap.Controls.Add(linkSearch);
            wrap.Controls.Add(linkAlerts);
            wrap.Controls.Add(linkChange);
            wrap.Controls.Add(linkLogout);
            return wrap;
        }

        private void UpdateNavState(int index)
        {
            foreach (var entry in _navButtons)
                SetActiveNav(entry.Value, entry.Key == index);
        }

        private void SetActiveNav(Button btn)
        {
            SetActiveNav(btn, true);
        }

        private void SetActiveNav(Button btn, bool active)
        {
            if (btn == null)
                return;

            btn.BackColor = active ? SbActive : Color.Transparent;
            btn.ForeColor = active ? Color.White : SbText;
            btn.Font = new Font("Segoe UI", _compactShell ? 8.2f : 8.8f, active ? FontStyle.Bold : FontStyle.Regular);
            btn.FlatAppearance.MouseOverBackColor = active ? SbActive : SbHover;
            btn.FlatAppearance.MouseDownBackColor = active ? SbActive : Color.FromArgb(38, 78, 198);
            btn.Invalidate();
        }

        private void RefreshWindowTitle()
        {
            string companyName = ConfigService.Get("Company", "CompanyName", string.Empty);
            Text = string.IsNullOrWhiteSpace(companyName)
                ? BrandingService.AppName
                : BrandingService.AppName + " – " + companyName.Trim();
        }

        private int GetSidebarWidth()
        {
            return _compactShell ? CompactSbWidth : SbWidth;
        }

        private static bool IsCompactWorkArea()
        {
            if (LayoutScaler.IsLaptopFitModeEnabled())
                return true;

            Rectangle workArea = Screen.PrimaryScreen?.WorkingArea ?? SystemInformation.WorkingArea;
            return workArea.Width <= 1366 || workArea.Height <= 760;
        }

        private void ApplyResponsiveShell()
        {
            bool compact = IsCompactWorkArea() || ClientSize.Width < 1220 || ClientSize.Height < 720;
            if (_sidebar != null && _sidebar.Width != (compact ? CompactSbWidth : SbWidth))
                _sidebar.Width = compact ? CompactSbWidth : SbWidth;

            if (_compactShell == compact)
                return;

            _compactShell = compact;
            MinimumSize = new Size(1024, 600);
            Font = new Font("Segoe UI", compact ? 8.25f : 9f);
            if (_sidebar != null)
                BuildSidebar();
        }

        public void ToggleSidebarVisibility()
        {
            if (_sidebar == null || _content == null)
                return;

            _sidebar.Visible = !_sidebar.Visible;
            _content.Left = _sidebar.Visible ? _sidebar.Width : 0;
            _content.Width = Math.Max(0, ClientSize.Width - _content.Left);
            _content.Height = ClientSize.Height;
            _layoutMemory.SetShellValue("SidebarVisible", _sidebar.Visible ? "1" : "0");
        }

        private void ApplyShellLayoutMemory()
        {
            if (_sidebar == null || _content == null)
                return;

            string visible = _layoutMemory.GetShellValue("SidebarVisible", "1");
            _sidebar.Visible = !string.Equals(visible, "0", StringComparison.OrdinalIgnoreCase);
            _content.Left = _sidebar.Visible ? _sidebar.Width : 0;
            _content.Width = Math.Max(0, ClientSize.Width - _content.Left);
            _content.Height = ClientSize.Height;
        }

        private bool CanViewNavItem(int index)
        {
            if (index < 0 || index >= NavItems.Length || index == RetiredServiceDeskPageIndex)
                return false;

            string moduleKey = GetModuleKeyForNav(index);
            return SessionManager.IsLoggedIn && SessionManager.HasPermission(moduleKey, "View");
        }

        private int GetFirstAllowedPage()
        {
            for (int i = 0; i < NavItems.Length; i++)
            {
                if (CanViewNavItem(i))
                    return i;
            }

            return -1;
        }

        private int MapPageKey(string pageKey)
        {
            string key = (pageKey ?? string.Empty).Trim();
            switch (key.ToUpperInvariant())
            {
                case "DASHBOARD": return 0;
                case "CLIENTS": return 1;
                case "CONTRACTS": return 2;
                case "INVOICES": return 3;
                case "PAYMENTS": return 4;
                case "QUOTATIONS": return 6;
                case "REPORTS": return 7;
                case "SETTINGS": return 8;
                case "VENDORS": return 9;
                case "PURCHASES": return 10;
                case "INVENTORY": return 11;
                case "EMPLOYEES": return 12;
                case "PAYROLL": return 13;
                case "GEOINTELLIGENCE": return 14;
                case "WORKORDERS":
                case "JOBS": return 15;
                case "SERVICEDESK":
                case "SERVICE DESK":
                case "INCIDENTS":
                case "TICKETS": return JobsPageIndex;
                case "MASTERDATA":
                case "MASTER DATA":
                case "DATAUPLOAD":
                case "DATA UPLOAD":
                case "CLIENTSETUP":
                case "CLIENT SETUP": return MasterDataPageIndex;
                case "WHATSAPP":
                case "WHATSAPPHUB":
                case "WHATSAPP HUB": return WhatsAppHubPageIndex;
                default: return 0;
            }
        }

        private string GetModuleKeyForNav(int index)
        {
            switch (index)
            {
                case 0: return "Dashboard";
                case 1: return "Clients";
                case 2: return "Contracts";
                case 3: return "Invoices";
                case 4: return "Payments";
                case 5: return "Dashboard";
                case 6: return "Quotations";
                case 7: return "Reports";
                case 8: return "Settings";
                case 9: return "Vendors";
                case 10: return "Purchases";
                case 11: return "Inventory";
                case 12: return "Employees";
                case 13: return "Payroll";
                case 14: return "GeoIntelligence";
                case 15: return "WorkOrders";
                case 16: return "WorkOrders";
                case 17: return "MasterData";
                case 18: return "Dashboard";
                default: return "Dashboard";
            }
        }

        private string GetUserInitials()
        {
            string displayName = SessionManager.CurrentUser?.DisplayName ?? "NA";
            string[] parts = displayName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return "NA";
            if (parts.Length == 1)
                return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
            return (parts[0][0].ToString() + parts[1][0].ToString()).ToUpperInvariant();
        }

        private Control BuildNoAccessPage()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = DS.BgPage };
            panel.Controls.Add(new Label
            {
                Text = "No modules are available for this role.",
                AutoSize = true,
                Font = new Font("Segoe UI", 15, FontStyle.Bold),
                ForeColor = DS.Slate900,
                Location = new Point(40, 40)
            });
            return panel;
        }

        private void BeginVersionCheck()
        {
            if (!SessionManager.IsLoggedIn || _hideUpdateBannerForSession)
                return;

            Task.Run(async () =>
            {
                try
                {
                    UpdateCheckResult result = await UpdateService.CheckForUpdatesAsync();
                    if (!result.IsUpdateAvailable)
                        return;

                    if (IsDisposed || !IsHandleCreated)
                        return;

                    BeginInvoke((Action)(() => ShowUpdateDialog(result)));
                }
                catch (Exception ex)
                {
                    AppLogger.LogInfo("Version check task failed silently: " + ex.Message);
                }
            });
        }

        private void ShowUpdateBanner(UpdateCheckResult result)
        {
            if (result == null || _hideUpdateBannerForSession || IsUpdateDismissed())
                return;

            _lblUpdateMessage.Text = "ServoERP " + result.LatestVersion + " is available.";
            _latestUpdateResult = result;
            _btnDownloadUpdate.Visible = !string.IsNullOrWhiteSpace(result.DownloadUrl);
            _pnlUpdateBanner.Visible = true;
            _pnlUpdateBanner.BringToFront();
        }

        private void ShowUpdateDialog(UpdateCheckResult result)
        {
            if (result == null || _hideUpdateBannerForSession || IsUpdateDismissed())
                return;

            _latestUpdateResult = result;
            ShowUpdateBanner(result);

            using (Form dialog = BuildUpdateDialog(result))
            {
                dialog.ShowDialog(this);
            }
        }

        private Form BuildUpdateDialog(UpdateCheckResult result)
        {
            var dialog = new Form
            {
                Text = "ServoERP Update Available",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowIcon = true,
                Icon = Icon,
                BackColor = DS.BgPage,
                Size = new Size(540, 430),
                Font = new Font("Segoe UI", 9f)
            };

            Panel body = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24),
                BackColor = DS.BgPage
            };

            Label title = new Label
            {
                Text = "ServoERP " + result.LatestVersion + " is available",
                Dock = DockStyle.Top,
                Height = 34,
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                ForeColor = DS.Slate900
            };

            Label subtitle = new Label
            {
                Text = "Installed version: " + result.CurrentVersion,
                Dock = DockStyle.Top,
                Height = 26,
                Font = new Font("Segoe UI", 9f),
                ForeColor = DS.Slate600
            };

            TextBox changelog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                ForeColor = DS.Slate800,
                Font = new Font("Segoe UI", 9f),
                Text = string.IsNullOrWhiteSpace(result.ChangelogText)
                    ? "No changelog details were provided for this version."
                    : result.ChangelogText
            };

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = DS.BgPage,
                Padding = new Padding(0, 10, 0, 0)
            };

            Button btnDownload = DS.PrimaryBtn("Install", 116, 34);
            btnDownload.Click += (s, e) =>
            {
                dialog.Close();
                DownloadAndInstallUpdate();
            };

            Button btnLater = DS.GhostBtn("Later", 92, 34);
            btnLater.Click += (s, e) => dialog.Close();

            actions.Controls.Add(btnDownload);
            actions.Controls.Add(btnLater);
            body.Controls.Add(changelog);
            body.Controls.Add(subtitle);
            body.Controls.Add(title);
            body.Controls.Add(actions);
            dialog.Controls.Add(body);
            return dialog;
        }

        private void OpenUpdateDownload()
        {
            try
            {
                string url = _latestUpdateResult == null ? null : _latestUpdateResult.DownloadUrl;
                if (string.IsNullOrWhiteSpace(url))
                    return;

                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppLogger.LogError("UpdateBanner.Download", ex);
                MessageBox.Show("Unable to open the update download link.\r\n" + ex.Message,
                    "Download update", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void DownloadAndInstallUpdate()
        {
            if (_latestUpdateResult == null)
                return;

            DialogResult confirm = MessageBox.Show(
                "ServoERP will download version " + _latestUpdateResult.LatestVersion + ", close the app, apply the update, and reopen automatically.\r\n\r\nSave your work before continuing.",
                "Install update",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Information);
            if (confirm != DialogResult.OK)
                return;

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
                        string packagePath = await UpdateService.DownloadUpdatePackageAsync(_latestUpdateResult, progressReporter, cancelSource.Token);
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
                        AppLogger.LogError("UpdateDownload.Install", ex);
                        MessageBox.Show(
                            "Automatic update could not complete.\r\n\r\n" + ex.Message + "\r\n\r\nThe download page will open now.",
                            "Install update",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        OpenUpdateDownload();
                    }
                };

                progressForm.ShowDialog(this);
            }
        }

        private static bool IsUpdateDismissed()
        {
            string raw = ConfigService.Get("App", "UpdateDismissedUntil", string.Empty);
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            DateTime dismissedUntil;
            return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out dismissedUntil)
                   && dismissedUntil > DateTime.Now;
        }
    }
}


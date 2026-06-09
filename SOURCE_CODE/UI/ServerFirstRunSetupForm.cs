using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.Services.Licensing;
using HVAC_Pro_Desktop.UI.Licensing;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class ServerFirstRunSetupForm : ServoERP.Infrastructure.ServoFormBase
    {
        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _lblTitle = new Label();
        private readonly Label _lblSubtitle = new Label();
        private readonly Label _lblStatus = new Label();
        private readonly Button _btnPrepare = new Button();
        private readonly Button _btnActivateLicense = new Button();
        private readonly Button _btnCreateAdmin = new Button();
        private readonly Button _btnAcceptLegal = new Button();
        private readonly Button _btnGeneratePack = new Button();
        private readonly Button _btnOpenFolder = new Button();
        private readonly Button _btnSupportReport = new Button();
        private readonly Button _btnFinish = new Button();
        private string _lastPackagePath;
        private bool _sqlReady;
        private bool _licenseReady;
        private bool _adminReady;
        private bool _legalReady;
        private bool _terminalPackReady;

        public ServerFirstRunSetupForm()
        {
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(940, 640);
            Font = new Font("Segoe UI", 9F);
            BackColor = DS.BgPage;
            Text = BrandingService.WindowTitle("Server Setup");

            BuildLayout();
            DS.ApplyTheme(this);
            UIHelper.ApplyButtonAlignment(this);
            RefreshReadiness();
        }

        private void BuildLayout()
        {
            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 116,
                BackColor = DS.BgPage,
                Padding = new Padding(24, 18, 24, 10)
            };

            _lblTitle.Text = "ServoERP Server Setup";
            _lblTitle.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
            _lblTitle.ForeColor = DS.Slate900;
            _lblTitle.Dock = DockStyle.Top;
            _lblTitle.Height = 34;

            _lblSubtitle.Text = "One guided setup for the office server, license, owner login, legal approval, and terminal PCs.";
            _lblSubtitle.Font = DS.Body;
            _lblSubtitle.ForeColor = DS.Slate600;
            _lblSubtitle.Dock = DockStyle.Top;
            _lblSubtitle.Height = 44;

            header.Controls.Add(_lblSubtitle);
            header.Controls.Add(_lblTitle);

            Panel body = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 0, 24, 16),
                BackColor = DS.BgPage
            };

            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.ReadOnly = true;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.BackgroundColor = Color.White;
            _grid.BorderStyle = BorderStyle.FixedSingle;
            _grid.Columns.Add("Step", "Step");
            _grid.Columns.Add("Status", "Status");
            _grid.Columns.Add("Detail", "Detail");
            _grid.Columns[0].FillWeight = 90;
            _grid.Columns[1].FillWeight = 70;
            _grid.Columns[2].FillWeight = 240;
            GridTheme.Apply(_grid);

            body.Controls.Add(_grid);

            Panel footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 132,
                BackColor = DS.BgPage,
                Padding = new Padding(24, 10, 24, 16)
            };

            _lblStatus.Text = "Start with Prepare Server. ServoERP will unlock Finish Start only after every office setup step is ready.";
            _lblStatus.ForeColor = DS.Slate600;
            _lblStatus.Dock = DockStyle.Top;
            _lblStatus.Height = 52;

            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 70,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = true,
                BackColor = DS.BgPage
            };

            _btnPrepare.Text = "Prepare Server";
            _btnPrepare.Width = 130;
            _btnPrepare.Click += (s, e) => PrepareServer();

            _btnActivateLicense.Text = "Activate License";
            _btnActivateLicense.Width = 140;
            _btnActivateLicense.Click += (s, e) => ActivateLicense();

            _btnCreateAdmin.Text = "Create Admin";
            _btnCreateAdmin.Width = 120;
            _btnCreateAdmin.Click += (s, e) => CreateAdminAccount();

            _btnAcceptLegal.Text = "Accept Legal";
            _btnAcceptLegal.Width = 120;
            _btnAcceptLegal.Click += (s, e) => AcceptLegalTerms();

            _btnGeneratePack.Text = "Generate Terminal Pack";
            _btnGeneratePack.Width = 170;
            _btnGeneratePack.Click += (s, e) => GenerateTerminalPack();

            _btnOpenFolder.Text = "Open Pack Folder";
            _btnOpenFolder.Width = 140;
            _btnOpenFolder.Click += (s, e) => OpenPackFolder();

            _btnSupportReport.Text = "Copy Support Report";
            _btnSupportReport.Width = 160;
            _btnSupportReport.Click += (s, e) => CopySupportReport();

            _btnFinish.Text = "Finish & Start";
            _btnFinish.Width = 120;
            _btnFinish.Click += (s, e) => FinishSetup();

            buttons.Controls.Add(_btnFinish);
            buttons.Controls.Add(_btnGeneratePack);
            buttons.Controls.Add(_btnOpenFolder);
            buttons.Controls.Add(_btnAcceptLegal);
            buttons.Controls.Add(_btnCreateAdmin);
            buttons.Controls.Add(_btnActivateLicense);
            buttons.Controls.Add(_btnPrepare);
            buttons.Controls.Add(_btnSupportReport);

            footer.Controls.Add(buttons);
            footer.Controls.Add(_lblStatus);

            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(header);
        }

        private void RefreshReadiness()
        {
            _grid.Rows.Clear();

            DatabaseConnectionStateSnapshot state = DatabaseConnectionStateService.GetCurrentState();
            string configured = DatabaseManager.GetConfiguredConnectionString();
            bool hasConfig = !string.IsNullOrWhiteSpace(configured);
            bool fallbackReady = File.Exists(LocalSqliteFallbackStore.GetDatabasePath());
            _sqlReady = hasConfig && state.BusinessWritesAllowed;
            _licenseReady = IsLicenseReady(out string licenseDetail);
            _adminReady = IsAdminReady(out string adminDetail);
            _legalReady = IsLegalReady();
            _terminalPackReady = !string.IsNullOrWhiteSpace(_lastPackagePath) && File.Exists(_lastPackagePath);

            AddRow("1. Server PC", _sqlReady ? "Ready" : "Needs setup", _sqlReady ? "SQL Server is online and business entries are unlocked." : "Click Prepare Server. ServoERP will configure SQL and verify business writes.");
            AddRow("2. SQL target", hasConfig ? "Configured" : "Missing", hasConfig ? configured : "No SQL target is configured for this office server.");
            AddRow("3. License", _licenseReady ? "Active" : "Needs activation", licenseDetail);
            AddRow("4. Owner/Admin login", _adminReady ? "Ready" : "Needs account", adminDetail);
            AddRow("5. Legal approval", _legalReady ? "Accepted" : "Needs acceptance", _legalReady ? "Legal terms already accepted for this workstation." : "Review and accept the ServoERP legal terms before business use.");
            AddRow("6. Terminal setup pack", _terminalPackReady ? "Ready" : "Needs pack", _terminalPackReady ? _lastPackagePath : "Generate this pack and apply it on each office terminal before users start work.");
            AddRow("7. Business writes", state.BusinessWritesAllowed ? "Unlocked" : "Locked", state.BusinessWritesAllowed ? "SQL Server is reachable." : "Writes stay locked until SQL Server is reachable. Diagnostics fallback is read-only.");
            AddRow("Diagnostics fallback", fallbackReady ? "Ready" : "Pending", LocalSqliteFallbackStore.GetDatabasePath());

            _btnActivateLicense.Enabled = _sqlReady && !_licenseReady;
            _btnCreateAdmin.Enabled = _sqlReady && !_adminReady;
            _btnAcceptLegal.Enabled = _sqlReady && !_legalReady;
            _btnGeneratePack.Enabled = _sqlReady && _licenseReady;
            _btnOpenFolder.Enabled = _terminalPackReady;
            _btnFinish.Enabled = _sqlReady && _licenseReady && _adminReady && _legalReady && _terminalPackReady;

            if (_btnFinish.Enabled)
                _lblStatus.Text = "Office setup is ready. Click Finish Start to open ServoERP.";
            else
                _lblStatus.Text = "Complete the pending steps from top to bottom. If anything fails, click Copy Support Report and send it to ServoERP support.";
        }

        private void AddRow(string step, string status, string detail)
        {
            int index = _grid.Rows.Add(step, status, detail);
            DataGridViewRow row = _grid.Rows[index];
            string key = (status ?? string.Empty).ToLowerInvariant();
            if (key.Contains("ready") || key.Contains("configured") || key.Contains("online") || key.Contains("unlocked") || key.Contains("active") || key.Contains("accepted"))
                row.DefaultCellStyle.ForeColor = DS.Green600;
            else if (key.Contains("locked") || key.Contains("missing") || key.Contains("offline") || key.Contains("error") || key.Contains("needs"))
                row.DefaultCellStyle.ForeColor = DS.Red600;
            else
                row.DefaultCellStyle.ForeColor = DS.Amber600;
        }

        private void PrepareServer()
        {
            SetBusy(true, "Preparing SQL Server, database, firewall rules, backup folders, and diagnostics fallback...");

            BackgroundWorker worker = CreateWorker();
            worker.DoWork += (s, e) =>
            {
                AppStartupService.RunInteractiveServerSetup();
                EnsureSqlFirewallRules();
            };
            worker.RunWorkerCompleted += (s, e) =>
            {
                if (e.Error != null)
                {
                    AppRuntime.LogException("ServerFirstRunSetupForm.PrepareServer", e.Error);
                    RunOnUI(() =>
                    {
                        SetBusy(false, "Server setup failed. Click Copy Support Report and send it to ServoERP support.");
                        DatabaseConnectionStateService.CheckNow("ServerFirstRunSetupForm.PrepareServer", true);
                        RefreshReadiness();
                    });
                    ShowError( BuildFriendlyError("Server setup failed", e.Error.Message), e.Error);
                    return;
                }
                if (e.Cancelled) return;

                RunOnUI(() =>
                {
                    SetBusy(false, "Server setup complete. Activate the license next.");
                    DatabaseConnectionStateService.CheckNow("ServerFirstRunSetupForm.PrepareServer", true);
                    RefreshReadiness();
                });
            };
            worker.RunWorkerAsync();
        }

        private void ActivateLicense()
        {
            using (var form = new LicenseActivationForm())
            {
                form.ShowDialog(this);
            }

            RefreshReadiness();
        }

        private void CreateAdminAccount()
        {
            using (var form = new CreateAccountForm())
            {
                form.ShowDialog(this);
            }

            RefreshReadiness();
        }

        private void AcceptLegalTerms()
        {
            LegalAgreementForm.EnsureAccepted(this);
            RefreshReadiness();
        }

        private void GenerateTerminalPack()
        {
            try
            {
                SupportToolResult result = new SupportCenterService().GenerateClientServerSetupPackage();
                if (!result.Success)
                    throw new InvalidOperationException(result.Message);

                _lastPackagePath = result.OutputPath;
                _lblStatus.Text = "Terminal pack ready. Apply it on each office terminal before users start ServoERP.";
                RefreshReadiness();
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("ServerFirstRunSetupForm.GenerateTerminalPack", ex);
                MessageBox.Show(BuildFriendlyError("Terminal pack failed", ex.Message), BrandingService.WindowTitle("Terminal Pack Failed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenPackFolder()
        {
            try
            {
                string folder = string.IsNullOrWhiteSpace(_lastPackagePath) ? @"C:\HVAC_PRO_MSE\DIAGNOSTICS" : Path.GetDirectoryName(_lastPackagePath);
                if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
                    Process.Start("explorer.exe", folder);
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("ServerFirstRunSetupForm.OpenPackFolder", ex);
            }
        }

        private void FinishSetup()
        {
            if (!(_sqlReady && _licenseReady && _adminReady && _legalReady && _terminalPackReady))
            {
                MessageBox.Show("Complete all setup steps before starting ServoERP. Pending rows are shown in red.", BrandingService.WindowTitle("Office Setup"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshReadiness();
                return;
            }

            ConfigService.Set("Setup", "ServerFirstRunComplete", "true");
            DialogResult = DialogResult.OK;
            Close();
        }

        private void SetBusy(bool busy, string message)
        {
            _btnPrepare.Enabled = !busy;
            _btnActivateLicense.Enabled = !busy && _sqlReady && !_licenseReady;
            _btnCreateAdmin.Enabled = !busy && _sqlReady && !_adminReady;
            _btnAcceptLegal.Enabled = !busy && _sqlReady && !_legalReady;
            _btnGeneratePack.Enabled = !busy && _sqlReady && _licenseReady;
            _btnOpenFolder.Enabled = !busy && _terminalPackReady;
            _btnSupportReport.Enabled = !busy;
            _btnFinish.Enabled = !busy && _sqlReady && _licenseReady && _adminReady && _legalReady && _terminalPackReady;
            _lblStatus.Text = message;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private bool IsLicenseReady(out string detail)
        {
            try
            {
                LicenseValidationResult result = new LicenseService().ValidateCurrentLicense();
                detail = result.Success ? result.Message : "Click Activate License. Use online activation, trial, or import the signed ServoERP license file.";
                if (result.Snapshot != null && !string.IsNullOrWhiteSpace(result.Snapshot.CompanyName))
                    detail = result.Snapshot.CompanyName + " - " + detail;
                return result.Success && !result.RequiresActivation && !result.IsFrozen;
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("ServerFirstRunSetupForm.IsLicenseReady", ex);
                detail = "License can be checked after SQL Server is ready.";
                return false;
            }
        }

        private bool IsAdminReady(out string detail)
        {
            try
            {
                bool ready = new AuthService().HasAnyUsers();
                detail = ready ? "At least one ServoERP login exists." : "Create the proprietor/owner Admin login before handover.";
                return ready;
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("ServerFirstRunSetupForm.IsAdminReady", ex);
                detail = "Admin account can be checked after SQL Server is ready.";
                return false;
            }
        }

        private static bool IsLegalReady()
        {
            try
            {
                return string.Equals(DbSettings.Get("LegalAccepted", "false"), "true", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private void CopySupportReport()
        {
            string report =
                "ServoERP Office Setup Support Report" + Environment.NewLine +
                "Time: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") + Environment.NewLine +
                "SQL: " + DatabaseConnectionStateService.BuildUserMessage() + Environment.NewLine +
                "Connection: " + (DatabaseManager.GetConfiguredConnectionString() ?? "missing") + Environment.NewLine +
                "License ready: " + _licenseReady + Environment.NewLine +
                "Admin ready: " + _adminReady + Environment.NewLine +
                "Legal accepted: " + _legalReady + Environment.NewLine +
                "Terminal pack: " + (_terminalPackReady ? _lastPackagePath : "not generated") + Environment.NewLine +
                "Fallback: " + LocalSqliteFallbackStore.GetDatabasePath();

            UIHelper.TrySetClipboardText(this, report, BrandingService.WindowTitle("Support Report"));
            _lblStatus.Text = "Support report copied. Send it to ServoERP support if setup is stuck.";
        }

        private static string BuildFriendlyError(string title, string detail)
        {
            return title + "." + Environment.NewLine + Environment.NewLine +
                "ServoERP could not complete this step automatically. Check SQL Server, administrator permission, and the office network, or send the support report to ServoERP support." +
                Environment.NewLine + Environment.NewLine +
                "Technical detail: " + detail;
        }

        private static void EnsureSqlFirewallRules()
        {
            RunNetsh("advfirewall firewall add rule name=\"ServoERP SQL Server TCP 1433\" dir=in action=allow protocol=TCP localport=1433");
            RunNetsh("advfirewall firewall add rule name=\"ServoERP SQL Browser UDP 1434\" dir=in action=allow protocol=UDP localport=1434");
        }

        private static void RunNetsh(string arguments)
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo("netsh.exe", arguments)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using (Process process = Process.Start(info))
                {
                    if (process != null)
                        process.WaitForExit(15000);
                }
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("ServerFirstRunSetupForm.RunNetsh", ex);
            }
        }
    }
}



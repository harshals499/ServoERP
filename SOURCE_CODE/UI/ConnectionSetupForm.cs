using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class ConnectionSetupForm : ServoERP.Infrastructure.ServoFormBase
    {
        public const string ConnectionStringName = "HVACPro_Connection";
        public const string DefaultConnectionString = @"Server=localhost\SQLEXPRESS;Database=HVAC_PRO;Integrated Security=True;Pooling=True;Min Pool Size=0;Max Pool Size=100;Connect Timeout=15;";

        private readonly RadioButton _rbLocalServer = new RadioButton();
        private readonly RadioButton _rbPrivateServer = new RadioButton();
        private readonly TextBox _txtServerIP = new TextBox();
        private readonly TextBox _txtInstance = new TextBox();
        private readonly TextBox _txtDatabase = new TextBox();
        private readonly RadioButton _rbWindowsAuth = new RadioButton();
        private readonly RadioButton _rbSqlAuth = new RadioButton();
        private readonly TextBox _txtUsername = new TextBox();
        private readonly TextBox _txtPassword = new TextBox();
        private readonly NumericUpDown _numMaxPoolSize = new NumericUpDown();
        private readonly Label _lblModeHint = new Label();
        private readonly Label _lblStatus = new Label();
        private readonly Label _lblUsername = new Label();
        private readonly Label _lblPassword = new Label();
        private bool _lastTestSucceeded;
        private string _lastTestedConnectionString;

        public ConnectionSetupForm()
        {
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "Connect to office server";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(820, 612);
            Font = new Font("Segoe UI", 9F);
            BackColor = DS.BgPage;

            BuildLayout();
            UIHelper.ApplyInputStyles(Controls);
            DS.ApplyTheme(this);
            ApplyConnectionTheme();
            LoadCurrentConnection();
            UpdateServerMode();
            UpdateAuthFields();
        }

        private void BuildLayout()
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 108,
                BackColor = DS.BgPage
            };
            var icon = ModernIconSystem.Badge(ModernIconKind.Security, 46, DS.Primary50, DS.Primary600, 12);
            icon.Location = new Point(28, 26);
            var title = new Label
            {
                Text = "Connect to your office server",
                Font = DS.H1,
                ForeColor = DS.Slate900,
                Location = new Point(88, 23),
                Size = new Size(560, 28)
            };
            var hint = new Label
            {
                Text = "Set up the secure SQL Server connection used by every ServoERP PC in your office.",
                Font = DS.Body,
                ForeColor = DS.Slate600,
                Location = new Point(90, 54),
                Size = new Size(660, 34)
            };
            header.Controls.AddRange(new Control[] { icon, title, hint });

            var setupCard = BuildCard(new Rectangle(28, 122, 500, 406));
            var serverTitle = new Label
            {
                Text = "1. Choose where your data is stored",
                Font = DS.H3,
                ForeColor = DS.Slate900,
                Location = new Point(22, 18),
                Size = new Size(410, 22)
            };
            var serverHint = new Label
            {
                Text = "Use the office server for client PCs so everyone works with the same live data.",
                Font = DS.Small,
                ForeColor = DS.Slate500,
                Location = new Point(22, 40),
                Size = new Size(445, 30)
            };

            _rbLocalServer.Text = "This server PC";
            _rbLocalServer.Location = new Point(24, 77);
            _rbLocalServer.Size = new Size(170, 24);
            _rbLocalServer.Checked = true;
            _rbLocalServer.CheckedChanged += (s, e) => UpdateServerMode();

            _rbPrivateServer.Text = "Another PC in this office";
            _rbPrivateServer.Location = new Point(226, 77);
            _rbPrivateServer.Size = new Size(220, 24);
            _rbPrivateServer.CheckedChanged += (s, e) => UpdateServerMode();

            _lblModeHint.Location = new Point(24, 105);
            _lblModeHint.Size = new Size(448, 34);
            _lblModeHint.Font = DS.Small;
            _lblModeHint.ForeColor = DS.Slate600;

            AddLabel(setupCard, "Server address", 150);
            _txtServerIP.Location = new Point(24, 174);
            _txtServerIP.Size = new Size(285, 28);
            _txtServerIP.Tag = "Example: 192.168.1.10 or OFFICE-SERVER";

            AddLabel(setupCard, "SQL instance", 150, 326);
            _txtInstance.Location = new Point(326, 174);
            _txtInstance.Size = new Size(148, 28);
            _txtInstance.Tag = "SQLEXPRESS";

            AddLabel(setupCard, "Database", 218);
            _txtDatabase.Location = new Point(24, 242);
            _txtDatabase.Size = new Size(450, 28);

            var authTitle = new Label { Text = "2. Sign in to SQL Server", Font = DS.H3, ForeColor = DS.Slate900, Location = new Point(22, 289), Size = new Size(300, 22) };
            var authModePanel = new Panel { Location = new Point(0, 312), Size = new Size(480, 34), BackColor = Color.Transparent };

            _rbWindowsAuth.Text = "Windows Authentication";
            _rbWindowsAuth.Location = new Point(24, 2);
            _rbWindowsAuth.Size = new Size(190, 24);
            _rbWindowsAuth.Checked = true;
            _rbWindowsAuth.CheckedChanged += (s, e) => UpdateAuthFields();

            _rbSqlAuth.Text = "SQL Authentication";
            _rbSqlAuth.Location = new Point(230, 2);
            _rbSqlAuth.Size = new Size(170, 24);
            _rbSqlAuth.CheckedChanged += (s, e) => UpdateAuthFields();

            _lblUsername.Text = "SQL username";
            _lblUsername.Location = new Point(24, 351);
            _lblUsername.Size = new Size(190, 20);
            _lblUsername.Font = DS.SmallBold;
            _txtUsername.Location = new Point(24, 373);
            _txtUsername.Size = new Size(215, 28);

            _lblPassword.Text = "Password";
            _lblPassword.Location = new Point(258, 351);
            _lblPassword.Size = new Size(190, 20);
            _lblPassword.Font = DS.SmallBold;
            _txtPassword.Location = new Point(258, 373);
            _txtPassword.Size = new Size(216, 28);
            _txtPassword.UseSystemPasswordChar = true;

            var advanced = new Label { Text = "Connection pool", Font = DS.SmallBold, ForeColor = DS.Slate600, Location = new Point(24, 421), Size = new Size(140, 20) };
            _numMaxPoolSize.Location = new Point(155, 417);
            _numMaxPoolSize.Size = new Size(88, 28);
            _numMaxPoolSize.Minimum = 20;
            _numMaxPoolSize.Maximum = 500;
            _numMaxPoolSize.Value = DatabaseConnectionFactory.DefaultMaxPoolSize;

            var guideCard = BuildCard(new Rectangle(546, 122, 246, 406));
            var guideIcon = ModernIconSystem.Badge(ModernIconKind.Status, 38, DS.Green50, DS.Green600, 10);
            guideIcon.Location = new Point(20, 20);
            var guideTitle = new Label { Text = "Before you save", Font = DS.H3, ForeColor = DS.Slate900, Location = new Point(68, 24), Size = new Size(156, 22) };
            var guideText = new Label
            {
                Text = "1. Enter the office server name or IP.\r\n\r\n2. Select the SQL sign-in provided by your administrator.\r\n\r\n3. Test first, then save the verified connection.\r\n\r\nYour server details stay on this PC.",
                Font = DS.Body,
                ForeColor = DS.Slate600,
                Location = new Point(20, 78),
                Size = new Size(205, 224)
            };
            var statusCaption = new Label { Text = "Connection check", Font = DS.SmallBold, ForeColor = DS.Slate600, Location = new Point(20, 324), Size = new Size(160, 18) };
            _lblStatus.Location = new Point(20, 347);
            _lblStatus.Size = new Size(205, 42);
            _lblStatus.Font = DS.Small;
            _lblStatus.ForeColor = DS.Slate500;

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 68, BackColor = DS.White, Padding = new Padding(28, 16, 28, 16) };
            var btnTestConnection = new Button
            {
                Text = "Test connection",
                Location = new Point(326, 17),
                Size = new Size(130, 34)
            };
            btnTestConnection.Click += (s, e) => TestConnection();

            var btnSave = new Button
            {
                Text = "Save connection",
                Location = new Point(466, 17),
                Size = new Size(132, 34)
            };
            btnSave.Click += (s, e) => SaveConnection();

            var btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(610, 17),
                Size = new Size(82, 34)
            };
            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            authModePanel.Controls.AddRange(new Control[] { _rbWindowsAuth, _rbSqlAuth });
            setupCard.Controls.AddRange(new Control[] { serverTitle, serverHint, _rbLocalServer, _rbPrivateServer, _lblModeHint, _txtServerIP, _txtInstance, _txtDatabase, authTitle, authModePanel, _lblUsername, _txtUsername, _lblPassword, _txtPassword, advanced, _numMaxPoolSize });
            guideCard.Controls.AddRange(new Control[] { guideIcon, guideTitle, guideText, statusCaption, _lblStatus });
            footer.Controls.AddRange(new Control[] { btnTestConnection, btnSave, btnCancel });
            Controls.AddRange(new Control[] { header, setupCard, guideCard, footer });
        }

        private static Panel BuildCard(Rectangle bounds)
        {
            var card = new Panel
            {
                Location = bounds.Location,
                Size = bounds.Size,
                BackColor = DS.BgCard,
                BorderStyle = BorderStyle.FixedSingle
            };
            DS.Rounded(card, DS.RadiusLg);
            return card;
        }

        private static void AddLabel(Control parent, string text, int y, int x = 24)
        {
            parent.Controls.Add(new Label { Text = text, Location = new Point(x, y), Size = new Size(190, 20), Font = DS.SmallBold, ForeColor = DS.Slate600, TextAlign = ContentAlignment.MiddleLeft });
        }

        private void ApplyConnectionTheme()
        {
            foreach (Control control in Controls)
                UIHelper.ApplyInputStyles(control.Controls);
        }

        private void LoadCurrentConnection()
        {
            _txtServerIP.Text = "localhost";
            _txtInstance.Text = "SQLEXPRESS";
            _txtDatabase.Text = "HVAC_PRO";
            _rbWindowsAuth.Checked = true;
            _numMaxPoolSize.Value = DatabaseConnectionFactory.DefaultMaxPoolSize;

            string connectionString = DatabaseManager.GetConfiguredConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString))
                return;

            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString);
                string dataSource = builder.DataSource ?? string.Empty;
                int slashIndex = dataSource.IndexOf('\\');
                if (slashIndex >= 0)
                {
                    _txtServerIP.Text = dataSource.Substring(0, slashIndex);
                    _txtInstance.Text = dataSource.Substring(slashIndex + 1);
                }
                else if (!string.IsNullOrWhiteSpace(dataSource))
                {
                    _txtServerIP.Text = dataSource;
                    _txtInstance.Text = string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(builder.InitialCatalog))
                    _txtDatabase.Text = builder.InitialCatalog;

                _rbWindowsAuth.Checked = builder.IntegratedSecurity;
                _rbSqlAuth.Checked = !builder.IntegratedSecurity;
                _txtUsername.Text = builder.UserID;
                _txtPassword.Text = builder.Password;
                decimal poolSize = Math.Min(_numMaxPoolSize.Maximum, Math.Max(_numMaxPoolSize.Minimum, DatabaseConnectionFactory.GetConfiguredMaxPoolSize()));
                _numMaxPoolSize.Value = poolSize;
                _rbPrivateServer.Checked = !IsLocalServer(_txtServerIP.Text);
                _rbLocalServer.Checked = !_rbPrivateServer.Checked;
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("ConnectionSetupForm.LoadCurrentConnection", ex);
            }
        }

        private void UpdateServerMode()
        {
            if (_rbPrivateServer.Checked)
            {
                if (IsLocalServer(_txtServerIP.Text))
                    _txtServerIP.Text = string.Empty;

                _lblModeHint.Text = "Use this on every client PC. The shared office SQL Server is the live source of truth for all users.";
                _txtServerIP.Focus();
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_txtServerIP.Text))
                    _txtServerIP.Text = "localhost";

                if (string.IsNullOrWhiteSpace(_txtInstance.Text))
                    _txtInstance.Text = "SQLEXPRESS";

                _lblModeHint.Text = "Use this for a single PC installation or when SQL Server runs on this computer.";
            }

            if (string.IsNullOrWhiteSpace(_txtDatabase.Text))
                _txtDatabase.Text = "HVAC_PRO";

            _lastTestSucceeded = false;
        }

        private void UpdateAuthFields()
        {
            bool sqlAuth = _rbSqlAuth.Checked;
            _txtUsername.Visible = sqlAuth;
            _txtPassword.Visible = sqlAuth;
            _lblUsername.Visible = sqlAuth;
            _lblPassword.Visible = sqlAuth;

            _lastTestSucceeded = false;
        }

        private static bool IsLocalServer(string server)
        {
            if (string.IsNullOrWhiteSpace(server))
                return true;

            string value = server.Trim();
            return string.Equals(value, "localhost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, ".", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "(local)", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, Environment.MachineName, StringComparison.OrdinalIgnoreCase);
        }

        private void TestConnection()
        {
            _lastTestSucceeded = false;
            _lastTestedConnectionString = null;
            _lblStatus.ForeColor = Color.DimGray;
            _lblStatus.Text = "Testing connection...";
            Application.DoEvents();

            try
            {
                string connectionString = BuildConnectionString();
                DatabaseConnectionTestResult result = DatabaseConnectionFactory.TestDatabaseConnectionAsync(connectionString, (int)_numMaxPoolSize.Value)
                    .GetAwaiter()
                    .GetResult();

                _lastTestSucceeded = result.Success;
                _lastTestedConnectionString = result.ConnectionString;
                _lblStatus.ForeColor = result.Success ? Color.ForestGreen : Color.Firebrick;
                _lblStatus.Text = result.Message;
                AppRuntime.LogConnection(result.Success ? "Connection setup test succeeded." : "Connection setup test failed.");
            }
            catch (Exception ex)
            {
                LocalSqliteFallbackStore.RecordSqlUnavailable(BuildConnectionStringForFallback(), ex);
                _lblStatus.ForeColor = Color.Firebrick;
                _lblStatus.Text = "Connection failed: " + ex.Message;
                AppRuntime.LogException("ConnectionSetupForm.TestConnection", ex);
            }
        }

        private void SaveConnection()
        {
            try
            {
                string connectionString = BuildConnectionString();
                if (!_lastTestSucceeded || !string.Equals(connectionString, _lastTestedConnectionString, StringComparison.Ordinal))
                {
                    _lblStatus.ForeColor = Color.Firebrick;
                    _lblStatus.Text = "Please test the connection successfully before saving.";
                    return;
                }

                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                ConnectionStringSettings setting = config.ConnectionStrings.ConnectionStrings[ConnectionStringName];
                if (setting == null)
                {
                    config.ConnectionStrings.ConnectionStrings.Add(
                        new ConnectionStringSettings(ConnectionStringName, connectionString, "System.Data.SqlClient"));
                }
                else
                {
                    setting.ConnectionString = connectionString;
                    setting.ProviderName = "System.Data.SqlClient";
                }

                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("connectionStrings");
                SaveInstallerDatabaseConfig(connectionString);
                ConfigService.Set("Database", "ServerRole", _rbPrivateServer.Checked ? "ClientPC" : "LocalSqlServer");
                NodeIdentityService.EnsureRegistered();
                AppRuntime.LogConnection("Connection string saved.");
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                _lblStatus.ForeColor = Color.Firebrick;
                _lblStatus.Text = "Save failed: " + ex.Message;
                AppRuntime.LogException("ConnectionSetupForm.SaveConnection", ex);
            }
        }

        private string BuildConnectionString()
        {
            string server = (_txtServerIP.Text ?? string.Empty).Trim();
            string instance = (_txtInstance.Text ?? string.Empty).Trim();
            string database = (_txtDatabase.Text ?? string.Empty).Trim();
            NormalizeServerAndInstance(ref server, ref instance);

            if (string.IsNullOrWhiteSpace(server))
                throw new InvalidOperationException("Server IP / Name is required.");
            if (string.IsNullOrWhiteSpace(database))
                throw new InvalidOperationException("Database is required.");
            if (_rbPrivateServer.Checked && IsLocalServer(server))
                throw new InvalidOperationException("For a client private server, enter the client's server IP address or server name.");

            var builder = new SqlConnectionStringBuilder
            {
                DataSource = string.IsNullOrWhiteSpace(instance) ? server : server + "\\" + instance,
                InitialCatalog = database,
                ConnectTimeout = DatabaseConnectionFactory.DefaultConnectTimeoutSeconds,
                Pooling = true,
                MinPoolSize = DatabaseConnectionFactory.DefaultMinPoolSize,
                MaxPoolSize = (int)_numMaxPoolSize.Value,
                IntegratedSecurity = _rbWindowsAuth.Checked
            };

            if (_rbSqlAuth.Checked)
            {
                builder.UserID = (_txtUsername.Text ?? string.Empty).Trim();
                builder.Password = _txtPassword.Text ?? string.Empty;
                builder.IntegratedSecurity = false;
            }

            return DatabaseConnectionFactory.NormalizeConnectionString(builder.ConnectionString, (int)_numMaxPoolSize.Value);
        }

        /// <summary>Normalizes server and instance fields so full named-instance entries are accepted safely.</summary>
        private void NormalizeServerAndInstance(ref string server, ref string instance)
        {
            if (string.IsNullOrWhiteSpace(server))
                return;

            int slashIndex = server.IndexOf('\\');
            if (slashIndex < 0)
                return;

            string serverName = server.Substring(0, slashIndex).Trim();
            string embeddedInstance = slashIndex >= server.Length - 1
                ? string.Empty
                : server.Substring(slashIndex + 1).Trim();

            if (string.IsNullOrWhiteSpace(serverName))
                return;

            if (string.IsNullOrWhiteSpace(instance) ||
                string.Equals(instance, embeddedInstance, StringComparison.OrdinalIgnoreCase))
            {
                server = serverName;
                instance = embeddedInstance;
                _txtServerIP.Text = server;
                _txtInstance.Text = instance;
                return;
            }

            throw new InvalidOperationException(
                "Enter the server name and SQL instance separately. Example: Server IP / Name = PC-5, Instance = SQLEXPRESS.");
        }

        private static void SaveInstallerDatabaseConfig(string connectionString)
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            ConfigService.Set("Database", "Server", builder.DataSource ?? string.Empty);
            ConfigService.Set("Database", "DatabaseName", builder.InitialCatalog ?? string.Empty);
            ConfigService.Set("Database", "UseWindowsAuth", builder.IntegratedSecurity ? "true" : "false");
            ConfigService.Set("Database", "Username", builder.IntegratedSecurity ? string.Empty : builder.UserID ?? string.Empty);
            ConfigService.Set("Database", "Password", builder.IntegratedSecurity ? string.Empty : HVAC_Pro_Desktop.Helpers.SecureStorageHelper.ProtectMachineText(builder.Password ?? string.Empty));
            DatabaseConnectionFactory.SetConfiguredMaxPoolSize(builder.MaxPoolSize);
            ConfigService.Set("Database", "ServerRole", "AlwaysOnOfficeServer");
            ConfigService.Set("Fallback", "Mode", "LocalSQLiteDiagnostics");
            ConfigService.Set("Fallback", "SqlitePath", LocalSqliteFallbackStore.GetDatabasePath());
            ConfigService.Set("Fallback", "AllowBusinessWrites", "false");
        }

        /// <summary>Builds the entered connection string for fallback logging only.</summary>
        private string BuildConnectionStringForFallback()
        {
            try
            {
                return BuildConnectionString();
            }
            catch
            {
                return DatabaseManager.GetConfiguredConnectionString();
            }
        }

    }
}



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
        private bool _lastTestSucceeded;
        private string _lastTestedConnectionString;

        public ConnectionSetupForm()
        {
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "Database Connection Setup";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 560);
            Font = new Font("Segoe UI", 9F);
            BackColor = DS.BgPage;

            BuildLayout();
            UIHelper.ApplyInputStyles(Controls);
            DS.ApplyTheme(this);
            LoadCurrentConnection();
            UpdateServerMode();
            UpdateAuthFields();
        }

        private void BuildLayout()
        {
            var title = new Label
            {
                Text = "ServoERP Database Connection",
                Font = new Font("Segoe UI", 15F, FontStyle.Bold),
                Location = new Point(24, 18),
                Size = new Size(500, 32)
            };

            var hint = new Label
            {
                Text = "Choose local setup or connect this client to the always-on office SQL Server.",
                ForeColor = Color.DimGray,
                Location = new Point(26, 54),
                Size = new Size(500, 24)
            };

            _rbLocalServer.Text = "This computer / local SQL Server";
            _rbLocalServer.Location = new Point(180, 88);
            _rbLocalServer.Size = new Size(250, 24);
            _rbLocalServer.Checked = true;
            _rbLocalServer.CheckedChanged += (s, e) => UpdateServerMode();

            _rbPrivateServer.Text = "Always-on office server";
            _rbPrivateServer.Location = new Point(180, 116);
            _rbPrivateServer.Size = new Size(220, 24);
            _rbPrivateServer.CheckedChanged += (s, e) => UpdateServerMode();

            _lblModeHint.Location = new Point(180, 144);
            _lblModeHint.Size = new Size(330, 42);
            _lblModeHint.ForeColor = Color.DimGray;

            AddLabel("Setup Type", 92);

            AddLabel("Server IP / Name", 198);
            _txtServerIP.Location = new Point(180, 194);
            _txtServerIP.Size = new Size(330, 24);

            AddLabel("Instance", 236);
            _txtInstance.Location = new Point(180, 232);
            _txtInstance.Size = new Size(330, 24);

            AddLabel("Database", 274);
            _txtDatabase.Location = new Point(180, 270);
            _txtDatabase.Size = new Size(330, 24);

            _rbWindowsAuth.Text = "Windows Authentication";
            _rbWindowsAuth.Location = new Point(180, 311);
            _rbWindowsAuth.Size = new Size(190, 24);
            _rbWindowsAuth.Checked = true;
            _rbWindowsAuth.CheckedChanged += (s, e) => UpdateAuthFields();

            _rbSqlAuth.Text = "SQL Authentication";
            _rbSqlAuth.Location = new Point(180, 338);
            _rbSqlAuth.Size = new Size(170, 24);
            _rbSqlAuth.CheckedChanged += (s, e) => UpdateAuthFields();

            AddLabel("Username", 378);
            _txtUsername.Location = new Point(180, 374);
            _txtUsername.Size = new Size(330, 24);

            AddLabel("Password", 416);
            _txtPassword.Location = new Point(180, 412);
            _txtPassword.Size = new Size(330, 24);
            _txtPassword.UseSystemPasswordChar = true;

            AddLabel("Max Pool Size", 454);
            _numMaxPoolSize.Location = new Point(180, 450);
            _numMaxPoolSize.Size = new Size(120, 24);
            _numMaxPoolSize.Minimum = 20;
            _numMaxPoolSize.Maximum = 500;
            _numMaxPoolSize.Value = DatabaseConnectionFactory.DefaultMaxPoolSize;

            var btnTestConnection = new Button
            {
                Text = "Test Connection",
                Location = new Point(180, 492),
                Size = new Size(130, 32)
            };
            btnTestConnection.Click += (s, e) => TestConnection();

            var btnSave = new Button
            {
                Text = "Save",
                Location = new Point(320, 492),
                Size = new Size(68, 32)
            };
            btnSave.Click += (s, e) => SaveConnection();

            var btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(396, 492),
                Size = new Size(64, 32)
            };
            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            _lblStatus.Location = new Point(24, 532);
            _lblStatus.Size = new Size(500, 24);
            _lblStatus.ForeColor = Color.DimGray;

            Controls.AddRange(new Control[]
            {
                title, hint, _rbLocalServer, _rbPrivateServer, _lblModeHint,
                _txtServerIP, _txtInstance, _txtDatabase,
                _rbWindowsAuth, _rbSqlAuth, _txtUsername, _txtPassword, _numMaxPoolSize,
                btnTestConnection, btnSave, btnCancel, _lblStatus
            });
        }

        private void AddLabel(string text, int y)
        {
            Controls.Add(new Label
            {
                Text = text,
                Location = new Point(28, y),
                Size = new Size(140, 24),
                TextAlign = ContentAlignment.MiddleLeft
            });
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

                _lblModeHint.Text = "Use this when the office has a dedicated server PC that stays on for all ServoERP users.";
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
            foreach (Control control in Controls)
            {
                if (control is Label label && (label.Text == "Username" || label.Text == "Password"))
                    label.Visible = sqlAuth;
            }

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
            ConfigService.Set("Database", "Password", builder.IntegratedSecurity ? string.Empty : builder.Password ?? string.Empty);
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



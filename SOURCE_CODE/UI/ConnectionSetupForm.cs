using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class ConnectionSetupForm : Form
    {
        public const string ConnectionStringName = "HVACPro_Connection";
        public const string DefaultConnectionString = @"Server=localhost\SQLEXPRESS;Database=HVAC_PRO;Integrated Security=True;";

        private readonly RadioButton _rbLocalServer = new RadioButton();
        private readonly RadioButton _rbPrivateServer = new RadioButton();
        private readonly TextBox _txtServerIP = new TextBox();
        private readonly TextBox _txtInstance = new TextBox();
        private readonly TextBox _txtDatabase = new TextBox();
        private readonly RadioButton _rbWindowsAuth = new RadioButton();
        private readonly RadioButton _rbSqlAuth = new RadioButton();
        private readonly TextBox _txtUsername = new TextBox();
        private readonly TextBox _txtPassword = new TextBox();
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
            ClientSize = new Size(560, 520);
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
                Text = "Choose local setup or connect this client to a private SQL Server.",
                ForeColor = Color.DimGray,
                Location = new Point(26, 54),
                Size = new Size(500, 24)
            };

            _rbLocalServer.Text = "This computer / local SQL Server";
            _rbLocalServer.Location = new Point(180, 88);
            _rbLocalServer.Size = new Size(250, 24);
            _rbLocalServer.Checked = true;
            _rbLocalServer.CheckedChanged += (s, e) => UpdateServerMode();

            _rbPrivateServer.Text = "Client private server";
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

            var btnTestConnection = new Button
            {
                Text = "Test Connection",
                Location = new Point(180, 454),
                Size = new Size(130, 32)
            };
            btnTestConnection.Click += (s, e) => TestConnection();

            var btnSave = new Button
            {
                Text = "Save",
                Location = new Point(320, 454),
                Size = new Size(68, 32)
            };
            btnSave.Click += (s, e) => SaveConnection();

            var btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(396, 454),
                Size = new Size(64, 32)
            };
            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            _lblStatus.Location = new Point(24, 492);
            _lblStatus.Size = new Size(500, 24);
            _lblStatus.ForeColor = Color.DimGray;

            Controls.AddRange(new Control[]
            {
                title, hint, _rbLocalServer, _rbPrivateServer, _lblModeHint,
                _txtServerIP, _txtInstance, _txtDatabase,
                _rbWindowsAuth, _rbSqlAuth, _txtUsername, _txtPassword,
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

            string connectionString = DatabaseManager.GetConfiguredConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return;
            }

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

                _lblModeHint.Text = "Use this when the client's data is hosted on their own office/server machine. Enter IP or server name.";
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
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                }

                _lastTestSucceeded = true;
                _lastTestedConnectionString = connectionString;
                _lblStatus.ForeColor = Color.ForestGreen;
                _lblStatus.Text = "Connected successfully.";
                AppRuntime.LogConnection("Connection setup test succeeded.");
            }
            catch (Exception ex)
            {
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
                ConnectTimeout = 5,
                IntegratedSecurity = _rbWindowsAuth.Checked
            };

            if (_rbSqlAuth.Checked)
            {
                builder.UserID = (_txtUsername.Text ?? string.Empty).Trim();
                builder.Password = _txtPassword.Text ?? string.Empty;
                builder.IntegratedSecurity = false;
            }

            return builder.ConnectionString;
        }

        private static void SaveInstallerDatabaseConfig(string connectionString)
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            ConfigService.Set("Database", "Server", builder.DataSource ?? string.Empty);
            ConfigService.Set("Database", "DatabaseName", builder.InitialCatalog ?? string.Empty);
            ConfigService.Set("Database", "UseWindowsAuth", builder.IntegratedSecurity ? "true" : "false");
            ConfigService.Set("Database", "Username", builder.IntegratedSecurity ? string.Empty : builder.UserID ?? string.Empty);
            ConfigService.Set("Database", "Password", builder.IntegratedSecurity ? string.Empty : builder.Password ?? string.Empty);
        }
    }
}


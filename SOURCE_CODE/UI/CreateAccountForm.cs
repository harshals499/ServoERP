using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class CreateAccountForm : Form
    {
        private readonly AuthService _authService = new AuthService();
        private TextBox _txtDisplayName;
        private TextBox _txtUsername;
        private TextBox _txtPassword;
        private TextBox _txtConfirmPassword;
        private ComboBox _cmbRole;
        private TextBox _txtAdminUsername;
        private TextBox _txtAdminPassword;
        private Label _lblStatus;
        private bool _hasUsers;

        public string CreatedUsername { get; private set; }

        public CreateAccountForm()
        {
            Text = BrandingService.WindowTitle("Create Account");
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(520, 640);
            MinimumSize = new Size(500, 600);
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(248, 251, 255);
            Font = new Font("Segoe UI", 9f);
            Build();
            LoadRoles();
        }

        private void Build()
        {
            Controls.Add(new Label
            {
                Text = "Create ServoERP Account",
                Location = new Point(28, 22),
                Size = new Size(440, 34),
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = Color.FromArgb(8, 22, 61)
            });

            Controls.Add(new Label
            {
                Text = "New accounts are created locally. Existing installations require Admin approval.",
                Location = new Point(30, 62),
                Size = new Size(440, 36),
                ForeColor = Color.FromArgb(71, 85, 125)
            });

            int y = 116;
            _txtDisplayName = AddText("Display name", y, false); y += 72;
            _txtUsername = AddText("Username / email", y, false); y += 72;
            _txtPassword = AddText("Password", y, true); y += 72;
            _txtConfirmPassword = AddText("Confirm password", y, true); y += 72;

            Controls.Add(Label("Role", y));
            _cmbRole = new ComboBox
            {
                Location = new Point(30, y + 24),
                Size = new Size(440, 30),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            Controls.Add(_cmbRole);
            y += 78;

            Controls.Add(new Label
            {
                Text = "Admin approval",
                Location = new Point(30, y),
                Size = new Size(180, 20),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(8, 22, 61)
            });
            y += 26;
            _txtAdminUsername = AddText("Admin username", y, false); y += 72;
            _txtAdminPassword = AddText("Admin password", y, true); y += 70;

            Button create = new Button
            {
                Text = "Create Account",
                Location = new Point(30, y),
                Size = new Size(210, 42),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };
            create.FlatAppearance.BorderSize = 0;
            create.Click += (s, e) => CreateAccount();
            Controls.Add(create);

            Button cancel = new Button
            {
                Text = "Cancel",
                Location = new Point(260, y),
                Size = new Size(210, 42),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(51, 65, 85),
                Font = new Font("Segoe UI", 10f)
            };
            cancel.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            cancel.Click += (s, e) => Close();
            Controls.Add(cancel);

            _lblStatus = new Label
            {
                Location = new Point(30, y + 52),
                Size = new Size(440, 42),
                ForeColor = Color.FromArgb(220, 38, 38)
            };
            Controls.Add(_lblStatus);
        }

        private TextBox AddText(string label, int y, bool password)
        {
            Controls.Add(Label(label, y));
            var textBox = new TextBox
            {
                Location = new Point(30, y + 24),
                Size = new Size(440, 30),
                UseSystemPasswordChar = password
            };
            Controls.Add(textBox);
            return textBox;
        }

        private static Label Label(string text, int y)
        {
            return new Label
            {
                Text = text,
                Location = new Point(30, y),
                Size = new Size(250, 20),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(51, 65, 85)
            };
        }

        private void LoadRoles()
        {
            try
            {
                _hasUsers = _authService.HasAnyUsers();
                RoleDto[] roles = _authService.GetRoles()
                    .Where(r => _hasUsers || string.Equals(r.RoleName, "Admin", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                _cmbRole.DisplayMember = "RoleName";
                _cmbRole.ValueMember = "RoleId";
                _cmbRole.DataSource = roles;

                if (!_hasUsers)
                {
                    _txtAdminUsername.Enabled = false;
                    _txtAdminPassword.Enabled = false;
                    _txtAdminUsername.Text = "First account setup";
                    _txtAdminPassword.Text = string.Empty;
                    _lblStatus.ForeColor = Color.FromArgb(37, 99, 235);
                    _lblStatus.Text = "No users found. This will create the first Admin account.";
                }
                else
                {
                    RoleDto viewer = roles.FirstOrDefault(r => string.Equals(r.RoleName, "Viewer", StringComparison.OrdinalIgnoreCase));
                    if (viewer != null)
                        _cmbRole.SelectedItem = viewer;
                }
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("CreateAccount.LoadRoles", ex);
                _lblStatus.Text = "Unable to load account roles.";
            }
        }

        private void CreateAccount()
        {
            string username = (_txtUsername.Text ?? string.Empty).Trim();
            string displayName = (_txtDisplayName.Text ?? string.Empty).Trim();
            string password = _txtPassword.Text ?? string.Empty;
            string confirm = _txtConfirmPassword.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(username))
            {
                SetError("Display name and username are required.");
                return;
            }

            if (!string.Equals(password, confirm, StringComparison.Ordinal))
            {
                SetError("Passwords do not match.");
                return;
            }

            if (_cmbRole.SelectedItem == null)
            {
                SetError("Select a role.");
                return;
            }

            if (_hasUsers && !_authService.ValidateAdminCredentials(_txtAdminUsername.Text, _txtAdminPassword.Text))
            {
                SetError("Admin approval failed.");
                return;
            }

            var role = (RoleDto)_cmbRole.SelectedItem;
            var result = _authService.CreateUserWithPassword(username, displayName, role.RoleId, password, false);
            if (!result.Success)
            {
                SetError(result.ErrorMessage);
                return;
            }

            CreatedUsername = username;
            MessageBox.Show("Account created. You can sign in now.", BrandingService.WindowTitle("Create Account"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void SetError(string message)
        {
            _lblStatus.ForeColor = Color.FromArgb(220, 38, 38);
            _lblStatus.Text = message ?? "Unable to create account.";
        }
    }
}

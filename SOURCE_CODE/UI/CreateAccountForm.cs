using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.UI.Controls;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class CreateAccountForm : ServoERP.Infrastructure.ServoFormBase
    {
        private readonly AuthService _authService = new AuthService();
        private Panel _card;
        private ModernTextBox _txtDisplayName;
        private ModernTextBox _txtUsername;
        private ModernTextBox _txtPassword;
        private ModernTextBox _txtConfirmPassword;
        private ComboBox _cmbRole;
        private Label _lblStatus;
        private bool _hasUsers;

        public string CreatedUsername { get; private set; }

        public CreateAccountForm()
        {
            Text = BrandingService.WindowTitle("Create Account");
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(560, 620);
            MinimumSize = new Size(540, 600);
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(239, 246, 255);
            Font = new Font("Segoe UI", 9f);
            Build();
            LoadRoles();
            Shown += (s, e) => _txtDisplayName.InnerTextBox.Focus();
        }

        private void Build()
        {
            _card = new BufferedPanel
            {
                BackColor = Color.White,
                Location = new Point(28, 24),
                Size = new Size(504, 566)
            };
            _card.Paint += DrawCard;
            Controls.Add(_card);

            _card.Controls.Add(new Label
            {
                Text = "Create ServoERP Account",
                Location = new Point(34, 26),
                Size = new Size(440, 34),
                Font = new Font("Segoe UI", 18.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(8, 22, 61)
            });

            _card.Controls.Add(new Label
            {
                Text = "New accounts are created locally on this workstation.",
                Location = new Point(36, 62),
                Size = new Size(440, 36),
                ForeColor = Color.FromArgb(71, 85, 125)
            });

            int y = 102;
            _txtDisplayName = AddText("Display name", y, false, 0); y += 74;
            _txtUsername = AddText("Username / email", y, false, 1); y += 74;
            _txtPassword = AddText("Password", y, true, 2); y += 74;
            _txtConfirmPassword = AddText("Confirm password", y, true, 3); y += 74;

            _card.Controls.Add(Label("Role", y));
            _cmbRole = new ComboBox
            {
                Location = new Point(36, y + 24),
                Size = new Size(432, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10f),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(15, 23, 42)
            };
            _cmbRole.TabIndex = 4;
            _card.Controls.Add(_cmbRole);
            y += 68;

            Button create = new Button
            {
                Text = "Create Account",
                Location = new Point(36, y),
                Size = new Size(206, 42),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 91, 224),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold)
            };
            create.FlatAppearance.BorderSize = 0;
            create.FlatAppearance.MouseOverBackColor = Color.FromArgb(37, 99, 235);
            create.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 64, 175);
            create.Click += (s, e) => CreateAccount();
            create.TabIndex = 5;
            _card.Controls.Add(create);
            AcceptButton = create;

            Button cancel = new Button
            {
                Text = "Cancel",
                Location = new Point(262, y),
                Size = new Size(206, 42),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(51, 65, 85),
                Font = new Font("Segoe UI", 10.5f)
            };
            cancel.FlatAppearance.BorderColor = DS.Border;
            cancel.Click += (s, e) => Close();
            cancel.TabIndex = 6;
            _card.Controls.Add(cancel);
            CancelButton = cancel;

            _lblStatus = new Label
            {
                Location = new Point(36, y + 52),
                Size = new Size(432, 48),
                ForeColor = Color.FromArgb(220, 38, 38),
                Font = new Font("Segoe UI", 9.5f)
            };
            _card.Controls.Add(_lblStatus);
        }

        private ModernTextBox AddText(string label, int y, bool password, int tabIndex)
        {
            _card.Controls.Add(Label(label, y));
            var textBox = new ModernTextBox
            {
                Location = new Point(36, y + 24),
                Size = new Size(432, 50),
                UseSystemPasswordChar = password,
                Placeholder = label,
                LeadingIcon = password ? '\uE72E' : '\uE77B',
                TabIndex = tabIndex,
                TabStop = true
            };
            textBox.InnerTextBox.TabIndex = 0;
            _card.Controls.Add(textBox);
            return textBox;
        }

        private static Label Label(string text, int y)
        {
            return new Label
            {
                Text = text,
                Location = new Point(36, y),
                Size = new Size(250, 20),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59)
            };
        }

        private void LoadRoles()
        {
            try
            {
                _hasUsers = _authService.HasAnyUsers();
                RoleDto[] roles = _authService.GetRoles()
                    .Where(r =>
                        (!_hasUsers && string.Equals(r.RoleName, "Admin", StringComparison.OrdinalIgnoreCase)) ||
                        (_hasUsers && string.Equals(r.RoleName, "Admin", StringComparison.OrdinalIgnoreCase)))
                    .ToArray();

                if (_hasUsers && roles.Length == 0)
                {
                    roles = _authService.GetRoles()
                        .Take(1)
                        .ToArray();
                }

                _cmbRole.DisplayMember = "RoleName";
                _cmbRole.ValueMember = "RoleId";
                _cmbRole.DataSource = roles;

                if (!_hasUsers)
                {
                    _lblStatus.ForeColor = Color.FromArgb(37, 99, 235);
                    _lblStatus.Text = "No users found. This will create the first Admin account.";
                }
                else
                {
                    RoleDto admin = roles.FirstOrDefault(r => string.Equals(r.RoleName, "Admin", StringComparison.OrdinalIgnoreCase));
                    if (admin != null)
                        _cmbRole.SelectedItem = admin;
                    _cmbRole.Enabled = roles.Length > 1;
                    _lblStatus.ForeColor = Color.FromArgb(37, 99, 235);
                    _lblStatus.Text = "New accounts receive full ServoERP access.";
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

        private void DrawCard(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, _card.Width - 1, _card.Height - 1);
            using (GraphicsPath path = Rounded(rect, 18))
            using (SolidBrush bg = new SolidBrush(Color.White))
            using (Pen border = new Pen(DS.Border))
            {
                e.Graphics.FillPath(bg, path);
                e.Graphics.DrawPath(border, path);
            }
        }

        private static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private sealed class BufferedPanel : Panel
        {
            public BufferedPanel()
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
                SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.UserPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw,
                    true);
                UpdateStyles();
            }
        }
    }
}


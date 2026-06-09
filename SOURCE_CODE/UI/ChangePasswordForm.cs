using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public class ChangePasswordForm : ServoERP.Infrastructure.ServoFormBase
    {
        private readonly AuthService _authService = new AuthService();
        private readonly int _userId;
        private readonly string _resetUsername;
        private readonly bool _selfServiceReset;
        private TextBox _txtCurrent;
        private TextBox _txtNew;
        private TextBox _txtConfirm;
        private Label _lblStrength;
        private Label _lblStatus;
        private Button _btnSave;

        public ChangePasswordForm(int userId, bool forceChange)
        {
            _userId = userId;
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = forceChange ? "Change Password Required" : "Change Password";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(420, 280);
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9);
            BuildLayout(forceChange);
            UIHelper.ApplyInputStyles(Controls);
            DS.ApplyTheme(this);
        }

        public ChangePasswordForm(string usernameOrEmail)
        {
            _resetUsername = usernameOrEmail ?? string.Empty;
            _selfServiceReset = true;
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "Reset Password";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(420, 280);
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9);
            BuildLayout(false);
            UIHelper.ApplyInputStyles(Controls);
            DS.ApplyTheme(this);
        }

        private void BuildLayout(bool forceChange)
        {
            Controls.Add(new Label
            {
                Text = _selfServiceReset ? "Set a new password for your ServoERP account." : forceChange ? "Set a new password before continuing." : "Update your account password.",
                Location = new Point(24, 20),
                Size = new Size(360, 22),
                ForeColor = Color.FromArgb(71, 85, 105)
            });

            _txtCurrent = CreatePasswordBox(new Point(24, 58));
            if (_selfServiceReset)
            {
                _txtCurrent.PasswordChar = '\0';
                _txtCurrent.Text = _resetUsername;
            }
            _txtNew = CreatePasswordBox(new Point(24, 106));
            _txtConfirm = CreatePasswordBox(new Point(24, 154));
            _txtNew.TextChanged += (s, e) => UpdateStrength();

            Controls.Add(MakeFieldLabel(_selfServiceReset ? "Username / Email" : "Current Password", 24, 42));
            Controls.Add(MakeFieldLabel("New Password", 24, 90));
            Controls.Add(MakeFieldLabel("Confirm New Password", 24, 138));
            Controls.Add(_txtCurrent);
            Controls.Add(_txtNew);
            Controls.Add(_txtConfirm);

            _lblStrength = new Label
            {
                Location = new Point(24, 190),
                Size = new Size(180, 18),
                ForeColor = Color.FromArgb(71, 85, 105)
            };
            Controls.Add(_lblStrength);

            _lblStatus = new Label
            {
                Location = new Point(24, 212),
                Size = new Size(360, 18),
                ForeColor = Color.Firebrick
            };
            Controls.Add(_lblStatus);

            _btnSave = new Button
            {
                Text = _selfServiceReset ? "Reset Password" : "Save Password",
                Size = new Size(132, 38),
                Location = new Point(252, 226),
                BackColor = Color.FromArgb(13, 148, 136),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.Click += (s, e) => SavePassword();
            Controls.Add(_btnSave);

            AcceptButton = _btnSave;
            UpdateStrength();
        }

        private void SavePassword()
        {
            _lblStatus.ForeColor = Color.Firebrick;
            _lblStatus.Text = string.Empty;

            if (_txtNew.Text != _txtConfirm.Text)
            {
                _lblStatus.Text = "New password and confirm password do not match.";
                return;
            }

            if (!SecurityHelpers.MeetsPasswordPolicy(_txtNew.Text))
            {
                _lblStatus.Text = "Password must be 8+ chars with 1 uppercase and 1 number.";
                return;
            }

            _btnSave.Enabled = false;
            bool ok;
            string errorMessage = null;
            if (_selfServiceReset)
            {
                var result = _authService.ResetOwnPassword(_txtCurrent.Text, _txtNew.Text);
                ok = result.Success;
                errorMessage = result.ErrorMessage;
            }
            else
            {
                ok = _authService.ChangePassword(_userId, _txtCurrent.Text, _txtNew.Text);
            }
            _btnSave.Enabled = true;

            if (!ok)
            {
                _lblStatus.Text = string.IsNullOrWhiteSpace(errorMessage) ? "Password change failed. Check your current password." : errorMessage;
                return;
            }

            _lblStatus.ForeColor = Color.FromArgb(22, 163, 74);
            _lblStatus.Text = _selfServiceReset ? "Password reset." : "Password changed.";
            DialogResult = DialogResult.OK;
            Close();
        }

        private void UpdateStrength()
        {
            string password = _txtNew?.Text ?? string.Empty;
            if (password.Length < 8)
            {
                _lblStrength.Text = "Strength: Weak";
                _lblStrength.ForeColor = Color.Firebrick;
                return;
            }

            bool strong = password.Any(char.IsUpper) && password.Any(char.IsDigit);
            _lblStrength.Text = strong ? "Strength: Strong" : "Strength: Fair";
            _lblStrength.ForeColor = strong ? Color.FromArgb(22, 163, 74) : Color.FromArgb(245, 158, 11);
        }

        private static TextBox CreatePasswordBox(Point location)
        {
            return new TextBox
            {
                Location = location,
                Size = new Size(360, 30),
                BorderStyle = BorderStyle.FixedSingle,
                PasswordChar = '*'
            };
        }

        private static Label MakeFieldLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(220, 18),
                ForeColor = Color.FromArgb(30, 41, 59)
            };
        }
    }
}



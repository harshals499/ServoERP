using System;
using System.Drawing;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.Services.Licensing;

namespace HVAC_Pro_Desktop.UI.Licensing
{
    public sealed class LicenseActivationForm : Form
    {
        private readonly LicenseService _licenseService = new LicenseService();
        private readonly DeviceFingerprintService _fingerprint = new DeviceFingerprintService();
        private TextBox _txtLicenseKey;
        private TextBox _txtCompany;
        private Label _lblStatus;

        public bool ActivationCompleted { get; private set; }

        public LicenseActivationForm()
        {
            Text = BrandingService.WindowTitle("License Activation");
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(640, 430);
            MinimumSize = new Size(560, 380);
            BackColor = DS.BgPage;
            Font = new Font("Segoe UI", 9f);
            Build();
        }

        private void Build()
        {
            var title = new Label
            {
                Text = "Activate ServoERP",
                Location = new Point(28, 24),
                Size = new Size(560, 36),
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = DS.Slate900
            };
            Controls.Add(title);

            Controls.Add(new Label
            {
                Text = "Start a 14-day trial, use online activation when available, or import a signed offline license file from ServoERP.",
                Location = new Point(30, 66),
                Size = new Size(560, 46),
                ForeColor = DS.Slate600
            });

            Controls.Add(Label("License Key", 32, 128));
            _txtLicenseKey = new TextBox { Location = new Point(32, 150), Size = new Size(260, 30) };
            Controls.Add(_txtLicenseKey);

            Controls.Add(Label("Company Name", 318, 128));
            _txtCompany = new TextBox { Location = new Point(318, 150), Size = new Size(260, 30) };
            Controls.Add(_txtCompany);

            var fingerprintLabel = new Label
            {
                Text = "Device fingerprint:\r\n" + _fingerprint.GetFingerprintHash(),
                Location = new Point(32, 204),
                Size = new Size(420, 54),
                ForeColor = DS.Slate500
            };
            Controls.Add(fingerprintLabel);

            Button copyFingerprint = Button("Copy Fingerprint", 462, 210, Color.White);
            copyFingerprint.ForeColor = DS.Slate700;
            copyFingerprint.FlatAppearance.BorderColor = DS.Slate300;
            copyFingerprint.FlatAppearance.BorderSize = 1;
            copyFingerprint.Click += (s, e) =>
            {
                Clipboard.SetText(_fingerprint.GetFingerprintHash());
                _lblStatus.ForeColor = DS.Teal600;
                _lblStatus.Text = "Device fingerprint copied. Send it to ServoERP support for offline license issuance.";
            };
            Controls.Add(copyFingerprint);

            Button trial = Button("Start Trial", 32, 284, DS.Primary600);
            trial.Click += (s, e) => StartTrial();
            Controls.Add(trial);

            Button online = Button("Activate Online", 196, 284, DS.Teal600);
            online.Click += (s, e) => ActivateOnline();
            Controls.Add(online);

            Button offline = Button("Import Offline File", 360, 284, Color.White);
            offline.ForeColor = DS.Slate700;
            offline.FlatAppearance.BorderColor = DS.Slate300;
            offline.FlatAppearance.BorderSize = 1;
            offline.Click += (s, e) => ImportOffline();
            Controls.Add(offline);

            Button close = Button("Close", 430, 334, Color.White);
            close.ForeColor = DS.Slate700;
            close.FlatAppearance.BorderColor = DS.Slate300;
            close.FlatAppearance.BorderSize = 1;
            close.Click += (s, e) => Close();
            Controls.Add(close);

            _lblStatus = new Label
            {
                Location = new Point(32, 334),
                Size = new Size(546, 44),
                ForeColor = DS.Slate700
            };
            Controls.Add(_lblStatus);
        }

        private void ActivateOnline()
        {
            var result = _licenseService.ActivateOnline(new LicenseActivationRequest
            {
                LicenseKey = _txtLicenseKey.Text.Trim(),
                CompanyName = _txtCompany.Text.Trim()
            });
            HandleResult(result);
        }

        private void StartTrial()
        {
            string company = _txtCompany.Text.Trim();
            if (string.IsNullOrWhiteSpace(company))
            {
                _lblStatus.ForeColor = Color.FromArgb(220, 38, 38);
                _lblStatus.Text = "Enter the company name before starting the trial.";
                _txtCompany.Focus();
                return;
            }

            HandleResult(_licenseService.ActivateTrial(company));
        }

        private void ImportOffline()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select signed ServoERP license file";
                dialog.Filter = "ServoERP License (*.servoerp-license;*.json)|*.servoerp-license;*.json|All files (*.*)|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                HandleResult(_licenseService.ActivateOffline(dialog.FileName));
            }
        }

        private void HandleResult(LicenseValidationResult result)
        {
            _lblStatus.ForeColor = result.Success ? DS.Teal600 : Color.FromArgb(220, 38, 38);
            _lblStatus.Text = result.Message;
            if (result.Success)
            {
                ActivationCompleted = true;
                MessageBox.Show("License activated.", BrandingService.WindowTitle("License"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private static Label Label(string text, int x, int y)
        {
            return new Label { Text = text, Location = new Point(x, y), Size = new Size(220, 20), Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = DS.Slate700 };
        }

        private static Button Button(string text, int x, int y, Color color)
        {
            var button = new Button { Text = text, Location = new Point(x, y), Size = new Size(148, 38), FlatStyle = FlatStyle.Flat, BackColor = color, ForeColor = color == Color.White ? DS.Slate700 : Color.White };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }
    }
}

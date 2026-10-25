using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ValidationException = FluentValidation.ValidationException;
using HVAC_Pro_Desktop.Services;
using QRCoder;
using Serilog;

namespace HVAC_Pro_Desktop.UI
{
    public partial class SettingsForm
    {
        internal bool MobileConnectionPreviewOnly { get; set; }
        private Button _saveMobileConnection;

        private Control BuildMobileConnectionContent()
        {
            bool admin = IsAdminUser();
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = DS.BgPage,
                Name = "MobileConnectionPage", Padding = new Padding(20), Tag = "NO_CARD_SURFACE" };
            var layout = new FlowLayoutPanel { Location = new Point(20, 20), AutoSize = true,
                FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = DS.BgPage };
            scroll.Controls.Add(layout);
            Func<string, Label> text = value => new Label { Text = value, AutoSize = false,
                Font = DS.Body, ForeColor = DS.Slate600, Margin = new Padding(0, 0, 0, 12), Height = 52 };
            var heading = text("Mobile Connection"); heading.Font = new Font("Segoe UI", 18, FontStyle.Bold); heading.ForeColor = DS.Slate900; heading.Height = 42;
            layout.Controls.Add(heading);
            var intro = text("Connect ServoERP Mobile to your office. Scan the QR or enter the office details, then sign in with your office user ID and password. The QR does not sign anyone in.");
            layout.Controls.Add(intro);
            var columns = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = DS.BgPage, Margin = Padding.Empty };
            layout.Controls.Add(columns);
            var setup = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false,
                AutoSize = true, Padding = new Padding(18), Margin = new Padding(0, 0, 16, 16), BackColor = Color.White, Width = 510 };
            var pairing = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false,
                AutoSize = true, Padding = new Padding(18), Margin = new Padding(0, 0, 0, 16), BackColor = Color.White, Width = 390 };
            columns.Controls.Add(setup); columns.Controls.Add(pairing);
            Action<FlowLayoutPanel, string> label = (panel, value) => { var item = text(value); item.Height = 26; panel.Controls.Add(item); };
            label(setup, "1. Office connection details");
            var note = text("An administrator supplies these details from the existing office HTTPS service. Saving here does not install a server or open network ports."); note.Height = 66; setup.Controls.Add(note);
            label(setup, "Office HTTPS addresses (one per line)");
            var addresses = new TextBox { Name = "MobileOfficeAddresses", Multiline = true, Height = 72,
                ScrollBars = ScrollBars.Vertical, MaxLength = 804, ReadOnly = !admin, Font = DS.Body, Margin = new Padding(0, 0, 0, 14) };
            setup.Controls.Add(addresses);
            label(setup, "Company number provided by your office");
            var company = new TextBox { Name = "MobileCompanyNumber", MaxLength = 10, ReadOnly = !admin,
                Font = DS.Body, Margin = new Padding(0, 0, 0, 14) }; setup.Controls.Add(company);
            label(setup, "Certificate SHA-256 fingerprint");
            var fingerprint = new TextBox { Name = "MobileCertificateFingerprint", MaxLength = 95,
                ReadOnly = !admin, Font = DS.Body, Margin = new Padding(0, 0, 0, 14) }; setup.Controls.Add(fingerprint);
            var trust = text("Use the exact fingerprint for a private certificate. Leave blank only for a publicly trusted certificate. Verify these details with your office administrator."); trust.Height = 66; setup.Controls.Add(trust);
            var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = Padding.Empty };
            setup.Controls.Add(actions);
            var preview = MakeBtn("Show QR", InfoBlue, 116); preview.Name = "ShowMobileQr";
            var save = MakeBtn("Save connection", InfoBlue, 152); save.Enabled = admin; save.Name = "SaveMobileConnection";
            _saveMobileConnection = save;
            var import = MakeBtn("Import profile", Color.White, 140); import.Enabled = admin;
            actions.Controls.AddRange(new Control[] { preview, save, import });
            var status = text(admin ? "Enter your office details or import its connection profile." : "Your administrator manages these details. Contact the office if no connection is configured.");
            status.Name = "MobileConnectionStatus"; status.Height = 76; setup.Controls.Add(status);

            label(pairing, "2. Scan or use your office login");
            var qr = new PictureBox { Name = "MobileConnectionQr", Size = new Size(320, 320), SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Color.White, AccessibleName = "Office connection QR code", Margin = new Padding(0, 0, 0, 12) };
            pairing.Controls.Add(qr);
            var steps = text("In ServoERP Mobile, open Connect to desktop > Photograph office QR. Review the office details, then enter your office user ID and password. You can also import the profile or paste its connection code."); steps.Height = 100; pairing.Controls.Add(steps);
            var code = new TextBox { Name = "MobileConnectionCode", ReadOnly = true, Multiline = true,
                Height = 58, ScrollBars = ScrollBars.Vertical, Font = DS.Small, AccessibleName = "Office connection code" }; pairing.Controls.Add(code);
            var sharing = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = Padding.Empty };
            var copy = MakeBtn("Copy code", Color.White, 120); copy.Enabled = false;
            var export = MakeBtn("Export profile", Color.White, 140); export.Enabled = false;
            sharing.Controls.AddRange(new Control[] { copy, export }); pairing.Controls.Add(sharing);
            var manual = text("Manual sign-in: choose Use office server sign-in and enter an office address, company number and certificate fingerprint shown here, then your user ID and password. User ID and password alone do not locate an office server."); manual.Height = 100; layout.Controls.Add(manual);
            var users = MakeBtn("Manage office users", Color.White, 200); users.Enabled = admin; users.Name = "ManageMobileOfficeUsers";
            users.Click += (s, e) => OpenUserManagementTab(); layout.Controls.Add(users);
            var accountHelp = text("Office administrators create accounts in Users & Logins. Complete any required password change on desktop first. Field engineers also need a linked employee, company access and job permissions. Keep the office PC online and use office Wi-Fi or your existing office VPN."); accountHelp.Height = 100; layout.Controls.Add(accountHelp);

            MobileConnectionProfile shownProfile = null;
            Action clear = () => { shownProfile = null; var old = qr.Image; qr.Image = null; old?.Dispose(); code.Clear(); copy.Enabled = export.Enabled = false; };
            Action changed = () => { clear(); status.Text = "Details changed. Show the QR to review them, then save the connection. Server availability has not been checked."; };
            addresses.TextChanged += (s, e) => changed(); company.TextChanged += (s, e) => changed(); fingerprint.TextChanged += (s, e) => changed();
            Func<MobileConnectionProfile> read = () => {
                int id; int.TryParse(company.Text.Trim(), out id);
                var profile = new MobileConnectionProfile { CompanyId = id, CertificateSha256 = fingerprint.Text.Trim(),
                    Addresses = addresses.Lines.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).ToList() };
                profile.Validate(); return profile;
            };
            Action<MobileConnectionProfile> show = profile => {
                clear(); string payload = MobileConnectionService.ToCode(profile);
                using (var generator = new QRCodeGenerator())
                using (var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M))
                using (var renderer = new QRCode(data))
                    qr.Image = renderer.GetGraphic(Math.Max(1, 320 / data.ModuleMatrix.Count));
                shownProfile = profile; code.Text = payload; copy.Enabled = export.Enabled = true;
                status.Text = "QR ready. It contains office details only, with no password or access token. Server availability has not been checked.";
            };
            Action<Exception> error = ex => {
                clear();
                status.Text = ex is ValidationException ? ((ValidationException)ex).Errors.Select(v => v.ErrorMessage).FirstOrDefault() :
                    "ServoERP could not use these connection details. Check the profile, office address and company number, then try again.";
                // Do not log supplied profiles, certificate details or file contents.
                Log.Warning("Mobile connection operation failed ({FailureType})", ex.GetType().Name);
            };
            preview.Click += (s, e) => { try { show(read()); } catch (Exception ex) { error(ex); } };
            save.Click += async (s, e) => {
                if (!IsAdminUser() || MobileConnectionPreviewOnly) return;
                save.Enabled = false;
                addresses.Enabled = company.Enabled = fingerprint.Enabled = import.Enabled = false;
                try { var profile = read(); await System.Threading.Tasks.Task.Run(() => MobileConnectionService.Save(profile)); show(profile); status.Text = "Office connection saved. Users must sign in with their own office accounts. Server availability has not been checked."; }
                catch (Exception ex) { error(ex); }
                finally { if (!save.IsDisposed) { save.Enabled = import.Enabled = IsAdminUser(); addresses.Enabled = company.Enabled = fingerprint.Enabled = true; } }
            };
            import.Click += (s, e) => {
                if (!IsAdminUser()) return;
                using (var dialog = new OpenFileDialog { Title = "Import public office connection profile", Filter = "Office connection profile|*.json;*.txt", CheckFileExists = true })
                {
                    if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
                    try {
                        if (new FileInfo(dialog.FileName).Length > 8192) throw new ArgumentException("Profile too large.");
                        var profile = MobileConnectionService.Parse(File.ReadAllText(dialog.FileName));
                        addresses.Text = string.Join(Environment.NewLine, profile.Addresses); company.Text = profile.CompanyId.ToString(); fingerprint.Text = profile.CertificateSha256;
                        show(profile); status.Text = "Profile imported for review. Select Save connection to keep it for this office.";
                    } catch (Exception ex) { error(ex); }
                }
            };
            copy.Click += (s, e) => { try { if (shownProfile != null) Clipboard.SetText(MobileConnectionService.ToCode(shownProfile)); } catch (Exception ex) { error(ex); } };
            export.Click += (s, e) => {
                if (shownProfile == null) return;
                using (var dialog = new SaveFileDialog { Title = "Export public office connection profile", Filter = "Office connection profile|*.servo-office.json", FileName = "office.servo-office.json" })
                {
                    if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
                    try { File.WriteAllText(dialog.FileName, MobileConnectionService.ToJson(shownProfile)); } catch (Exception ex) { error(ex); }
                }
            };
            qr.Disposed += (s, e) => { var image = qr.Image; qr.Image = null; image?.Dispose(); };
            Action resize = () => {
                int width = Math.Max(350, scroll.ClientSize.Width - 56);
                layout.MaximumSize = columns.MaximumSize = new Size(width, 0);
                layout.Width = columns.Width = width;
                setup.Width = Math.Min(510, width - 16); pairing.Width = Math.Min(390, width);
                setup.MinimumSize = new Size(Math.Min(510, width - 16), 0); pairing.MinimumSize = new Size(Math.Min(390, width), 0);
                setup.MaximumSize = new Size(setup.Width, 0); pairing.MaximumSize = new Size(pairing.Width, 0);
                foreach (Control item in layout.Controls) if (item != columns) item.Width = width;
                foreach (var panel in new[] { setup, pairing })
                    foreach (Control item in panel.Controls) if (item != qr) item.Width = panel.Width - panel.Padding.Horizontal;
            };
            scroll.Resize += (s, e) => resize(); resize();
            scroll.HandleCreated += async (s, e) => {
                if (MobileConnectionPreviewOnly) return;
                try {
                    string saved = await System.Threading.Tasks.Task.Run(() => new SettingsService().Get(MobileConnectionService.SettingsKey));
                    if (scroll.IsDisposed || string.IsNullOrWhiteSpace(saved)) return;
                    // Do not replace details the administrator started entering while loading.
                    if (addresses.TextLength != 0 || company.TextLength != 0 || fingerprint.TextLength != 0) return;
                    var profile = MobileConnectionService.Parse(saved);
                    addresses.Text = string.Join(Environment.NewLine, profile.Addresses); company.Text = profile.CompanyId.ToString(); fingerprint.Text = profile.CertificateSha256; show(profile);
                } catch (Exception ex) { if (!scroll.IsDisposed) error(ex); }
            };
            return scroll;
        }
    }
}

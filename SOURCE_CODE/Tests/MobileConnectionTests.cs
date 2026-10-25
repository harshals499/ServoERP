using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.UI;

namespace HVAC_Pro_Desktop.Tests
{
    public static class MobileConnectionTests
    {
        // Invoked by a separate STA harness, bypassing application startup and all SQL operations.
        public static void Run(string output)
        {
            Directory.CreateDirectory(output);
            var profile = new MobileConnectionProfile { CompanyId = 42,
                Addresses = new System.Collections.Generic.List<string> { "https://office.example.invalid:18446/", "https://office.example.invalid:18446" },
                CertificateSha256 = new string('A', 64) };
            string code = MobileConnectionService.ToCode(profile);
            var parsed = MobileConnectionService.Parse(code);
            Assert(parsed.CompanyId == 42 && parsed.Addresses.Count == 1 && parsed.CertificateSha256 == new string('A', 64), "Profile round trip and address normalization");
            File.WriteAllText(Path.Combine(output, "synthetic-profile.json"), MobileConnectionService.ToJson(parsed));
            File.WriteAllText(Path.Combine(output, "synthetic-code.txt"), code);
            foreach (string invalid in new[] {
                "{}", "null", "[]", "{\"companyId\":42,\"addresses\":[\"http://office.invalid\"]}",
                "{\"companyId\":42,\"addresses\":[\"https://user:password@office.invalid\"]}",
                "{\"companyId\":42,\"addresses\":[\"https://office.invalid/private\"]}",
                "{\"companyId\":42,\"addresses\":[\"https://office.invalid?token=secret\"]}",
                "{\"companyId\":42,\"addresses\":[\"https://office.invalid#secret\"]}",
                "{\"schemaVersion\":2,\"companyId\":42,\"addresses\":[\"https://office.invalid\"]}",
                "{\"companyId\":-1,\"addresses\":[\"https://office.invalid\"]}",
                "{\"companyId\":42,\"addresses\":[\"https://office.invalid\"],\"certificateSha256\":\"wrong\"}",
                "{\"companyId\":42,\"addresses\":[\"https://office.invalid\"],\"password\":\"secret\"}",
                "{\"companyId\":42,\"addresses\":[\"https://office.invalid\"],\"installationApiKey\":\"secret\"}",
                "servoerp-office:v1:invalid!", new string('x', 8193)
            }) {
                bool rejected = false;
                try { MobileConnectionService.Parse(invalid); } catch { rejected = true; }
                Assert(rejected, "Unsafe or malformed profile rejected");
            }
            Application.EnableVisualStyles();
            ServoERP.Infrastructure.ServoTheme.Initialize();
            foreach (bool admin in new[] { true, false })
            {
                SessionManager.SetSession(new AppUserDto { UserId = 999, Username = "synthetic-test", RoleId = admin ? 1 : 2,
                    RoleName = admin ? "Admin" : "Engineer", IsActive = true });
                if (!admin) {
                    bool denied = false;
                    try { MobileConnectionService.Save(profile); } catch (UnauthorizedAccessException) { denied = true; }
                    Assert(denied, "Non-admin settings save rejected before database access");
                }
                using (var form = new Form { Text = "ServoERP Mobile Connection Preview", ClientSize = new Size(1280, 980), StartPosition = FormStartPosition.CenterScreen })
                using (var settings = new SettingsForm { Dock = DockStyle.Fill, MobileConnectionPreviewOnly = true })
                {
                    form.Controls.Add(settings);
                    var tabs = settings.Controls.OfType<TabControl>().Single();
                    var tab = tabs.TabPages.Cast<TabPage>().Single(p => p.Text == "Mobile Connection");
                    form.Show(); Application.DoEvents();
                    tabs.SelectedTab = tab; Application.DoEvents();
                    var addresses = (TextBox)Find(tab, "MobileOfficeAddresses");
                    var company = (TextBox)Find(tab, "MobileCompanyNumber");
                    var fingerprint = (TextBox)Find(tab, "MobileCertificateFingerprint");
                    Assert(addresses.ReadOnly == !admin && company.ReadOnly == !admin, "Role-specific fields");
                    Assert(Find(tab, "SaveMobileConnection").Enabled == admin && Find(tab, "ManageMobileOfficeUsers").Enabled == admin, "Role-specific actions");
                    addresses.Text = parsed.Addresses[0]; company.Text = "42"; fingerprint.Text = parsed.CertificateSha256;
                    ((Button)Find(tab, "ShowMobileQr")).PerformClick(); Application.DoEvents();
                    var qr = (PictureBox)Find(tab, "MobileConnectionQr");
                    Assert(qr.Image != null && ((TextBox)Find(tab, "MobileConnectionCode")).Text == code, "QR and copyable code agree");
                    qr.Image.Save(Path.Combine(output, "qr-" + (admin ? "admin" : "user") + ".png"), ImageFormat.Png);
                    Capture(form, Path.Combine(output, "mobile-connection-" + (admin ? "admin" : "user") + "-wide.png"));
                    form.ClientSize = new Size(760, 900); Application.DoEvents();
                    Capture(form, Path.Combine(output, "mobile-connection-" + (admin ? "admin" : "user") + "-narrow.png"));
                    var scroll = (Panel)Find(tab, "MobileConnectionPage");
                    scroll.AutoScrollPosition = new Point(0, scroll.DisplayRectangle.Height); Application.DoEvents();
                    Capture(form, Path.Combine(output, "mobile-connection-" + (admin ? "admin" : "user") + "-narrow-bottom.png"));
                    addresses.Text = "http://invalid.invalid"; Application.DoEvents();
                    Assert(qr.Image == null && Find(tab, "MobileConnectionCode").Text.Length == 0, "Edited details invalidate stale QR");
                    ((Button)Find(tab, "ShowMobileQr")).PerformClick();
                    Assert(qr.Image == null && Find(tab, "MobileConnectionStatus").Text.Contains("HTTPS"), "Invalid configuration never generates QR");
                    form.Close();
                }
            }
            SessionManager.ClearSession();
            File.WriteAllText(Path.Combine(output, "desktop-tests.txt"), "PASS: profile round trip/normalization; 15 invalid/credential-bearing profiles rejected; non-admin save denied before SQL; admin/user UI permissions; generated QR/code equality; wide/narrow native render; edited profile clears QR; invalid HTTPS blocked. No database operations or account changes performed.");
        }
        private static void Assert(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); }
        private static Control Find(Control root, string name) {
            if (root.Name == name) return root;
            foreach (Control child in root.Controls) { var match = Find(child, name); if (match != null) return match; }
            return null;
        }
        private static void Capture(Form form, string path) {
            using (var image = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(path, ImageFormat.Png); }
        }
    }
}

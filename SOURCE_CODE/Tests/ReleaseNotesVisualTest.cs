using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.UI;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Tests
{
    internal static class ReleaseNotesVisualTest
    {
        internal static void Run()
        {
            string output = Path.Combine(Directory.GetCurrentDirectory(), "TEST_RESULTS");
            Directory.CreateDirectory(output);
            string version = UpdateService.GetCurrentAssemblyVersion();
            SessionManager.SetSession(new AppUserDto
            {
                UserId = 1, Username = "visual-test-admin", DisplayName = "Visual Test Admin",
                RoleId = 1, RoleName = "Admin", IsActive = true
            });
            using (var main = new MainForm())
            using (var host = new Form { ClientSize = new Size(340, 760), BackColor = DS.BgPage })
            {
                // Render the actual built sidebar after MainForm has applied the global theme.
                var sidebar = (Control)typeof(MainForm).GetField("_sidebar", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(main);
                sidebar.Parent.Controls.Remove(sidebar);
                host.Controls.Add(sidebar);
                host.Show();
                Application.DoEvents();
                if (ContainsText(sidebar, "Porter Deliveries"))
                    throw new InvalidOperationException("The removed Porter Deliveries page must not appear in the sidebar.");
                Control refresh = Find(sidebar, "SidebarRefreshPage");
                if (refresh == null || refresh.BackColor.ToArgb() != DS.Primary700.ToArgb())
                    throw new InvalidOperationException("Refresh Page must retain the sidebar blue after theming and rendering.");
                Capture(host, Path.Combine(output, "sidebar-refresh-" + version + ".png"));
                host.Close();
            }
            using (var dialog = new ReleaseNotesDialog(version, UpdateService.GetInstalledReleaseNotesText()))
            {
                dialog.Show();
                Application.DoEvents();
                Control notes = Find(dialog, "InstalledReleaseNotes");
                if (notes == null || string.IsNullOrWhiteSpace(notes.Text) || !notes.Text.Contains(version))
                    throw new InvalidOperationException("The rendered dialog must show installed-version notes.");
                Capture(dialog, Path.Combine(output, "release-notes-" + version + ".png"));
                dialog.Close();
            }
            using (var settings = new SettingsForm())
            using (var host = new Form { ClientSize = new Size(600, 380), BackColor = DS.BgPage })
            {
                settings.LoadPreviewForVisualTest();
                Control button = Find(settings, "ViewInstalledReleaseNotes");
                if (button == null) throw new InvalidOperationException("Settings must provide Release Notes.");
                Control updatesBody = button.Parent;
                Control card = updatesBody.Parent;
                card.Parent.Controls.Remove(card);
                card.Dock = DockStyle.Fill;
                host.Controls.Add(card);
                host.Show();
                Application.DoEvents();
                Capture(host, Path.Combine(output, "settings-release-notes-" + version + ".png"));
                host.Close();
            }
            SessionManager.ClearSession();
        }

        private static Control Find(Control root, string name)
        {
            if (root.Name == name) return root;
            foreach (Control child in root.Controls)
            {
                Control found = Find(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static bool ContainsText(Control root, string text)
        {
            if (string.Equals(root.Text, text, StringComparison.OrdinalIgnoreCase)) return true;
            foreach (Control child in root.Controls)
                if (ContainsText(child, text)) return true;
            return false;
        }

        private static void Capture(Form form, string path)
        {
            using (var bitmap = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                bitmap.Save(path, ImageFormat.Png);
            }
        }
    }
}

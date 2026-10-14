using System.Drawing;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    internal sealed class ReleaseNotesDialog : ServoERP.Infrastructure.ServoFormBase
    {
        internal ReleaseNotesDialog(string version, string text)
        {
            Text = BrandingService.WindowTitle("Release Notes");
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(570, 360);
            BackColor = DS.BgPage;
            var title = new Label
            {
                Text = "What's new in ServoERP", Dock = DockStyle.Top, Height = 48,
                Padding = new Padding(22, 16, 22, 0), Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                ForeColor = DS.Slate900
            };
            var subtitle = new Label
            {
                Text = "Version " + version, Dock = DockStyle.Top, Height = 28,
                Padding = new Padding(24, 0, 24, 0), Font = new Font("Segoe UI", 9f), ForeColor = DS.Slate600
            };
            var notes = new TextBox
            {
                Name = "InstalledReleaseNotes", Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
                ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White, ForeColor = DS.Slate800, Font = new Font("Segoe UI", 10f),
                Text = string.IsNullOrWhiteSpace(text) ? UpdateService.GetInstalledReleaseNotesText() : text
            };
            Button close = DS.PrimaryBtn("Got it", 112, 34);
            close.DialogResult = DialogResult.OK;
            var footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom, Height = 56, FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 10, 22, 10), BackColor = DS.BgPage
            };
            footer.Controls.Add(close);
            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(22, 8, 22, 8), BackColor = DS.BgPage };
            body.Controls.Add(notes);
            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(subtitle);
            Controls.Add(title);
            AcceptButton = close;
            Shown += (sender, args) =>
            {
                notes.Select(0, 0);
                close.Focus();
            };
        }
    }
}

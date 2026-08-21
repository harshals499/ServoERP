using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class UserGuideForm : ServoERP.Infrastructure.ServoFormBase
    {
        private readonly SupportCenterService _support = new SupportCenterService();
        private readonly Panel _rail = new Panel();
        private readonly Panel _detail = new Panel();
        private readonly Label _progress = new Label();
        private readonly Label _next = new Label();
        private readonly List<GuideStep> _steps = new List<GuideStep>();
        private int _selected;

        private sealed class GuideStep
        {
            public string Key { get; set; }
            public string Title { get; set; }
            public string Instruction { get; set; }
            public string ReadyWhen { get; set; }
            public string ActionText { get; set; }
            public Action Action { get; set; }
        }

        public UserGuideForm()
        {
            Text = BrandingService.WindowTitle("User Guide");
            StartPosition = FormStartPosition.CenterParent;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(1050, 680);
            BackColor = DS.BgPage;
            Font = DS.Body;
            BuildSteps();
            BuildLayout();
            SelectFirstIncomplete();
        }

        private void BuildSteps()
        {
            _steps.Add(Step("SetupOverview", "Understand the office setup", "ServoERP uses one office SQL Server as the source of truth. Every connected PC works on the same live business records. This is not PC-to-PC file syncing.", "You know the server and office network must be available while users work.", "I understand", () => { }));
            _steps.Add(Step("OfficeServer", "Prepare the office server", "On the server PC only, prepare SQL Server, the HVAC_PRO database, licence, owner login, and terminal setup package.", "The server checklist shows SQL Server, licence, and owner account as ready.", "Open server setup", OpenServerSetup));
            _steps.Add(Step("TerminalConnection", "Connect this PC", "Connect this workstation directly to the office SQL Server using the server name or IP provided by the office administrator.", "The connection test succeeds and this PC can read shared company data.", "Open connection setup", OpenConnectionSetup));
            _steps.Add(Step("SharedStorage", "Connect shared storage", "Optionally connect documents, templates, exports, and backups through the shared server folder. Auto Detect Server proposes the standard folder.", "The shared folder is reachable and the standard ServoERP folders are available.", "Open shared storage", OpenSharedStorage));
            _steps.Add(Step("VerifyConnection", "Verify this PC", "Run the safe database check before entering business data. It confirms this PC can use the shared office database.", "The database check reports that business writes are available.", "Run database check", VerifyDatabase));
            _steps.Add(Step("ClientPackage", "Set up and monitor the remaining PCs", "Use LAN Control Center on the server to detect office PCs, verify WinRM readiness, remotely install the Enterprise build with administrator approval, and monitor enrolled-PC health. Unmanaged workgroup PCs need the included one-time WinRM bootstrap before remote installation.", "Every terminal has ServoERP installed, has a tested connection to the same office SQL Server, and appears as Online in LAN Control Center.", "Open LAN Control", OpenLanControl));
            _steps.Add(Step("DailySafety", "Work safely every day", "Keep the server powered on, retain regular backups, and use Help & Support > System Health when a connection or update problem occurs.", "Your office has an agreed backup and support routine.", "Finish guide", () => { }));
        }

        private static GuideStep Step(string key, string title, string instruction, string readyWhen, string actionText, Action action)
        {
            return new GuideStep { Key = key, Title = title, Instruction = instruction, ReadyWhen = readyWhen, ActionText = actionText, Action = action };
        }

        private void BuildLayout()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 118, BackColor = DS.White, Padding = new Padding(38, 24, 38, 20) };
            header.Controls.Add(new Label { Text = "Follow the next recommended action. You can return to any step whenever you need help.", Font = DS.Body, ForeColor = DS.Slate600, Dock = DockStyle.Top, Height = 30 });
            header.Controls.Add(new Label { Text = "ServoERP User Guide", Font = DS.H1, ForeColor = DS.Slate900, Dock = DockStyle.Top, Height = 38 });
            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 66, BackColor = DS.White, Padding = new Padding(38, 16, 38, 12) };
            _progress.Dock = DockStyle.Left; _progress.Width = 300; _progress.Font = DS.BodyBold; _progress.ForeColor = DS.Slate700; _progress.TextAlign = ContentAlignment.MiddleLeft;
            _next.Dock = DockStyle.Fill; _next.Font = DS.Body; _next.ForeColor = DS.Teal600; _next.TextAlign = ContentAlignment.MiddleRight;
            footer.Controls.Add(_next); footer.Controls.Add(_progress);
            _rail.Dock = DockStyle.Left; _rail.Width = 330; _rail.BackColor = DS.White; _rail.Padding = new Padding(22, 22, 18, 22);
            _detail.Dock = DockStyle.Fill; _detail.BackColor = DS.BgPage; _detail.Padding = new Padding(44, 40, 44, 40);
            Controls.Add(_detail); Controls.Add(_rail); Controls.Add(footer); Controls.Add(header);
        }

        private void SelectFirstIncomplete()
        {
            _selected = 0;
            for (int i = 0; i < _steps.Count; i++) if (!IsComplete(_steps[i])) { _selected = i; break; }
            Render();
        }

        private bool IsComplete(GuideStep step) => string.Equals(ConfigService.Get("InteractiveGuide", step.Key, "false"), "true", StringComparison.OrdinalIgnoreCase);

        private void Render()
        {
            _rail.Controls.Clear();
            for (int i = _steps.Count - 1; i >= 0; i--)
            {
                int index = i; GuideStep step = _steps[index];
                Button item = DS.GhostBtn((IsComplete(step) ? "✓  " : (index + 1) + ".  ") + step.Title, 286, 44);
                item.Dock = DockStyle.Top; item.Margin = new Padding(0, 0, 0, 8); item.TextAlign = ContentAlignment.MiddleLeft;
                item.BackColor = index == _selected ? DS.Primary600 : DS.White; item.ForeColor = index == _selected ? DS.White : DS.Slate700;
                item.Click += (s, e) => { _selected = index; Render(); }; _rail.Controls.Add(item);
            }
            GuideStep selected = _steps[_selected]; _detail.Controls.Clear();
            Button later = DS.GhostBtn("I will do this later", 180, 36); later.Dock = DockStyle.Top; later.Click += (s, e) => Advance(); _detail.Controls.Add(later);
            Button action = DS.PrimaryBtn(selected.ActionText, 210, 40); action.Dock = DockStyle.Top; action.Margin = new Padding(0, 18, 0, 8); action.Click += (s, e) => { selected.Action(); MarkCompleteAndAdvance(selected); }; _detail.Controls.Add(action);
            _detail.Controls.Add(new Label { Text = "You are ready to continue when: " + selected.ReadyWhen, Font = DS.Body, ForeColor = DS.Slate700, Dock = DockStyle.Top, Height = 66, Padding = new Padding(0, 12, 0, 0) });
            _detail.Controls.Add(new Label { Text = selected.Instruction, Font = DS.Body, ForeColor = DS.Slate600, Dock = DockStyle.Top, Height = 92 });
            _detail.Controls.Add(new Label { Text = "What you should do now", Font = DS.H3, ForeColor = DS.Slate800, Dock = DockStyle.Top, Height = 48, Padding = new Padding(0, 16, 0, 0) });
            _detail.Controls.Add(new Label { Text = selected.Title, Font = DS.H1, ForeColor = DS.Slate900, Dock = DockStyle.Top, Height = 48 });
            _detail.Controls.Add(new Label { Text = "STEP " + (_selected + 1) + " OF " + _steps.Count, Font = DS.CaptionBold(), ForeColor = DS.Primary600, Dock = DockStyle.Top, Height = 24 });
            int completed = 0; foreach (GuideStep step in _steps) if (IsComplete(step)) completed++;
            _progress.Text = completed + " of " + _steps.Count + " steps completed";
            _next.Text = _selected < _steps.Count - 1 ? "Next: " + _steps[_selected + 1].Title : "Guide complete";
        }

        private void MarkCompleteAndAdvance(GuideStep step) { ConfigService.Set("InteractiveGuide", step.Key, "true"); Advance(); }
        private void Advance() { if (_selected < _steps.Count - 1) _selected++; Render(); }
        private void OpenServerSetup() { using (var form = new ServerFirstRunSetupForm()) form.ShowDialog(this); }
        private void OpenConnectionSetup() { using (var form = new ConnectionSetupForm()) form.ShowDialog(this); }
        private void OpenSharedStorage() { using (var form = new SharedStorageSettingsForm()) form.ShowDialog(this); }
        private void VerifyDatabase() { ShowResult(_support.CheckDatabase()); }
        private void GenerateClientPackage() { ShowResult(_support.GenerateClientServerSetupPackage()); }
        private void OpenLanControl() { using (var form = new OfficeLanControlForm()) form.ShowDialog(this); }
        private void ShowResult(SupportToolResult result) { MessageBox.Show(this, result.Message + (string.IsNullOrWhiteSpace(result.Detail) ? string.Empty : Environment.NewLine + Environment.NewLine + result.Detail), result.Title, MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning); }
    }
}

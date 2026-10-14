using System;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    // Owns only page chrome. The shell and its sidebar remain untouched.
    internal sealed class WorkspaceHeaderPanel : Panel
    {
        internal static bool Owns(Control control)
        {
            for (var current = control; current != null; current = current.Parent)
                if (current is WorkspaceHeaderPanel) return true;
            return false;
        }

        private static bool HasInput(Control control) => control is TextBoxBase || control is ComboBox ||
            (control != null && control.Controls.Cast<Control>().Any(HasInput));

        internal static SharedPageHeaderResult Build(SharedPageHeaderModel model,
            Func<SharedPageHeaderModel, SharedPageHeaderResult> buildPage)
        {
            var wrapper = new WorkspaceHeaderPanel { Name = model.Name, Dock = model.Dock,
                BackColor = Color.White, Tag = "custom-header-actions no-global-actions" };
            var originalDock = model.Dock;
            model.Dock = DockStyle.Top;
            model.BackColor = Color.White;
            model.Padding = model.CompactUtility ? new Padding(18, 18, 18, 12) : new Padding(24, 30, 24, 16);
            model.DefaultHeight = model.CompactUtility ? 88 : Math.Max(108, model.DefaultHeight + 22);
            model.CompactHeight = model.CompactUtility ? 134 : Math.Max(152, model.CompactHeight + 22);
            model.ActionTop = model.CenterTop = model.CompactUtility ? 22 : 30;
            model.CompactCenterTop = model.CompactUtility ? 76 : (string.IsNullOrWhiteSpace(model.StatusText) ? 96 : 112);
            model.CompactActionTop = model.CompactCenterTop;
            // Wrap when the actual controls cannot fit, rather than at a page-specific legacy breakpoint.
            model.CompactBreakpoint = 1;
            // Global search lives in the utility bar; editable module filters stay on the page.
            if (model.CenterContent != null && !HasInput(model.CenterContent) &&
                model.CenterContent.Name.IndexOf("search", StringComparison.OrdinalIgnoreCase) >= 0)
            { model.CenterContent.Dispose(); model.CenterContent = null; }
            if (model.RightMetaControl?.Name == "DashboardHeaderMetaPanel" || model.RightMetaControl?.Name == "SettingsHeaderMetaPanel")
            { model.RightMetaControl.Dispose(); model.RightMetaControl = null; }
            if (model.RightMetaControl == null && !model.CompactUtility)
                model.RightMetaControl = new Label { Name = "WorkspaceDate", Text = DateTime.Today.ToString("dd/MM/yyyy"),
                    Width = 96, Height = 32, TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 12f), ForeColor = Color.FromArgb(119, 132, 144) };
            var notification = model.RightActions.OfType<Button>().FirstOrDefault(c => c.Name == "btnDashboardNotifications");
            if (notification != null) model.RightActions.Remove(notification);
            foreach (var button in model.RightActions.OfType<Button>()) StyleAction(button);
            var result = buildPage(model);
            var page = result.Header;
            page.Name = (model.Name ?? "Page") + "PageHeading";
            var breadcrumb = new Label { Name = "WorkspaceBreadcrumb", Text = "Workspace  ›  " + model.Title,
                Location = new Point(25, 8), Size = new Size(550, 19), Font = new Font("Segoe UI", 12f),
                ForeColor = Color.FromArgb(111, 123, 135), AutoEllipsis = true };
            if (!model.CompactUtility) page.Controls.Add(breadcrumb);
            result.TitleLabel.TextChanged += (s, e) => breadcrumb.Text = "Workspace  ›  " + result.TitleLabel.Text;
            var utility = new Panel { Name = "WorkspaceUtilityBar", Dock = DockStyle.Top, Height = 64,
                BackColor = Color.White };
            var brand = new HeaderBrand { Name = "WorkspaceBrand", AccessibleName = "ServoERP", Size = new Size(48, 52) };
            var search = new HeaderSearchButton { Name = "WorkspaceGlobalSearch", AccessibleName = "Global search (Ctrl+K)",
                Size = new Size(480, 38) };
            search.Click += (s, e) => OpenSearch(wrapper);
            var profile = new HeaderAccountButton { Name = "WorkspaceAccount", Size = new Size(196, 40) };
            var user = SessionManager.CurrentUser;
            string name = AccountName();
            profile.Text = name + "  ▾";
            profile.AccessibleName = "Account menu for " + name;
            profile.Click += (s, e) => ShowAccount(wrapper, profile);
            if (notification == null)
            {
                notification = MakeButton("WorkspaceNotifications", "Alerts", 64);
                notification.Text = string.Empty;
                notification.Width = 38;
                var bell = LucideIconService.GetIcon("bell", 26, Color.FromArgb(31, 52, 77));
                notification.Image = bell;
                notification.ImageAlign = ContentAlignment.MiddleCenter;
                notification.Disposed += (sender, args) => bell.Dispose();
                notification.AccessibleName = "Open alerts and notifications";
                notification.Click += (s, e) =>
                {
                    var main = ResolveShell(wrapper);
                    using (var dialog = new NotificationCenterDialog(main == null ? (Action<string>)null : main.NavigateTo))
                        dialog.ShowDialog(wrapper.FindForm());
                };
            }
            utility.Controls.AddRange(new Control[] { brand, search, notification, profile });
            notification.FlatStyle = FlatStyle.Flat;
            notification.BackColor = Color.White;
            notification.FlatAppearance.BorderSize = 0;
            notification.Size = new Size(44, 44);
            brand.Visible = true;
            if (model.UtilityOnly) { page.Visible = false; page.Height = 0; }
            foreach (Control action in model.UtilityActions) { action.Dock = DockStyle.None; utility.Controls.Add(action); }
            utility.Paint += (s, e) => { using (var pen = new Pen(Color.FromArgb(235, 238, 240)))
                e.Graphics.DrawLine(pen, 0, utility.Height - 1, utility.Width, utility.Height - 1); };
            wrapper.Controls.Add(page); wrapper.Controls.Add(utility);
            bool arranging = false;
            Action layout = () =>
            {
                if (arranging || wrapper.IsDisposed) return;
                arranging = true;
                try
                {
                    int width = wrapper.ClientSize.Width;
                    bool narrow = width < 1000;
                    utility.Height = narrow ? 136 : 88;
                    page.Width = width;
                    page.Top = utility.Height;
                    int accountWidth = width < 760 ? 54 : 248;
                    profile.SetBounds(Math.Max(0, width - accountWidth - 18), 16, accountWidth, 52);
                    notification.Location = new Point(Math.Max(0, profile.Left - notification.Width - 18), 18);
                    int utilityRight = notification.Left;
                    foreach (Control action in model.UtilityActions.Reverse())
                    { utilityRight -= action.Width + 8; action.Location = new Point(utilityRight, 23); }
                    brand.SetBounds(24, 14, 48, 52);
                    int left = narrow ? 20 : brand.Right + 64;
                    int available = narrow ? width - 40 : utilityRight - left - 28;
                    int searchWidth = Math.Max(80, Math.Min(1000, available));
                    search.SetBounds(left, narrow ? 82 : 20, searchWidth, 48);
                    breadcrumb.Width = Math.Max(80, page.Width - 48);
                    int footerHeight = wrapper.Controls.Cast<Control>().Where(c => c != page && c != utility && c.Dock == DockStyle.Bottom).Sum(c => c.Height);
                    int height = utility.Height + (model.UtilityOnly ? 0 : page.Height) + footerHeight;
                    wrapper.Height = height;
                    var table = wrapper.Parent as TableLayoutPanel;
                    int row = table == null ? -1 : table.GetPositionFromControl(wrapper).Row;
                    if (row >= 0 && row < table.RowStyles.Count && table.RowStyles[row].SizeType == SizeType.Absolute)
                        table.RowStyles[row].Height = height;
                }
                finally { arranging = false; }
            };
            wrapper.Resize += (s, e) => layout();
            wrapper.ParentChanged += (s, e) => layout();
            page.SizeChanged += (s, e) => layout();
            wrapper.VisibleChanged += (s, e) => layout();
            wrapper.ControlAdded += (s, e) => layout();
            wrapper.ControlRemoved += (s, e) => layout();
            result.Header = wrapper;
            model.Dock = originalDock;
            layout();
            return result;
        }

        /// <summary>Adds the same chrome to standalone workflow editors that have no shared page header.</summary>
        internal static void EnsureOnSurface(Control surface)
        {
            if (surface == null || surface.IsDisposed || SessionManager.CurrentUser == null ||
                surface is MainForm || surface is GlobalSearchDialog || surface is NotificationCenterDialog ||
                surface is ServoERP.Infrastructure.ServoConfirmDialog || surface is ServoERP.Infrastructure.ServoErrorDialog)
                return;
            string[] focusedDialogs = { "ToastNotification", "ReleaseNotesDialog", "HtmlPreviewDialog", "FormTemplatePickerDialog",
                "WhatsAppQuickActionDialog", "LegalAgreementForm", "LicenseActivationForm", "CreateAccountForm", "ServerFirstRunSetupForm" };
            if (focusedDialogs.Contains(surface.GetType().Name)) return;
            if (WorkspaceActionUi.Descendants(surface).Any(c => c is WorkspaceHeaderPanel)) return;
            // Embedded pages inherit the enclosing page/form's global chrome.
            if (surface is UserControl)
                for (Control ancestor = surface.Parent; ancestor != null; ancestor = ancestor.Parent)
                    if (ancestor is ServoERP.Infrastructure.ServoPageBase || ancestor is ServoERP.Infrastructure.ServoFormBase)
                        return;
            var existing = surface.Controls.Cast<Control>().ToArray();
            if (existing.Length == 0) return;
            var model = new SharedPageHeaderModel { Name = "WorkspaceEditorHeader", Title = string.Empty,
                UtilityOnly = true, Dock = DockStyle.Top };
            var header = SharedPageHeader.Build(model).Header;
            header.Width = surface.ClientSize.Width;
            var originalSize = surface.ClientSize;
            var originalPadding = surface.Padding;
            surface.SuspendLayout();
            try
            {
                var body = new Panel { Name = "WorkspaceEditorBody", Dock = DockStyle.Fill,
                    BackColor = surface.BackColor, Padding = Padding.Empty,
                    AutoScroll = true };
                int contentHeight = Math.Max(originalSize.Height, existing.Max(c => c.MinimumSize.Height));
                var content = new Panel { Name = "WorkspaceEditorContent", Location = Point.Empty,
                    Size = new Size(originalSize.Width, contentHeight), BackColor = surface.BackColor, Padding = originalPadding };
                body.Controls.Add(content);
                body.Resize += (sender, args) => content.Size = new Size(
                    Math.Max(1, body.ClientSize.Width), Math.Max(contentHeight, body.ClientSize.Height));
                // Keep each control's z-order, docking and original handlers intact.
                foreach (Control child in existing.Reverse()) { surface.Controls.Remove(child); content.Controls.Add(child); content.Controls.SetChildIndex(child, 0); }
                surface.Padding = Padding.Empty;
                var form = surface as Form;
                if (form != null && form.WindowState == FormWindowState.Normal)
                {
                    int height = originalSize.Height + header.Height;
                    form.ClientSize = new Size(originalSize.Width, Math.Min(height, Screen.FromControl(form).WorkingArea.Height - 48));
                }
                surface.Controls.Add(body); surface.Controls.Add(header);
                surface.Controls.SetChildIndex(body, 0);
            }
            finally { surface.ResumeLayout(true); }
        }

        private sealed class HeaderBrand : Control
        {
            private readonly Image _logo;
            internal HeaderBrand()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
                string file = Path.Combine(Path.GetDirectoryName(typeof(WorkspaceHeaderPanel).Assembly.Location),
                    "Resources", "Branding", "servoerp_logo_transparent.png");
                if (File.Exists(file)) using (var source = Image.FromFile(file)) _logo = new Bitmap(source);
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(Color.White);
                if (_logo != null)
                {
                    e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    float scale = Math.Min(48f / _logo.Width, 44f / _logo.Height);
                    int w = (int)(_logo.Width * scale), h = (int)(_logo.Height * scale);
                    e.Graphics.DrawImage(_logo, new Rectangle(0, (Height - h) / 2, w, h));
                }

            }
            protected override void Dispose(bool disposing) { if (disposing) _logo?.Dispose(); base.Dispose(disposing); }
        }

        private static Button MakeButton(string name, string text, int width)
        {
            var button = new Button { Name = name, Text = text, Size = new Size(width, 36),
                FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Color.FromArgb(63, 79, 96),
                Font = new Font("Segoe UI", 9f), Cursor = Cursors.Hand, AutoEllipsis = true };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private static void StyleAction(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(219, 225, 231);
            button.Font = new Font("Segoe UI", 9f);
            button.AutoEllipsis = true;
            if (button.BackColor.GetBrightness() > .8f)
            {
                button.BackColor = Color.White;
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(244, 247, 250);
            }
            DS.Rounded(button, 7);
            string caption = (button.Text + " " + button.Name).ToLowerInvariant();
            bool refresh = caption.Contains("refresh");
            bool settings = caption.Contains("customize") || caption.Contains("settings") || caption.Contains("⚙");
            if (button.Text.Trim() == "⚙" || button.Text.Trim() == "⚙️")
            {
                button.Text = string.Empty;
                if (string.IsNullOrWhiteSpace(button.AccessibleName)) button.AccessibleName = "Settings";
            }
            bool transfer = caption.Contains("import") || caption.Contains("export");
            bool document = caption.Contains("forms") || caption.Contains("service") || caption.Contains("template");
            bool add = caption.Trim().StartsWith("add") || caption.Trim().StartsWith("new") || caption.Contains("+ new");
            if (refresh || settings || (button.Image != null && (transfer || document || add)))
            {
                var bitmap = new Bitmap(18, 18);
                using (var graphics = Graphics.FromImage(bitmap))
                using (var pen = new Pen(button.ForeColor, 1.4f))
                {
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    if (refresh)
                    {
                        graphics.DrawArc(pen, 3, 3, 12, 12, 40, 285);
                        graphics.DrawLines(pen, new[] { new Point(11, 2), new Point(15, 3), new Point(14, 7) });
                    }
                    else if (settings)
                    {
                        graphics.DrawEllipse(pen, 4, 4, 10, 10); graphics.DrawEllipse(pen, 7, 7, 4, 4);
                        for (int i = 0; i < 8; i++)
                        {
                            double angle = Math.PI * i / 4;
                            graphics.DrawLine(pen, 9 + (float)Math.Cos(angle) * 5, 9 + (float)Math.Sin(angle) * 5,
                                9 + (float)Math.Cos(angle) * 8, 9 + (float)Math.Sin(angle) * 8);
                        }
                    }
                    else if (add)
                    { graphics.DrawLine(pen, 9, 3, 9, 15); graphics.DrawLine(pen, 3, 9, 15, 9); }
                    else if (transfer)
                    {
                        bool import = caption.Contains("import");
                        graphics.DrawLines(pen, new[] { new Point(3, 11), new Point(3, 15), new Point(15, 15), new Point(15, 11) });
                        graphics.DrawLine(pen, 9, 2, 9, 12);
                        graphics.DrawLines(pen, import ? new[] { new Point(5, 8), new Point(9, 12), new Point(13, 8) } :
                            new[] { new Point(5, 6), new Point(9, 2), new Point(13, 6) });
                    }
                    else
                    {
                        graphics.DrawRectangle(pen, 4, 2, 10, 14);
                        graphics.DrawLine(pen, 6, 6, 12, 6); graphics.DrawLine(pen, 6, 9, 12, 9); graphics.DrawLine(pen, 6, 12, 10, 12);
                    }
                }
                button.Image = bitmap;
                button.ImageAlign = string.IsNullOrWhiteSpace(button.Text) ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;
                button.TextImageRelation = TextImageRelation.ImageBeforeText;
                button.Padding = string.IsNullOrWhiteSpace(button.Text) ? Padding.Empty : new Padding(8, 0, 8, 0);
                button.Disposed += (s, e) => bitmap.Dispose();
            }
        }

        internal static void OpenSearch(Control owner)
        {
            var main = ResolveShell(owner);
            var ownerForm = owner.FindForm();
            using (var dialog = new GlobalSearchDialog(main == null ? (Action<string>)null : main.NavigateTo))
            {
                dialog.ShowDialog(ownerForm);
                if (dialog.RecordOpened && ownerForm is UserGuideForm guide) guide.Close();
            }
        }

        private static void ShowAccount(Control owner, Button anchor)
        {
            var menu = BuildAccountMenu(owner);
            // Let the selected item's Click finish before disposing its menu.
            menu.Closed += (s, e) =>
            {
                if (!owner.IsDisposed && owner.IsHandleCreated) owner.BeginInvoke((Action)(() => menu.Dispose()));
                else menu.Dispose();
            };
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        private static ContextMenuStrip BuildAccountMenu(Control owner)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add(SessionManager.CurrentUser?.RoleName ?? "Account").Enabled = false;
            menu.Items.Add("Change password", null, (s, e) =>
            {
                if (SessionManager.CurrentUser == null) return;
                using (var dialog = new ChangePasswordForm(SessionManager.CurrentUser.UserId, false))
                    dialog.ShowDialog(owner.FindForm());
            }).Enabled = SessionManager.CurrentUser != null;
            var main = ResolveShell(owner);
            menu.Items.Add("Settings", null, (s, e) =>
            {
                main?.NavigateTo("Settings");
                if (owner.FindForm() is UserGuideForm guide) guide.Close();
            }).Enabled = main != null;
            return menu;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.K)) { OpenSearch(this); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private static MainForm ResolveShell(Control owner)
        {
            for (Form form = owner.FindForm(); form != null; form = form.Owner)
                if (form is MainForm main) return main;
            return Application.OpenForms.OfType<MainForm>().FirstOrDefault();
        }

        private static string AccountName()
        {
            var user = SessionManager.CurrentUser;
            return !string.IsNullOrWhiteSpace(user?.DisplayName) ? user.DisplayName :
                !string.IsNullOrWhiteSpace(user?.Username) ? user.Username : "Account";
        }

        private sealed class HeaderSearchButton : Button
        {
            internal HeaderSearchButton()
            {
                FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; BackColor = Color.White;
                Cursor = Cursors.Hand; Font = new Font("Segoe UI", 12f);
                SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(Color.White);
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = DS.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 9))
                using (var fill = new SolidBrush(Color.FromArgb(250, 251, 252)))
                using (var pen = new Pen(Focused ? Color.FromArgb(79, 119, 157) : Color.FromArgb(220, 226, 232)))
                { e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(pen, path); }
                using (var icon = LucideIconService.GetIcon("search", 22, Color.FromArgb(83, 104, 129)))
                    e.Graphics.DrawImage(icon, 14, (Height - 22) / 2);
                using (var searchFont = new Font("Segoe UI", 12f))
                    TextRenderer.DrawText(e.Graphics, "Search across ServoERP...", searchFont,
                        new Rectangle(48, 0, Math.Max(0, Width - 144), Height), Color.FromArgb(111, 125, 140),
                        TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                Rectangle key = new Rectangle(Math.Max(36, Width - 88), 8, 76, Height - 16);
                using (var path = DS.RoundedRect(key, 6))
                using (var fill = new SolidBrush(Color.White))
                using (var border = new Pen(Color.FromArgb(216, 225, 236)))
                { e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(border, path); }
                using (var font = new Font("Segoe UI", 9f))
                    TextRenderer.DrawText(e.Graphics, "Ctrl + K", font, key, Color.FromArgb(93, 112, 137),
                        TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);

            }
        }

        private sealed class HeaderAccountButton : Button
        {
            internal HeaderAccountButton()
            {
                FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
                Cursor = Cursors.Hand; BackColor = Color.White;
                SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(Focused ? Color.FromArgb(244, 248, 251) : Color.White);
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                string name = AccountName();
                using (var brush = new SolidBrush(Color.FromArgb(220, 237, 248))) e.Graphics.FillEllipse(brush, 4, 4, 40, 40);
                using (var font = new Font("Segoe UI", 9f, FontStyle.Bold))
                    TextRenderer.DrawText(e.Graphics, string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant(), font,
                        new Rectangle(4, 4, 40, 40), Color.FromArgb(57, 85, 110), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                if (Width < 100) return;
                using (var font = new Font("Segoe UI", 12f))
                {
                    TextRenderer.DrawText(e.Graphics, name, font, new Rectangle(56, 2, Width - 78, 25), Color.FromArgb(43, 60, 78), TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                    TextRenderer.DrawText(e.Graphics, SessionManager.CurrentUser?.RoleName ?? "Account", font,
                        new Rectangle(56, 27, Width - 78, 24), Color.FromArgb(121, 134, 148), TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                    using (var chevron = LucideIconService.GetIcon("chevron-down", 16, Color.FromArgb(73, 94, 119)))
                        e.Graphics.DrawImage(chevron, Width - 18, 16);
                }
            }
        }
    }
}

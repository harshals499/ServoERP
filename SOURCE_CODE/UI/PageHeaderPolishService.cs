using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using ServoERP.Infrastructure;

namespace HVAC_Pro_Desktop.UI
{
    /// <summary>Applies a light-touch visual polish to existing page headers without replacing local layouts.</summary>
    internal static class PageHeaderPolishService
    {
        private const string GlobalActionHostName = "__servoerpHeaderActionHost";
        private const string GlobalPreviewButtonName = "__servoerpHeaderPreviewButton";
        private const string GlobalRefreshButtonName = "__servoerpHeaderRefreshButton";
        private static readonly HashSet<Control> BoundRoots = new HashSet<Control>();
        private static readonly HashSet<Control> BorderBoundHeaders = new HashSet<Control>();

        /// <summary>Applies the shared page-header polish pass to an existing control tree.</summary>
        public static void Apply(Control root)
        {
            if (root == null || root.IsDisposed)
                return;

            BindRoot(root);
            ApplyRecursive(root, 0);
        }

        private static void BindRoot(Control root)
        {
            if (BoundRoots.Contains(root))
                return;

            BoundRoots.Add(root);
            root.Disposed += (s, e) => BoundRoots.Remove(root);
            root.ControlAdded += (s, e) => Apply(e.Control);
        }

        private static void ApplyRecursive(Control control, int depth)
        {
            if (control == null || control.IsDisposed)
                return;

            if (LooksLikePageHeader(control, depth))
                PolishHeader(control);

            foreach (Control child in control.Controls)
                ApplyRecursive(child, depth + 1);
        }

        private static bool LooksLikePageHeader(Control control, int depth)
        {
            if (control == null || depth > 5)
                return false;

            string meta = ((control.Name ?? string.Empty) + " " + (control.Tag ?? string.Empty)).ToLowerInvariant();
            if (ContainsAny(meta, "sidebar", "grid", "footer", "filter", "search", "lineitem", "empty", "dialog", "card", "dash-card", "kpi", "metric", "stat", "tile", "widget", "chart", "legend"))
                return false;

            if (!(control is Panel) && !(control is TableLayoutPanel) && !(control is FlowLayoutPanel))
                return false;

            bool topPosition = control.Dock == DockStyle.Top || control.Top <= 16 || ContainsAny(meta, "header", "topbar", "top-bar");
            if (!topPosition)
                return false;

            int width = control.Width;
            if (width > 0 && width < 420 && !ContainsAny(meta, "header", "topbar", "top-bar"))
                return false;

            int height = control.Height;
            if (height > 0 && (height < 44 || height > 156))
                return false;

            List<Label> labels = control.Controls.OfType<Label>().Where(l => !string.IsNullOrWhiteSpace(l.Text)).ToList();
            if (labels.Count == 0)
                labels = control.Controls.Cast<Control>().SelectMany(ImmediateLabels).Where(l => !string.IsNullOrWhiteSpace(l.Text)).ToList();

            bool namedHeader = ContainsAny(meta, "header", "topbar", "top-bar");
            Label title = labels
                .Where(IsHeaderTitleCandidate)
                .OrderBy(l => l.Top)
                .ThenByDescending(l => l.Font == null ? 0f : l.Font.Size)
                .FirstOrDefault();
            bool hasTitle = title != null;
            bool hasSubtitle = title != null && labels.Any(l =>
                !ReferenceEquals(l, title) &&
                l.Top >= title.Top &&
                l.Top <= title.Bottom + 28 &&
                (l.Font == null || l.Font.Size <= 11f) &&
                !LooksLikeMetricValue(l.Text));
            bool hasActions = control.Controls.OfType<Button>().Any() ||
                              control.Controls.OfType<TextBox>().Any() ||
                              control.Controls.OfType<ComboBox>().Any() ||
                              control.Controls.OfType<FlowLayoutPanel>().Any(f => f.Controls.OfType<Button>().Any()) ||
                              control.Controls.Cast<Control>().SelectMany(c => c.Controls.OfType<Button>()).Any() ||
                              control.Controls.Cast<Control>().SelectMany(c => c.Controls.OfType<TextBox>()).Any() ||
                              control.Controls.Cast<Control>().SelectMany(c => c.Controls.OfType<ComboBox>()).Any();

            return namedHeader || (hasTitle && (hasActions || hasSubtitle));
        }

        private static IEnumerable<Label> ImmediateLabels(Control control)
        {
            if (control == null)
                yield break;

            foreach (Control child in control.Controls)
            {
                Label label = child as Label;
                if (label != null)
                    yield return label;
            }
        }

        private static void PolishHeader(Control header)
        {
            header.BackColor = DS.BgPage;
            if (header.Padding == Padding.Empty)
                header.Padding = new Padding(22, 12, 22, 10);

            AttachBottomBorder(header);
            PolishHeaderLabels(header);
            PolishHeaderButtons(header);
            PolishActionRails(header);
            EnsureHeaderActions(header);
        }

        private static void AttachBottomBorder(Control header)
        {
            if (BorderBoundHeaders.Contains(header))
                return;

            BorderBoundHeaders.Add(header);
            header.Disposed += (s, e) => BorderBoundHeaders.Remove(header);
            header.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Slate200))
                    e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };
        }

        private static void PolishHeaderLabels(Control header)
        {
            List<Label> labels = Descendants(header).OfType<Label>()
                .Where(l => !string.IsNullOrWhiteSpace(l.Text) && !LooksLikeBadge(l))
                .OrderByDescending(l => l.Font == null ? 0f : l.Font.Size)
                .ToList();

            Label title = labels.FirstOrDefault(l => l.Font != null && l.Font.Size >= 13f);
            if (title != null)
            {
                title.Font = new Font("Segoe UI", Math.Min(18f, Math.Max(15.5f, title.Font.Size)), FontStyle.Bold);
                title.ForeColor = DS.Slate950;
                title.AutoEllipsis = true;
                title.UseMnemonic = false;
            }

            foreach (Label label in labels.Where(l => l != title))
            {
                if (label.Font != null && label.Font.Size <= 10.5f)
                {
                    label.Font = new Font("Segoe UI", Math.Max(8.6f, label.Font.Size), FontStyle.Regular);
                    label.ForeColor = DS.Slate600;
                    label.AutoEllipsis = true;
                    label.UseMnemonic = false;
                }
            }
        }

        private static void PolishHeaderButtons(Control header)
        {
            foreach (Button button in Descendants(header).OfType<Button>())
            {
                string text = (button.Text ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                UIHelper.ApplyActionButton(button, IsPrimaryAction(text) ? UiActionVariant.Primary : UiActionVariant.Secondary);
                button.Height = Math.Max(button.Height, 34);
                button.MinimumSize = new Size(Math.Max(button.MinimumSize.Width, IsCompact(text) ? 34 : 86), Math.Max(button.MinimumSize.Height, 34));
                button.AutoEllipsis = true;
                button.TextAlign = ContentAlignment.MiddleCenter;
                button.Margin = new Padding(4, 0, 4, 0);
            }
        }

        private static void PolishActionRails(Control header)
        {
            foreach (FlowLayoutPanel flow in Descendants(header).OfType<FlowLayoutPanel>())
            {
                if (!flow.Controls.OfType<Button>().Any())
                    continue;

                flow.WrapContents = false;
                flow.AutoScroll = false;
                flow.BackColor = Color.Transparent;
                flow.Height = Math.Max(flow.Height, 40);
                flow.Padding = new Padding(0, Math.Max(0, flow.Padding.Top), 0, 0);
            }
        }

        private static void EnsureHeaderActions(Control header)
        {
            string meta = ((header.Name ?? string.Empty) + " " + (header.Tag ?? string.Empty)).ToLowerInvariant();
            if (ContainsAny(meta, "no-global-actions", "custom-header-actions"))
                return;

            if (header == null || header.IsDisposed || (HasRefreshButton(header) && HasPreviewButton(header)))
                return;

            FlowLayoutPanel existingFlowRail = Descendants(header)
                .OfType<FlowLayoutPanel>()
                .FirstOrDefault(f => f.Controls.OfType<Button>().Any());

            if (existingFlowRail != null)
            {
                AddHeaderButtonsToFlowRail(existingFlowRail);
                return;
            }

            FlowLayoutPanel host = header.Controls.Find(GlobalActionHostName, false).OfType<FlowLayoutPanel>().FirstOrDefault();
            if (host == null)
            {
                host = new FlowLayoutPanel
                {
                    Name = GlobalActionHostName,
                    Dock = DockStyle.Right,
                    Width = 276,
                    BackColor = Color.Transparent,
                    Padding = new Padding(0, 4, 0, 0),
                    Margin = Padding.Empty,
                    WrapContents = false,
                    AutoScroll = false,
                    FlowDirection = FlowDirection.RightToLeft
                };
                header.Controls.Add(host);
                host.BringToFront();
            }

            AddHeaderButtonsToFlowRail(host);
        }

        private static void AddHeaderButtonsToFlowRail(FlowLayoutPanel rail)
        {
            if (rail == null || rail.IsDisposed)
                return;

            if (rail.Controls.Find(GlobalRefreshButtonName, false).Length == 0 && !HasRefreshButton(rail.Parent ?? rail))
                rail.Controls.Add(CreateRefreshButton());

            if (rail.Controls.Find(GlobalPreviewButtonName, false).Length == 0 && !HasPreviewButton(rail.Parent ?? rail))
                rail.Controls.Add(CreatePreviewButton());
        }

        private static Button CreateRefreshButton()
        {
            Button refresh = DS.GhostBtn("Refresh", 108, 36);
            refresh.Name = GlobalRefreshButtonName;
            refresh.UseMnemonic = false;
            refresh.Margin = new Padding(8, 0, 8, 0);
            ModernIconSystem.AddButtonIcon(refresh, ModernIconKind.Refresh);
            UIHelper.ApplyActionButton(refresh, UiActionVariant.Secondary);
            refresh.Padding = new Padding(10, 0, 10, 0);
            refresh.TextAlign = ContentAlignment.MiddleCenter;
            refresh.Click += (s, e) =>
            {
                Button source = s as Button;
                if (source == null)
                    return;

                source.Enabled = false;
                _ = GlobalRefreshService.RefreshFromAsync(source)
                    .ContinueWith(_ =>
                    {
                        if (!source.IsDisposed)
                            source.BeginInvoke((Action)(() => source.Enabled = true));
                    }, TaskScheduler.Default);
            };
            return refresh;
        }

        private static Button CreatePreviewButton()
        {
            Button preview = DS.GhostBtn("Preview", 108, 36);
            preview.Name = GlobalPreviewButtonName;
            preview.UseMnemonic = false;
            preview.Margin = new Padding(8, 0, 8, 0);
            ModernIconSystem.AddButtonIcon(preview, ModernIconKind.Document);
            UIHelper.ApplyActionButton(preview, UiActionVariant.Secondary);
            preview.Padding = new Padding(10, 0, 10, 0);
            preview.TextAlign = ContentAlignment.MiddleCenter;
            preview.Click += (s, e) =>
            {
                Button source = s as Button;
                if (source == null)
                    return;

                source.Enabled = false;
                _ = GlobalPreviewService.PreviewFromAsync(source)
                    .ContinueWith(_ =>
                    {
                        if (!source.IsDisposed)
                            source.BeginInvoke((Action)(() => source.Enabled = true));
                    }, TaskScheduler.Default);
            };
            return preview;
        }

        private static IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (Control grand in Descendants(child))
                    yield return grand;
            }
        }

        private static bool LooksLikeBadge(Label label)
        {
            if (label == null)
                return false;

            string text = (label.Text ?? string.Empty).Trim();
            if (text.Length <= 3 && label.Width <= 72 && label.Height <= 72 && label.BackColor != Color.Transparent)
                return true;

            string meta = ((label.Name ?? string.Empty) + " " + (label.Tag ?? string.Empty)).ToLowerInvariant();
            return ContainsAny(meta, "badge", "chip", "pill", "avatar", "status");
        }

        private static bool IsPrimaryAction(string text)
        {
            string key = (text ?? string.Empty).ToLowerInvariant();
            return key.StartsWith("+") ||
                   ContainsAny(key, "new", "add", "save", "record", "generate", "run payroll", "sync now", "assign");
        }

        private static bool IsCompact(string text)
        {
            string key = (text ?? string.Empty).Trim();
            return key.Length <= 2 || key == "..." || key == "⋯" || key == "⋮";
        }

        private static bool HasRefreshButton(Control header)
        {
            return Descendants(header)
                .Concat(new[] { header })
                .OfType<Button>()
                .Any(button => IsRefreshButton(button));
        }

        private static bool HasPreviewButton(Control header)
        {
            return Descendants(header)
                .Concat(new[] { header })
                .OfType<Button>()
                .Any(button => IsPreviewButton(button));
        }

        private static bool IsRefreshButton(Button button)
        {
            if (button == null)
                return false;

            string meta = ((button.Name ?? string.Empty) + " " + (button.Tag ?? string.Empty) + " " + (button.Text ?? string.Empty)).ToLowerInvariant();
            return ContainsAny(meta, "refresh", GlobalRefreshButtonName.ToLowerInvariant());
        }

        private static bool IsPreviewButton(Button button)
        {
            if (button == null)
                return false;

            string meta = ((button.Name ?? string.Empty) + " " + (button.Tag ?? string.Empty) + " " + (button.Text ?? string.Empty)).ToLowerInvariant();
            return ContainsAny(meta, "preview", "print preview", "preview xml", GlobalPreviewButtonName.ToLowerInvariant());
        }

        private static bool IsHeaderTitleCandidate(Label label)
        {
            if (label == null || label.Font == null)
                return false;

            string text = (label.Text ?? string.Empty).Trim();
            if (text.Length < 3 || text.Length > 80)
                return false;

            if (label.Font.Size < 13f || label.Top > 30 || LooksLikeMetricValue(text))
                return false;

            return true;
        }

        private static bool LooksLikeMetricValue(string text)
        {
            string value = (text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
                return false;

            int digits = value.Count(char.IsDigit);
            int letters = value.Count(char.IsLetter);
            if (digits >= 3 && letters <= 1)
                return true;

            return value.StartsWith("₹", StringComparison.Ordinal) ||
                   value.StartsWith("$", StringComparison.Ordinal) ||
                   value.StartsWith("€", StringComparison.Ordinal) ||
                   value.StartsWith("£", StringComparison.Ordinal);
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            foreach (string token in tokens)
            {
                if (!string.IsNullOrWhiteSpace(token) && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
    }

    internal static class GlobalRefreshService
    {
        private static readonly string[] RefreshMethodNames =
        {
            "RefreshData",
            "RefreshNow",
            "LoadList",
            "LoadData",
            "QueueInitialLoad",
            "QueueInitialize",
            "QueueInitialInventoryLoad",
            "QueueLoadDispatchData",
            "RefreshDashboard",
            "RefreshQuotationDashboardSafe",
            "RefreshLicenseStatus",
            "BeginLoadReferenceDataAsync",
            "RefreshAllAsync",
            "LoadPageDataAndRefreshAsync"
        };

        public static async Task RefreshFromAsync(Control source)
        {
            if (source == null || source.IsDisposed)
                return;

            if (TryRefreshShellPage(source))
                return;

            Control target = ResolveRefreshTarget(source);
            if (target == null)
                return;

            if (target is IRefreshable refreshable)
            {
                refreshable.RefreshData();
                return;
            }

            await TryInvokeKnownRefreshAsync(target).ConfigureAwait(true);
        }

        private static bool TryRefreshShellPage(Control source)
        {
            MainForm mainForm = source.FindForm() as MainForm;
            if (mainForm == null)
                return false;

            if (IsDescendantOf(mainForm, source))
            {
                mainForm.RequestCurrentPageRefresh();
                return true;
            }

            return false;
        }

        private static Control ResolveRefreshTarget(Control source)
        {
            Control current = source;
            while (current != null)
            {
                if (current is BaseUserControl || current is ServoPageBase || current is BaseForm || current is ServoFormBase)
                    return current;

                current = current.Parent;
            }

            return source.FindForm();
        }

        private static async Task TryInvokeKnownRefreshAsync(Control target)
        {
            foreach (string methodName in RefreshMethodNames)
            {
                MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (method == null)
                    continue;

                object result = method.Invoke(target, null);
                Task task = result as Task;
                if (task != null)
                {
                    await task.ConfigureAwait(true);
                }
                return;
            }
        }

        private static bool IsDescendantOf(Control ancestor, Control candidate)
        {
            Control current = candidate;
            while (current != null)
            {
                if (ReferenceEquals(current, ancestor))
                    return true;

                current = current.Parent;
            }

            return false;
        }
    }

    internal static class GlobalPreviewService
    {
        private static readonly string[] PreviewMethodNames =
        {
            "PreviewQuotation",
            "PreviewPurchaseOrder",
            "PreviewStockReport",
            "PreviewStockValuation",
            "PreviewConnection",
            "PreviewSelectedXml",
            "BtnPreview_Click"
        };

        public static async Task PreviewFromAsync(Control source)
        {
            if (source == null || source.IsDisposed)
                return;

            Control target = ResolvePreviewTarget(source);
            if (target == null)
                return;

            if (await TryInvokeKnownPreviewAsync(target).ConfigureAwait(true))
                return;

            ShowVisualPreview(target);
        }

        private static Control ResolvePreviewTarget(Control source)
        {
            Control current = source;
            while (current != null)
            {
                if (current is BaseUserControl || current is ServoPageBase || current is BaseForm || current is ServoFormBase)
                    return current;

                current = current.Parent;
            }

            return source.FindForm();
        }

        private static async Task<bool> TryInvokeKnownPreviewAsync(Control target)
        {
            foreach (string methodName in PreviewMethodNames)
            {
                MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method == null)
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                object result = null;
                if (parameters.Length == 0)
                {
                    result = method.Invoke(target, null);
                }
                else if (parameters.Length == 2 &&
                         parameters[0].ParameterType == typeof(object) &&
                         typeof(EventArgs).IsAssignableFrom(parameters[1].ParameterType))
                {
                    result = method.Invoke(target, new object[] { target, EventArgs.Empty });
                }
                else
                {
                    continue;
                }

                Task task = result as Task;
                if (task != null)
                    await task.ConfigureAwait(true);

                return true;
            }

            return false;
        }

        private static void ShowVisualPreview(Control target)
        {
            Bitmap snapshot = TryRenderControl(target);
            if (snapshot == null)
            {
                MessageBox.Show(target.FindForm() ?? target, "Preview is not available for this screen yet.", "Preview", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new VisualPreviewDialog(target.TextOrTypeName(), snapshot))
                dialog.ShowDialog(target.FindForm() ?? target);
        }

        private static Bitmap TryRenderControl(Control target)
        {
            if (target == null || target.IsDisposed)
                return null;

            Size size = target.ClientSize.Width > 0 && target.ClientSize.Height > 0
                ? target.ClientSize
                : target.Size;

            if (size.Width <= 0 || size.Height <= 0)
                return null;

            Bitmap bitmap = new Bitmap(size.Width, size.Height);
            try
            {
                target.DrawToBitmap(bitmap, new Rectangle(Point.Empty, size));
                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                return null;
            }
        }

        private static string TextOrTypeName(this Control control)
        {
            Form form = control as Form ?? control.FindForm();
            if (form != null && !string.IsNullOrWhiteSpace(form.Text))
                return form.Text;

            return control.GetType().Name.Replace("Form", string.Empty).Replace("Page", string.Empty).Trim();
        }
    }

    internal sealed class VisualPreviewDialog : ServoFormBase
    {
        private readonly Bitmap _snapshot;
        private readonly PictureBox _picture;

        public VisualPreviewDialog(string title, Bitmap snapshot)
        {
            _snapshot = snapshot;
            Text = string.IsNullOrWhiteSpace(title) ? "Preview" : title + " Preview";
            StartPosition = FormStartPosition.CenterParent;
            Width = 1180;
            Height = 820;
            BackColor = Color.White;

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.White };
            Button save = MakeButton("Save PNG", 10, Color.FromArgb(37, 99, 235));
            save.Click += SaveSnapshot;
            toolbar.Controls.Add(save);

            _picture = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = DS.Slate100,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = _snapshot
            };

            Controls.Add(_picture);
            Controls.Add(toolbar);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (_picture != null)
                _picture.Image = null;
            if (_snapshot != null)
                _snapshot.Dispose();
        }

        private static Button MakeButton(string text, int left, Color color)
        {
            Button button = new Button
            {
                Text = text,
                Width = 96,
                Height = 28,
                Left = left,
                Top = 8,
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private void SaveSnapshot(object sender, EventArgs e)
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "PNG Files (*.png)|*.png";
                dialog.DefaultExt = "png";
                dialog.AddExtension = true;
                dialog.FileName = "servo-preview.png";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                _snapshot.Save(dialog.FileName, ImageFormat.Png);
                MessageBox.Show(this, "Preview saved to " + dialog.FileName, "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}

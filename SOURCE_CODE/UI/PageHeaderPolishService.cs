using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    /// <summary>Applies a light-touch visual polish to existing page headers without replacing local layouts.</summary>
    internal static class PageHeaderPolishService
    {
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
            if (ContainsAny(meta, "sidebar", "grid", "footer", "filter", "search", "lineitem", "empty", "dialog"))
                return false;

            if (!(control is Panel) && !(control is TableLayoutPanel) && !(control is FlowLayoutPanel))
                return false;

            bool topPosition = control.Dock == DockStyle.Top || control.Top <= 16 || ContainsAny(meta, "header", "topbar", "top-bar");
            if (!topPosition)
                return false;

            int height = control.Height;
            if (height > 0 && (height < 44 || height > 156))
                return false;

            List<Label> labels = control.Controls.OfType<Label>().Where(l => !string.IsNullOrWhiteSpace(l.Text)).ToList();
            if (labels.Count == 0)
                labels = control.Controls.Cast<Control>().SelectMany(ImmediateLabels).Where(l => !string.IsNullOrWhiteSpace(l.Text)).ToList();

            bool hasTitle = labels.Any(l => l.Font != null && l.Font.Size >= 13f && l.Text.Trim().Length <= 80);
            bool namedHeader = ContainsAny(meta, "header", "topbar", "top-bar");
            bool hasActions = control.Controls.OfType<Button>().Any() ||
                              control.Controls.OfType<FlowLayoutPanel>().Any(f => f.Controls.OfType<Button>().Any()) ||
                              control.Controls.Cast<Control>().SelectMany(c => c.Controls.OfType<Button>()).Any();

            return namedHeader || (hasTitle && (hasActions || labels.Count >= 2));
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
}

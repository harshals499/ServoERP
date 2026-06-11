using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    internal static class SharedUiPrimitives
    {
        public const int CardGap = DS.Space3;
        public const int CardPadding = DS.Space3;
        public const int FormMargin = DS.Space2;
        private static readonly HashSet<Button> DisabledStateButtons = new HashSet<Button>();
        private static readonly Dictionary<Button, Tuple<Color, Color, Color, Cursor>> ButtonEnabledState =
            new Dictionary<Button, Tuple<Color, Color, Color, Cursor>>();
        private static readonly ToolTip SharedToolTip = new ToolTip
        {
            AutomaticDelay = 350,
            AutoPopDelay = 7000,
            InitialDelay = 350,
            ReshowDelay = 100,
            ShowAlways = true
        };

        /// <summary>Applies shared visual primitives to an existing control tree.</summary>
        public static void ApplyToTree(Control root)
        {
            if (root == null || root.IsDisposed)
                return;

            ApplyToControl(root);
            foreach (Control child in root.Controls)
                ApplyToTree(child);
        }

        /// <summary>Applies shared visual primitives to a single control.</summary>
        public static void ApplyToControl(Control control)
        {
            if (control == null || control.IsDisposed)
                return;

            ApplyCommonControlPrimitive(control);
            ApplyRequiredFieldCue(control);

            DataGridView grid = control as DataGridView;
            if (grid != null)
            {
                ApplyGridPrimitive(grid);
                return;
            }

            Button button = control as Button;
            if (button != null)
            {
                ApplyButtonPrimitive(button);
                return;
            }

            Label label = control as Label;
            if (label != null)
                ApplyLabelPrimitive(label);

            Panel panel = control as Panel;
            if (panel != null)
                ApplyCardPrimitive(panel);

            GroupBox group = control as GroupBox;
            if (group != null)
                ApplyGroupPrimitive(group);

            FlowLayoutPanel flow = control as FlowLayoutPanel;
            if (flow != null)
                ApplyFlowPrimitive(flow);

            TableLayoutPanel table = control as TableLayoutPanel;
            if (table != null)
                ApplyTablePrimitive(table);
        }

        /// <summary>Creates a shared empty-state panel for lists and dashboards.</summary>
        public static Panel EmptyState(string title, string detail = null)
        {
            Panel panel = new Panel
            {
                BackColor = DS.BgCard,
                Padding = new Padding(DS.Space4),
                MinimumSize = new Size(180, 86)
            };
            DS.Rounded(panel, DS.RadiusMd);

            Label titleLabel = new Label
            {
                Text = string.IsNullOrWhiteSpace(title) ? "No records found" : title,
                Dock = DockStyle.Top,
                Height = 24,
                Font = DS.BodyBold,
                ForeColor = DS.Slate700,
                TextAlign = ContentAlignment.MiddleCenter
            };
            panel.Controls.Add(titleLabel);

            if (!string.IsNullOrWhiteSpace(detail))
            {
                Label detailLabel = new Label
                {
                    Text = detail,
                    Dock = DockStyle.Top,
                    Height = 42,
                    Font = DS.Small,
                    ForeColor = DS.Slate500,
                    TextAlign = ContentAlignment.TopCenter
                };
                panel.Controls.Add(detailLabel);
            }

            return panel;
        }

        /// <summary>Creates a shared status badge label.</summary>
        public static Label StatusBadge(string status, int width = 96)
        {
            Color bg;
            Color fg;
            ResolveStatusColors(status, out bg, out fg);
            return DS.StatusChipLabel(status, bg, fg, width);
        }

        private static void ApplyCardPrimitive(Panel panel)
        {
            if (!CardSurfacePolicy.IsDashboardLayoutCard(panel))
                return;

            if (panel.BackColor == SystemColors.Control || panel.BackColor == Color.Empty || DS.IsLegacyLightBackColor(panel.BackColor))
                panel.BackColor = DS.BgCard;
            panel.BorderStyle = BorderStyle.None;
            panel.Padding = EnsureMinimumPadding(panel.Padding, 8);
            panel.Margin = EnsureMinimumMargin(panel.Margin, CardGap);
            DS.Rounded(panel, DS.RadiusMd);
        }

        private static void ApplyGroupPrimitive(GroupBox group)
        {
            group.Font = DS.BodyBold;
            group.ForeColor = DS.Slate800;
            group.Padding = EnsureMinimumPadding(group.Padding, 8);
            group.Margin = EnsureMinimumMargin(group.Margin, CardGap);
        }

        private static void ApplyCommonControlPrimitive(Control control)
        {
            if (control.Margin == Padding.Empty && IsInteractiveControl(control))
                control.Margin = new Padding(0, 0, DS.Space2, DS.Space2);

            TextBoxBase textBox = control as TextBoxBase;
            if (textBox != null)
            {
                if (!textBox.Multiline && textBox.Height < 28)
                    textBox.Height = 28;
                if (textBox.Font.Size < 8f)
                    textBox.Font = DS.Body;
                textBox.ForeColor = textBox.Enabled ? DS.InputText : DS.InputMutedText;
                textBox.BackColor = textBox.Enabled ? DS.BgInput : DS.InputDisabledBack;
                return;
            }

            ComboBox combo = control as ComboBox;
            if (combo != null)
            {
                if (combo.Height < 30)
                    combo.Height = 30;
                if (combo.Font.Size < 8f)
                    combo.Font = DS.Body;
                combo.ForeColor = combo.Enabled ? DS.InputText : DS.InputMutedText;
                combo.BackColor = combo.Enabled ? DS.BgInput : DS.InputDisabledBack;
                return;
            }

            DateTimePicker date = control as DateTimePicker;
            if (date != null)
            {
                if (date.Height < 30)
                    date.Height = 30;
                if (date.Font.Size < 8f)
                    date.Font = DS.Body;
                return;
            }

            NumericUpDown numeric = control as NumericUpDown;
            if (numeric != null)
            {
                if (numeric.Height < 30)
                    numeric.Height = 30;
                if (numeric.Font.Size < 8f)
                    numeric.Font = DS.Body;
            }
        }

        private static bool IsInteractiveControl(Control control)
        {
            return control is Button ||
                   control is TextBoxBase ||
                   control is ComboBox ||
                   control is DateTimePicker ||
                   control is NumericUpDown ||
                   control is CheckBox ||
                   control is RadioButton;
        }

        private static void ApplyGridPrimitive(DataGridView grid)
        {
            GridTheme.Apply(grid);
            grid.RowHeadersVisible = false;
            grid.AllowUserToResizeRows = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.BackgroundColor = DS.BgCard;
            grid.EnableHeadersVisualStyles = false;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            grid.RowTemplate.Height = Math.Max(grid.RowTemplate.Height, 30);

            if (grid.Dock == DockStyle.None && grid.Anchor == AnchorStyles.Top)
                grid.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

            foreach (DataGridViewColumn column in grid.Columns)
            {
                column.MinimumWidth = Math.Max(column.MinimumWidth, 70);
                column.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            }
        }

        private static void ApplyButtonPrimitive(Button button)
        {
            UIHelper.ApplyActionButton(button);
            button.AutoEllipsis = true;
            ApplyFullTextToolTip(button);
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.MinimumSize = new Size(Math.Max(button.MinimumSize.Width, 88), Math.Max(button.MinimumSize.Height, 32));
            if (button.Height < button.MinimumSize.Height)
                button.Height = button.MinimumSize.Height;
            if (button.Width < button.MinimumSize.Width && button.Dock == DockStyle.None && button.AutoSize == false)
                button.Width = button.MinimumSize.Width;

            if (!DisabledStateButtons.Contains(button))
            {
                DisabledStateButtons.Add(button);
                button.EnabledChanged += ButtonEnabledChanged;
                button.Disposed += ButtonDisposed;
            }

            ApplyButtonEnabledState(button);
        }

        private static void ButtonDisposed(object sender, EventArgs e)
        {
            Button button = sender as Button;
            if (button != null)
            {
                DisabledStateButtons.Remove(button);
                ButtonEnabledState.Remove(button);
            }
        }

        private static void ButtonEnabledChanged(object sender, EventArgs e)
        {
            ApplyButtonEnabledState(sender as Button);
        }

        private static void ApplyButtonEnabledState(Button button)
        {
            if (button == null || button.IsDisposed)
                return;

            if (button.Enabled)
            {
                Tuple<Color, Color, Color, Cursor> state;
                if (ButtonEnabledState.TryGetValue(button, out state))
                {
                    button.BackColor = state.Item1;
                    button.ForeColor = state.Item2;
                    button.FlatAppearance.BorderColor = state.Item3;
                    button.Cursor = state.Item4;
                }
                return;
            }

            if (!ButtonEnabledState.ContainsKey(button))
            {
                ButtonEnabledState[button] = Tuple.Create(
                    button.BackColor,
                    button.ForeColor,
                    button.FlatAppearance.BorderColor,
                    button.Cursor);
            }

            button.BackColor = DS.InputDisabledBack;
            button.ForeColor = DS.Slate500;
            button.FlatAppearance.BorderColor = DS.Slate300;
            button.Cursor = Cursors.Default;
        }

        private static void ApplyFlowPrimitive(FlowLayoutPanel flow)
        {
            if (flow.Controls.Count == 0 || IsChromeContainer(flow))
                return;

            if ((flow.FlowDirection == FlowDirection.LeftToRight || flow.FlowDirection == FlowDirection.RightToLeft) && !flow.WrapContents)
                flow.WrapContents = true;

            foreach (Control child in flow.Controls)
            {
                if (child.Visible && child.Margin == Padding.Empty)
                    child.Margin = new Padding(0, 0, DS.Space2, DS.Space2);
            }
        }

        private static void ApplyTablePrimitive(TableLayoutPanel table)
        {
            if (table.Controls.Count == 0 || IsChromeContainer(table))
                return;

            foreach (Control child in table.Controls)
            {
                if (!child.Visible)
                    continue;
                if (child.Margin == Padding.Empty)
                    child.Margin = new Padding(0, 0, CardGap, CardGap);
                if (child is Label && child.Dock == DockStyle.None && child.Anchor == AnchorStyles.Top)
                    child.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            }
        }

        private static Padding EnsureMinimumPadding(Padding padding, int minimum)
        {
            return new Padding(
                Math.Max(padding.Left, minimum),
                Math.Max(padding.Top, minimum),
                Math.Max(padding.Right, minimum),
                Math.Max(padding.Bottom, minimum));
        }

        private static Padding EnsureMinimumMargin(Padding margin, int minimum)
        {
            return new Padding(
                Math.Max(margin.Left, 0),
                Math.Max(margin.Top, 0),
                Math.Max(margin.Right, minimum),
                Math.Max(margin.Bottom, minimum));
        }

        private static bool IsChromeContainer(Control control)
        {
            string metadata = ((control.Name ?? string.Empty) + " " + (control.Tag ?? string.Empty)).ToUpperInvariant();
            return ContainsAny(metadata, "SIDEBAR", "NAV", "TOOLBAR", "HEADER", "FOOTER", "MENU", "BANNER", "TITLE");
        }

        private static void ApplyLabelPrimitive(Label label)
        {
            string text = (label.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text) || CardResizeGripService.IsResizeGrip(label))
                return;

            if (LooksLikeEmptyState(text))
            {
                label.Font = DS.Small;
                label.ForeColor = DS.Slate500;
                label.TextAlign = ContentAlignment.MiddleCenter;
                label.AutoEllipsis = false;
                if (!label.AutoSize)
                    label.MaximumSize = new Size(Math.Max(120, label.Width), 0);
                return;
            }

            if (LooksLikeCompactIconBadge(label, text))
            {
                NormalizeAccessibleBadgeColors(label);
                ApplyCompactIconBadge(label);
                return;
            }

            if (LooksLikeAvatarBadge(label, text))
            {
                NormalizeAccessibleBadgeColors(label);
                ApplyCompactIconBadge(label);
                return;
            }

            if (!LooksLikeExplicitStatusBadge(label, text))
            {
                if (!label.AutoSize)
                {
                    label.AutoEllipsis = true;
                    ApplyFullTextToolTip(label);
                }
                if (label.Font.Size < 7.5f)
                    label.Font = DS.Small;
                if (label.Height < 18 && label.Dock == DockStyle.None)
                    label.Height = 18;
                return;
            }

            Color bg;
            Color fg;
            ResolveStatusColors(text, out bg, out fg);
            label.BackColor = bg;
            label.ForeColor = fg;
            label.Font = DS.SmallBold;
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.AutoEllipsis = false;
            label.MinimumSize = new Size(Math.Max(label.MinimumSize.Width, 76), Math.Max(label.MinimumSize.Height, 22));
            if (label.Width < label.MinimumSize.Width)
                label.Width = label.MinimumSize.Width;
            if (label.Height < label.MinimumSize.Height)
                label.Height = label.MinimumSize.Height;
            DS.Rounded(label, Math.Min(11, Math.Max(4, label.Height / 2)));
        }

        private static void ApplyFullTextToolTip(Control control)
        {
            string text = (control == null ? string.Empty : control.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text) || text.Length < 12)
                return;

            SharedToolTip.SetToolTip(control, text.Replace(Environment.NewLine, " / "));
        }

        private static void ApplyRequiredFieldCue(Control control)
        {
            if (!IsInteractiveControl(control) || control.Parent == null)
                return;

            Label requiredLabel = control.Parent.Controls
                .OfType<Label>()
                .FirstOrDefault(label => !string.IsNullOrWhiteSpace(label.Text) && label.Text.TrimEnd().EndsWith("*", StringComparison.Ordinal));
            if (requiredLabel == null)
                return;

            string fieldName = requiredLabel.Text.Trim().TrimEnd('*').Trim();
            if (string.IsNullOrWhiteSpace(fieldName))
                fieldName = "This field";

            SharedToolTip.SetToolTip(control, fieldName + " is required.");
            SharedToolTip.SetToolTip(requiredLabel, fieldName + " is required.");
        }

        private static bool LooksLikeCompactIconBadge(Label label, string text)
        {
            if (label == null || string.IsNullOrWhiteSpace(text))
                return false;

            if (!string.Equals(text, "!", StringComparison.Ordinal) &&
                !string.Equals(text, "i", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(text, "?", StringComparison.Ordinal))
                return false;

            if (label.Width > 44 || label.Height > 44)
                return false;

            return label.BackColor != Color.Empty &&
                   label.BackColor != Color.Transparent &&
                   label.BackColor != SystemColors.Control;
        }

        private static bool LooksLikeAvatarBadge(Label label, string text)
        {
            if (label == null || string.IsNullOrWhiteSpace(text))
                return false;

            if (text.Length > 3 || label.Width < 20 || label.Height < 20 || label.Width > 72 || label.Height > 72)
                return false;

            if (label.BackColor == Color.Empty || label.BackColor == Color.Transparent || label.BackColor == SystemColors.Control)
                return false;

            bool brightForeground = label.ForeColor.ToArgb() == Color.White.ToArgb() || label.ForeColor.GetBrightness() > 0.82f;
            bool darkBackground = label.BackColor.GetBrightness() < 0.70f;
            string metadata = ((label.Name ?? string.Empty) + " " + (label.Tag ?? string.Empty) + " " + (label.Parent == null ? string.Empty : label.Parent.Name ?? string.Empty)).ToUpperInvariant();
            bool namedBadge = ContainsAny(metadata, "AVATAR", "ICON", "INITIAL", "BADGE", "PILL");

            return brightForeground && darkBackground && (namedBadge || label.Font.Bold);
        }

        private static void NormalizeAccessibleBadgeColors(Label label)
        {
            if (label == null)
                return;

            Color accent = label.BackColor;
            if (accent == Color.Empty || accent == Color.Transparent || accent == SystemColors.Control)
                return;

            label.BackColor = DS.Lighten(accent, 0.84f);
            label.ForeColor = accent.GetBrightness() < 0.55f ? DS.Darken(accent, 0.08f) : DS.Slate900;
        }

        private static void ApplyCompactIconBadge(Label label)
        {
            label.Font = DS.SmallBold;
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.AutoEllipsis = false;
            label.MinimumSize = new Size(Math.Max(label.MinimumSize.Width, 28), Math.Max(label.MinimumSize.Height, 28));
            if (label.Width < label.MinimumSize.Width)
                label.Width = label.MinimumSize.Width;
            if (label.Height < label.MinimumSize.Height)
                label.Height = label.MinimumSize.Height;
            DS.Rounded(label, Math.Max(8, Math.Min(label.Width, label.Height) / 2));
        }

        private static bool LooksLikeEmptyState(string text)
        {
            string key = text.ToLowerInvariant();
            return key.Contains("no records") ||
                   key.Contains("no data") ||
                   key.Contains("no invoices") ||
                   key.Contains("no jobs") ||
                   key.Contains("not found") ||
                   key.Contains("nothing to show");
        }

        private static bool LooksLikeExplicitStatusBadge(Label label, string text)
        {
            string metadata = ((label.Name ?? string.Empty) + " " +
                               (label.Tag ?? string.Empty) + " " +
                               (label.Parent == null ? string.Empty : label.Parent.Name ?? string.Empty) + " " +
                               (label.Parent == null || label.Parent.Tag == null ? string.Empty : label.Parent.Tag.ToString()))
                .ToUpperInvariant();

            if (!ContainsAny(metadata, "BADGE", "CHIP", "STATUSPILL", "STATUS_BADGE", "STATUS-BADGE"))
                return false;

            string key = Normalize(text);
            string[] statuses =
            {
                "draft", "pending", "approved", "open", "active", "inactive", "paid", "unpaid",
                "overdue", "completed", "cancelled", "canceled", "critical", "high", "medium",
                "low", "emergency", "amc", "available", "busy", "offline", "on job", "scheduled",
                "in progress", "on hold", "resolved", "closed", "sent", "accepted", "rejected"
            };
            return statuses.Any(status => string.Equals(key, status, StringComparison.OrdinalIgnoreCase));
        }

        private static void ResolveStatusColors(string status, out Color bg, out Color fg)
        {
            string key = Normalize(status);
            if (ContainsAny(key, "critical", "emergency", "overdue", "rejected", "cancelled", "canceled", "unpaid"))
            {
                bg = DS.Red50;
                fg = DS.Red600;
                return;
            }

            if (ContainsAny(key, "approved", "active", "paid", "completed", "resolved", "accepted", "available"))
            {
                bg = DS.Green50;
                fg = DS.Green600;
                return;
            }

            if (ContainsAny(key, "pending", "high", "medium", "scheduled", "in progress", "on hold", "sent", "busy", "on job"))
            {
                bg = DS.Amber50;
                fg = DS.Amber600;
                return;
            }

            if (ContainsAny(key, "draft", "open", "low", "offline", "closed", "amc"))
            {
                bg = DS.Primary50;
                fg = DS.Primary700;
                return;
            }

            bg = DS.Slate100;
            fg = DS.Slate700;
        }

        /// <summary>Builds the shared sent/received workflow layout used by ERP dashboard cards.</summary>
        public static Control BuildDirectionalWorkflowLayout(
            string leftTitle,
            string leftSentTitle,
            IEnumerable<string> leftSentRows,
            string leftReceivedTitle,
            IEnumerable<string> leftReceivedRows,
            string rightTitle,
            string rightSentTitle,
            IEnumerable<string> rightSentRows,
            string rightReceivedTitle,
            IEnumerable<string> rightReceivedRows)
        {
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(0),
                BackColor = Color.Transparent
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.Controls.Add(BuildDirectionalSection(leftTitle, leftSentTitle, leftSentRows, leftReceivedTitle, leftReceivedRows), 0, 0);
            layout.Controls.Add(BuildDirectionalSection(rightTitle, rightSentTitle, rightSentRows, rightReceivedTitle, rightReceivedRows), 1, 0);
            return layout;
        }

        /// <summary>Builds one workflow section with sent on top and received on bottom.</summary>
        private static Control BuildDirectionalSection(string sectionTitle, string sentTitle, IEnumerable<string> sentRows, string receivedTitle, IEnumerable<string> receivedRows)
        {
            Panel section = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(8),
                Margin = new Padding(0, 0, 8, 0)
            };

            Label title = new Label
            {
                Text = sectionTitle,
                Dock = DockStyle.Top,
                Height = 24,
                Font = new Font("Segoe UI", 9.2f, FontStyle.Bold),
                ForeColor = DS.Slate900,
                AutoEllipsis = true
            };

            TableLayoutPanel buckets = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(0),
                BackColor = Color.Transparent
            };
            buckets.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            buckets.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            buckets.Controls.Add(BuildDirectionalBucket(sentTitle, sentRows), 0, 0);
            buckets.Controls.Add(BuildDirectionalBucket(receivedTitle, receivedRows), 0, 1);

            section.Controls.Add(buckets);
            section.Controls.Add(title);
            return section;
        }

        /// <summary>Builds a single mini workflow bucket for recent records.</summary>
        private static Control BuildDirectionalBucket(string title, IEnumerable<string> rows)
        {
            Panel bucket = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = DS.BgCard,
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(10)
            };
            bucket.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (System.Drawing.Drawing2D.GraphicsPath path = DS.RoundedRect(new Rectangle(0, 0, bucket.Width - 1, bucket.Height - 1), 8))
                using (Pen pen = new Pen(DS.Border))
                    e.Graphics.DrawPath(pen, path);
            };
            DS.Rounded(bucket, 8);

            Label heading = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 22,
                Font = new Font("Segoe UI", 8.6f, FontStyle.Bold),
                ForeColor = DS.Primary500,
                AutoEllipsis = true
            };
            FlowLayoutPanel list = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = false,
                BackColor = DS.BgCard,
                Padding = new Padding(0, 4, 0, 0)
            };

            List<string> values = (rows ?? Enumerable.Empty<string>())
                .Where(row => !string.IsNullOrWhiteSpace(row))
                .Take(4)
                .ToList();

            if (values.Count == 0)
            {
                list.Controls.Add(new Label
                {
                    Text = "No records yet.",
                    Width = 260,
                    Height = 22,
                    Font = new Font("Segoe UI", 8f),
                    ForeColor = DS.Slate500,
                    AutoEllipsis = true
                });
            }
            else
            {
                foreach (string value in values)
                {
                    list.Controls.Add(new Label
                    {
                        Text = value,
                        Width = 260,
                        Height = 22,
                        Font = new Font("Segoe UI", 8f),
                        ForeColor = DS.Slate700,
                        AutoEllipsis = true,
                        Margin = new Padding(0, 0, 0, 2)
                    });
                }
            }

            list.Resize += (s, e) =>
            {
                foreach (Control child in list.Controls)
                    child.Width = Math.Max(80, list.ClientSize.Width - 4);
            };

            bucket.Controls.Add(list);
            bucket.Controls.Add(heading);
            return bucket;
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            return tokens.Any(token => value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}

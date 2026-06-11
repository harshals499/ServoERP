using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    internal static class LayoutAuditService
    {
        private const int DesiredGap = 12;
        private const int GapWarning = 16;
        private static readonly object Sync = new object();
        private static readonly Dictionary<int, Size> ProcessedRootSizes = new Dictionary<int, Size>();
        private static bool _logsPrepared;
        private static bool _globalAuditorAttached;
        private static int _totalFound;
        private static int _totalFixed;
        private static int _totalRemaining;

        /// <summary>Attaches the layout audit pass to all currently open and newly opened forms.</summary>
        public static void AttachGlobalFormAuditor()
        {
            lock (Sync)
            {
                if (_globalAuditorAttached)
                    return;

                _globalAuditorAttached = true;
            }

            Application.Idle += (sender, args) => AuditOpenForms();
        }

        /// <summary>Audits and fixes a root control once per app session.</summary>
        public static void AuditAndFix(Control root)
        {
            if (root == null || root.IsDisposed)
                return;
            if (root.Controls.Count == 0 && !(root is DataGridView))
                return;

            int key = RuntimeHelpers.GetHashCode(root);
            Size rootSize = root.Size;
            lock (Sync)
            {
                PrepareLogs();
                Size processedSize;
                if (ProcessedRootSizes.TryGetValue(key, out processedSize) && processedSize == rootSize)
                    return;
                ProcessedRootSizes[key] = rootSize;
            }

            int before = Audit(root, GetBeforePath(), false);
            int fixedCount = TightenLayout(root);
            int after = Audit(root, GetAfterPath(), true);

            lock (Sync)
            {
                _totalFound += before;
                _totalFixed += fixedCount;
                _totalRemaining += after;
                AppendSummary();
            }
        }

        public static string Summary()
        {
            lock (Sync)
                return "Layout fix complete - " + _totalFound + " gaps found, " + _totalFixed + " fixed, " + _totalRemaining + " remaining";
        }

        private static void AuditOpenForms()
        {
            try
            {
                foreach (Form form in Application.OpenForms.Cast<Form>().ToList())
                    AuditAndFix(form);
            }
            catch
            {
            }
        }

        private static int TightenLayout(Control root)
        {
            int fixedCount = 0;
            try
            {
                root.SuspendLayout();
                fixedCount += TightenControl(root);
            }
            catch
            {
            }
            finally
            {
                try { root.ResumeLayout(true); } catch { }
            }

            return fixedCount;
        }

        private static int TightenControl(Control control)
        {
            if (control == null || control.IsDisposed)
                return 0;

            int fixedCount = 0;
            fixedCount += NormalizeContainer(control);
            fixedCount += NormalizeGrid(control as DataGridView);
            fixedCount += NormalizeFlow(control as FlowLayoutPanel);
            fixedCount += NormalizeTable(control as TableLayoutPanel);

            foreach (Control child in control.Controls)
                fixedCount += TightenControl(child);

            fixedCount += TightenDirectChildren(control);
            return fixedCount;
        }

        private static int NormalizeGrid(DataGridView grid)
        {
            if (grid == null || grid.IsDisposed)
                return 0;

            int fixedCount = 0;
            AnchorStyles targetAnchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            if (grid.Dock == DockStyle.None && grid.Anchor != targetAnchor)
            {
                grid.Anchor = targetAnchor;
                fixedCount++;
            }

            if (grid.AutoSizeColumnsMode != DataGridViewAutoSizeColumnsMode.Fill)
            {
                grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                fixedCount++;
            }

            Control parent = grid.Parent;
            if (parent != null && grid.Dock == DockStyle.None)
            {
                int right = parent.ClientSize.Width - parent.Padding.Right - DesiredGap;
                if (right > grid.Right + GapWarning)
                {
                    grid.Width = Math.Max(grid.Width, right - grid.Left);
                    fixedCount++;
                }

                if (!HasVisibleSiblingBelow(grid))
                {
                    int bottom = parent.ClientSize.Height - parent.Padding.Bottom - DesiredGap;
                    if (bottom > grid.Bottom + GapWarning)
                    {
                        grid.Height = Math.Max(grid.Height, bottom - grid.Top);
                        fixedCount++;
                    }
                }
            }

            return fixedCount;
        }

        private static int NormalizeContainer(Control control)
        {
            if (!IsCardLike(control))
                return 0;

            int fixedCount = 0;
            if (control.Padding != new Padding(DesiredGap))
            {
                control.Padding = new Padding(DesiredGap);
                fixedCount++;
            }

            if (!(control.Parent is TableLayoutPanel) && !(control.Parent is FlowLayoutPanel) && control.Margin != new Padding(DesiredGap))
            {
                control.Margin = new Padding(DesiredGap);
                fixedCount++;
            }

            return fixedCount;
        }

        private static int NormalizeFlow(FlowLayoutPanel flow)
        {
            if (flow == null || flow.IsDisposed)
                return 0;
            if (IsHeaderOrActionChrome(flow))
                return 0;

            int fixedCount = 0;
            if (flow.FlowDirection == FlowDirection.LeftToRight || flow.FlowDirection == FlowDirection.RightToLeft)
            {
                if (!flow.WrapContents)
                {
                    flow.WrapContents = true;
                    fixedCount++;
                }
            }

            if (!IsChrome(flow) && flow.Dock != DockStyle.Fill && !flow.AutoSize)
            {
                flow.AutoSize = true;
                flow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                fixedCount++;
            }

            foreach (Control child in flow.Controls)
            {
                if (child.Visible && child.Margin != new Padding(0, 0, DesiredGap, DesiredGap))
                {
                    child.Margin = new Padding(0, 0, DesiredGap, DesiredGap);
                    fixedCount++;
                }
            }

            return fixedCount;
        }

        private static int NormalizeTable(TableLayoutPanel table)
        {
            if (table == null || table.IsDisposed)
                return 0;

            int fixedCount = 0;
            for (int row = 0; row < table.RowCount && row < table.RowStyles.Count; row++)
            {
                if (!HasControlInRow(table, row) && table.RowStyles[row].SizeType == SizeType.Absolute && table.RowStyles[row].Height > 0)
                {
                    table.RowStyles[row].Height = 0;
                    fixedCount++;
                }
            }

            for (int col = 0; col < table.ColumnCount && col < table.ColumnStyles.Count; col++)
            {
                if (!HasControlInColumn(table, col) && table.ColumnStyles[col].SizeType == SizeType.Absolute && table.ColumnStyles[col].Width > 0)
                {
                    table.ColumnStyles[col].Width = 0;
                    fixedCount++;
                }
            }

            return fixedCount;
        }

        private static int TightenDirectChildren(Control parent)
        {
            if (parent == null || parent.IsDisposed || parent.Controls.Count < 2 || parent is TableLayoutPanel || parent is FlowLayoutPanel || parent is Form)
                return 0;

            int fixedCount = 0;
            List<Control> candidates = parent.Controls.Cast<Control>()
                .Where(c => c.Visible && c.Width > 0 && c.Height > 0 && !IsAuditUtilityControl(c) && IsLayoutCandidate(c))
                .OrderBy(c => c.Top)
                .ThenBy(c => c.Left)
                .ToList();

            foreach (Control lower in candidates)
            {
                Control upper = candidates
                    .Where(c => c != lower && c.Bottom <= lower.Top && HorizontalOverlap(c, lower) > Math.Min(c.Width, lower.Width) / 4)
                    .OrderByDescending(c => c.Bottom)
                    .FirstOrDefault();

                if (upper == null)
                    continue;

                int gap = lower.Top - upper.Bottom;
                if (gap <= 12)
                    continue;

                int newTop = upper.Bottom + DesiredGap;
                Rectangle proposed = new Rectangle(lower.Left, newTop, lower.Width, lower.Height);
                if (!WouldOverlapSibling(parent, lower, proposed))
                {
                    lower.Top = newTop;
                    fixedCount++;
                }
            }

            candidates = parent.Controls.Cast<Control>()
                .Where(c => c.Visible && c.Width > 0 && c.Height > 0 && !IsAuditUtilityControl(c) && IsLayoutCandidate(c))
                .OrderBy(c => c.Left)
                .ThenBy(c => c.Top)
                .ToList();

            foreach (Control left in candidates)
            {
                Control right = candidates
                    .Where(c => c != left && c.Left >= left.Right && VerticalOverlap(c, left) > Math.Min(c.Height, left.Height) / 4)
                    .OrderBy(c => c.Left)
                    .FirstOrDefault();

                if (right == null)
                    continue;

                int gap = right.Left - left.Right;
                if (gap <= 12)
                    continue;

                Control stretch = left.Width >= right.Width ? left : right;
                if (stretch == left)
                {
                    int newWidth = right.Left - DesiredGap - left.Left;
                    if (newWidth > left.Width)
                    {
                        left.Width = newWidth;
                        fixedCount++;
                    }
                }
                else
                {
                    int newLeft = left.Right + DesiredGap;
                    int delta = right.Left - newLeft;
                    if (delta > 0)
                    {
                        right.Left = newLeft;
                        right.Width += delta;
                        fixedCount++;
                    }
                }
            }

            return fixedCount;
        }

        private static int Audit(Control root, string path, bool after)
        {
            int gaps = 0;
            var sb = new StringBuilder();
            sb.AppendLine("==================================================");
            sb.AppendLine((after ? "AFTER" : "BEFORE") + " Layout Audit");
            sb.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Form name: " + ResolveFormName(root));
            sb.AppendLine("Root: " + root.GetType().FullName);
            sb.AppendLine();
            AuditControl(root, sb, 0, ref gaps);
            sb.AppendLine();
            sb.AppendLine("Gaps flagged in this root: " + gaps);

            lock (Sync)
                File.AppendAllText(path, sb.ToString());

            return gaps;
        }

        private static void AuditControl(Control control, StringBuilder sb, int depth, ref int gaps)
        {
            if (control == null)
                return;

            string indent = new string(' ', depth * 2);
            sb.AppendLine(indent + Describe(control));
            List<Control> children = control.Controls.Cast<Control>().Where(c => c.Visible).OrderBy(c => c.Top).ThenBy(c => c.Left).ToList();
            foreach (Control child in children)
                AuditControl(child, sb, depth + 1, ref gaps);

            List<Control> gapChildren = children
                .Where(c => c.Width > 0 && c.Height > 0 && !IsAuditUtilityControl(c))
                .ToList();
            AuditSiblingGaps(control, gapChildren, sb, indent + "  ", ref gaps);
        }

        private static void AuditSiblingGaps(Control parent, List<Control> children, StringBuilder sb, string indent, ref int gaps)
        {
            if (children.Count < 2)
                return;

            foreach (Control current in children)
            {
                Control below = children
                    .Where(c => c != current && c.Top >= current.Bottom && HorizontalOverlap(c, current) > 0)
                    .OrderBy(c => c.Top)
                    .FirstOrDefault();
                if (below != null)
                {
                    int gap = below.Top - current.Bottom;
                    sb.AppendLine(indent + "Vertical gap " + ControlKey(current) + " -> " + ControlKey(below) + ": " + gap + "px");
                    if (gap > GapWarning && IsActionableGap(parent, current, below) && !HasInterveningControl(children, current, below, true))
                    {
                        gaps++;
                        sb.AppendLine(indent + "LAYOUT GAP vertical " + gap + "px in " + ControlKey(parent));
                    }
                }

                Control right = children
                    .Where(c => c != current && c.Left >= current.Right && VerticalOverlap(c, current) > 0)
                    .OrderBy(c => c.Left)
                    .FirstOrDefault();
                if (right != null)
                {
                    int gap = right.Left - current.Right;
                    sb.AppendLine(indent + "Horizontal gap " + ControlKey(current) + " -> " + ControlKey(right) + ": " + gap + "px");
                    if (gap > GapWarning && IsActionableGap(parent, current, right) && !HasInterveningControl(children, current, right, false))
                    {
                        gaps++;
                        sb.AppendLine(indent + "LAYOUT GAP horizontal " + gap + "px in " + ControlKey(parent));
                    }
                }
            }
        }

        private static string Describe(Control control)
        {
            return ControlKey(control) +
                " Loc=(" + control.Left + "," + control.Top + ")" +
                " Size=(" + control.Width + "," + control.Height + ")" +
                " Right=" + control.Right +
                " Bottom=" + control.Bottom;
        }

        private static string ControlKey(Control control)
        {
            string name = string.IsNullOrWhiteSpace(control.Name) ? "(unnamed)" : control.Name;
            return name + " [" + control.GetType().Name + "]";
        }

        private static string ResolveFormName(Control root)
        {
            Form form = root as Form ?? root.FindForm();
            return form == null ? root.GetType().Name : form.Name + " / " + form.GetType().Name;
        }

        private static bool IsLayoutCandidate(Control control)
        {
            if (control == null || control.Dock != DockStyle.None || IsChrome(control) || IsAuditUtilityControl(control))
                return false;

            return control is Panel || control is GroupBox || control is DataGridView || control is TabControl || control is ListView;
        }

        private static bool IsActionableGap(Control parent, Control first, Control second)
        {
            if (parent is DashboardDeptCard)
                return false;
            if (parent is TableLayoutPanel || parent is FlowLayoutPanel)
                return false;
            if (!HasUsableBounds(first) || !HasUsableBounds(second))
                return false;
            if (parent is Panel && IsMixedContentPanel(parent, first, second))
                return false;

            if (parent is Form || parent is UserControl || parent is TabPage || parent is SplitterPanel)
                return true;

            return IsMajorSurface(first) && IsMajorSurface(second);
        }

        private static bool IsMixedContentPanel(Control parent, Control first, Control second)
        {
            foreach (Control child in parent.Controls)
            {
                if (child == first || child == second || IsAuditUtilityControl(child) || !child.Visible)
                    continue;
                if (child is Label || child is Button || child is TextBoxBase || child is ComboBox || child is DateTimePicker || child is NumericUpDown || child is CheckBox)
                    return true;
            }

            return false;
        }

        private static bool HasUsableBounds(Control control)
        {
            if (control == null || control.Width <= 0 || control.Height <= 0)
                return false;
            if (control.Right <= 0 || control.Bottom <= 0)
                return false;
            return true;
        }

        private static bool HasInterveningControl(List<Control> siblings, Control first, Control second, bool vertical)
        {
            foreach (Control candidate in siblings)
            {
                if (candidate == first || candidate == second || IsAuditUtilityControl(candidate) || !HasUsableBounds(candidate))
                    continue;

                if (vertical)
                {
                    bool between = candidate.Top >= first.Bottom && candidate.Bottom <= second.Top;
                    bool overlapsBand = HorizontalOverlap(candidate, first) > 0 || HorizontalOverlap(candidate, second) > 0;
                    if (between && overlapsBand)
                        return true;
                }
                else
                {
                    bool between = candidate.Left >= first.Right && candidate.Right <= second.Left;
                    bool overlapsBand = VerticalOverlap(candidate, first) > 0 || VerticalOverlap(candidate, second) > 0;
                    if (between && overlapsBand)
                        return true;
                }
            }

            return false;
        }

        private static bool IsMajorSurface(Control control)
        {
            if (control == null || IsChrome(control) || IsAuditUtilityControl(control))
                return false;
            if ((control is Panel) && (control.Width <= 2 || control.Height <= 2))
                return false;

            return control is Panel ||
                   control is GroupBox ||
                   control is DataGridView ||
                   control is TabControl ||
                   control is TableLayoutPanel ||
                   control is FlowLayoutPanel ||
                   control is ListView;
        }

        private static bool IsAuditUtilityControl(Control control)
        {
            string name = ((control == null ? string.Empty : control.Name) ?? string.Empty).ToUpperInvariant();
            return name == "__SERVOERPRESETLAYOUTBUTTON" ||
                   name == "__SERVOERPCARDRESIZEGRIP" ||
                   name == "__SERVOERPCARDHEIGHTGRIP";
        }

        private static bool IsCardLike(Control control)
        {
            if (control == null || control is Form || control is UserControl || control is TabPage || control is SplitterPanel)
                return false;
            if (control is FlowLayoutPanel || control is TableLayoutPanel)
                return false;
            if (!(control is Panel) && !(control is GroupBox))
                return false;
            if (IsChrome(control))
                return false;
            if (IsEditorFieldContainer(control))
                return false;

            string name = (control.Name ?? string.Empty).ToUpperInvariant();
            bool namedCard = ContainsAny(name, "CARD", "PANEL", "GROUP", "SECTION", "SUMMARY", "DETAIL", "KPI", "WIDGET", "LIST", "GRID");
            bool whiteSurface = control.BackColor == Color.White || control.BackColor == DS.White || control.BackColor == DS.BgCard || control.BackColor == SystemColors.Window;
            return namedCard || (whiteSurface && control.Controls.Count > 0);
        }

        private static bool IsEditorFieldContainer(Control control)
        {
            if (control == null)
                return false;

            int inputCount = CountDescendants(control, IsEditableInput);
            if (inputCount == 0)
                return false;

            string name = ((control.Name ?? string.Empty) + " " + (control.Tag ?? string.Empty)).ToUpperInvariant();
            bool namedAsField = ContainsAny(name, "FIELD", "EDITOR", "INPUT", "FORMROW", "FORM_ROW");
            bool compact = control.Height <= 190;
            bool mostlyInputs = inputCount >= CountDescendants(control, c => c is Button || c is DataGridView || c is ListView);
            bool hasFieldLabel = control.Controls.OfType<Label>().Any(label => !string.IsNullOrWhiteSpace(label.Text) && label.Text.Length <= 80);
            return namedAsField || compact || (hasFieldLabel && mostlyInputs);
        }

        private static bool IsEditableInput(Control control)
        {
            return control is TextBoxBase ||
                   control is ComboBox ||
                   control is DateTimePicker ||
                   control is NumericUpDown ||
                   control is CheckBox ||
                   control is RadioButton;
        }

        private static int CountDescendants(Control control, Func<Control, bool> predicate)
        {
            if (control == null || predicate == null)
                return 0;

            int count = 0;
            foreach (Control child in control.Controls)
            {
                if (predicate(child))
                    count++;
                count += CountDescendants(child, predicate);
            }

            return count;
        }

        private static bool IsChrome(Control control)
        {
            string name = ((control == null ? string.Empty : control.Name) ?? string.Empty).ToUpperInvariant();
            return ContainsAny(name, "SIDEBAR", "NAV", "TOOLBAR", "HEADER", "FOOTER", "STRIP", "MENU", "BANNER", "TITLE");
        }

        private static bool IsHeaderOrActionChrome(Control control)
        {
            for (Control current = control; current != null; current = current.Parent)
            {
                string metadata = ((current.Name ?? string.Empty) + " " + (current.Tag ?? string.Empty)).ToUpperInvariant();
                if (ContainsAny(metadata, "HEADER", "TOOLBAR", "TOPBAR", "ACTIONRAIL", "BUTTONRAIL", "HEADERACTIONS", "PAGEACTIONS"))
                    return true;
            }

            return false;
        }

        private static bool HasVisibleSiblingBelow(Control control)
        {
            Control parent = control.Parent;
            if (parent == null)
                return false;

            return parent.Controls.Cast<Control>().Any(sibling =>
                sibling != control &&
                sibling.Visible &&
                sibling.Top >= control.Bottom &&
                HorizontalOverlap(sibling, control) > 0);
        }

        private static bool WouldOverlapSibling(Control parent, Control moved, Rectangle proposed)
        {
            return parent.Controls.Cast<Control>().Any(sibling =>
                sibling != moved &&
                sibling.Visible &&
                IsLayoutCandidate(sibling) &&
                proposed.IntersectsWith(sibling.Bounds));
        }

        private static bool HasControlInRow(TableLayoutPanel table, int row)
        {
            foreach (Control control in table.Controls)
            {
                if (table.GetRow(control) == row)
                    return true;
            }
            return false;
        }

        private static bool HasControlInColumn(TableLayoutPanel table, int column)
        {
            foreach (Control control in table.Controls)
            {
                if (table.GetColumn(control) == column)
                    return true;
            }
            return false;
        }

        private static int HorizontalOverlap(Control a, Control b)
        {
            return Math.Max(0, Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left));
        }

        private static int VerticalOverlap(Control a, Control b)
        {
            return Math.Max(0, Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top));
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            if (string.IsNullOrEmpty(value))
                return false;
            foreach (string token in tokens)
            {
                if (value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static void PrepareLogs()
        {
            if (_logsPrepared)
                return;

            Directory.CreateDirectory(GetLogDir());
            File.WriteAllText(GetBeforePath(), "ServoERP Layout Audit BEFORE" + Environment.NewLine);
            File.WriteAllText(GetAfterPath(), "ServoERP Layout Audit AFTER" + Environment.NewLine);
            _logsPrepared = true;
        }

        private static void AppendSummary()
        {
            string summary = Summary();
            File.AppendAllText(GetBeforePath(), Environment.NewLine + summary + Environment.NewLine);
            File.AppendAllText(GetAfterPath(), Environment.NewLine + summary + Environment.NewLine);
        }

        private static string GetLogDir()
        {
            return Path.Combine(@"C:\HVAC_PRO_MSE", "LOGS");
        }

        private static string GetBeforePath()
        {
            return Path.Combine(GetLogDir(), "LayoutAudit.txt");
        }

        private static string GetAfterPath()
        {
            return Path.Combine(GetLogDir(), "LayoutAuditAfter.txt");
        }
    }
}

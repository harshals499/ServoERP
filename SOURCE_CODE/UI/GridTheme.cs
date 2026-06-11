using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    public enum GridColumnPriority
    {
        Required,
        Secondary,
        Optional
    }

    public sealed class GridColumnPolicy
    {
        public GridColumnPolicy(string columnName, int minimumWidth, GridColumnPriority priority)
        {
            ColumnName = columnName;
            MinimumWidth = minimumWidth;
            Priority = priority;
        }

        public string ColumnName { get; private set; }
        public int MinimumWidth { get; private set; }
        public GridColumnPriority Priority { get; private set; }
    }

    public static class GridTheme
    {
        public static readonly Color HeaderBack = Color.FromArgb(248, 250, 252);
        public static readonly Color HeaderFore = Color.FromArgb(15, 23, 42);
        public static readonly Color RowAlt = Color.FromArgb(248, 250, 252);
        public static readonly Color RowNormal = Color.White;
        public static readonly Color RowSelected = Color.FromArgb(37, 99, 235);
        public static readonly Color RowSelectedFore = Color.White;
        public static readonly Color GridLine = Color.FromArgb(209, 213, 219);
        public static readonly Color BorderColor = Color.FromArgb(209, 213, 219);

        private static readonly HashSet<DataGridView> BoundGrids = new HashSet<DataGridView>();
        private static readonly HashSet<DataGridView> StyledGrids = new HashSet<DataGridView>();
        private static readonly Dictionary<DataGridView, GridColumnPolicy[]> ColumnPolicies = new Dictionary<DataGridView, GridColumnPolicy[]>();

        public static void Apply(DataGridView dgv, bool fillWidth = true, bool alternateRows = true, int rowHeight = 34)
        {
            if (dgv == null)
                return;

            if (StyledGrids.Contains(dgv))
                return;

            StyledGrids.Add(dgv);
            UiPerformanceService.ApplyGridPerformance(dgv);
            dgv.Disposed += (s, e) =>
            {
                StyledGrids.Remove(dgv);
                BoundGrids.Remove(dgv);
                ColumnPolicies.Remove(dgv);
            };

            dgv.Dock = DockStyle.Fill;
            dgv.AutoSizeColumnsMode = fillWidth
                ? DataGridViewAutoSizeColumnsMode.Fill
                : dgv.AutoSizeColumnsMode;
            dgv.ScrollBars = ScrollBars.Both;
            dgv.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            dgv.RowTemplate.Height = rowHeight;
            SetColumnHeadersHeightSafely(dgv, 38);
            dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            dgv.AllowUserToResizeRows = false;
            dgv.MultiSelect = false;
            if (dgv.SelectionMode != DataGridViewSelectionMode.CellSelect)
                dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.BorderStyle = BorderStyle.None;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgv.GridColor = GridLine;
            dgv.BackgroundColor = RowNormal;
            dgv.EnableHeadersVisualStyles = false;
            dgv.RowHeadersVisible = false;

            dgv.ColumnHeadersDefaultCellStyle.BackColor = HeaderBack;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = HeaderFore;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dgv.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 0, 0);
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = HeaderBack;
            dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = HeaderFore;

            dgv.DefaultCellStyle.Font = new Font("Segoe UI", 9f);
            dgv.DefaultCellStyle.ForeColor = DS.Slate800;
            dgv.DefaultCellStyle.BackColor = RowNormal;
            dgv.DefaultCellStyle.SelectionBackColor = RowSelected;
            dgv.DefaultCellStyle.SelectionForeColor = RowSelectedFore;
            dgv.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
            dgv.DefaultCellStyle.WrapMode = DataGridViewTriState.False;

            if (alternateRows)
            {
                dgv.AlternatingRowsDefaultCellStyle.BackColor = RowAlt;
                dgv.AlternatingRowsDefaultCellStyle.SelectionBackColor = RowSelected;
                dgv.AlternatingRowsDefaultCellStyle.SelectionForeColor = RowSelectedFore;
            }

            FormatColumns(dgv);
            FillColumns(dgv);
            GlobalStatusEditor.Attach(dgv);

            if (!BoundGrids.Contains(dgv))
            {
                BoundGrids.Add(dgv);
                dgv.DataBindingComplete += (s, e) =>
                {
                    if (ColumnPolicies.ContainsKey(dgv))
                        ApplyColumnPolicyCore(dgv, ColumnPolicies[dgv]);
                    else if (fillWidth)
                        FillColumns(dgv);
                    FormatColumns(dgv);
                    dgv.Invalidate();
                };
                dgv.ColumnAdded += (s, e) =>
                {
                    if (ColumnPolicies.ContainsKey(dgv))
                        ApplyColumnPolicyCore(dgv, ColumnPolicies[dgv]);
                    else if (fillWidth)
                        FillColumns(dgv);
                    FormatColumns(dgv);
                };
                ShowEmptyState(dgv);
            }
        }

        public static void FillColumns(DataGridView dgv)
        {
            if (dgv == null || dgv.Columns.Count == 0)
                return;

            foreach (DataGridViewColumn col in dgv.Columns)
            {
                if (!col.Visible)
                    continue;

                col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                if (col.MinimumWidth < 70)
                    col.MinimumWidth = 70;
            }

            using (Graphics g = dgv.CreateGraphics())
            {
                Font hFont = dgv.ColumnHeadersDefaultCellStyle.Font ?? dgv.Font;
                foreach (DataGridViewColumn col in dgv.Columns)
                {
                    if (!col.Visible)
                        continue;

                    int headerW = (int)g.MeasureString(col.HeaderText ?? col.Name, hFont).Width + 30;
                    col.Width = Math.Max(col.MinimumWidth, Math.Max(col.Width, headerW));
                }
            }

            foreach (DataGridViewColumn col in dgv.Columns)
            {
                if (col.Visible)
                    col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }
        }

        public static void ApplyColumnPolicy(DataGridView dgv, IEnumerable<GridColumnPolicy> policies)
        {
            if (dgv == null)
                return;

            GridColumnPolicy[] policySnapshot = (policies ?? Enumerable.Empty<GridColumnPolicy>())
                .Where(p => p != null)
                .ToArray();
            ColumnPolicies[dgv] = policySnapshot;
            dgv.Disposed -= DataGridViewPolicyDisposed;
            dgv.Disposed += DataGridViewPolicyDisposed;

            ApplyColumnPolicyCore(dgv, policySnapshot);
        }

        private static void ApplyColumnPolicyCore(DataGridView dgv, IEnumerable<GridColumnPolicy> policies)
        {
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgv.ScrollBars = ScrollBars.Both;
            SetColumnHeadersHeightSafely(dgv, Math.Max(dgv.ColumnHeadersHeight, 38));

            var byName = (policies ?? Enumerable.Empty<GridColumnPolicy>())
                .GroupBy(p => Normalize(p.ColumnName))
                .ToDictionary(g => g.Key, g => g.Last());

            using (Graphics g = dgv.CreateGraphics())
            {
                Font headerFont = dgv.ColumnHeadersDefaultCellStyle.Font ?? dgv.Font;
                foreach (DataGridViewColumn column in dgv.Columns)
                {
                    string key = Normalize(column.Name);
                    GridColumnPolicy policy;
                    int measured = (int)g.MeasureString(column.HeaderText ?? column.Name, headerFont).Width + 30;
                    int min = Math.Max(70, measured);
                    if (byName.TryGetValue(key, out policy))
                        min = Math.Max(min, policy.MinimumWidth);

                    column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                    column.MinimumWidth = min;
                    column.Width = Math.Max(column.Width, min);
                }
            }

            dgv.Resize -= DataGridViewPolicyResize;
            dgv.Resize += DataGridViewPolicyResize;
        }

        private static void DataGridViewPolicyDisposed(object sender, EventArgs e)
        {
            DataGridView dgv = sender as DataGridView;
            if (dgv != null)
                ColumnPolicies.Remove(dgv);
        }

        private static void DataGridViewPolicyResize(object sender, EventArgs e)
        {
            DataGridView dgv = sender as DataGridView;
            if (dgv == null)
                return;

            int visibleWidth = dgv.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).Sum(c => c.Width);
            dgv.HorizontalScrollingOffset = 0;
            dgv.ScrollBars = visibleWidth > dgv.ClientSize.Width ? ScrollBars.Both : ScrollBars.Vertical;
        }

        private static void SetColumnHeadersHeightSafely(DataGridView dgv, int height)
        {
            if (dgv == null || dgv.IsDisposed || dgv.ColumnHeadersHeight == height)
                return;

            try
            {
                dgv.ColumnHeadersHeight = height;
            }
            catch (NullReferenceException ex)
            {
                AppRuntime.LogException("GridTheme.ColumnHeadersHeight", ex);
            }
            catch (InvalidOperationException ex)
            {
                AppRuntime.LogException("GridTheme.ColumnHeadersHeight", ex);
            }
            catch (ArgumentException ex)
            {
                AppRuntime.LogException("GridTheme.ColumnHeadersHeight", ex);
            }
        }

        public static void FormatColumns(DataGridView dgv)
        {
            if (dgv == null)
                return;

            string[] amountKeywords =
            {
                "amount", "value", "total", "cost", "price", "salary", "net", "gross",
                "tax", "cgst", "sgst", "igst", "epf", "esi", "advance", "balance",
                "revenue", "margin", "payment", "rate", "mrr"
            };
            string[] dateKeywords = { "date", "dob", "joindate", "created", "modified", "expiry", "start", "end", "due" };
            string[] centerKeywords = { "status", "type", "mode", "priority", "action", "active", "recovered" };

            foreach (DataGridViewColumn col in dgv.Columns)
            {
                string name = Normalize(col.Name);
                string header = Normalize(col.HeaderText);

                if (amountKeywords.Any(k => name.Contains(k) || header.Contains(k)))
                {
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    if (!header.Contains("margin") && !name.Contains("margin") && !header.Contains("gst%") && !name.Contains("gstpct"))
                        col.DefaultCellStyle.Format = "₹#,##0.00";
                    col.DefaultCellStyle.Padding = new Padding(0, 0, 10, 0);
                    col.DefaultCellStyle.Font = new Font("Segoe UI", 9f);
                }
                else if (dateKeywords.Any(k => name.Contains(k) || header.Contains(k)))
                {
                    col.DefaultCellStyle.Format = "dd/MM/yyyy";
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                }
                else if (centerKeywords.Any(k => name.Contains(k) || header.Contains(k)))
                {
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                }
                else
                {
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
                    col.DefaultCellStyle.Padding = new Padding(8, 0, 0, 0);
                }

                col.ToolTipText = col.HeaderText;

                if (col is DataGridViewComboBoxColumn combo)
                {
                    combo.FlatStyle = FlatStyle.Flat;
                    combo.DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox;
                    combo.DisplayStyleForCurrentCellOnly = true;
                }
                else if (col is DataGridViewButtonColumn button)
                {
                    button.FlatStyle = FlatStyle.Flat;
                    button.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                }
            }
        }

        public static void ShowEmptyState(DataGridView dgv, string message = "No records found.", string hint = "Use search, filters, or the New button to continue.")
        {
            if (dgv == null)
                return;

            dgv.Paint += (s, e) =>
            {
                if (dgv.Rows.Count != 0)
                    return;

                using (Font titleFont = new Font("Segoe UI", 10f, FontStyle.Bold))
                using (Font hintFont = new Font("Segoe UI", 8.75f, FontStyle.Regular))
                using (SolidBrush titleBrush = new SolidBrush(DS.Slate700))
                using (SolidBrush hintBrush = new SolidBrush(DS.Slate500))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    Rectangle rect = dgv.ClientRectangle;
                    rect.Y += dgv.ColumnHeadersHeight;
                    rect.Height = Math.Max(0, rect.Height - dgv.ColumnHeadersHeight);
                    Rectangle titleRect = new Rectangle(rect.X, rect.Y - 10, rect.Width, rect.Height);
                    Rectangle hintRect = new Rectangle(rect.X + 24, rect.Y + 18, Math.Max(0, rect.Width - 48), rect.Height);
                    e.Graphics.DrawString(message, titleFont, titleBrush, titleRect, sf);
                    if (!string.IsNullOrWhiteSpace(hint))
                        e.Graphics.DrawString(hint, hintFont, hintBrush, hintRect, sf);
                }
            };
        }

        public static void StyleSectionLabel(Label label)
        {
            if (label == null)
                return;

            label.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            label.ForeColor = DS.Primary700;
            label.AutoSize = true;
            label.Margin = new Padding(label.Margin.Left, label.Margin.Top, label.Margin.Right, 8);
            if (label.Padding.Bottom < 8)
                label.Padding = new Padding(label.Padding.Left, label.Padding.Top, label.Padding.Right, 8);
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).ToLowerInvariant().Replace(" ", string.Empty).Replace("_", string.Empty);
        }
    }
}

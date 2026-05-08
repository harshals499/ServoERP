using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    public static class GridTheme
    {
        public static readonly Color HeaderBack = Color.FromArgb(21, 101, 192);
        public static readonly Color HeaderFore = Color.White;
        public static readonly Color RowAlt = Color.FromArgb(240, 247, 255);
        public static readonly Color RowNormal = Color.White;
        public static readonly Color RowSelected = Color.FromArgb(187, 222, 251);
        public static readonly Color RowSelectedFore = Color.FromArgb(13, 71, 161);
        public static readonly Color GridLine = Color.FromArgb(220, 230, 241);
        public static readonly Color BorderColor = Color.FromArgb(189, 213, 234);

        private static readonly HashSet<DataGridView> BoundGrids = new HashSet<DataGridView>();
        private static readonly HashSet<DataGridView> StyledGrids = new HashSet<DataGridView>();

        public static void Apply(DataGridView dgv, bool fillWidth = true, bool alternateRows = true, int rowHeight = 30)
        {
            if (dgv == null)
                return;

            if (StyledGrids.Contains(dgv))
                return;

            StyledGrids.Add(dgv);
            dgv.Disposed += (s, e) =>
            {
                StyledGrids.Remove(dgv);
                BoundGrids.Remove(dgv);
            };

            dgv.Dock = DockStyle.Fill;
            dgv.AutoSizeColumnsMode = fillWidth
                ? DataGridViewAutoSizeColumnsMode.Fill
                : dgv.AutoSizeColumnsMode;
            dgv.ScrollBars = ScrollBars.Both;
            dgv.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            dgv.RowTemplate.Height = rowHeight;
            dgv.ColumnHeadersHeight = 36;
            dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            dgv.AllowUserToResizeRows = false;
            dgv.MultiSelect = false;
            if (dgv.SelectionMode != DataGridViewSelectionMode.CellSelect)
                dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.BorderStyle = BorderStyle.None;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgv.GridColor = GridLine;
            dgv.BackgroundColor = Color.White;
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
            dgv.DefaultCellStyle.ForeColor = Color.FromArgb(30, 30, 50);
            dgv.DefaultCellStyle.BackColor = RowNormal;
            dgv.DefaultCellStyle.SelectionBackColor = RowSelected;
            dgv.DefaultCellStyle.SelectionForeColor = RowSelectedFore;
            dgv.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
            dgv.DefaultCellStyle.WrapMode = DataGridViewTriState.False;

            if (alternateRows)
            {
                dgv.AlternatingRowsDefaultCellStyle.BackColor = RowAlt;
                dgv.AlternatingRowsDefaultCellStyle.SelectionBackColor = RowSelected;
                dgv.AlternatingRowsDefaultCellStyle.SelectionForeColor = RowSelectedFore;
            }

            FormatColumns(dgv);
            FillColumns(dgv);

            if (!BoundGrids.Contains(dgv))
            {
                BoundGrids.Add(dgv);
                dgv.DataBindingComplete += (s, e) =>
                {
                    if (fillWidth)
                        FillColumns(dgv);
                    FormatColumns(dgv);
                    dgv.Invalidate();
                };
                dgv.ColumnAdded += (s, e) =>
                {
                    if (fillWidth)
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

        public static void ShowEmptyState(DataGridView dgv, string message = "No records found.")
        {
            if (dgv == null)
                return;

            dgv.Paint += (s, e) =>
            {
                if (dgv.Rows.Count != 0)
                    return;

                using (Font font = new Font("Segoe UI", 10f, FontStyle.Regular))
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(150, 150, 170)))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    Rectangle rect = dgv.ClientRectangle;
                    rect.Y += dgv.ColumnHeadersHeight;
                    rect.Height = Math.Max(0, rect.Height - dgv.ColumnHeadersHeight);
                    e.Graphics.DrawString(message, font, brush, rect, sf);
                }
            };
        }

        public static void StyleSectionLabel(Label label)
        {
            if (label == null)
                return;

            label.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            label.ForeColor = HeaderBack;
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

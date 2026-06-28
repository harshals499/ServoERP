using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class InvoiceOverviewChart : Control
    {
        public InvoiceDashboardSnapshot Snapshot { get; set; }

        public InvoiceOverviewChart()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            List<InvoiceOverviewPoint> rows = (Snapshot?.Overview ?? Enumerable.Empty<InvoiceOverviewPoint>()).ToList();
            Rectangle plot = new Rectangle(42, 18, Math.Max(80, Width - 64), Math.Max(60, Height - 58));
            using (Pen grid = new Pen(Color.FromArgb(229, 235, 245)))
            {
                for (int i = 0; i <= 4; i++)
                {
                    int y = plot.Top + (plot.Height * i / 4);
                    e.Graphics.DrawLine(grid, plot.Left, y, plot.Right, y);
                }
            }

            decimal max = Math.Max(1m, rows.Select(r => r.TotalAmount).DefaultIfEmpty(1m).Max());
            DrawSeries(e.Graphics, plot, rows.Select(r => r.TotalAmount).ToArray(), max, DS.Primary600);
            using (Brush text = new SolidBrush(DS.Slate500))
            using (Font labelFont = new Font("Segoe UI", 7f))
            {
                DrawPeriodLabels(e.Graphics, plot, rows, labelFont, text);
                e.Graphics.DrawString("Invoice total", labelFont, text, plot.Left, plot.Bottom + 22);
            }
        }

        private static void DrawSeries(Graphics graphics, Rectangle plot, decimal[] values, decimal max, Color color)
        {
            if (values.Length == 0)
                values = new[] { 0m, 0m };
            PointF[] points = new PointF[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                float x = plot.Left + (values.Length == 1 ? plot.Width / 2f : plot.Width * i / (float)(values.Length - 1));
                float y = plot.Bottom - (float)(Math.Max(0m, values[i]) / max) * plot.Height;
                points[i] = new PointF(x, y);
            }
            using (Pen pen = new Pen(color, 2f))
                if (points.Length > 1) graphics.DrawLines(pen, points);
            using (SolidBrush brush = new SolidBrush(color))
            {
                foreach (PointF point in points)
                    graphics.FillEllipse(brush, point.X - 3f, point.Y - 3f, 6f, 6f);
            }
        }

        private static void DrawPeriodLabels(Graphics graphics, Rectangle plot, List<InvoiceOverviewPoint> rows, Font font, Brush brush)
        {
            if (rows == null || rows.Count == 0)
                return;

            int last = rows.Count - 1;
            var labelIndexes = new SortedSet<int> { 0, last };
            if (rows.Count > 2)
                labelIndexes.Add(rows.Count / 2);
            if (rows.Count > 6)
            {
                labelIndexes.Add(rows.Count / 4);
                labelIndexes.Add(rows.Count * 3 / 4);
            }

            foreach (int index in labelIndexes)
            {
                string label = rows[index]?.Period ?? string.Empty;
                if (string.IsNullOrWhiteSpace(label))
                    continue;

                float x = plot.Left + (rows.Count == 1 ? plot.Width / 2f : plot.Width * index / (float)last);
                SizeF size = graphics.MeasureString(label, font);
                graphics.DrawString(label, font, brush, x - size.Width / 2f, plot.Bottom + 4);
            }
        }
    }

    internal sealed class InvoiceStatusBucket
    {
        public string Label { get; set; }
        public int Count { get; set; }
        public Color Color { get; set; }
    }

    public sealed class InvoiceStatusDonut : Control
    {
        public InvoiceDashboardSnapshot Snapshot { get; set; }

        public InvoiceStatusDonut()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            List<InvoiceStatusBucket> buckets = BuildBuckets(Snapshot).ToList();
            decimal total = Math.Max(1m, buckets.Sum(b => b.Count));
            Rectangle donut = new Rectangle(24, 16, Math.Min(110, Height - 34), Math.Min(110, Height - 34));
            float start = -90f;
            if (buckets.Sum(b => b.Count) == 0)
                DrawSlice(e.Graphics, donut, ref start, 1m, DS.Slate200);
            else
            {
                foreach (InvoiceStatusBucket bucket in buckets)
                    DrawSlice(e.Graphics, donut, ref start, bucket.Count / total, bucket.Color);
            }
            using (SolidBrush white = new SolidBrush(Color.White))
                e.Graphics.FillEllipse(white, Rectangle.Inflate(donut, -28, -28));
            using (Brush text = new SolidBrush(DS.Slate900))
            using (Font totalFont = new Font("Segoe UI", 8f, FontStyle.Bold))
                e.Graphics.DrawString("Total " + buckets.Sum(b => b.Count), totalFont, text, donut.Right + 22, donut.Top + 8);
            using (Brush muted = new SolidBrush(DS.Slate600))
            using (Font legendFont = new Font("Segoe UI", 7.5f))
            {
                int y = donut.Top + 36;
                foreach (InvoiceStatusBucket bucket in buckets)
                {
                    using (SolidBrush swatch = new SolidBrush(bucket.Color))
                        e.Graphics.FillRectangle(swatch, donut.Right + 22, y + 5, 8, 8);
                    e.Graphics.DrawString(bucket.Label + "  " + bucket.Count, legendFont, muted, donut.Right + 36, y);
                    y += 22;
                }
            }
        }

        internal static IEnumerable<InvoiceStatusBucket> BuildBuckets(InvoiceDashboardSnapshot snapshot)
        {
            List<InvoiceStatusSlice> statuses = snapshot?.Statuses ?? new List<InvoiceStatusSlice>();
            int paid = CountStatus(statuses, "Paid");
            int overdue = CountStatus(statuses, "Overdue");
            int pending = statuses
                .Where(s => s != null &&
                            !string.Equals(s.Status, "Paid", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(s.Status, "Overdue", StringComparison.OrdinalIgnoreCase))
                .Sum(s => s.Count);

            return new[]
            {
                new InvoiceStatusBucket { Label = "Paid", Count = paid, Color = DS.Green600 },
                new InvoiceStatusBucket { Label = "Pending", Count = pending, Color = DS.Amber500 },
                new InvoiceStatusBucket { Label = "Overdue", Count = overdue, Color = DS.Red600 }
            };
        }

        private static int CountStatus(IEnumerable<InvoiceStatusSlice> statuses, string status)
        {
            return statuses
                .Where(s => s != null && string.Equals(s.Status, status, StringComparison.OrdinalIgnoreCase))
                .Sum(s => s.Count);
        }

        private static void DrawSlice(Graphics graphics, Rectangle rect, ref float start, decimal share, Color color)
        {
            float sweep = share <= 0m ? 0f : Math.Max(1f, (float)share * 360f);
            if (sweep <= 0f)
                return;
            using (Pen pen = new Pen(color, 18f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                graphics.DrawArc(pen, rect, start, sweep);
            start += sweep;
        }
    }
}

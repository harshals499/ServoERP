using System;
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
            Rectangle plot = new Rectangle(36, 18, Math.Max(80, Width - 52), Math.Max(60, Height - 42));
            using (Pen grid = new Pen(Color.FromArgb(229, 235, 245)))
            {
                for (int i = 0; i <= 4; i++)
                {
                    int y = plot.Top + (plot.Height * i / 4);
                    e.Graphics.DrawLine(grid, plot.Left, y, plot.Right, y);
                }
            }

            var rows = Snapshot?.Overview ?? Enumerable.Empty<InvoiceOverviewPoint>();
            decimal max = Math.Max(1m, rows.Select(r => r.TotalAmount).DefaultIfEmpty(1m).Max());
            DrawSeries(e.Graphics, plot, rows.Select(r => r.TotalAmount).ToArray(), max, DS.Primary600);
            using (Brush text = new SolidBrush(DS.Slate500))
                e.Graphics.DrawString("Invoices", new Font("Segoe UI", 7f), text, plot.Left, plot.Bottom + 4);
        }

        private static void DrawSeries(Graphics graphics, Rectangle plot, decimal[] values, decimal max, Color color)
        {
            if (values.Length == 0)
                values = new[] { 0m, 0m };
            PointF[] points = new PointF[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                float x = plot.Left + (values.Length == 1 ? 0 : plot.Width * i / (float)(values.Length - 1));
                float y = plot.Bottom - (float)(Math.Max(0m, values[i]) / max) * plot.Height;
                points[i] = new PointF(x, y);
            }
            using (Pen pen = new Pen(color, 2f))
                if (points.Length > 1) graphics.DrawLines(pen, points);
        }
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
            decimal paid = Snapshot?.Kpis?.PaidAmount?.Value ?? 0m;
            decimal pending = Snapshot?.Kpis?.PendingAmount?.Value ?? 0m;
            decimal overdue = Snapshot?.Kpis?.OverdueAmount?.Value ?? 0m;
            decimal total = Math.Max(1m, paid + pending + overdue);
            Rectangle donut = new Rectangle(24, 16, Math.Min(110, Height - 34), Math.Min(110, Height - 34));
            float start = -90f;
            DrawSlice(e.Graphics, donut, ref start, paid / total, DS.Green600);
            DrawSlice(e.Graphics, donut, ref start, pending / total, DS.Amber500);
            DrawSlice(e.Graphics, donut, ref start, overdue / total, DS.Red600);
            using (SolidBrush white = new SolidBrush(Color.White))
                e.Graphics.FillEllipse(white, Rectangle.Inflate(donut, -28, -28));
            using (Brush text = new SolidBrush(DS.Slate900))
                e.Graphics.DrawString("Total", new Font("Segoe UI", 8f, FontStyle.Bold), text, donut.Right + 22, donut.Top + 8);
            using (Brush muted = new SolidBrush(DS.Slate600))
            {
                e.Graphics.DrawString("Paid", new Font("Segoe UI", 7.5f), muted, donut.Right + 22, donut.Top + 36);
                e.Graphics.DrawString("Pending", new Font("Segoe UI", 7.5f), muted, donut.Right + 22, donut.Top + 58);
                e.Graphics.DrawString("Overdue", new Font("Segoe UI", 7.5f), muted, donut.Right + 22, donut.Top + 80);
            }
        }

        private static void DrawSlice(Graphics graphics, Rectangle rect, ref float start, decimal share, Color color)
        {
            float sweep = Math.Max(1f, (float)share * 360f);
            using (Pen pen = new Pen(color, 18f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                graphics.DrawArc(pen, rect, start, sweep);
            start += sweep;
        }
    }
}

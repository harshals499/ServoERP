using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace HVAC_Pro_Desktop.UI
{
    internal sealed class ChartHoverContent
    {
        public string Key { get; set; }
        public string Title { get; set; }
        public string Value { get; set; }
        public string Calculation { get; set; }

        public override string ToString()
        {
            string title = string.IsNullOrWhiteSpace(Title) ? "Chart value" : Title.Trim();
            string value = string.IsNullOrWhiteSpace(Value) ? "0" : Value.Trim();
            string calculation = string.IsNullOrWhiteSpace(Calculation) ? string.Empty : "\nCalculation: " + Calculation.Trim();
            return title + "\nExact value: " + value + calculation;
        }
    }

    internal static class ChartHoverFormat
    {
        private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");

        public static string Currency(decimal value)
        {
            return string.Format(India, "{0:C2}", value).Replace("₹ ", "₹");
        }

        public static string Number(decimal value)
        {
            return value.ToString("N2", India);
        }

        public static string Count(decimal value)
        {
            return value.ToString("N0", India);
        }

        public static string Percent(decimal value)
        {
            return value.ToString("0.##", India) + "%";
        }
    }

    internal static class ChartHoverService
    {
        private sealed class Binding
        {
            public ToolTip ToolTip { get; } = CreateToolTip();
            public string ActiveKey { get; set; }
        }

        private sealed class ChartBinding
        {
            public Binding Hover { get; } = new Binding();
            public Func<Series, DataPoint, ChartHoverContent> Formatter { get; set; }
        }

        private static readonly ConditionalWeakTable<Chart, ChartBinding> ChartBindings = new ConditionalWeakTable<Chart, ChartBinding>();

        public static void Enable(Chart chart, Func<Series, DataPoint, ChartHoverContent> formatter)
        {
            if (chart == null)
                return;

            ChartBinding existing;
            if (ChartBindings.TryGetValue(chart, out existing))
            {
                existing.Formatter = formatter;
                return;
            }

            var binding = new ChartBinding { Formatter = formatter };
            ChartBindings.Add(chart, binding);
            chart.MouseMove += (sender, args) =>
            {
                HitTestResult hit = chart.HitTest(args.X, args.Y);
                if (hit == null || hit.Series == null || hit.PointIndex < 0 || hit.PointIndex >= hit.Series.Points.Count)
                {
                    Hide(chart, binding.Hover);
                    return;
                }

                DataPoint point = hit.Series.Points[hit.PointIndex];
                ChartHoverContent content = binding.Formatter == null ? DefaultContent(hit.Series, point) : binding.Formatter(hit.Series, point);
                Show(chart, binding.Hover, content, args.Location);
            };
            chart.MouseLeave += (sender, args) => Hide(chart, binding.Hover);
            chart.Disposed += (sender, args) =>
            {
                binding.Hover.ToolTip.Dispose();
                ChartBindings.Remove(chart);
            };
        }

        private static ChartHoverContent DefaultContent(Series series, DataPoint point)
        {
            string label = string.IsNullOrWhiteSpace(point.AxisLabel) ? series.Name : point.AxisLabel;
            double raw = point.YValues == null || point.YValues.Length == 0 ? 0d : point.YValues[0];
            return new ChartHoverContent
            {
                Key = series.Name + ":" + label + ":" + raw.ToString(CultureInfo.InvariantCulture),
                Title = label + " · " + series.Name,
                Value = raw.ToString("N2", CultureInfo.GetCultureInfo("en-IN")),
                Calculation = "plotted value for " + label
            };
        }

        internal static ToolTip CreateToolTip()
        {
            return new ToolTip
            {
                AutoPopDelay = 15000,
                InitialDelay = 120,
                ReshowDelay = 60,
                ShowAlways = true,
                ToolTipIcon = ToolTipIcon.Info,
                ToolTipTitle = "ServoERP chart details"
            };
        }

        internal static void Show(Control owner, object state, ChartHoverContent content, Point location)
        {
            Binding binding = state as Binding;
            if (binding == null || content == null)
                return;
            string key = string.IsNullOrWhiteSpace(content.Key) ? content.ToString() : content.Key;
            owner.Cursor = Cursors.Hand;
            if (string.Equals(binding.ActiveKey, key, StringComparison.Ordinal))
                return;
            binding.ActiveKey = key;
            binding.ToolTip.Show(content.ToString(), owner, location.X + 14, location.Y + 18, 15000);
        }

        internal static object CreateBinding()
        {
            return new Binding();
        }

        internal static void Hide(Control owner, object state)
        {
            Binding binding = state as Binding;
            if (binding == null)
                return;
            binding.ActiveKey = null;
            binding.ToolTip.Hide(owner);
            owner.Cursor = Cursors.Default;
        }

        internal static void Release(Control owner, object state)
        {
            Binding binding = state as Binding;
            if (binding == null)
                return;
            binding.ToolTip.Hide(owner);
            binding.ToolTip.Dispose();
        }
    }

    public abstract class HoverChartControl : Panel
    {
        private sealed class HoverRegion
        {
            public Func<PointF, bool> Contains { get; set; }
            public ChartHoverContent Content { get; set; }
        }

        private readonly List<HoverRegion> _hoverRegions = new List<HoverRegion>();
        private readonly object _hoverBinding = ChartHoverService.CreateBinding();

        protected HoverChartControl()
        {
            DoubleBuffered = true;
        }

        protected internal void BeginHoverRegions()
        {
            _hoverRegions.Clear();
        }

        protected internal void AddHoverRectangle(RectangleF bounds, string key, string title, string value, string calculation)
        {
            RectangleF target = bounds;
            _hoverRegions.Add(new HoverRegion
            {
                Contains = point => target.Contains(point),
                Content = Content(key, title, value, calculation)
            });
        }

        protected internal void AddHoverPoint(PointF center, float radius, string key, string title, string value, string calculation)
        {
            float hitRadius = Math.Max(8f, radius);
            _hoverRegions.Add(new HoverRegion
            {
                Contains = point => Distance(point, center) <= hitRadius,
                Content = Content(key, title, value, calculation)
            });
        }

        protected internal void AddHoverDonutSlice(RectangleF bounds, float startAngle, float sweepAngle, float innerRatio, string key, string title, string value, string calculation)
        {
            RectangleF target = bounds;
            float start = NormalizeAngle(startAngle);
            float sweep = Math.Max(0f, Math.Min(360f, sweepAngle));
            float inner = Math.Max(0f, Math.Min(.95f, innerRatio));
            _hoverRegions.Add(new HoverRegion
            {
                Contains = point => IsInDonutSlice(point, target, start, sweep, inner),
                Content = Content(key, title, value, calculation)
            });
        }

        protected internal void AddHoverPolygon(PointF[] points, string key, string title, string value, string calculation)
        {
            PointF[] polygon = points == null ? Array.Empty<PointF>() : points.ToArray();
            _hoverRegions.Add(new HoverRegion
            {
                Contains = point => IsInPolygon(point, polygon),
                Content = Content(key, title, value, calculation)
            });
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            HoverRegion region = _hoverRegions.LastOrDefault(candidate => candidate.Contains(e.Location));
            if (region == null)
                ChartHoverService.Hide(this, _hoverBinding);
            else
                ChartHoverService.Show(this, _hoverBinding, region.Content, e.Location);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            ChartHoverService.Hide(this, _hoverBinding);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ChartHoverService.Release(this, _hoverBinding);
            base.Dispose(disposing);
        }

        internal string HoverTextAtForTest(Point point)
        {
            HoverRegion region = _hoverRegions.LastOrDefault(candidate => candidate.Contains(point));
            return region == null ? null : region.Content.ToString();
        }

        private static ChartHoverContent Content(string key, string title, string value, string calculation)
        {
            return new ChartHoverContent { Key = key, Title = title, Value = value, Calculation = calculation };
        }

        private static float Distance(PointF first, PointF second)
        {
            float dx = first.X - second.X;
            float dy = first.Y - second.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private static bool IsInDonutSlice(PointF point, RectangleF bounds, float start, float sweep, float innerRatio)
        {
            if (bounds.Width <= 0f || bounds.Height <= 0f || sweep <= 0f)
                return false;
            float cx = bounds.Left + bounds.Width / 2f;
            float cy = bounds.Top + bounds.Height / 2f;
            float rx = bounds.Width / 2f;
            float ry = bounds.Height / 2f;
            float nx = (point.X - cx) / rx;
            float ny = (point.Y - cy) / ry;
            float radius = (float)Math.Sqrt(nx * nx + ny * ny);
            if (radius > 1.12f || radius < innerRatio)
                return false;
            float angle = NormalizeAngle((float)(Math.Atan2(ny, nx) * 180d / Math.PI));
            float relative = NormalizeAngle(angle - start);
            return sweep >= 359.9f || relative <= sweep;
        }

        private static bool IsInPolygon(PointF point, PointF[] polygon)
        {
            if (polygon == null || polygon.Length < 3)
                return false;
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                PointF pi = polygon[i];
                PointF pj = polygon[j];
                bool intersects = ((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                                  point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y) + pi.X;
                if (intersects)
                    inside = !inside;
            }
            return inside;
        }

        private static float NormalizeAngle(float angle)
        {
            float normalized = angle % 360f;
            return normalized < 0f ? normalized + 360f : normalized;
        }
    }

    internal sealed class HoverChartPanel : HoverChartControl
    {
    }
}

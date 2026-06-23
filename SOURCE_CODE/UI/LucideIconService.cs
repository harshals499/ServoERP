using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Linq;

namespace HVAC_Pro_Desktop.UI
{
    public static class LucideIconService
    {
        private const string DefaultIconRoot = @"C:\HVAC_PRO_MSE\lucide-main\icons";
        private const string NestedIconRoot = @"C:\HVAC_PRO_MSE\lucide-main\lucide-main\icons";
        private static readonly ConcurrentDictionary<string, Bitmap> Cache = new ConcurrentDictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);

        public static Image GetIcon(string iconName, int size, Color color)
        {
            size = Math.Max(12, size);
            string normalized = NormalizeIconName(iconName);
            string key = normalized + "|" + size + "|" + color.ToArgb().ToString(CultureInfo.InvariantCulture);
            Bitmap bitmap = Cache.GetOrAdd(key, _ => LoadIconBitmap(normalized, size, color));
            return (Image)bitmap.Clone();
        }

        public static void ApplyToButton(Button button, string iconName, int size = 18)
        {
            if (button == null)
                return;

            button.Image = GetIcon(iconName, size, button.ForeColor);
            button.ImageAlign = ContentAlignment.MiddleLeft;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
            button.Padding = new Padding(8, 0, 8, 0);
        }

        public static bool IconExists(string iconName)
        {
            return File.Exists(ResolvePath(NormalizeIconName(iconName)));
        }

        private static Bitmap LoadIconBitmap(string iconName, int size, Color color)
        {
            string path = ResolvePath(iconName);
            if (!File.Exists(path))
                return DrawFallback(size, color);

            try
            {
                XDocument document = XDocument.Load(path);
                Bitmap bitmap = new Bitmap(size, size);
                bitmap.SetResolution(96f, 96f);
                using (Graphics graphics = Graphics.FromImage(bitmap))
                using (Pen pen = new Pen(color, Math.Max(1.4f, size / 12f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    graphics.Clear(Color.Transparent);
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    float scale = size / 24f;
                    graphics.ScaleTransform(scale, scale);
                    foreach (XElement element in document.Root.Descendants())
                        DrawElement(graphics, pen, element);
                }
                return bitmap;
            }
            catch
            {
                return DrawFallback(size, color);
            }
        }

        private static void DrawElement(Graphics graphics, Pen pen, XElement element)
        {
            string name = element.Name.LocalName;
            if (name == "line")
            {
                graphics.DrawLine(pen, F(element, "x1"), F(element, "y1"), F(element, "x2"), F(element, "y2"));
                return;
            }
            if (name == "circle")
            {
                float cx = F(element, "cx");
                float cy = F(element, "cy");
                float r = F(element, "r");
                graphics.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
                return;
            }
            if (name == "rect")
            {
                graphics.DrawRectangle(pen, F(element, "x"), F(element, "y"), F(element, "width"), F(element, "height"));
                return;
            }
            if (name == "polyline" || name == "polygon")
            {
                PointF[] points = ParsePoints((string)element.Attribute("points"));
                if (points.Length > 1)
                {
                    if (name == "polygon")
                        graphics.DrawPolygon(pen, points);
                    else
                        graphics.DrawLines(pen, points);
                }
                return;
            }
            if (name == "path")
            {
                using (GraphicsPath path = ParsePath((string)element.Attribute("d")))
                    graphics.DrawPath(pen, path);
            }
        }

        private static GraphicsPath ParsePath(string data)
        {
            GraphicsPath path = new GraphicsPath();
            if (string.IsNullOrWhiteSpace(data))
                return path;

            MatchCollection tokens = Regex.Matches(data, @"[MmLlHhVvCcSsQqTtZz]|-?\d*\.?\d+(?:e[-+]?\d+)?", RegexOptions.IgnoreCase);
            int i = 0;
            char command = 'M';
            PointF current = PointF.Empty;
            PointF start = PointF.Empty;
            while (i < tokens.Count)
            {
                string token = tokens[i].Value;
                if (char.IsLetter(token[0]))
                {
                    command = token[0];
                    i++;
                }

                bool relative = char.IsLower(command);
                char upper = char.ToUpperInvariant(command);
                if (upper == 'Z')
                {
                    path.CloseFigure();
                    current = start;
                    continue;
                }
                if (upper == 'M' && HasNumbers(tokens, i, 2))
                {
                    current = Point(tokens, ref i, relative, current);
                    start = current;
                    path.StartFigure();
                    command = relative ? 'l' : 'L';
                    continue;
                }
                if (upper == 'L' && HasNumbers(tokens, i, 2))
                {
                    PointF next = Point(tokens, ref i, relative, current);
                    path.AddLine(current, next);
                    current = next;
                    continue;
                }
                if (upper == 'H' && HasNumbers(tokens, i, 1))
                {
                    float x = Number(tokens[i++].Value);
                    if (relative) x += current.X;
                    PointF next = new PointF(x, current.Y);
                    path.AddLine(current, next);
                    current = next;
                    continue;
                }
                if (upper == 'V' && HasNumbers(tokens, i, 1))
                {
                    float y = Number(tokens[i++].Value);
                    if (relative) y += current.Y;
                    PointF next = new PointF(current.X, y);
                    path.AddLine(current, next);
                    current = next;
                    continue;
                }
                if (upper == 'C' && HasNumbers(tokens, i, 6))
                {
                    PointF c1 = Point(tokens, ref i, relative, current);
                    PointF c2 = Point(tokens, ref i, relative, current);
                    PointF next = Point(tokens, ref i, relative, current);
                    path.AddBezier(current, c1, c2, next);
                    current = next;
                    continue;
                }
                if (upper == 'Q' && HasNumbers(tokens, i, 4))
                {
                    PointF c = Point(tokens, ref i, relative, current);
                    PointF next = Point(tokens, ref i, relative, current);
                    PointF c1 = new PointF(current.X + (2f / 3f) * (c.X - current.X), current.Y + (2f / 3f) * (c.Y - current.Y));
                    PointF c2 = new PointF(next.X + (2f / 3f) * (c.X - next.X), next.Y + (2f / 3f) * (c.Y - next.Y));
                    path.AddBezier(current, c1, c2, next);
                    current = next;
                    continue;
                }
                break;
            }
            return path;
        }

        private static bool HasNumbers(MatchCollection tokens, int index, int count)
        {
            return index + count <= tokens.Count && Enumerable.Range(index, count).All(i => !char.IsLetter(tokens[i].Value[0]));
        }

        private static PointF Point(MatchCollection tokens, ref int index, bool relative, PointF current)
        {
            float x = Number(tokens[index++].Value);
            float y = Number(tokens[index++].Value);
            return relative ? new PointF(current.X + x, current.Y + y) : new PointF(x, y);
        }

        private static PointF[] ParsePoints(string points)
        {
            if (string.IsNullOrWhiteSpace(points))
                return new PointF[0];
            float[] values = Regex.Matches(points, @"-?\d*\.?\d+").Cast<Match>().Select(m => Number(m.Value)).ToArray();
            return Enumerable.Range(0, values.Length / 2).Select(i => new PointF(values[i * 2], values[i * 2 + 1])).ToArray();
        }

        private static float F(XElement element, string attr)
        {
            return Number((string)element.Attribute(attr));
        }

        private static float Number(string value)
        {
            float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result);
            return result;
        }

        private static string NormalizeIconName(string iconName)
        {
            iconName = string.IsNullOrWhiteSpace(iconName) ? "circle-help.svg" : iconName.Trim();
            return iconName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? iconName : iconName + ".svg";
        }

        private static string ResolvePath(string iconName)
        {
            string direct = Path.Combine(DefaultIconRoot, iconName);
            if (File.Exists(direct))
                return direct;
            return Path.Combine(NestedIconRoot, iconName);
        }

        private static Bitmap DrawFallback(int size, Color color)
        {
            Bitmap bitmap = new Bitmap(size, size);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (Pen pen = new Pen(color, Math.Max(1.4f, size / 12f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                float pad = size * 0.22f;
                graphics.DrawEllipse(pen, pad, pad, size - pad * 2, size - pad * 2);
                graphics.DrawLine(pen, size * 0.50f, size * 0.34f, size * 0.50f, size * 0.58f);
                graphics.DrawLine(pen, size * 0.50f, size * 0.72f, size * 0.50f, size * 0.73f);
            }
            return bitmap;
        }
    }
}

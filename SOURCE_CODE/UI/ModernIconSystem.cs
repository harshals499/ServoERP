using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    public enum ModernIconKind
    {
        Activity,
        Alert,
        Analytics,
        Backup,
        Calendar,
        Checklist,
        ChevronDown,
        Client,
        Company,
        Contract,
        Document,
        EmptyBox,
        Email,
        Export,
        Filter,
        Import,
        Inventory,
        Invoice,
        Job,
        Location,
        Money,
        Parts,
        Payment,
        Phone,
        Preference,
        Print,
        Purchase,
        Refresh,
        Save,
        Search,
        Security,
        Service,
        Settings,
        Status,
        Tax,
        Technician,
        User,
        Vendor
    }

    public static class ModernIconSystem
    {
        private static readonly string IconFontFamily = ResolveIconFontFamily();

        public static Label Icon(ModernIconKind kind, int size, Color foreColor)
        {
            return new Label
            {
                AutoSize = false,
                Text = Glyph(kind),
                Font = IconFont(size),
                ForeColor = foreColor,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = Padding.Empty
            };
        }

        public static Label Badge(ModernIconKind kind, int size, Color backColor, Color foreColor, int radius = 10)
        {
            Label label = Icon(kind, Math.Max(12, (int)(size * 0.46f)), foreColor);
            label.Size = new Size(size, size);
            label.BackColor = backColor;
            label.Paint += (s, e) =>
            {
                Label owner = (Label)s;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = RoundedRect(new Rectangle(0, 0, owner.Width - 1, owner.Height - 1), radius))
                using (SolidBrush brush = new SolidBrush(backColor))
                    e.Graphics.FillPath(brush, path);

                TextRenderer.DrawText(
                    e.Graphics,
                    owner.Text,
                    owner.Font,
                    owner.ClientRectangle,
                    owner.ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            };
            return label;
        }

        public static Image IconBitmap(ModernIconKind kind, int size, Color foreColor)
        {
            int bitmapSize = Math.Max(12, size);
            Bitmap bitmap = new Bitmap(bitmapSize, bitmapSize);
            bitmap.SetResolution(96f, 96f);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (SolidBrush brush = new SolidBrush(foreColor))
            using (Font font = IconFont(Math.Max(10, (int)(bitmapSize * 0.58f))))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                StringFormat format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                graphics.DrawString(Glyph(kind), font, brush, new RectangleF(0, 0, bitmapSize, bitmapSize), format);
            }
            return bitmap;
        }

        public static Panel EmptyStateIcon(ModernIconKind kind, int size, Color backColor, Color foreColor)
        {
            Panel host = new Panel
            {
                Size = new Size(size, size),
                BackColor = backColor
            };
            host.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = RoundedRect(new Rectangle(0, 0, host.Width - 1, host.Height - 1), Math.Max(10, size / 4)))
                using (SolidBrush brush = new SolidBrush(backColor))
                    e.Graphics.FillPath(brush, path);
            };

            Label icon = Icon(kind, Math.Max(20, (int)(size * 0.42f)), foreColor);
            icon.Dock = DockStyle.Fill;
            host.Controls.Add(icon);
            return host;
        }

        public static void AddButtonIcon(Button button, ModernIconKind kind)
        {
            if (button == null)
                return;

            string text = (button.Text ?? string.Empty).Trim();
            button.Text = text;
            button.Image = IconBitmap(kind, Math.Min(18, Math.Max(14, button.Height - 14)), button.ForeColor);
            button.ImageAlign = ContentAlignment.MiddleLeft;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
            button.Padding = new Padding(8, 0, 8, 0);
        }

        public static ModernIconKind KindForTitle(string title)
        {
            string text = (title ?? string.Empty).ToUpperInvariant();
            if (text.Contains("ACTIVITY") || text.Contains("RECENT")) return ModernIconKind.Activity;
            if (text.Contains("ACTION")) return ModernIconKind.Status;
            if (text.Contains("BACKUP") || text.Contains("RESTORE")) return ModernIconKind.Backup;
            if (text.Contains("CHECKLIST") || text.Contains("TASK")) return ModernIconKind.Checklist;
            if (text.Contains("CLIENT")) return ModernIconKind.Client;
            if (text.Contains("COMPANY")) return ModernIconKind.Company;
            if (text.Contains("CONTRACT") || text.Contains("AMC")) return ModernIconKind.Contract;
            if (text.Contains("DISPLAY") || text.Contains("LAYOUT") || text.Contains("PREFERENCE")) return ModernIconKind.Preference;
            if (text.Contains("GST") || text.Contains("TAX") || text.Contains("HSN") || text.Contains("SAC")) return ModernIconKind.Tax;
            if (text.Contains("INVOICE")) return ModernIconKind.Invoice;
            if (text.Contains("JOB")) return ModernIconKind.Job;
            if (text.Contains("LICENSE") || text.Contains("SECURITY")) return ModernIconKind.Security;
            if (text.Contains("PART")) return ModernIconKind.Parts;
            if (text.Contains("PAYMENT") || text.Contains("VALUE") || text.Contains("COST") || text.Contains("REVENUE") || text.Contains("SUMMARY")) return ModernIconKind.Payment;
            if (text.Contains("REPORT") || text.Contains("ANALYTIC") || text.Contains("CHART")) return ModernIconKind.Analytics;
            if (text.Contains("SETTING") || text.Contains("SYSTEM") || text.Contains("TOOL")) return ModernIconKind.Settings;
            if (text.Contains("SLA")) return ModernIconKind.Alert;
            if (text.Contains("TECH")) return ModernIconKind.Technician;
            if (text.Contains("USER") || text.Contains("LOGIN")) return ModernIconKind.User;
            if (text.Contains("VENDOR")) return ModernIconKind.Vendor;
            return ModernIconKind.Document;
        }

        public static Font IconFont(float size)
        {
            return new Font(IconFontFamily, size, FontStyle.Regular, GraphicsUnit.Point);
        }

        private static string Glyph(ModernIconKind kind)
        {
            switch (kind)
            {
                case ModernIconKind.Activity: return "\uE81C";
                case ModernIconKind.Alert: return "\uE7BA";
                case ModernIconKind.Analytics: return "\uE9D2";
                case ModernIconKind.Backup: return "\uE777";
                case ModernIconKind.Calendar: return "\uE787";
                case ModernIconKind.Checklist: return "\uE9D5";
                case ModernIconKind.ChevronDown: return "\uE70D";
                case ModernIconKind.Client: return "\uE77B";
                case ModernIconKind.Company: return "\uE80F";
                case ModernIconKind.Contract: return "\uE8A5";
                case ModernIconKind.Document: return "\uE8A5";
                case ModernIconKind.EmptyBox: return "\uE7B8";
                case ModernIconKind.Email: return "\uE715";
                case ModernIconKind.Export: return "\uEDE1";
                case ModernIconKind.Filter: return "\uE71C";
                case ModernIconKind.Import: return "\uE8B5";
                case ModernIconKind.Inventory: return "\uE7B8";
                case ModernIconKind.Invoice: return "\uE9D5";
                case ModernIconKind.Job: return "\uE821";
                case ModernIconKind.Location: return "\uE707";
                case ModernIconKind.Money: return "\uEAFD";
                case ModernIconKind.Parts: return "\uE90F";
                case ModernIconKind.Payment: return "\uE8C7";
                case ModernIconKind.Phone: return "\uE717";
                case ModernIconKind.Preference: return "\uE713";
                case ModernIconKind.Print: return "\uE749";
                case ModernIconKind.Purchase: return "\uE7BF";
                case ModernIconKind.Refresh: return "\uE895";
                case ModernIconKind.Save: return "\uE74E";
                case ModernIconKind.Search: return "\uE721";
                case ModernIconKind.Security: return "\uE72E";
                case ModernIconKind.Service: return "\uE90F";
                case ModernIconKind.Settings: return "\uE713";
                case ModernIconKind.Status: return "\uE9D9";
                case ModernIconKind.Tax: return "\uE9D9";
                case ModernIconKind.Technician: return "\uE77B";
                case ModernIconKind.User: return "\uE77B";
                case ModernIconKind.Vendor: return "\uE8D4";
                default: return "\uE10F";
            }
        }

        private static string ResolveIconFontFamily()
        {
            string[] candidates = { "Segoe Fluent Icons", "Segoe MDL2 Assets", "Segoe UI Symbol" };
            using (System.Drawing.Text.InstalledFontCollection fonts = new System.Drawing.Text.InstalledFontCollection())
            {
                foreach (string candidate in candidates)
                    if (fonts.Families.Any(f => string.Equals(f.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                        return candidate;
            }
            return "Segoe UI Symbol";
        }

        private static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            int diameter = Math.Max(1, radius * 2);
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}

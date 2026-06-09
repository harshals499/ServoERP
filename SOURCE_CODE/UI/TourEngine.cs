using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Properties;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    /// <summary>Describes one first-launch tour step and its target control.</summary>
    public sealed class TourStep
    {
        /// <summary>Creates a tour step for a live WinForms control.</summary>
        public TourStep(Func<Control> targetProvider, string title, string body, Color highlightColor, TourTooltipPosition position, Action beforeShow)
        {
            TargetProvider = targetProvider;
            Title = title;
            Body = body;
            HighlightColor = highlightColor;
            Position = position;
            BeforeShow = beforeShow;
        }

        public Func<Control> TargetProvider { get; private set; }
        public string Title { get; private set; }
        public string Body { get; private set; }
        public Color HighlightColor { get; private set; }
        public TourTooltipPosition Position { get; private set; }
        public Action BeforeShow { get; private set; }
    }

    /// <summary>Preferred side for the floating tour tooltip.</summary>
    public enum TourTooltipPosition
    {
        Right,
        Left,
        Top,
        Bottom,
        Center
    }

    /// <summary>Runs the ServoERP first-launch spotlight tour.</summary>
    public static class TourEngine
    {
        private const string TourOfferedMarkerFileName = "first-login-tour-offered.marker";

        /// <summary>Starts the tour when it has not already been completed by this Windows user.</summary>
        public static void StartFirstLaunchTour(Form owner, IList<TourStep> steps)
        {
            StartFirstLaunchTour(owner, owner, steps);
        }

        /// <summary>Starts the tour inside the supplied host when it has not already been completed by this Windows user.</summary>
        public static void StartFirstLaunchTour(Form owner, Control host, IList<TourStep> steps)
        {
            if (owner == null || owner.IsDisposed || host == null || host.IsDisposed || steps == null || steps.Count == 0)
                return;

            try
            {
                if (!ShouldOfferFirstLoginTour())
                    return;

                MarkTourOffered();
                var overlay = new SpotlightOverlay(owner, host, steps.Where(s => s != null).ToList());
                host.Controls.Add(overlay);
                overlay.BringToFront();
                overlay.StartTour();
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("TourEngine.StartFirstLaunchTour", ex);
            }
        }

        /// <summary>Marks the first-launch tour as complete and saves the user setting.</summary>
        internal static void MarkCompleted()
        {
            try
            {
                Settings.Default.TourCompleted = true;
                Settings.Default.Save();
                MarkTourOffered();
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("TourEngine.MarkCompleted", ex);
            }
        }

        /// <summary>Returns true only before the first authenticated main-shell tour has ever been offered on this Windows profile.</summary>
        private static bool ShouldOfferFirstLoginTour()
        {
            if (Settings.Default.TourCompleted)
                return false;

            return !File.Exists(GetTourOfferedMarkerPath());
        }

        /// <summary>Records that the first-login tour has already been offered so future app versions do not reopen it.</summary>
        private static void MarkTourOffered()
        {
            try
            {
                string markerPath = GetTourOfferedMarkerPath();
                Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
                if (!File.Exists(markerPath))
                {
                    File.WriteAllText(
                        markerPath,
                        "ServoERP first-login guided tour offered on "
                        + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        + Environment.NewLine
                        + "WindowsUser=" + Environment.UserName
                        + Environment.NewLine
                        + "Machine=" + Environment.MachineName);
                }
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("TourEngine.MarkTourOffered", ex);
            }
        }

        /// <summary>Gets the version-independent first-login tour marker path for this Windows profile.</summary>
        private static string GetTourOfferedMarkerPath()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ServoERP");
            return Path.Combine(folder, TourOfferedMarkerFileName);
        }
    }

    /// <summary>Draws the dimmed screen, spotlight hole, and tour tooltip.</summary>
    internal sealed class SpotlightOverlay : Control
    {
        private readonly Form _owner;
        private readonly Control _host;
        private readonly IList<TourStep> _steps;
        private readonly Panel _tooltip;
        private readonly Label _title;
        private readonly Label _body;
        private readonly Label _counter;
        private readonly Button _back;
        private readonly Button _next;
        private readonly Button _skip;
        private Bitmap _snapshot;
        private Rectangle _spotlightBounds;
        private int _index;

        /// <summary>Creates a full-form tour overlay.</summary>
        public SpotlightOverlay(Form owner, IList<TourStep> steps)
            : this(owner, owner, steps)
        {
        }

        /// <summary>Creates a tour overlay inside the supplied host control.</summary>
        public SpotlightOverlay(Form owner, Control host, IList<TourStep> steps)
        {
            _owner = owner;
            _host = host;
            _steps = steps;
            Dock = DockStyle.Fill;
            TabStop = true;
            BackColor = Color.Black;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

            _tooltip = BuildTooltipPanel(out _title, out _body, out _counter, out _back, out _next, out _skip);
            Controls.Add(_tooltip);
            _back.Click += (s, e) => MoveStep(-1);
            _next.Click += (s, e) => MoveStep(1);
            _skip.Click += (s, e) => CompleteTour();
            KeyDown += OverlayKeyDown;
            Resize += (s, e) => ShowCurrentStep();
        }

        /// <summary>Displays the first tour step.</summary>
        public void StartTour()
        {
            Focus();
            ShowCurrentStep();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _snapshot != null)
                _snapshot.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            if (_snapshot != null)
                e.Graphics.DrawImageUnscaled(_snapshot, Point.Empty);

            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddRectangle(ClientRectangle);
                Rectangle hole = InflateWithinClient(_spotlightBounds, 12);
                path.AddRoundedRectangle(hole, 14);
                using (Brush dim = new SolidBrush(Color.FromArgb(160, 15, 23, 42)))
                    e.Graphics.FillPath(dim, path);

                TourStep step = CurrentStep;
                using (Pen pen = new Pen(step == null ? DS.Primary600 : step.HighlightColor, 3f))
                    e.Graphics.DrawRoundedRectangle(pen, hole, 14);
            }
        }

        private TourStep CurrentStep
        {
            get
            {
                if (_index < 0 || _index >= _steps.Count)
                    return null;
                return _steps[_index];
            }
        }

        private void OverlayKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                CompleteTour();
            else if (e.KeyCode == Keys.Left)
                MoveStep(-1);
            else if (e.KeyCode == Keys.Right)
                MoveStep(1);
        }

        private void MoveStep(int delta)
        {
            int next = _index + delta;
            if (next < 0)
                return;
            if (next >= _steps.Count)
            {
                CompleteTour();
                return;
            }

            _index = next;
            ShowCurrentStep();
        }

        private void CompleteTour()
        {
            TourEngine.MarkCompleted();
            if (Parent != null)
                Parent.Controls.Remove(this);
            Dispose();
        }

        private void ShowCurrentStep()
        {
            TourStep step = CurrentStep;
            if (step == null || _owner == null || _owner.IsDisposed)
                return;

            try
            {
                if (step.BeforeShow != null)
                    step.BeforeShow();

                CaptureOwner();
                Control target = step.TargetProvider == null ? null : step.TargetProvider();
                _spotlightBounds = ResolveTargetBounds(target);

                _title.Text = step.Title;
                _body.Text = step.Body;
                _counter.Text = "Step " + (_index + 1).ToString("0") + " of " + _steps.Count.ToString("0");
                _back.Enabled = _index > 0;
                _next.Text = _index == _steps.Count - 1 ? "Finish" : "Next";
                LayoutTooltip(step.Position);
                Invalidate();
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("TourEngine.ShowCurrentStep", ex);
            }
        }

        private void CaptureOwner()
        {
            if (_snapshot != null)
            {
                _snapshot.Dispose();
                _snapshot = null;
            }

            if (Width <= 0 || Height <= 0)
                return;

            _snapshot = new Bitmap(Width, Height);
            bool visible = Visible;
            try
            {
                Visible = false;
                DrawToBitmapSafe(_host, _snapshot);
            }
            finally
            {
                Visible = visible;
            }
        }

        private void DrawToBitmapSafe(Control control, Bitmap target)
        {
            try
            {
                control.DrawToBitmap(target, new Rectangle(Point.Empty, target.Size));
            }
            catch
            {
                using (Graphics g = Graphics.FromImage(target))
                    g.Clear(control.BackColor);
            }
        }

        private Rectangle ResolveTargetBounds(Control target)
        {
            if (target == null || target.IsDisposed || !target.Visible)
                return new Rectangle(Math.Max(20, Width / 2 - 120), Math.Max(20, Height / 2 - 70), 240, 140);

            Rectangle screen = target.RectangleToScreen(target.ClientRectangle);
            Point topLeft = PointToClient(screen.Location);
            Rectangle bounds = new Rectangle(topLeft, screen.Size);
            if (bounds.Width < 8 || bounds.Height < 8 || !ClientRectangle.IntersectsWith(bounds))
                return new Rectangle(Math.Max(20, Width / 2 - 120), Math.Max(20, Height / 2 - 70), 240, 140);
            return bounds;
        }

        private void LayoutTooltip(TourTooltipPosition position)
        {
            Size tooltipSize = new Size(Math.Min(420, Math.Max(320, Width - 48)), 210);
            _tooltip.Size = tooltipSize;

            Rectangle hole = InflateWithinClient(_spotlightBounds, 14);
            int gap = 18;
            Point location;
            switch (position)
            {
                case TourTooltipPosition.Left:
                    location = new Point(hole.Left - tooltipSize.Width - gap, hole.Top);
                    break;
                case TourTooltipPosition.Top:
                    location = new Point(hole.Left, hole.Top - tooltipSize.Height - gap);
                    break;
                case TourTooltipPosition.Bottom:
                    location = new Point(hole.Left, hole.Bottom + gap);
                    break;
                case TourTooltipPosition.Center:
                    location = new Point((Width - tooltipSize.Width) / 2, (Height - tooltipSize.Height) / 2);
                    break;
                default:
                    location = new Point(hole.Right + gap, hole.Top);
                    break;
            }

            location.X = Math.Max(18, Math.Min(location.X, Math.Max(18, Width - tooltipSize.Width - 18)));
            location.Y = Math.Max(18, Math.Min(location.Y, Math.Max(18, Height - tooltipSize.Height - 18)));
            _tooltip.Location = location;
            _tooltip.BringToFront();
        }

        private static Rectangle InflateWithinClient(Rectangle source, int padding)
        {
            Rectangle result = source;
            result.Inflate(padding, padding);
            return result;
        }

        private static Panel BuildTooltipPanel(out Label title, out Label body, out Label counter, out Button back, out Button next, out Button skip)
        {
            Panel panel = new Panel
            {
                BackColor = Color.White,
                Padding = new Padding(18),
                Size = new Size(390, 210)
            };
            panel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen pen = new Pen(DS.Border))
                    e.Graphics.DrawRoundedRectangle(pen, new Rectangle(0, 0, panel.Width - 1, panel.Height - 1), 8);
            };

            title = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = DS.Slate900,
                AutoEllipsis = true
            };
            body = new Label
            {
                Dock = DockStyle.Top,
                Height = 82,
                Font = new Font("Segoe UI", 9.3f),
                ForeColor = DS.Slate700,
                AutoEllipsis = true
            };
            counter = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = DS.Primary700
            };

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 42,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 8, 0, 0)
            };
            next = DS.PrimaryBtn("Next", 86, 32);
            back = DS.GhostBtn("Back", 78, 32);
            skip = DS.GhostBtn("Skip tour", 94, 32);
            actions.Controls.Add(next);
            actions.Controls.Add(back);
            actions.Controls.Add(skip);

            panel.Controls.Add(actions);
            panel.Controls.Add(counter);
            panel.Controls.Add(body);
            panel.Controls.Add(title);
            return panel;
        }
    }

    internal static class TourGraphicsExtensions
    {
        /// <summary>Adds a rounded rectangle to a GDI+ path.</summary>
        public static void AddRoundedRectangle(this GraphicsPath path, Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
        }

        /// <summary>Draws a rounded rectangle with the supplied pen.</summary>
        public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, Rectangle bounds, int radius)
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddRoundedRectangle(bounds, radius);
                graphics.DrawPath(pen, path);
            }
        }
    }
}

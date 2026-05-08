using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public enum CardResizeAxes { Both, HeightOnly, WidthOnly }
    public enum ResizeDirection { None, Right, Bottom, BottomRight }

    public sealed class CardResizeEventArgs : EventArgs
    {
        public string CardKey { get; set; }
        public string PageKey { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string SizePreset { get; set; }
    }

    public sealed class ResizableCard : UserControl
    {
        private const int HeaderHeight = 36;
        private const int Radius = 10;
        private static readonly Color DefaultBorder = DS.Border;
        private static readonly Color DefaultHint = DS.Slate400;
        private static readonly Color DefaultHeaderLine = DS.Border;
        private static readonly Color SavedColor = DS.Teal600;

        private readonly Panel _headerPanel;
        private readonly Label _titleLabel;
        private readonly Button _menuButton;
        private readonly Label _savedLabel;
        private readonly Label _overflowLabel;
        private readonly Panel _contentPanel;
        private readonly Panel _rightZone;
        private readonly Panel _bottomZone;
        private readonly Panel _cornerZone;
        private readonly ContextMenuStrip _menu;
        private readonly Timer _savedTimer;
        private readonly Timer _animateTimer;

        private bool _isResizing;
        private bool _showGrip;
        private bool _suppressAutoSave;
        private ResizeDirection _resizeDirection = ResizeDirection.None;
        private Point _resizeStart;
        private Size _sizeAtResizeStart;
        private Size _animationStartSize;
        private Size _animationTargetSize;
        private int _animationStep;
        private readonly List<ToolStripMenuItem> _presetItems = new List<ToolStripMenuItem>();

        public event EventHandler<CardResizeEventArgs> CardResized;
        public event EventHandler<CardResizeEventArgs> CardResizeComplete;
        public event EventHandler<MouseEventArgs> CardDragRequested;

        public ResizableCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = DS.White;
            Padding = new Padding(1);
            MinimumSize = new Size(200, 120);
            MaximumSize = new Size(6000, 3000);
            Margin = new Padding(0, 0, 12, 12);

            _headerPanel = new Panel { Height = HeaderHeight, Dock = DockStyle.Top, BackColor = DS.White };
            _headerPanel.Paint += (s, e) =>
            {
                if (ShowHeader)
                {
                    using (Pen pen = new Pen(DefaultHeaderLine, 1f))
                        e.Graphics.DrawLine(pen, 0, _headerPanel.Height - 1, _headerPanel.Width, _headerPanel.Height - 1);
                }
            };

            _titleLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = DS.Slate900,
                Padding = new Padding(14, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _savedLabel = new Label
            {
                AutoSize = false,
                Width = 48,
                Height = 18,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = SavedColor,
                BackColor = DS.White,
                Text = "Saved",
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };

            _overflowLabel = new Label
            {
                AutoSize = false,
                Width = 64,
                Height = 18,
                Font = new Font("Segoe UI", 8f, FontStyle.Regular),
                ForeColor = DefaultHint,
                BackColor = DS.White,
                Text = "Scroll",
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };

            _menuButton = new Button
            {
                Text = "Size",
                Width = 86,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = DS.White,
                ForeColor = DS.Slate600,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0),
                TextAlign = ContentAlignment.MiddleCenter
            };
            _menuButton.FlatAppearance.BorderSize = 1;
            _menuButton.FlatAppearance.BorderColor = DefaultBorder;
            _menuButton.FlatAppearance.MouseDownBackColor = DS.Slate100;
            _menuButton.FlatAppearance.MouseOverBackColor = DS.Slate50;
            _menuButton.MouseEnter += (s, e) => _menuButton.ForeColor = DS.Slate900;
            _menuButton.MouseLeave += (s, e) => _menuButton.ForeColor = DS.Slate600;
            _menuButton.Click += (s, e) => ShowCardMenu(new Point(Width - 160, HeaderHeight));

            Panel headerRight = new Panel { Dock = DockStyle.Right, Width = 204, BackColor = DS.White, Padding = new Padding(0, 6, 10, 6) };
            headerRight.Controls.Add(_menuButton);
            headerRight.Controls.Add(_savedLabel);
            headerRight.Controls.Add(_overflowLabel);

            _headerPanel.Controls.Add(_titleLabel);
            _headerPanel.Controls.Add(headerRight);
            _headerPanel.MouseDown += HeaderDrag_MouseDown;
            _titleLabel.MouseDown += HeaderDrag_MouseDown;

            _contentPanel = new Panel { Dock = DockStyle.Fill, BackColor = DS.White, Padding = new Padding(14, 14, 16, 14), AutoScroll = true };
            _contentPanel.Layout += (s, e) => QueueOverflowUpdate();
            _contentPanel.Resize += (s, e) => QueueOverflowUpdate();
            _contentPanel.ControlAdded += (s, e) => QueueOverflowUpdate();
            _contentPanel.ControlRemoved += (s, e) => QueueOverflowUpdate();

            _rightZone = CreateResizeZone(DockStyle.Right, 10, Cursors.SizeWE);
            _bottomZone = CreateResizeZone(DockStyle.Bottom, 10, Cursors.SizeNS);
            HookResizeEvents(_rightZone, ResizeDirection.Right);
            HookResizeEvents(_bottomZone, ResizeDirection.Bottom);
            _cornerZone = new Panel
            {
                Width = 18,
                Height = 18,
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                BackColor = DS.White,
                Cursor = Cursors.SizeNWSE
            };
            _cornerZone.Location = new Point(Width - _cornerZone.Width - 1, Height - _cornerZone.Height - 1);
            HookResizeEvents(_cornerZone, ResizeDirection.BottomRight);
            _cornerZone.MouseEnter += (s, e) => SetGripVisible(true);
            _cornerZone.MouseLeave += (s, e) => { if (!_isResizing) SetGripVisible(false); };

            _menu = BuildMenu();
            ContextMenuStrip = _menu;

            _savedTimer = new Timer { Interval = 120 };
            _savedTimer.Tick += SavedTimer_Tick;

            _animateTimer = new Timer { Interval = 15 };
            _animateTimer.Tick += AnimateTimer_Tick;

            Controls.Add(_cornerZone);
            Controls.Add(_contentPanel);
            Controls.Add(_bottomZone);
            Controls.Add(_rightZone);
            Controls.Add(_headerPanel);

            Resize += (s, e) =>
            {
                _cornerZone.Location = new Point(Math.Max(1, Width - _cornerZone.Width - 1), Math.Max(1, Height - _cornerZone.Height - 1));
                UpdateLayoutState();
                QueueOverflowUpdate();
            };
            Load += (s, e) =>
            {
                if (Width > 0 && Height > 0)
                    CardLayoutService.RegisterDefaultSize(PageKey, CardKey, Size, SizePreset);
                UpdateLayoutState();
                QueueOverflowUpdate();
            };
            MouseUp += CardMouseUp;
            MouseDown += CardMouseDown;
            MouseMove += CardMouseMove;
        }

        public string CardKey { get; set; }
        public string PageKey { get; set; }
        public string CardTitle
        {
            get { return _titleLabel.Text; }
            set { _titleLabel.Text = value ?? string.Empty; }
        }
        public bool ShowHeader { get; set; } = true;
        public bool AllowResize { get; set; } = true;
        public Color BorderColor { get; set; } = DefaultBorder;
        public Color BackgroundColor { get; set; } = DS.White;
        public Panel ContentPanel => _contentPanel;
        public string SizePreset { get; set; } = "Medium";
        public CardResizeAxes ResizeAxes { get; set; } = CardResizeAxes.Both;
        public int MinCardWidth => MinimumSize.Width;
        public int MinCardHeight => MinimumSize.Height;

        public void ApplyPersistedLayout(Size size, string preset)
        {
            try
            {
                _suppressAutoSave = true;
                SizePreset = string.IsNullOrWhiteSpace(preset) ? SizePreset : preset;
                int width = ResizeAxes == CardResizeAxes.HeightOnly ? Width : Math.Max(MinCardWidth, size.Width);
                int height = ResizeAxes == CardResizeAxes.WidthOnly ? Height : Math.Max(MinCardHeight, size.Height);
                Size = new Size(width, height);
                Parent?.PerformLayout();
                QueueOverflowUpdate();
            }
            finally
            {
                _suppressAutoSave = false;
            }
        }

        public void ResetToDefault()
        {
            CardDefaultSize defaultSize;
            if (new CardLayoutService().GetDefaultSizes().TryGetValue(CardKey ?? string.Empty, out defaultSize))
            {
                ApplyPersistedLayout(defaultSize.Size, defaultSize.SizePreset);
                TriggerResizeComplete();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = GetRoundedRectPath(new Rectangle(0, 0, Width - 1, Height - 1), Radius))
            using (SolidBrush brush = new SolidBrush(BackgroundColor))
            using (Pen pen = new Pen(BorderColor, 1f))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            if (AllowResize && (_showGrip || _isResizing))
                DrawGrip(e.Graphics);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            CardMouseUp(this, e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            CardMouseDown(this, e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            CardMouseMove(this, e);
        }

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip { ShowImageMargin = false };

            var header = new ToolStripMenuItem("Card size") { Enabled = false };
            menu.Items.Add(header);
            menu.Items.Add(new ToolStripSeparator());

            AddPresetItem(menu, "Small");
            AddPresetItem(menu, "Medium");
            AddPresetItem(menu, "Large");
            AddPresetItem(menu, "Full width");

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Reset to default", null, (s, e) => ResetToDefault()));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Save layout", null, (s, e) => SavePageLayout()));
            menu.Items.Add(new ToolStripMenuItem("Reset page layout", null, (s, e) => ResetPageLayout()));
            menu.Opening += (s, e) => RefreshMenuChecks();

            return menu;
        }

        private void AddPresetItem(ContextMenuStrip menu, string text)
        {
            var item = new ToolStripMenuItem(text, null, (s, e) => ApplyPreset(text));
            _presetItems.Add(item);
            menu.Items.Add(item);
        }

        private Panel CreateResizeZone(DockStyle dock, int size, Cursor cursor)
        {
            Panel zone = new Panel
            {
                Dock = dock,
                BackColor = DS.White,
                Cursor = cursor
            };
            if (dock == DockStyle.Right)
                zone.Width = size;
            else
                zone.Height = size;

            zone.MouseEnter += (s, e) => SetGripVisible(true);
            zone.MouseLeave += (s, e) => { if (!_isResizing) SetGripVisible(false); };
            return zone;
        }

        private void HookResizeEvents(Control control, ResizeDirection direction)
        {
            control.MouseDown += (s, e) =>
            {
                if (!AllowResize || e.Button != MouseButtons.Left)
                    return;

                _isResizing = true;
                _resizeDirection = AdjustDirection(direction);
                _resizeStart = Control.MousePosition;
                _sizeAtResizeStart = Size;
                Capture = true;
            };

            control.MouseMove += (s, e) =>
            {
                if (!_isResizing)
                    return;

                PerformResize(Control.MousePosition);
            };

            control.MouseUp += (s, e) => FinishResize();
        }

        private void HeaderDrag_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !_isResizing)
                CardDragRequested?.Invoke(this, e);
        }

        private void CardMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                ShowCardMenu(e.Location);
                return;
            }
        }

        private void CardMouseMove(object sender, MouseEventArgs e)
        {
            if (_isResizing)
                PerformResize(Control.MousePosition);
        }

        private void CardMouseUp(object sender, MouseEventArgs e)
        {
            FinishResize();
        }

        private void PerformResize(Point currentPosition)
        {
            if (!_isResizing || _resizeDirection == ResizeDirection.None)
                return;

            Point delta = new Point(currentPosition.X - _resizeStart.X, currentPosition.Y - _resizeStart.Y);
            int maxWidth = GetMaxWidth();
            int maxHeight = GetMaxHeight();

            int newWidth = _sizeAtResizeStart.Width;
            int newHeight = _sizeAtResizeStart.Height;

            if (_resizeDirection == ResizeDirection.Right || _resizeDirection == ResizeDirection.BottomRight)
                newWidth = Math.Max(MinCardWidth, Math.Min(maxWidth, _sizeAtResizeStart.Width + delta.X));
            if (_resizeDirection == ResizeDirection.Bottom || _resizeDirection == ResizeDirection.BottomRight)
                newHeight = Math.Max(MinCardHeight, Math.Min(maxHeight, _sizeAtResizeStart.Height + delta.Y));

            if (ResizeAxes == CardResizeAxes.HeightOnly)
                newWidth = Width;
            if (ResizeAxes == CardResizeAxes.WidthOnly)
                newHeight = Height;

            Size = new Size(newWidth, newHeight);
            Parent?.PerformLayout();
            CardResized?.Invoke(this, BuildArgs());
        }

        private void FinishResize()
        {
            if (!_isResizing)
                return;

            _isResizing = false;
            Capture = false;
            SizePreset = "Custom";
            TriggerResizeComplete();
            SetGripVisible(false);
        }

        private void ApplyPreset(string preset)
        {
            Size target = GetPresetSize(preset);
            SizePreset = preset;
            _animationStartSize = Size;
            _animationTargetSize = target;
            _animationStep = 0;
            _animateTimer.Start();
        }

        private Size GetPresetSize(string preset)
        {
            int availableWidth = Math.Max(MinCardWidth, GetMaxWidth());
            int snappedWidth = availableWidth;

            if (ResizeAxes != CardResizeAxes.HeightOnly)
            {
                decimal ratio = 1m;
                switch ((preset ?? string.Empty).Trim().ToUpperInvariant())
                {
                    case "SMALL": ratio = 0.25m; break;
                    case "MEDIUM": ratio = 0.50m; break;
                    case "LARGE": ratio = 0.75m; break;
                    case "FULL WIDTH": ratio = 1.00m; break;
                }

                int candidate = (int)Math.Round(availableWidth * ratio);
                snappedWidth = SnapWidthToGrid(candidate, availableWidth);
            }

            int height = Height;
            if (ResizeAxes != CardResizeAxes.WidthOnly)
            {
                switch ((preset ?? string.Empty).Trim().ToUpperInvariant())
                {
                    case "SMALL": height = 160; break;
                    case "MEDIUM": height = 220; break;
                    case "LARGE": height = 300; break;
                    case "FULL WIDTH": height = Height; break;
                }
            }

            if (ResizeAxes == CardResizeAxes.HeightOnly)
                snappedWidth = Width;
            if (ResizeAxes == CardResizeAxes.WidthOnly)
                height = Height;

            return new Size(Math.Max(MinCardWidth, snappedWidth), Math.Max(MinCardHeight, height));
        }

        private int GetMaxWidth()
        {
            Control parent = Parent;
            if (parent == null)
                return Math.Max(MinCardWidth, Width);

            if (parent is FlowLayoutPanel flow)
                return Math.Max(MinCardWidth, flow.ClientSize.Width - Margin.Horizontal - flow.Padding.Horizontal - 8);
            if (parent is TableLayoutPanel)
                return Math.Max(MinCardWidth, Width);

            return Math.Max(MinCardWidth, parent.ClientSize.Width - Margin.Horizontal - 8);
        }

        private int GetMaxHeight()
        {
            Control parent = Parent;
            if (parent == null)
                return Math.Max(MinCardHeight, Height);

            int viewportHeight = parent.ClientSize.Height > 0 ? parent.ClientSize.Height : 1200;
            bool parentScrolls = parent is ScrollableControl scrollable && scrollable.AutoScroll;
            int generousHeight = parentScrolls ? viewportHeight * 2 : viewportHeight - Margin.Vertical - 8;
            return Math.Max(MinCardHeight, Math.Min(3000, Math.Max(900, generousHeight)));
        }

        private int SnapWidthToGrid(int width, int maxWidth)
        {
            if (!(Parent is FlowLayoutPanel))
                return Math.Min(maxWidth, width);

            int[] columns =
            {
                Math.Max(MinCardWidth, (int)Math.Round(maxWidth * 0.25m)),
                Math.Max(MinCardWidth, (int)Math.Round(maxWidth * 0.50m)),
                Math.Max(MinCardWidth, (int)Math.Round(maxWidth * 0.75m)),
                Math.Max(MinCardWidth, maxWidth)
            };

            return columns.OrderBy(value => Math.Abs(value - width)).FirstOrDefault();
        }

        private ResizeDirection AdjustDirection(ResizeDirection direction)
        {
            if (ResizeAxes == CardResizeAxes.HeightOnly)
                return direction == ResizeDirection.BottomRight ? ResizeDirection.Bottom : direction == ResizeDirection.Right ? ResizeDirection.None : direction;
            if (ResizeAxes == CardResizeAxes.WidthOnly)
                return direction == ResizeDirection.BottomRight ? ResizeDirection.Right : direction == ResizeDirection.Bottom ? ResizeDirection.None : direction;
            return direction;
        }

        private void TriggerResizeComplete()
        {
            CardResizeComplete?.Invoke(this, BuildArgs());

            if (_suppressAutoSave)
                return;

            CardLayoutService.RegisterDefaultSize(PageKey, CardKey, Size, SizePreset);
            Task saveTask = CardLayoutService.SaveCardLayoutAsync(this);
            saveTask.ContinueWith(t => { }, TaskScheduler.Default);
            ShowSavedFeedback();
        }

        private CardResizeEventArgs BuildArgs()
        {
            return new CardResizeEventArgs
            {
                CardKey = CardKey,
                PageKey = PageKey,
                Width = Width,
                Height = Height,
                SizePreset = SizePreset
            };
        }

        private void SavePageLayout()
        {
            Control root = FindPageRoot();
            List<CardLayoutDto> layouts = CardLayoutService.EnumerateCards(root)
                .Where(card => string.Equals(card.PageKey, PageKey, StringComparison.OrdinalIgnoreCase))
                .Select(card => new CardLayoutDto
                {
                    PageKey = card.PageKey,
                    CardKey = card.CardKey,
                    Width = card.Width,
                    Height = card.Height,
                    SizePreset = card.SizePreset
                })
                .ToList();

            int userId = CardLayoutService.ResolveCurrentUserId();
            Task.Run(() => new CardLayoutService().SavePageLayout(userId, PageKey, layouts));
            ShowSavedFeedback();
        }

        private void ResetPageLayout()
        {
            int userId = CardLayoutService.ResolveCurrentUserId();
            Task.Run(() => new CardLayoutService().ResetPageLayout(userId, PageKey));

            Control root = FindPageRoot();
            Dictionary<string, CardDefaultSize> defaults = new CardLayoutService().GetDefaultSizes();
            foreach (ResizableCard card in CardLayoutService.EnumerateCards(root).Where(card => string.Equals(card.PageKey, PageKey, StringComparison.OrdinalIgnoreCase)))
            {
                CardDefaultSize defaultSize;
                if (defaults.TryGetValue(card.CardKey ?? string.Empty, out defaultSize))
                    card.ApplyPersistedLayout(defaultSize.Size, defaultSize.SizePreset);
            }

            root?.PerformLayout();
        }

        private Control FindPageRoot()
        {
            Control current = this;
            while (current != null && !(current is UserControl) && !(current is Form))
                current = current.Parent;
            return current ?? this;
        }

        private void RefreshMenuChecks()
        {
            foreach (ToolStripMenuItem item in _presetItems)
                item.Checked = string.Equals(item.Text, SizePreset, StringComparison.OrdinalIgnoreCase);
        }

        private void ShowCardMenu(Point point)
        {
            RefreshMenuChecks();
            _menu.Show(this, point);
        }

        private void SetGripVisible(bool visible)
        {
            if (_showGrip == visible)
                return;

            _showGrip = visible;
            Invalidate();
        }

        private void UpdateLayoutState()
        {
            _headerPanel.Visible = ShowHeader;
            _menuButton.Visible = ShowHeader;
            _savedLabel.Visible = _savedLabel.Visible && ShowHeader;
            _overflowLabel.Visible = _overflowLabel.Visible && ShowHeader;
            _rightZone.Visible = AllowResize && ResizeAxes != CardResizeAxes.HeightOnly;
            _bottomZone.Visible = AllowResize && ResizeAxes != CardResizeAxes.WidthOnly;
            _cornerZone.Visible = AllowResize && ResizeAxes == CardResizeAxes.Both;
            _contentPanel.Padding = ShowHeader ? new Padding(14, 14, 16, 14) : new Padding(14, 14, 16, 14);
            Invalidate();
        }

        private void QueueOverflowUpdate()
        {
            if (IsDisposed)
                return;

            try
            {
                if (IsHandleCreated)
                    BeginInvoke((Action)UpdateOverflowHint);
                else
                    UpdateOverflowHint();
            }
            catch
            {
            }
        }

        private void UpdateOverflowHint()
        {
            bool overflow = false;
            try
            {
                overflow = _contentPanel.VerticalScroll.Visible
                    || _contentPanel.HorizontalScroll.Visible
                    || _contentPanel.DisplayRectangle.Height > _contentPanel.ClientSize.Height + 4
                    || _contentPanel.DisplayRectangle.Width > _contentPanel.ClientSize.Width + 4;
            }
            catch
            {
                overflow = false;
            }

            _overflowLabel.Visible = ShowHeader && overflow;
        }

        private void DrawGrip(Graphics graphics)
        {
            using (Pen pen = new Pen(DS.Slate300, 1f))
            {
                int right = Width - 6;
                int bottom = Height - 6;
                graphics.DrawLine(pen, right - 8, bottom, right, bottom - 8);
                graphics.DrawLine(pen, right - 5, bottom, right, bottom - 5);
                graphics.DrawLine(pen, right - 2, bottom, right, bottom - 2);
            }
        }

        private void SavedTimer_Tick(object sender, EventArgs e)
        {
            int alpha = _savedLabel.ForeColor.A - 35;
            if (alpha <= 0)
            {
                _savedTimer.Stop();
                _savedLabel.Visible = false;
                _savedLabel.ForeColor = SavedColor;
                return;
            }

            _savedLabel.ForeColor = Color.FromArgb(alpha, SavedColor);
        }

        private void ShowSavedFeedback()
        {
            if (!ShowHeader)
                return;

            _savedTimer.Stop();
            _savedLabel.ForeColor = SavedColor;
            _savedLabel.Visible = true;
            Task.Delay(1500).ContinueWith(t =>
            {
                if (IsDisposed)
                    return;

                try
                {
                    BeginInvoke((Action)(() => _savedTimer.Start()));
                }
                catch
                {
                }
            });
        }

        private void AnimateTimer_Tick(object sender, EventArgs e)
        {
            _animationStep++;
            float progress = Math.Min(1f, _animationStep / 10f);
            int width = _animationStartSize.Width + (int)Math.Round((_animationTargetSize.Width - _animationStartSize.Width) * progress);
            int height = _animationStartSize.Height + (int)Math.Round((_animationTargetSize.Height - _animationStartSize.Height) * progress);
            ApplyPersistedLayout(new Size(width, height), SizePreset);

            if (progress >= 1f)
            {
                _animateTimer.Stop();
                TriggerResizeComplete();
            }
        }

        private static GraphicsPath GetRoundedRectPath(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            Rectangle arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}

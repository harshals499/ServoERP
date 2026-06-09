using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    internal static class CardResizeGripService
    {
        public const string CornerGripName = "__servoerpCardResizeGrip";
        public const string HeightGripName = "__servoerpCardHeightGrip";
        public const string LockBadgeName = "__servoerpCardLockBadge";
        public const int CornerGripSize = 18;
        public const int HeightGripWidth = 46;
        public const int HeightGripHeight = 14;
        private const int MinimumWidth = 180;
        private const int MinimumHeight = 96;
        private static readonly object Sync = new object();
        private static readonly Dictionary<Control, ResizeState> States = new Dictionary<Control, ResizeState>();
        private static readonly Dictionary<Control, Size> UserSizes = new Dictionary<Control, Size>();

        /// <summary>Attaches a bottom-right resize grip to a card-like control.</summary>
        public static void Attach(Control card, Action<Control, Size> resizeComplete = null, Action<Control, Size> resizeChanging = null)
        {
            if (card == null || card.IsDisposed || card is ResizableCard || card is DashboardDeptCard || IsResizeGrip(card))
                return;
            if (!CardSurfacePolicy.IsDashboardLayoutCard(card) && !CardSurfacePolicy.IsContextMenuCard(card))
            {
                RemoveDetachedGripControls(card);
                return;
            }

            lock (Sync)
            {
                ResizeState existing;
                if (States.TryGetValue(card, out existing))
                {
                    if (resizeComplete != null)
                        existing.ResizeComplete = resizeComplete;
                    if (resizeChanging != null)
                        existing.ResizeChanging = resizeChanging;
                    existing.EnsureAttached();
                    return;
                }

                ResizeState state = new ResizeState(card, resizeComplete, resizeChanging);
                States[card] = state;
                state.Attach();
            }
        }

        public static bool IsResizeGrip(Control control)
        {
            string name = control == null ? null : control.Name;
            return string.Equals(name, CornerGripName, StringComparison.Ordinal) ||
                   string.Equals(name, HeightGripName, StringComparison.Ordinal);
        }

        public static bool HasCornerGrip(Control card)
        {
            return card != null && card.Controls.Cast<Control>().Any(control => string.Equals(control.Name, CornerGripName, StringComparison.Ordinal));
        }

        public static bool HasHeightGrip(Control card)
        {
            return card != null && card.Controls.Cast<Control>().Any(control => string.Equals(control.Name, HeightGripName, StringComparison.Ordinal));
        }

        public static bool HasBothGrips(Control card)
        {
            return HasCornerGrip(card) && HasHeightGrip(card);
        }

        /// <summary>Removes resize grips and resize state from a fixed editor surface.</summary>
        public static void Detach(Control card)
        {
            if (card == null)
                return;

            lock (Sync)
            {
                ResizeState state;
                if (States.TryGetValue(card, out state))
                {
                    state.Detach();
                    States.Remove(card);
                    UserSizes.Remove(card);
                    return;
                }
            }

            RemoveDetachedGripControls(card);
        }

        private static void RemoveDetachedGripControls(Control card)
        {
            if (card == null || card.IsDisposed)
                return;

            foreach (Control child in card.Controls.Cast<Control>().ToList())
            {
                if (string.Equals(child.Name, CornerGripName, StringComparison.Ordinal) ||
                    string.Equals(child.Name, HeightGripName, StringComparison.Ordinal) ||
                    string.Equals(child.Name, LockBadgeName, StringComparison.Ordinal))
                {
                    card.Controls.Remove(child);
                    child.Dispose();
                }
            }
        }

        /// <summary>Enables or disables resize grip use for one card.</summary>
        public static void SetLocked(Control card, bool locked)
        {
            if (card == null)
                return;

            lock (Sync)
            {
                ResizeState state;
                if (States.TryGetValue(card, out state))
                    state.SetLocked(locked);
            }
        }

        /// <summary>Returns true when the card resize grips are locked.</summary>
        public static bool IsLocked(Control card)
        {
            if (card == null)
                return false;

            lock (Sync)
            {
                ResizeState state;
                return States.TryGetValue(card, out state) && state.Locked;
            }
        }

        /// <summary>Returns true after the user has manually resized this card during the current session.</summary>
        public static bool HasUserSize(Control card)
        {
            if (card == null)
                return false;

            lock (Sync)
                return UserSizes.ContainsKey(card);
        }

        /// <summary>Returns the remembered user height, or the supplied default when the card has not been resized.</summary>
        public static int PreferredHeight(Control card, int defaultHeight)
        {
            if (card == null)
                return defaultHeight;

            lock (Sync)
            {
                Size size;
                if (UserSizes.TryGetValue(card, out size) && size.Height > 0)
                    return Math.Max(MinimumHeight, size.Height);
            }

            return defaultHeight;
        }

        /// <summary>Returns the remembered user width, or the supplied default when the card has not been resized.</summary>
        public static int PreferredWidth(Control card, int defaultWidth)
        {
            if (card == null)
                return defaultWidth;

            lock (Sync)
            {
                Size size;
                if (UserSizes.TryGetValue(card, out size) && size.Width > 0)
                    return Math.Max(MinimumWidth, size.Width);
            }

            return defaultWidth;
        }

        public static void PositionCornerGrip(Control card, Control grip, int offset)
        {
            if (card == null || grip == null || card.IsDisposed || grip.IsDisposed)
                return;

            grip.Location = new Point(
                Math.Max(0, card.ClientSize.Width - grip.Width - offset),
                Math.Max(0, card.ClientSize.Height - grip.Height - offset));
            grip.BringToFront();
        }

        public static void PositionHeightGrip(Control card, Control grip, int offset)
        {
            if (card == null || grip == null || card.IsDisposed || grip.IsDisposed)
                return;

            int inset = Math.Max(offset, 6);
            grip.Location = new Point(
                Math.Max(0, (card.ClientSize.Width - grip.Width) / 2),
                Math.Max(0, card.ClientSize.Height - grip.Height - inset));
            grip.BringToFront();
        }

        public static void PaintCornerGrip(Control grip, PaintEventArgs e)
        {
            if (grip == null || e == null)
                return;

            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (SolidBrush brush = new SolidBrush(DS.Slate400))
            {
                int left = grip.Width - 11;
                int top = grip.Height - 11;
                for (int row = 0; row < 3; row++)
                {
                    for (int col = 0; col < 2; col++)
                        e.Graphics.FillEllipse(brush, left + (col * 4), top + (row * 4), 2.1f, 2.1f);
                }
            }
        }

        public static void PaintHeightGrip(Control grip, PaintEventArgs e)
        {
            if (grip == null || e == null)
                return;

            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (SolidBrush brush = new SolidBrush(DS.Slate400))
            {
                int left = (grip.Width - 22) / 2;
                int top = (grip.Height - 5) / 2;
                for (int col = 0; col < 6; col++)
                    e.Graphics.FillEllipse(brush, left + (col * 4), top, 2.1f, 2.1f);
            }
        }

        private sealed class ResizeState
        {
            private readonly Control _card;
            private readonly Panel _grip;
            private readonly Panel _heightGrip;
            private readonly Label _lockBadge;
            private bool _isResizing;
            private bool _heightOnly;
            private bool _restoringUserSize;
            private bool _locked;
            private Point _startMouse;
            private Size _startSize;

            public ResizeState(Control card, Action<Control, Size> resizeComplete, Action<Control, Size> resizeChanging)
            {
                _card = card;
                ResizeComplete = resizeComplete;
                ResizeChanging = resizeChanging;
                _grip = new Panel
                {
                    Name = CornerGripName,
                    Size = new Size(CornerGripSize, CornerGripSize),
                    Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                    BackColor = card.BackColor == Color.Empty ? DS.White : card.BackColor,
                    Cursor = Cursors.SizeNWSE
                };

                _heightGrip = new Panel
                {
                    Name = HeightGripName,
                    Size = new Size(HeightGripWidth, HeightGripHeight),
                    Anchor = AnchorStyles.Bottom,
                    BackColor = card.BackColor == Color.Empty ? DS.White : card.BackColor,
                    Cursor = Cursors.SizeNS
                };

                _lockBadge = new Label
                {
                    Name = LockBadgeName,
                    AutoSize = false,
                    Size = new Size(58, 18),
                    Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    BackColor = DS.Primary600,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                    Text = "Locked",
                    TextAlign = ContentAlignment.MiddleCenter,
                    Enabled = false,
                    Visible = false
                };
            }

            public Action<Control, Size> ResizeComplete { get; set; }
            public Action<Control, Size> ResizeChanging { get; set; }
            public bool Locked { get { return _locked; } }

            /// <summary>Locks or unlocks resize grips for this card.</summary>
            public void SetLocked(bool locked)
            {
                _locked = locked;
                _grip.Visible = !locked;
                _heightGrip.Visible = !locked;
                _grip.Enabled = !locked;
                _heightGrip.Enabled = !locked;
                _lockBadge.Visible = locked;
                PositionGrip();
                if (locked)
                    _lockBadge.BringToFront();
                if (locked)
                    _isResizing = false;
            }

            /// <summary>Wires mouse and paint events required for card resizing.</summary>
            public void Attach()
            {
                _card.MinimumSize = new Size(
                    Math.Max(_card.MinimumSize.Width, MinimumWidth),
                    Math.Max(_card.MinimumSize.Height, MinimumHeight));

                _grip.Paint += Grip_Paint;
                _grip.MouseDown += Grip_MouseDown;
                _grip.MouseMove += Grip_MouseMove;
                _grip.MouseUp += Grip_MouseUp;
                _heightGrip.Paint += HeightGrip_Paint;
                _heightGrip.MouseDown += HeightGrip_MouseDown;
                _heightGrip.MouseMove += Grip_MouseMove;
                _heightGrip.MouseUp += Grip_MouseUp;
                _card.Resize += Card_Resize;
                _card.SizeChanged += Card_SizeChanged;
                _card.ControlAdded += Card_ControlAdded;
                _card.Disposed += Card_Disposed;

                EnsureAttached();
            }

            /// <summary>Unwires and removes resize grip controls from this card.</summary>
            public void Detach()
            {
                _grip.Paint -= Grip_Paint;
                _grip.MouseDown -= Grip_MouseDown;
                _grip.MouseMove -= Grip_MouseMove;
                _grip.MouseUp -= Grip_MouseUp;
                _heightGrip.Paint -= HeightGrip_Paint;
                _heightGrip.MouseDown -= HeightGrip_MouseDown;
                _heightGrip.MouseMove -= Grip_MouseMove;
                _heightGrip.MouseUp -= Grip_MouseUp;
                _card.Resize -= Card_Resize;
                _card.SizeChanged -= Card_SizeChanged;
                _card.ControlAdded -= Card_ControlAdded;
                _card.Disposed -= Card_Disposed;

                if (!_card.IsDisposed)
                {
                    if (_card.Controls.Contains(_heightGrip))
                        _card.Controls.Remove(_heightGrip);
                    if (_card.Controls.Contains(_grip))
                        _card.Controls.Remove(_grip);
                    if (_card.Controls.Contains(_lockBadge))
                        _card.Controls.Remove(_lockBadge);
                }

                _heightGrip.Dispose();
                _grip.Dispose();
                _lockBadge.Dispose();
            }

            /// <summary>Restores missing grip controls after a card clears or rebuilds its children.</summary>
            public void EnsureAttached()
            {
                if (_card.IsDisposed)
                    return;
                if (!CardSurfacePolicy.IsDashboardLayoutCard(_card) && !CardSurfacePolicy.IsContextMenuCard(_card))
                {
                    CardResizeGripService.Detach(_card);
                    return;
                }

                if (!_card.Controls.Contains(_heightGrip))
                    _card.Controls.Add(_heightGrip);
                if (!_card.Controls.Contains(_grip))
                    _card.Controls.Add(_grip);
                if (!_card.Controls.Contains(_lockBadge))
                    _card.Controls.Add(_lockBadge);

                PositionGrip();
                _heightGrip.BringToFront();
                _grip.BringToFront();
                _lockBadge.BringToFront();
                _heightGrip.Visible = !_locked;
                _grip.Visible = !_locked;
                _lockBadge.Visible = _locked;
                _heightGrip.Enabled = !_locked;
                _grip.Enabled = !_locked;
                PreserveImportantHeadingIndex();
            }

            private void Card_ControlAdded(object sender, ControlEventArgs e)
            {
                if (!CardSurfacePolicy.IsDashboardLayoutCard(_card) && !CardSurfacePolicy.IsContextMenuCard(_card))
                {
                    CardResizeGripService.Detach(_card);
                    return;
                }

                if (!_card.IsDisposed && _card.Controls.Contains(_grip))
                {
                    _heightGrip.BringToFront();
                    _grip.BringToFront();
                    _lockBadge.BringToFront();
                    _heightGrip.Visible = !_locked;
                    _grip.Visible = !_locked;
                    _lockBadge.Visible = _locked;
                    PreserveImportantHeadingIndex();
                }
            }

            private void Card_Disposed(object sender, EventArgs e)
            {
                lock (Sync)
                {
                    States.Remove(_card);
                    UserSizes.Remove(_card);
                }
            }

            private void Card_Resize(object sender, EventArgs e)
            {
                PositionGrip();
            }

            private void Card_SizeChanged(object sender, EventArgs e)
            {
                if (_isResizing || _restoringUserSize || _card.IsDisposed)
                    return;

                Size userSize;
                lock (Sync)
                {
                    if (!UserSizes.TryGetValue(_card, out userSize))
                        return;
                }

                Size target = new Size(
                    Math.Max(_card.MinimumSize.Width, userSize.Width),
                    Math.Max(_card.MinimumSize.Height, userSize.Height));
                if (_card.Size == target)
                    return;

                RestoreUserSizeSoon(target);
            }

            private void RestoreUserSizeSoon(Size target)
            {
                if (_card.IsDisposed || !_card.IsHandleCreated)
                    return;

                try
                {
                    _card.BeginInvoke((Action)(() =>
                    {
                        if (_card.IsDisposed || _isResizing)
                            return;

                        _restoringUserSize = true;
                        try
                        {
                            _card.Size = target;
                            _card.Parent?.PerformLayout();
                            PositionGrip();
                        }
                        finally
                        {
                            _restoringUserSize = false;
                        }
                    }));
                }
                catch
                {
                }
            }

            private void PositionGrip()
            {
                if (_card.IsDisposed || _grip.IsDisposed)
                    return;

                PositionCornerGrip(_card, _grip, 2);
                PositionHeightGrip(_card, _heightGrip, 2);
                PositionLockBadge();
            }

            private void PositionLockBadge()
            {
                if (_card.IsDisposed || _lockBadge.IsDisposed)
                    return;

                _lockBadge.Location = new Point(
                    Math.Max(4, _card.ClientSize.Width - _lockBadge.Width - 10),
                    8);
            }

            private void PreserveImportantHeadingIndex()
            {
                foreach (Control control in _card.Controls)
                {
                    Label label = control as Label;
                    if (label != null && string.Equals(label.Text, "Quote Summary", StringComparison.Ordinal))
                    {
                        _card.Controls.SetChildIndex(label, 0);
                        return;
                    }
                }
            }

            private void Grip_MouseDown(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left)
                    return;
                if (_locked)
                    return;

                _isResizing = true;
                _heightOnly = false;
                _startMouse = Control.MousePosition;
                _startSize = _card.Size;
                _grip.Capture = true;
            }

            private void HeightGrip_MouseDown(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left)
                    return;
                if (_locked)
                    return;

                _isResizing = true;
                _heightOnly = true;
                _startMouse = Control.MousePosition;
                _startSize = _card.Size;
                _heightGrip.Capture = true;
            }

            private void Grip_MouseMove(object sender, MouseEventArgs e)
            {
                if (!_isResizing)
                    return;

                Point mouse = Control.MousePosition;
                int width = _heightOnly ? _startSize.Width : Math.Max(_card.MinimumSize.Width, _startSize.Width + mouse.X - _startMouse.X);
                int height = Math.Max(_card.MinimumSize.Height, _startSize.Height + mouse.Y - _startMouse.Y);
                _card.Size = new Size(width, height);
                RememberUserSize(_card, _card.Size);
                ResizeChanging?.Invoke(_card, _card.Size);
                _card.Parent?.PerformLayout();
                PositionGrip();
            }

            private void Grip_MouseUp(object sender, MouseEventArgs e)
            {
                if (!_isResizing)
                    return;

                _isResizing = false;
                _heightOnly = false;
                _grip.Capture = false;
                _heightGrip.Capture = false;
                RememberUserSize(_card, _card.Size);
                ResizeComplete?.Invoke(_card, _card.Size);
            }

            private static void RememberUserSize(Control card, Size size)
            {
                if (card == null || card.IsDisposed)
                    return;

                lock (Sync)
                    UserSizes[card] = new Size(Math.Max(MinimumWidth, size.Width), Math.Max(MinimumHeight, size.Height));
            }

            private void Grip_Paint(object sender, PaintEventArgs e)
            {
                PaintCornerGrip(_grip, e);
            }

            private void HeightGrip_Paint(object sender, PaintEventArgs e)
            {
                PaintHeightGrip(_heightGrip, e);
            }
        }
    }
}

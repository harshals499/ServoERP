using System;
using System.Drawing;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    // One reusable, non-activating preview for all dashboard cards and their children.
    // Explicit placement keeps the preview inside the active monitor, including edge cards.
    internal sealed class DashboardHoverPreview : IDisposable
    {
        private readonly ToolTip _tip = new ToolTip
        {
            OwnerDraw = true, InitialDelay = int.MaxValue, ReshowDelay = int.MaxValue,
            AutoPopDelay = 15000, ShowAlways = false, UseAnimation = false, UseFading = false
        };
        private readonly Font _font = new Font("Segoe UI", 9f);
        private readonly Timer _hoverTimer = new Timer { Interval = 550 };
        private Control _hoverCard;
        private string _hoverText;
        private string _visibleText;

        private Size PreviewSize(string text)
        {
            Size size = TextRenderer.MeasureText(text, _font, new Size(390, 600), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            return new Size(420, size.Height + 32);
        }

        private void ShowPreview(Control card, string text)
        {
            if (card == null || card.IsDisposed || !card.Visible) return;
            Rectangle area = Screen.FromControl(card).WorkingArea;
            Size size = PreviewSize(text);
            Point cursor = Cursor.Position;
            Point screenPoint = new Point(Math.Max(area.Left + 8, Math.Min(cursor.X + 14, area.Right - size.Width - 8)),
                Math.Max(area.Top + 8, Math.Min(cursor.Y + 22, area.Bottom - size.Height - 8)));
            _visibleText = text;
            _tip.Show(text, card, card.PointToClient(screenPoint), 15000);
        }

        internal DashboardHoverPreview()
        {
            _hoverTimer.Tick += (s, e) =>
            {
                _hoverTimer.Stop();
                if (_hoverCard != null && !_hoverCard.IsDisposed && _hoverCard.RectangleToScreen(_hoverCard.ClientRectangle).Contains(Cursor.Position))
                    ShowPreview(_hoverCard, _hoverText);
            };
            _tip.Popup += (s, e) =>
            {
                e.ToolTipSize = PreviewSize(_visibleText ?? string.Empty);
            };
            _tip.Draw += (s, e) =>
            {
                using (var brush = new SolidBrush(Color.FromArgb(253, 253, 251))) e.Graphics.FillRectangle(brush, e.Bounds);
                using (var pen = new Pen(Color.FromArgb(218, 224, 221)))
                    e.Graphics.DrawRectangle(pen, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
                TextRenderer.DrawText(e.Graphics, e.ToolTipText, _font,
                    new Rectangle(15, 14, e.Bounds.Width - 30, e.Bounds.Height - 28),
                    Color.FromArgb(49, 61, 70), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            };
        }

        internal void Attach(Control card, string title, string description, string rows)
        {
            string text = title + "\n\n" + description + "\n\n" + rows;
            card.AccessibleName = title;
            card.AccessibleDescription = description + " " + rows;
            Bind(card, card, text);
        }

        private void Bind(Control control, Control card, string text)
        {
            // Do not register automatic native tips: they can later replace the bounded
            // tracked popup with an unclamped popup at the mouse position.
            bool interactive = control is Button || control is ComboBox;
            control.MouseEnter += (s, e) =>
            {
                if (interactive) { _hoverTimer.Stop(); _tip.Hide(card); _hoverCard = null; return; }
                if (_hoverCard == card) return;
                if (_hoverCard != null && !_hoverCard.IsDisposed) _tip.Hide(_hoverCard);
                _hoverCard = card; _hoverText = text;
                _hoverTimer.Stop(); _hoverTimer.Start();
            };
            control.MouseLeave += (s, e) =>
            {
                if (!card.IsDisposed && card.RectangleToScreen(card.ClientRectangle).Contains(Cursor.Position)) return;
                _hoverTimer.Stop(); _tip.Hide(card); _hoverCard = null;
            };
            control.MouseDown += (s, e) => { _hoverTimer.Stop(); _tip.Hide(card); _hoverCard = null; };
            if (control.TabStop)
            {
                // F1 offers the same preview to keyboard users without showing a popup
                // merely because Windows assigns initial focus when the page opens.
                control.KeyDown += (s, e) => { if (e.KeyCode == Keys.F1) { ShowPreview(card, text); e.Handled = true; } };
                control.Leave += (s, e) => _tip.Hide(card);
            }
            foreach (Control child in control.Controls) Bind(child, card, text);
        }

        public void Dispose() { _hoverTimer.Stop(); _hoverTimer.Dispose(); _tip.Dispose(); _font.Dispose(); }
    }
}

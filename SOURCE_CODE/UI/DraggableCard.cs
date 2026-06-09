using System;
using System.Drawing;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    public class DraggableCard : Panel
    {
        public const int GridSize = 10;
        private bool _dragging;
        private Point _startMouse;
        private Point _startLocation;

        public DraggableCard()
        {
            BackColor = DS.White;
            MinimumSize = new Size(180, 96);
            Margin = new Padding(0, 0, 12, 12);
            Cursor = Cursors.SizeAll;
            MouseDown += CardMouseDown;
            MouseMove += CardMouseMove;
            MouseUp += CardMouseUp;
            CardResizeGripService.Attach(this);
        }

        public string CardKey { get; set; }
        public string PageKey { get; set; }

        private void CardMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            _dragging = true;
            _startMouse = Control.MousePosition;
            _startLocation = Location;
            Capture = true;
            BringToFront();
        }

        private void CardMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging || Parent == null)
                return;

            Point mouse = Control.MousePosition;
            int x = Snap(_startLocation.X + mouse.X - _startMouse.X);
            int y = Snap(_startLocation.Y + mouse.Y - _startMouse.Y);
            x = Math.Max(0, Math.Min(x, Parent.ClientSize.Width - Width));
            y = Math.Max(0, y);
            Location = new Point(x, y);
        }

        private void CardMouseUp(object sender, MouseEventArgs e)
        {
            if (!_dragging)
                return;

            _dragging = false;
            Capture = false;
        }

        public static int Snap(int value)
        {
            return (int)Math.Round(value / (double)GridSize) * GridSize;
        }
    }
}

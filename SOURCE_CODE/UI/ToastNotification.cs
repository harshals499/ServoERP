using System;
using System.Drawing;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class ToastNotification : ServoERP.Infrastructure.ServoFormBase
    {
        private readonly Timer _lifeTimer;
        private readonly Timer _fadeTimer;
        private readonly Timer _slideTimer;
        private readonly int _targetTop;

        /// <summary>Shows a non-blocking toast message near the bottom-right of the current screen.</summary>
        public static void ShowToast(string message, Color accentColor)
        {
            try
            {
                var toast = new ToastNotification(message, accentColor);
                toast.Show();
            }
            catch
            {
            }
        }

        /// <summary>Shows a non-blocking toast message owned by an existing WinForms window.</summary>
        public static void Show(IWin32Window owner, string message, Color accentColor)
        {
            try
            {
                var toast = new ToastNotification(message, accentColor);
                toast.Show(owner);
            }
            catch
            {
            }
        }

        /// <summary>Initializes a slide-in toast notification.</summary>
        private ToastNotification(string message, Color accentColor)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(390, 92);
            BackColor = Color.White;
            Opacity = 0.96;

            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            _targetTop = work.Bottom - Height - 22;
            Location = new Point(work.Right - Width - 22, work.Bottom + 8);

            Panel accent = new Panel { Dock = DockStyle.Left, Width = 6, BackColor = accentColor };
            Label title = new Label
            {
                Text = "ServoERP",
                Location = new Point(22, 14),
                Size = new Size(330, 22),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = DS.Slate900
            };
            Label body = new Label
            {
                Text = message ?? string.Empty,
                Location = new Point(22, 38),
                Size = new Size(340, 38),
                Font = new Font("Segoe UI", 9f),
                ForeColor = DS.Slate700
            };

            Controls.Add(accent);
            Controls.Add(title);
            Controls.Add(body);

            _slideTimer = new Timer { Interval = 12 };
            _slideTimer.Tick += (s, e) =>
            {
                Top = Math.Max(_targetTop, Top - 18);
                if (Top <= _targetTop)
                    _slideTimer.Stop();
            };

            _lifeTimer = new Timer { Interval = 4000 };
            _lifeTimer.Tick += (s, e) =>
            {
                _lifeTimer.Stop();
                _fadeTimer.Start();
            };

            _fadeTimer = new Timer { Interval = 40 };
            _fadeTimer.Tick += (s, e) =>
            {
                Opacity -= 0.08;
                if (Opacity <= 0.05)
                {
                    _fadeTimer.Stop();
                    Close();
                }
            };

            Shown += (s, e) =>
            {
                _slideTimer.Start();
                _lifeTimer.Start();
            };
        }

        /// <summary>Releases timer resources used by the toast animation.</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _lifeTimer?.Dispose();
                _fadeTimer?.Dispose();
                _slideTimer?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}


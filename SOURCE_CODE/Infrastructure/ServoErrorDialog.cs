using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ServoERP.Infrastructure
{
    /// <summary>Friendly in-app error dialog backed by the ServoERP exception log.</summary>
    public class ServoErrorDialog : Form
    {
        private Label _lblTitle;
        private Label _lblMessage;
        private TextBox _txtDetail;
        private Button _btnOK;
        private Button _btnOpenLog;
        private PictureBox _iconBox;

        /// <summary>Creates an error dialog with user-safe message text and technical details.</summary>
        public ServoErrorDialog(string userMessage, Exception ex = null)
        {
            InitializeLayout(userMessage, ex);
        }

        /// <summary>Builds the error dialog layout.</summary>
        private void InitializeLayout(string userMessage, Exception ex)
        {
            Text = "ServoERP - Unexpected Error";
            Size = new Size(520, 340);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9f);

            _iconBox = new PictureBox
            {
                Location = new Point(20, 20),
                Size = new Size(40, 40),
                Image = SystemIcons.Warning.ToBitmap(),
                SizeMode = PictureBoxSizeMode.StretchImage
            };

            _lblTitle = new Label
            {
                Text = "Something went wrong",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                Location = new Point(70, 20),
                Size = new Size(420, 28),
                ForeColor = Color.FromArgb(180, 30, 30)
            };

            _lblMessage = new Label
            {
                Text = userMessage,
                Location = new Point(70, 52),
                Size = new Size(420, 44),
                ForeColor = Color.FromArgb(60, 60, 60)
            };

            _txtDetail = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Location = new Point(20, 110),
                Size = new Size(470, 140),
                BackColor = Color.FromArgb(245, 245, 245),
                ForeColor = Color.FromArgb(80, 80, 80),
                Font = new Font("Consolas", 8f),
                Text = ex != null
                    ? ex.GetType().Name + ": " + ex.Message + "\r\n\r\n" + ex.StackTrace
                    : "No additional details available.",
                BorderStyle = BorderStyle.FixedSingle
            };

            _btnOK = new Button
            {
                Text = "OK",
                Location = new Point(390, 265),
                Size = new Size(100, 32),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.OK
            };
            _btnOK.FlatAppearance.BorderSize = 0;

            _btnOpenLog = new Button
            {
                Text = "Open Log File",
                Location = new Point(270, 265),
                Size = new Size(110, 32),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(0, 100, 180)
            };
            _btnOpenLog.FlatAppearance.BorderSize = 0;
            _btnOpenLog.Click += (s, e) =>
            {
                string logPath = ExceptionLogger.CurrentLogPath();
                if (logPath != null)
                {
                    Process.Start("notepad.exe", logPath);
                }
                else
                {
                    MessageBox.Show("No log file found for this month.", "ServoERP", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            Controls.AddRange(new Control[] { _iconBox, _lblTitle, _lblMessage, _txtDetail, _btnOK, _btnOpenLog });
            AcceptButton = _btnOK;
        }

        /// <summary>Shows the error dialog safely from any thread.</summary>
        public static void Show(Control owner, string userMessage, Exception ex = null)
        {
            ExceptionLogger.Log(ex, userMessage);
            UIThread.Run(owner, () =>
            {
                using (var dlg = new ServoErrorDialog(userMessage, ex))
                    dlg.ShowDialog(owner);
            });
        }
    }
}

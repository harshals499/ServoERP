using System;
using System.Drawing;
using System.Windows.Forms;

namespace ServoERP.Infrastructure
{
    /// <summary>Use for destructive or irreversible actions; use MessageBox.Show for simple alerts.</summary>
    public class ServoConfirmDialog : Form
    {
        /// <summary>Shows a destructive-action confirmation dialog and returns true when confirmed.</summary>
        public static bool Show(Control owner, string action, string detail)
        {
            using (var dlg = new ServoConfirmDialog(action, detail))
            {
                dlg.ShowDialog(owner);
                return dlg.Confirmed;
            }
        }

        /// <summary>Gets whether the user confirmed the action.</summary>
        public bool Confirmed { get; private set; } = false;

        /// <summary>Creates a confirmation dialog with action and detail text.</summary>
        private ServoConfirmDialog(string action, string detail)
        {
            Text = "Confirm - ServoERP";
            Size = new Size(420, 200);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.White;

            var lblAction = new Label
            {
                Text = action,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(380, 26),
                ForeColor = Color.FromArgb(180, 30, 30)
            };

            var lblDetail = new Label
            {
                Text = detail,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(20, 50),
                Size = new Size(380, 60),
                ForeColor = Color.FromArgb(60, 60, 60)
            };

            var btnConfirm = new Button
            {
                Text = "Yes, confirm",
                Location = new Point(290, 130),
                Size = new Size(110, 32),
                BackColor = Color.FromArgb(180, 30, 30),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnConfirm.FlatAppearance.BorderSize = 0;
            btnConfirm.Click += (s, e) => { Confirmed = true; Close(); };

            var btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(170, 130),
                Size = new Size(110, 32),
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.Click += (s, e) => { Confirmed = false; Close(); };

            Controls.AddRange(new Control[]
                { lblAction, lblDetail, btnConfirm, btnCancel });
        }
    }
}

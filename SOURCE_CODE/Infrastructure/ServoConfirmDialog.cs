using System;
using System.Drawing;
using System.Windows.Forms;

namespace ServoERP.Infrastructure
{
    /// <summary>Use for destructive or irreversible actions; use MessageBox.Show for simple alerts.</summary>
    public class ServoConfirmDialog : ServoFormBase
    {
        /// <summary>Shows a destructive-action confirmation dialog and returns true when confirmed.</summary>
        public static bool Show(Control owner, string action, string detail)
        {
            return Show(owner as IWin32Window, action, detail);
        }

        /// <summary>Shows a destructive-action confirmation dialog and returns true when confirmed.</summary>
        public static bool Show(IWin32Window owner, string action, string detail)
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
            ClientSize = new Size(440, 206);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.White;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(20),
                BackColor = Color.White
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));

            var lblAction = new Label
            {
                Text = action,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(180, 30, 30),
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var lblDetail = new Label
            {
                Text = detail,
                Font = new Font("Segoe UI", 9f),
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(60, 60, 60),
                AutoEllipsis = true,
                TextAlign = ContentAlignment.TopLeft
            };

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = Color.White,
                Padding = new Padding(0, 8, 0, 0)
            };

            var btnConfirm = new Button
            {
                Text = "Yes, confirm",
                Size = new Size(110, 32),
                BackColor = Color.FromArgb(180, 30, 30),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.OK
            };
            btnConfirm.FlatAppearance.BorderSize = 0;
            btnConfirm.Click += (s, e) => { Confirmed = true; Close(); };

            var btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(110, 32),
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.Cancel,
                Margin = new Padding(8, 0, 0, 0)
            };
            btnCancel.Click += (s, e) => { Confirmed = false; Close(); };

            actions.Controls.Add(btnConfirm);
            actions.Controls.Add(btnCancel);
            layout.Controls.Add(lblAction, 0, 0);
            layout.Controls.Add(lblDetail, 0, 1);
            layout.Controls.Add(actions, 0, 2);
            Controls.Add(layout);
            AcceptButton = btnConfirm;
            CancelButton = btnCancel;
        }
    }
}

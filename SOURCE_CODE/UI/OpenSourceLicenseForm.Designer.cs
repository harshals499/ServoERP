using System.Drawing;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    public partial class OpenSourceLicenseForm
    {
        private DataGridView _gridComponents;
        private TextBox _txtDisclosure;
        private Label _lblStatus;

        /// <summary>Initializes open-source disclosure controls.</summary>
        private void InitializeComponent()
        {
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = DS.BgPage;
            ClientSize = new Size(1040, 720);
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "ServoERP - Open Source & Licenses";

            Label title = new Label
            {
                Text = "Open Source & Licenses",
                Location = new Point(24, 18),
                Size = new Size(460, 34),
                Font = DS.H1,
                ForeColor = DS.Slate900
            };

            Label subtitle = new Label
            {
                Text = "Review and export third-party component disclosures for procurement, audits, and client IT review.",
                Location = new Point(26, 52),
                Size = new Size(920, 28),
                Font = DS.Body,
                ForeColor = DS.Slate600
            };

            _gridComponents = new DataGridView
            {
                Location = new Point(24, 96),
                Size = new Size(992, 260),
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            DS.StyleGrid(_gridComponents);

            _txtDisclosure = new TextBox
            {
                Location = new Point(24, 374),
                Size = new Size(992, 238),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = DS.Mono,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                ForeColor = DS.Slate800
            };

            Button export = Button("Export Disclosure", DS.Green600, Color.White, 154);
            export.Location = new Point(24, 632);
            export.Click += ExportDisclosure;

            Button copy = Button("Copy Text", DS.Primary600, Color.White, 112);
            copy.Location = new Point(190, 632);
            copy.Click += CopyDisclosure;

            Button openSource = Button("Open Source URL", Color.White, DS.Slate700, 144);
            openSource.FlatAppearance.BorderColor = DS.Border;
            openSource.FlatAppearance.BorderSize = 1;
            openSource.Location = new Point(314, 632);
            openSource.Click += OpenSelectedSource;

            Button close = Button("Close", Color.White, DS.Slate700, 96);
            close.FlatAppearance.BorderColor = DS.Border;
            close.FlatAppearance.BorderSize = 1;
            close.Location = new Point(920, 632);
            close.Click += CloseClicked;

            _lblStatus = new Label
            {
                Text = "Ready.",
                Location = new Point(476, 638),
                Size = new Size(420, 24),
                Font = DS.Small,
                ForeColor = DS.Slate600,
                TextAlign = ContentAlignment.MiddleLeft
            };

            Controls.AddRange(new Control[] { title, subtitle, _gridComponents, _txtDisclosure, export, copy, openSource, _lblStatus, close });
            ResumeLayout(false);
        }

        /// <summary>Creates a styled command button.</summary>
        private static Button Button(string text, Color backColor, Color foreColor, int width)
        {
            Button button = new Button
            {
                Text = text,
                Size = new Size(width, 34),
                BackColor = backColor,
                ForeColor = foreColor,
                FlatStyle = FlatStyle.Flat,
                Font = DS.BodyBold,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = backColor == Color.White ? 1 : 0;
            return button;
        }
    }
}

using System.Drawing;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public partial class BackupSettingsForm
    {
        private TextBox _txtNetworkPath;
        private TextBox _txtLocalPath;
        private Label _lblNetworkStatus;
        private DateTimePicker _timeSchedule;
        private CheckBox _chkRunOnClose;
        private CheckBox _chkEnabled;
        private NumericUpDown _numRetention;
        private Button _btnBackupNow;
        private Button _btnSave;
        private Button _btnClose;
        private ProgressBar _progress;
        private Label _lblStatus;
        private Label _lblLastBackup;
        private DataGridView _gridLog;

        /// <summary>Initializes backup settings controls.</summary>
        private void InitializeComponent()
        {
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = DS.BgPage;
            ClientSize = new Size(980, 820);
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = T("ServoERP - Backup & Recovery");

            Label title = new Label
            {
                Text = T("Backup & Recovery"),
                Location = new Point(24, 18),
                Size = new Size(460, 34),
                Font = DS.H1,
                ForeColor = DS.Slate900
            };
            Label subtitle = new Label
            {
                Text = T("Backups stay on your own network, local machine, or external drive. ServoERP never sends client data to Harshal or servoerp.in."),
                Location = new Point(26, 52),
                Size = new Size(900, 34),
                Font = DS.Body,
                ForeColor = DS.Slate600
            };

            Panel network = Section(T("Network Server Connection"), 24, 96, 444, 146);
            Label networkLabel = Label(T("Network Server Path (UNC)"), 16, 18, 250);
            _txtNetworkPath = Input(16, 42, 296);
            _txtNetworkPath.Text = @"\\SERVERNAME\SharedFolder\ServoERP_Backups";
            Button test = Button(T("Test Connection"), DS.Primary600, Color.White, 128);
            test.Location = new Point(324, 41);
            test.Click += TestNetworkConnection;
            _lblNetworkStatus = new Label
            {
                Text = T("Leave blank to skip network backup"),
                Location = new Point(16, 86),
                Size = new Size(390, 38),
                Font = DS.Small,
                ForeColor = DS.Slate600
            };
            network.Controls.AddRange(new Control[] { networkLabel, _txtNetworkPath, test, _lblNetworkStatus });

            Panel local = Section(T("Local Backup Folder"), 492, 96, 444, 146);
            _txtLocalPath = Input(16, 42, 296);
            Button browse = Button(T("Browse"), DS.Primary600, Color.White, 96);
            browse.Location = new Point(324, 41);
            browse.Click += BrowseLocalFolder;
            local.Controls.Add(Label(T("Local Backup Folder"), 16, 18, 250));
            local.Controls.Add(_txtLocalPath);
            local.Controls.Add(browse);

            Panel schedule = Section(T("Schedule"), 24, 260, 444, 164);
            _timeSchedule = new DateTimePicker
            {
                Location = new Point(16, 42),
                Size = new Size(118, 28),
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "HH:mm",
                ShowUpDown = true
            };
            _chkRunOnClose = Check(T("Run backup when app closes (if not already run today)"), 16, 82, 390);
            _chkEnabled = Check(T("Enable automatic backups"), 16, 114, 250);
            schedule.Controls.Add(Label(T("Daily Backup Time"), 16, 18, 200));
            schedule.Controls.Add(_timeSchedule);
            schedule.Controls.Add(_chkRunOnClose);
            schedule.Controls.Add(_chkEnabled);

            Panel retention = Section(T("Retention"), 492, 260, 444, 164);
            _numRetention = new NumericUpDown
            {
                Location = new Point(16, 42),
                Size = new Size(86, 28),
                Minimum = 1,
                Maximum = 365,
                Value = 30
            };
            retention.Controls.Add(Label(T("Keep backups for"), 16, 18, 180));
            retention.Controls.Add(_numRetention);
            retention.Controls.Add(new Label
            {
                Text = T("days (older backups will be deleted automatically)"),
                Location = new Point(112, 45),
                Size = new Size(300, 24),
                Font = DS.Body,
                ForeColor = DS.Slate700
            });

            Panel manual = Section(T("Manual Backup & Status"), 24, 442, 912, 112);
            _btnBackupNow = Button(T("Backup Now"), DS.Green600, Color.White, 126);
            _btnBackupNow.Location = new Point(16, 38);
            _btnBackupNow.Click += RunManualBackup;
            Button open = Button(T("Open Backup Folder"), DS.Primary600, Color.White, 160);
            open.Location = new Point(154, 38);
            open.Click += OpenBackupFolder;
            _progress = new ProgressBar { Location = new Point(330, 43), Size = new Size(196, 18), Style = ProgressBarStyle.Blocks };
            _lblLastBackup = new Label { Location = new Point(544, 34), Size = new Size(330, 24), Font = DS.BodyBold, ForeColor = DS.Slate800 };
            _lblStatus = new Label { Location = new Point(544, 60), Size = new Size(330, 28), Font = DS.Small, ForeColor = DS.Slate600 };
            manual.Controls.AddRange(new Control[] { _btnBackupNow, open, _progress, _lblLastBackup, _lblStatus });

            Panel logPanel = Section(T("Backup Log"), 24, 572, 912, 184);
            _gridLog = new DataGridView
            {
                Location = new Point(16, 28),
                Size = new Size(748, 138),
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            DS.StyleGrid(_gridLog);
            Button clear = Button(T("Clear Log"), Color.White, DS.Slate700, 112);
            clear.FlatAppearance.BorderColor = DS.Border;
            clear.FlatAppearance.BorderSize = 1;
            clear.Location = new Point(780, 80);
            clear.Click += ClearLog;
            logPanel.Controls.Add(_gridLog);
            logPanel.Controls.Add(clear);

            _btnSave = Button(T("Save Settings"), DS.Green600, Color.White, 132);
            _btnSave.Location = new Point(676, 774);
            _btnSave.Click += SaveClicked;
            _btnClose = Button(T("Close"), Color.White, DS.Slate700, 96);
            _btnClose.FlatAppearance.BorderColor = DS.Border;
            _btnClose.FlatAppearance.BorderSize = 1;
            _btnClose.Location = new Point(824, 774);
            _btnClose.Click += CloseClicked;

            Controls.AddRange(new Control[] { title, subtitle, network, local, schedule, retention, manual, logPanel, _btnSave, _btnClose });
            ResumeLayout(false);
        }

        /// <summary>Creates a card-like section panel.</summary>
        private static Panel Section(string title, int x, int y, int width, int height)
        {
            Panel panel = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(width, height),
                BackColor = Color.White,
                Padding = new Padding(12)
            };
            panel.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
            };
            panel.Controls.Add(new Label
            {
                Text = title,
                Location = new Point(12, 0),
                Size = new Size(width - 24, 18),
                Font = DS.BodyBold,
                ForeColor = DS.Slate900
            });
            return panel;
        }

        /// <summary>Creates a field label.</summary>
        private static Label Label(string text, int x, int y, int width)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 20),
                Font = DS.SmallBold,
                ForeColor = DS.Slate600
            };
        }

        /// <summary>Creates a styled text input.</summary>
        private static TextBox Input(int x, int y, int width)
        {
            return new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(width, 28),
                BorderStyle = BorderStyle.FixedSingle,
                Font = DS.Body
            };
        }

        /// <summary>Creates a settings checkbox.</summary>
        private static CheckBox Check(string text, int x, int y, int width)
        {
            return new CheckBox
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 24),
                Font = DS.Body,
                ForeColor = DS.Slate700,
                BackColor = Color.White
            };
        }

        /// <summary>Creates a styled button.</summary>
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

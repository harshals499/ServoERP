using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.ComponentModel;
using System.Windows.Forms;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public partial class BackupSettingsForm : ServoERP.Infrastructure.ServoFormBase
    {
        private readonly BackupService _backupService = new BackupService();
        private BackupResult _lastManualResult;

        /// <summary>Initializes the backup settings screen.</summary>
        public BackupSettingsForm()
        {
            InitializeComponent();
            LoadSettings();
            RefreshBackupLog();
        }

        /// <summary>Loads persisted backup settings into the form.</summary>
        private void LoadSettings()
        {
            _backupService.EnsureBackupInfrastructure();
            _txtNetworkPath.Text = DbSettings.Get("BackupNetworkPath", string.Empty);
            _txtLocalPath.Text = DbSettings.Get("BackupLocalPath", BackupService.DefaultLocalBackupPath);
            _timeSchedule.Value = DateTime.Today.Add(ParseSchedule(DbSettings.Get("BackupScheduledTime", "18:00")));
            _chkRunOnClose.Checked = ParseBool(DbSettings.Get("BackupRunOnClose", "true"), true);
            _chkEnabled.Checked = ParseBool(DbSettings.Get("BackupEnabled", "true"), true);

            int days;
            _numRetention.Value = int.TryParse(DbSettings.Get("BackupRetentionDays", "30"), out days)
                ? Math.Max(_numRetention.Minimum, Math.Min(_numRetention.Maximum, days))
                : 30;
            RefreshLastBackupLabel();
        }

        /// <summary>Saves backup preferences to UserSettings.</summary>
        private void SaveSettings()
        {
            DbSettings.Set("BackupNetworkPath", _txtNetworkPath.Text.Trim());
            DbSettings.Set("BackupLocalPath", string.IsNullOrWhiteSpace(_txtLocalPath.Text) ? BackupService.DefaultLocalBackupPath : _txtLocalPath.Text.Trim());
            DbSettings.Set("BackupScheduledTime", _timeSchedule.Value.ToString("HH:mm"));
            DbSettings.Set("BackupRetentionDays", ((int)_numRetention.Value).ToString());
            DbSettings.Set("BackupRunOnClose", _chkRunOnClose.Checked ? "true" : "false");
            DbSettings.Set("BackupEnabled", _chkEnabled.Checked ? "true" : "false");
            SetStatus(T("Backup settings saved."), DS.Green600);
        }

        /// <summary>Tests whether the configured network path is reachable.</summary>
        private void TestNetworkConnection(object sender, EventArgs e)
        {
            string path = _txtNetworkPath.Text.Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                _lblNetworkStatus.Text = T("Network backup skipped.");
                _lblNetworkStatus.ForeColor = DS.Slate600;
                return;
            }

            bool connected = Directory.Exists(path);
            _lblNetworkStatus.Text = connected ? T("Connected") : T("Unreachable - check path or network");
            _lblNetworkStatus.ForeColor = connected ? DS.Green600 : DS.Red600;
        }

        /// <summary>Opens a folder picker for the local backup folder.</summary>
        private void BrowseLocalFolder(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = T("Select the local ServoERP backup folder");
                dialog.SelectedPath = Directory.Exists(_txtLocalPath.Text) ? _txtLocalPath.Text : @"C:\";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    _txtLocalPath.Text = dialog.SelectedPath;
            }
        }

        /// <summary>Starts a manual backup from the UI.</summary>
        private void RunManualBackup(object sender, EventArgs e)
        {
            SaveSettings();
            ToggleBackupUi(false);
            _progress.Style = ProgressBarStyle.Marquee;
            SetStatus(T("Creating manual backup..."), DS.Slate700);

            var worker = CreateWorker();
            worker.DoWork += (s, args) => args.Result = _backupService.RunBackup(BackupTrigger.Manual);
            worker.RunWorkerCompleted += (s, args) =>
            {
                if (args.Error != null)
                {
                    RunOnUI(() =>
                    {
                        worker.Dispose();
                        _progress.Style = ProgressBarStyle.Blocks;
                        ToggleBackupUi(true);
                        SetStatus(string.Format(T("Backup failed: {0}"), args.Error.Message), DS.Red600);
                        ToastNotification.ShowToast(T("Backup failed - please check settings"), DS.Red600);
                        RefreshBackupLog();
                    });
                    ShowError(T("Manual backup failed. Please check backup settings."), args.Error);
                    return;
                }
                if (args.Cancelled) return;

                RunOnUI(() =>
                {
                    worker.Dispose();
                    _progress.Style = ProgressBarStyle.Blocks;
                    ToggleBackupUi(true);

                    _lastManualResult = args.Result as BackupResult;
                    if (_lastManualResult != null && _lastManualResult.Success)
                    {
                        SetStatus(string.Format(T("Backup completed - saved to {0}"), FriendlyDestination(_lastManualResult.DestinationUsed)), DS.Green600);
                        ToastNotification.ShowToast(string.Format(T("Backup completed - saved to {0}"), FriendlyDestination(_lastManualResult.DestinationUsed)), DS.Green600);
                    }
                    else
                    {
                        string message = _lastManualResult == null ? T("Unknown backup failure.") : _lastManualResult.Message;
                        SetStatus(string.Format(T("Backup failed: {0}"), message), DS.Red600);
                        ToastNotification.ShowToast(T("Backup failed - please check settings"), DS.Red600);
                    }

                    RefreshLastBackupLabel();
                    RefreshBackupLog();
                });
            };
            worker.RunWorkerAsync();
        }

        /// <summary>Opens the last successful backup destination in Windows Explorer.</summary>
        private void OpenBackupFolder(object sender, EventArgs e)
        {
            try
            {
                string folder = ResolveLastBackupFolder();
                if (string.IsNullOrWhiteSpace(folder))
                    folder = _txtLocalPath.Text.Trim();

                Directory.CreateDirectory(folder);
                Process.Start("explorer.exe", folder);
            }
            catch (Exception ex)
            {
                SetStatus(string.Format(T("Could not open backup folder: {0}"), ex.Message), DS.Red600);
            }
        }

        /// <summary>Clears persisted backup log rows.</summary>
        private void ClearLog(object sender, EventArgs e)
        {
            _backupService.ClearBackupLog();
            RefreshBackupLog();
            RefreshLastBackupLabel();
            SetStatus(T("Backup log cleared."), DS.Slate700);
        }

        /// <summary>Refreshes the backup log table.</summary>
        private void RefreshBackupLog()
        {
            var rows = _backupService.GetBackupLog(20)
                .Select(r => new
                {
                    DateTime = r.BackupTime.ToString("dd/MM/yyyy HH:mm"),
                    r.Trigger,
                    r.Destination,
                    Status = r.Success ? T("Success") : T("Failed"),
                    FileSize = r.FileSizeKB > 0 ? r.FileSizeKB.ToString("N0") + " KB" : "-"
                })
                .ToList();

            _gridLog.DataSource = rows;
        }

        /// <summary>Updates the last successful backup label.</summary>
        private void RefreshLastBackupLabel()
        {
            BackupLogEntry latest = _backupService.GetBackupLog(50).FirstOrDefault(r => r.Success);
            _lblLastBackup.Text = latest == null
                ? T("Last backup: no successful backup found")
                : string.Format(T("Last backup: {0} to {1}"), latest.BackupTime.ToString("dd/MM/yyyy HH:mm"), FriendlyDestination(latest.Destination));
        }

        /// <summary>Resolves the folder containing the last successful backup.</summary>
        private string ResolveLastBackupFolder()
        {
            BackupLogEntry latest = _backupService.GetBackupLog(50).FirstOrDefault(r => r.Success && !string.IsNullOrWhiteSpace(r.FilePath));
            if (latest == null)
                return string.Empty;

            return Path.GetDirectoryName(latest.FilePath);
        }

        /// <summary>Enables or disables backup controls while a backup is running.</summary>
        private void ToggleBackupUi(bool enabled)
        {
            _btnBackupNow.Enabled = enabled;
            _btnSave.Enabled = enabled;
            _btnClose.Enabled = enabled;
        }

        /// <summary>Shows an inline status message.</summary>
        private void SetStatus(string text, Color color)
        {
            _lblStatus.Text = text;
            _lblStatus.ForeColor = color;
        }

        /// <summary>Parses the schedule time from UserSettings.</summary>
        private static TimeSpan ParseSchedule(string value)
        {
            TimeSpan parsed;
            return TimeSpan.TryParse(value, out parsed) ? parsed : new TimeSpan(18, 0, 0);
        }

        /// <summary>Parses a boolean UserSettings value.</summary>
        private static bool ParseBool(string value, bool fallback)
        {
            bool parsed;
            return bool.TryParse(value, out parsed) ? parsed : fallback;
        }

        /// <summary>Returns user-friendly destination text.</summary>
        private static string FriendlyDestination(string destination)
        {
            if (string.Equals(destination, "Network", StringComparison.OrdinalIgnoreCase))
                return T("Network Server");
            if (string.Equals(destination, "Local", StringComparison.OrdinalIgnoreCase))
                return T("Local Folder");
            if (string.Equals(destination, "ExternalDrive", StringComparison.OrdinalIgnoreCase))
                return T("External Drive");
            return string.IsNullOrWhiteSpace(destination) ? T("backup destination") : destination;
        }

        /// <summary>Handles Save button clicks.</summary>
        private void SaveClicked(object sender, EventArgs e)
        {
            SaveSettings();
            RefreshLastBackupLabel();
        }

        /// <summary>Closes the backup settings form.</summary>
        private void CloseClicked(object sender, EventArgs e)
        {
            Close();
        }

        private static string T(string key)
        {
            return LanguageManager.Get(key);
        }

    }
}



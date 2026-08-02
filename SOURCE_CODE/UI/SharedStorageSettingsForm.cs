using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    /// <summary>Configures the customer-owned SMB location used for shared ServoERP documents and backups.</summary>
    public sealed class SharedStorageSettingsForm : ServoERP.Infrastructure.ServoFormBase
    {
        private readonly TextBox _rootPath = new TextBox();
        private readonly CheckBox _enabled = new CheckBox();
        private readonly Label _status = new Label();

        public SharedStorageSettingsForm()
        {
            Text = "Shared Office Storage";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(690, 405);
            MinimumSize = new Size(620, 360);
            BackColor = Color.White;

            BuildLayout();
            LoadSettings();
        }

        private void BuildLayout()
        {
            var heading = new Label
            {
                Text = "Connect ServoERP to your private office server",
                Location = new Point(24, 22),
                Size = new Size(620, 28),
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = DS.Slate900
            };
            var help = new Label
            {
                Text = "Use a Windows network share such as \\SERVERNAME\\ServoERPShared. ServoERP uses your Windows sign-in permissions and does not store server passwords. Business records remain in the shared SQL Server database.",
                Location = new Point(24, 58),
                Size = new Size(630, 50),
                Font = new Font("Segoe UI", 9f),
                ForeColor = DS.Slate600
            };
            _enabled.Text = "Enable shared office storage";
            _enabled.Location = new Point(24, 123);
            _enabled.Size = new Size(260, 24);
            _enabled.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);

            var pathLabel = new Label { Text = "Private server share (UNC path)", Location = new Point(24, 156), Size = new Size(280, 22), Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = DS.Slate700 };
            _rootPath.Location = new Point(24, 181);
            _rootPath.Size = new Size(625, 26);
            _rootPath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _rootPath.Font = new Font("Segoe UI", 10f);

            var folders = new Label
            {
                Text = "When enabled, ServoERP stores shared Backups, Company Templates, Documents, Exports, Imports, Logs, and Updates below this folder. If the share is unavailable, each PC continues safely with its local fallback folder.",
                Location = new Point(24, 220),
                Size = new Size(625, 47),
                Font = new Font("Segoe UI", 8.8f),
                ForeColor = DS.Slate600
            };

            Button test = MakeButton("Test Connection", DS.Primary600);
            test.Location = new Point(24, 282);
            test.Click += (s, e) => TestConnection(false);
            Button create = MakeButton("Create Folders", DS.Slate700);
            create.Location = new Point(168, 282);
            create.Click += (s, e) => TestConnection(true);
            Button save = MakeButton("Save", DS.Green600);
            save.Location = new Point(480, 282);
            save.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            save.Click += (s, e) => SaveSettings();
            Button close = MakeButton("Close", DS.Slate500);
            close.Location = new Point(570, 282);
            close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            close.Click += (s, e) => Close();

            _status.Location = new Point(24, 327);
            _status.Size = new Size(625, 32);
            _status.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _status.Font = new Font("Segoe UI", 9f, FontStyle.Bold);

            Controls.AddRange(new Control[] { heading, help, _enabled, pathLabel, _rootPath, folders, test, create, save, close, _status });
        }

        private static Button MakeButton(string text, Color color)
        {
            var button = new Button { Text = text, Size = new Size(132, 34), BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Cursor = Cursors.Hand };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private void LoadSettings()
        {
            _enabled.Checked = SharedStorageService.IsEnabled;
            _rootPath.Text = SharedStorageService.RootPath;
            SetStatus(_enabled.Checked ? "Shared storage is enabled. Test the path before relying on it." : "Shared storage is disabled. This PC uses local folders.", DS.Slate600);
        }

        private void TestConnection(bool createFolders)
        {
            string root = (_rootPath.Text ?? string.Empty).Trim().TrimEnd('\\', '/');
            if (!root.StartsWith(@"\\", StringComparison.Ordinal))
            {
                SetStatus("Enter a UNC path such as \\SERVERNAME\\ServoERPShared.", DS.Red600);
                return;
            }

            try
            {
                if (createFolders)
                {
                    foreach (string folder in SharedStorageService.RequiredFolderNames)
                        Directory.CreateDirectory(Path.Combine(root, folder));
                    SetStatus("Connected. Standard ServoERP folders are ready on the private server.", DS.Green600);
                }
                else if (Directory.Exists(root))
                {
                    SetStatus("Connected. This private server share is reachable.", DS.Green600);
                }
                else
                {
                    SetStatus("Share is not reachable. Check the server name, share permissions, and office network.", DS.Red600);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SharedStorageSettingsForm.TestConnection", ex);
                SetStatus("Cannot access the share: " + ex.Message, DS.Red600);
            }
        }

        private void SaveSettings()
        {
            string root = (_rootPath.Text ?? string.Empty).Trim().TrimEnd('\\', '/');
            if (_enabled.Checked && !root.StartsWith(@"\\", StringComparison.Ordinal))
            {
                SetStatus("Shared storage requires a UNC path such as \\SERVERNAME\\ServoERPShared.", DS.Red600);
                return;
            }

            try
            {
                ConfigService.Set("SharedStorage", "Enabled", _enabled.Checked ? "true" : "false");
                ConfigService.Set("SharedStorage", "RootPath", root);
                SetStatus(_enabled.Checked ? "Shared office storage saved. Use Create Folders once on the server share." : "Shared storage disabled. Local fallback folders will be used.", DS.Green600);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SharedStorageSettingsForm.SaveSettings", ex);
                SetStatus("Could not save shared storage settings: " + ex.Message, DS.Red600);
            }
        }

        private void SetStatus(string message, Color color)
        {
            _status.Text = message;
            _status.ForeColor = color;
        }
    }
}

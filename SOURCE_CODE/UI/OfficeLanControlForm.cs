using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    /// <summary>Server-side terminal discovery, readiness, deployment, and managed command center.</summary>
    public sealed class OfficeLanControlForm : ServoERP.Infrastructure.ServoFormBase
    {
        private readonly OfficeLanControlService _service = new OfficeLanControlService();
        private readonly OfficeLanReadinessService _readinessService = new OfficeLanReadinessService();
        private readonly DataGridView _computers = new DataGridView();
        private readonly ListView _readinessDetails = new ListView();
        private readonly Button _scan = new Button();
        private readonly Button _addTerminal = new Button();
        private readonly Button _selectAll = new Button();
        private readonly Button _preflight = new Button();
        private readonly Button _chooseInstaller = new Button();
        private readonly Button _deploymentAccess = new Button();
        private readonly Button _deploy = new Button();
        private readonly Button _healthCheck = new Button();
        private readonly Button _updateCheck = new Button();
        private readonly Button _pilotRollout = new Button();
        private readonly Button _diagnostics = new Button();
        private readonly Button _repair = new Button();
        private readonly Button _retryFailed = new Button();
        private readonly Button _cancelDeployment = new Button();
        private readonly TextBox _installerPath = new TextBox();
        private readonly ComboBox _filter = new ComboBox();
        private readonly Label _summary = new Label();
        private readonly Label _status = new Label();
        private readonly Label _detailTitle = new Label();
        private readonly Dictionary<string, Label> _summaryValues = new Dictionary<string, Label>(StringComparer.OrdinalIgnoreCase);
        private readonly bool _autoLoad;
        private readonly System.Windows.Forms.Timer _deploymentMonitor = new System.Windows.Forms.Timer { Interval = 1000 };
        private BindingList<OfficeLanComputer> _rows = new BindingList<OfficeLanComputer>();
        private List<OfficeLanComputer> _allRows = new List<OfficeLanComputer>();
        private CancellationTokenSource _operationCancellation;
        private string _installerOverridePath;
        private OfficeLanDeploymentPackage _activePackage;

        public OfficeLanControlForm() : this(true) { }

        public OfficeLanControlForm(bool autoLoad)
        {
            _autoLoad = autoLoad;
            Text = "LAN Control Center";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(1120, 700);
            Size = new Size(1440, 860);
            BackColor = DS.BgPage;
            BuildLayout();
            Shown += async (sender, args) => { if (_autoLoad) await ScanNetworkAsync(); };
            FormClosing += (sender, args) => _operationCancellation?.Cancel();
            _deploymentMonitor.Tick += (sender, args) => RefreshDeploymentProgress();
        }

        private void BuildLayout()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = DS.White, Padding = new Padding(24, 14, 24, 10) };
            header.Controls.Add(new Label { Text = "Office LAN Control Center", Font = DS.H2, ForeColor = DS.Slate900, AutoSize = true, Location = new Point(24, 12) });
            _summary.Font = DS.Body;
            _summary.ForeColor = DS.Slate600;
            _summary.AutoSize = true;
            _summary.Location = new Point(26, 47);
            _summary.Text = "Discover, verify, install, update, and monitor every ServoERP terminal from the office server.";
            header.Controls.Add(_summary);

            FlowLayoutPanel cards = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 88,
                Padding = new Padding(20, 10, 10, 8),
                WrapContents = false,
                BackColor = DS.BgPage
            };
            cards.Controls.Add(CreateSummaryCard("Total", "Total PCs", DS.Primary600));
            cards.Controls.Add(CreateSummaryCard("Online", "Online", DS.Teal600));
            cards.Controls.Add(CreateSummaryCard("Install", "Need install", DS.Amber600));
            cards.Controls.Add(CreateSummaryCard("Update", "Need update", DS.Primary600));
            cards.Controls.Add(CreateSummaryCard("Installing", "Installing", DS.Indigo500));
            cards.Controls.Add(CreateSummaryCard("Failed", "Failed", DS.Red600));

            Panel actions = BuildActionArea();
            SplitContainer workspace = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 370,
                SplitterWidth = 6,
                BackColor = DS.Border,
                Panel1MinSize = 250,
                Panel2MinSize = 150
            };
            ConfigureGrid();
            workspace.Panel1.Controls.Add(_computers);
            workspace.Panel2.Controls.Add(BuildReadinessDetails());

            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 46, BackColor = DS.White, Padding = new Padding(22, 12, 22, 8) };
            _status.Dock = DockStyle.Fill;
            _status.Font = DS.Small;
            _status.ForeColor = DS.Slate600;
            _status.Text = "Run readiness checks before deployment. Approved administrator access is protected on this server and supplied automatically.";
            footer.Controls.Add(_status);

            Controls.Add(workspace);
            Controls.Add(footer);
            Controls.Add(actions);
            Controls.Add(cards);
            Controls.Add(header);
        }

        private Panel BuildActionArea()
        {
            var actions = new Panel { Dock = DockStyle.Top, Height = 168, BackColor = DS.BgPage, Padding = new Padding(20, 8, 20, 8) };
            var firstRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 80, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoScroll = false, BackColor = DS.BgPage };
            ConfigureButton(_scan, "Scan network", 118);
            ConfigureButton(_addTerminal, "Add terminal", 112);
            ConfigureButton(_selectAll, "Select terminals", 128);
            ConfigureButton(_preflight, "Run readiness", 126);
            ConfigureButton(_healthCheck, "Health check", 112);
            ConfigureButton(_updateCheck, "Request updates", 126);
            ConfigureButton(_pilotRollout, "Pilot rollout", 112);
            ConfigureButton(_diagnostics, "Collect diagnostics", 138);
            ConfigureButton(_repair, "Repair database", 126);
            ConfigureButton(_retryFailed, "Retry failed", 104);
            ConfigureButton(_cancelDeployment, "Cancel deployment", 132);
            _cancelDeployment.Enabled = false;
            _scan.Click += async (sender, args) => await ScanNetworkAsync();
            _addTerminal.Click += async (sender, args) => await AddTerminalAsync();
            _selectAll.Click += (sender, args) => SelectReadyComputers();
            _preflight.Click += async (sender, args) => await RunReadinessAsync();
            _healthCheck.Click += (sender, args) => QueueBatchCommand("HealthCheck", "health check");
            _updateCheck.Click += (sender, args) => QueueBatchCommand("CheckForUpdate", "verified update check");
            _pilotRollout.Click += (sender, args) => QueuePilotRollout();
            _diagnostics.Click += (sender, args) => QueueBatchCommand("CollectDiagnostics", "diagnostics collection");
            _repair.Click += (sender, args) => QueueBatchCommand("RepairDatabase", "safe database repair");
            _retryFailed.Click += async (sender, args) => await RetryFailedAsync();
            _cancelDeployment.Click += (sender, args) => CancelDeployment();
            firstRow.Controls.AddRange(new Control[] { _scan, _addTerminal, _selectAll, _preflight, _healthCheck, _updateCheck, _pilotRollout, _diagnostics, _repair, _retryFailed, _cancelDeployment });

            var secondRow = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 66, ColumnCount = 7, RowCount = 1, BackColor = DS.BgPage, Padding = new Padding(0, 12, 0, 4) };
            secondRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            secondRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            secondRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            secondRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            secondRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            secondRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            secondRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Label installerLabel = new Label { Text = "Deployment payload", Font = DS.SmallBold, ForeColor = DS.Slate700, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 12, 0) };
            _installerPath.Dock = DockStyle.Fill;
            _installerPath.ReadOnly = true;
            _installerPath.BackColor = DS.White;
            _installerPath.Text = "Built-in terminal installer with prerequisites (recommended)";
            _installerPath.Margin = new Padding(0, 3, 10, 3);
            Label filterLabel = new Label { Text = "Show", Font = DS.SmallBold, ForeColor = DS.Slate700, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(4, 8, 8, 0) };
            _filter.DropDownStyle = ComboBoxStyle.DropDownList;
            _filter.Width = 145;
            _filter.Items.AddRange(new object[] { "All terminals", "Online", "Need installation", "Update available", "Installing", "Failed", "Offline" });
            _filter.SelectedIndex = 0;
            _filter.SelectedIndexChanged += (sender, args) => ApplyFilter();
            ConfigureButton(_chooseInstaller, "Installer override", 130);
            ConfigureButton(_deploymentAccess, "Admin access", 116);
            ConfigureButton(_deploy, "Deploy selected", 136);
            _chooseInstaller.Click += (sender, args) => ChooseInstaller();
            _deploymentAccess.Click += (sender, args) => ConfigureDeploymentAccess();
            _deploy.Click += async (sender, args) => await DeploySelectedAsync();
            secondRow.Controls.Add(installerLabel, 0, 0);
            secondRow.Controls.Add(_installerPath, 1, 0);
            secondRow.Controls.Add(filterLabel, 2, 0);
            secondRow.Controls.Add(_filter, 3, 0);
            secondRow.Controls.Add(_chooseInstaller, 4, 0);
            secondRow.Controls.Add(_deploymentAccess, 5, 0);
            secondRow.Controls.Add(_deploy, 6, 0);
            actions.Controls.Add(secondRow);
            actions.Controls.Add(firstRow);
            return actions;
        }

        private Control BuildReadinessDetails()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, BackColor = DS.White, Padding = new Padding(18, 10, 18, 12) };
            _detailTitle.Text = "Readiness details — select a terminal";
            _detailTitle.Dock = DockStyle.Top;
            _detailTitle.Height = 30;
            _detailTitle.Font = DS.H3;
            _detailTitle.ForeColor = DS.Slate900;
            _readinessDetails.Dock = DockStyle.Fill;
            _readinessDetails.View = View.Details;
            _readinessDetails.FullRowSelect = true;
            _readinessDetails.BorderStyle = BorderStyle.None;
            _readinessDetails.Columns.Add("Check", 190);
            _readinessDetails.Columns.Add("Status", 120);
            _readinessDetails.Columns.Add("Details and recommended action", 920);
            panel.Controls.Add(_readinessDetails);
            panel.Controls.Add(_detailTitle);
            return panel;
        }

        private Panel CreateSummaryCard(string key, string title, Color accent)
        {
            var card = new Panel { Width = 214, Height = 66, BackColor = DS.White, Margin = new Padding(0, 0, 12, 0), Padding = new Padding(14, 8, 12, 6) };
            var marker = new Panel { Dock = DockStyle.Left, Width = 4, BackColor = accent };
            var value = new Label { Text = "0", Font = DS.H2, ForeColor = DS.Slate900, AutoSize = true, Location = new Point(28, 5) };
            var caption = new Label { Text = title, Font = DS.Small, ForeColor = DS.Slate600, AutoSize = true, Location = new Point(30, 37) };
            card.Controls.Add(marker);
            card.Controls.Add(value);
            card.Controls.Add(caption);
            _summaryValues[key] = value;
            return card;
        }

        private void ConfigureGrid()
        {
            _computers.Dock = DockStyle.Fill;
            _computers.AutoGenerateColumns = false;
            _computers.AllowUserToAddRows = false;
            _computers.AllowUserToDeleteRows = false;
            _computers.AllowUserToResizeRows = false;
            _computers.RowHeadersVisible = false;
            _computers.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _computers.MultiSelect = false;
            _computers.BackgroundColor = DS.White;
            _computers.BorderStyle = BorderStyle.None;
            _computers.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _computers.DataSource = _rows;
            _computers.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Select", DataPropertyName = "Selected", FillWeight = 46, MinimumWidth = 58 });
            AddTextColumn("PC", "HostName", 110, 100);
            AddTextColumn("IP address", "IpAddress", 78, 84);
            AddTextColumn("State", "ManagementState", 92, 98);
            AddTextColumn("Version", "AppVersion", 62, 72);
            AddTextColumn("Readiness", "ReadinessStatus", 84, 92);
            AddTextColumn("SQL", "SqlStatus", 66, 74);
            AddTextColumn("Stage", "CurrentStage", 92, 102);
            AddTextColumn("Progress", "ProgressPercent", 58, 70);
            AddTextColumn("Last seen", "LastSeenDisplay", 78, 92);
            AddTextColumn("Last result", "LastResult", 145, 180);
            _computers.CurrentCellDirtyStateChanged += (sender, args) => { if (_computers.IsCurrentCellDirty) _computers.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            _computers.SelectionChanged += (sender, args) => ShowSelectedDetails();
            _computers.CellFormatting += FormatGridCell;
        }

        private static void ConfigureButton(Button button, string text, int width)
        {
            button.Text = text;
            button.Width = width;
            button.Height = 34;
            button.Margin = new Padding(0, 0, 8, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = DS.Border;
            button.BackColor = DS.White;
            button.ForeColor = DS.Slate800;
        }

        private void AddTextColumn(string header, string property, float weight, int minimumWidth)
        {
            _computers.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = header, DataPropertyName = property, ReadOnly = true, FillWeight = weight, MinimumWidth = minimumWidth });
        }

        private void FormatGridCell(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _computers.Rows.Count)
                return;
            OfficeLanComputer item = _computers.Rows[e.RowIndex].DataBoundItem as OfficeLanComputer;
            if (item == null)
                return;
            if (_computers.Columns[e.ColumnIndex].DataPropertyName == "ProgressPercent")
                e.Value = item.ProgressPercent <= 0 ? "—" : item.ProgressPercent + "%";
            if (_computers.Columns[e.ColumnIndex].DataPropertyName == "ManagementState")
                e.CellStyle.ForeColor = item.ManagementState == "Failed" ? DS.Red600 : item.ManagementState == "Ready" ? DS.Teal600 : DS.Slate700;
        }

        private async Task ScanNetworkAsync()
        {
            ResetCancellation();
            CancellationToken token = _operationCancellation.Token;
            await RunSafeAsync("Scan office network", async () =>
            {
                _status.Text = "Scanning routed private IPv4 subnets and matching enrolled or saved terminals...";
                IList<OfficeLanComputer> discovered = await _service.DiscoverComputersAsync(token);
                _allRows = discovered.ToList();
                ApplyFilter();
                UpdateSummary();
            }, _scan, "Scanning...");
        }

        private async Task AddTerminalAsync()
        {
            string value = PromptForTerminal(this);
            if (string.IsNullOrWhiteSpace(value))
                return;
            ResetCancellation();
            await RunSafeAsync("Add terminal", async () =>
            {
                OfficeLanComputer computer = await _service.ProbeManualComputerAsync(value, _operationCancellation.Token);
                _allRows.RemoveAll(item => string.Equals(item.HostName, computer.HostName, StringComparison.OrdinalIgnoreCase));
                _allRows.Add(computer);
                ApplyFilter();
                UpdateSummary();
                _status.Text = computer.IsReachable ? "Terminal added and detected successfully." : "Terminal saved. It will remain visible while offline.";
            }, _addTerminal, "Adding...");
        }

        private async Task RunReadinessAsync()
        {
            _computers.EndEdit();
            List<OfficeLanComputer> targets = _allRows.Where(item => item.Selected && item.IsReachable && !item.IsLocalComputer).ToList();
            if (targets.Count == 0)
                targets = _allRows.Where(item => item.IsReachable && !item.IsLocalComputer).ToList();
            if (targets.Count == 0)
            {
                MessageBox.Show(this, "No reachable terminal is available for readiness checks.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ResetCancellation();
            await RunSafeAsync("Terminal readiness", async () =>
            {
                _status.Text = "Checking network, WinRM, prerequisites, and the shared SQL connection...";
                foreach (OfficeLanComputer target in targets)
                {
                    OfficeLanReadinessResult result = await _readinessService.CheckAsync(target, _operationCancellation.Token);
                    target.ReadinessStatus = result.OverallStatus;
                    target.ReadinessChecks = result.Checks;
                    OfficeLanReadinessCheck sql = result.Checks.FirstOrDefault(item => item.CheckKey == "Sql");
                    target.SqlStatus = sql == null ? "Not checked" : sql.Status;
                    target.LastResult = result.OverallStatus == "Blocked" ? "Resolve blocking readiness checks" : "Readiness checked";
                }
                _computers.Refresh();
                UpdateSummary();
                ShowSelectedDetails();
                _status.Text = targets.Count + " terminal(s) checked. Select a row to see every result and recommended action.";
            }, _preflight, "Checking...");
        }

        private void SelectReadyComputers()
        {
            bool shouldSelect = _allRows.Any(item => item.IsReachable && !item.IsLocalComputer && !item.Selected);
            foreach (OfficeLanComputer item in _allRows)
                item.Selected = shouldSelect && item.IsReachable && !item.IsLocalComputer;
            _computers.Refresh();
            _selectAll.Text = shouldSelect ? "Clear selection" : "Select terminals";
        }

        private void ChooseInstaller()
        {
            using (var dialog = new OpenFileDialog
            {
                Title = "Choose ServoERP terminal or Enterprise installer",
                Filter = "ServoERP installer|ServoERP.Terminal.Setup.*.exe;ServoERP.Setup.*.exe;ServoERP.App.*.msi|Windows installer|*.exe;*.msi",
                CheckFileExists = true,
                Multiselect = false
            })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _installerOverridePath = dialog.FileName;
                    _installerPath.Text = dialog.FileName;
                }
            }
        }

        private async Task DeploySelectedAsync()
        {
            _computers.EndEdit();
            List<OfficeLanComputer> selected = _allRows.Where(item => item.Selected && item.IsReachable && !item.IsLocalComputer).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Select at least one reachable terminal PC first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (selected.Any(item => string.Equals(item.ReadinessStatus, "Blocked", StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, "One or more selected terminals have blocking readiness failures. Correct them before deployment.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            OfficeLanDeploymentCredential deploymentCredential;
            if (!_service.TryLoadSavedDeploymentCredential(out deploymentCredential))
            {
                deploymentCredential = PromptForDeploymentCredential();
                if (deploymentCredential == null)
                    return;
                if (deploymentCredential.RememberOnServer)
                    _service.SaveDeploymentCredential(deploymentCredential);
            }
            bool confirmed = ServoERP.Infrastructure.ServoConfirmDialog.Show(this, "Deploy ServoERP to selected PCs?",
                string.Format("{0} PC(s) selected. LAN Control will use the approved administrator access automatically, verify the transferred package hash, configure SQL, and report live progress.", selected.Count));
            if (!confirmed)
                return;

            await RunSafeAsync("Prepare LAN deployment", async () =>
            {
                OfficeLanDeploymentPackage package;
                try
                {
                    package = await Task.Run(() => _service.CreateDeploymentPackage(selected, _installerOverridePath, deploymentCredential));
                }
                finally
                {
                    deploymentCredential.Password = string.Empty;
                }
                _activePackage = package;
                foreach (OfficeLanComputer item in selected)
                {
                    item.ManagementState = "Installing";
                    item.CurrentStage = "Starting securely";
                    item.ProgressPercent = 1;
                }
                _service.LaunchDeployment(package);
                _deploymentMonitor.Start();
                _cancelDeployment.Enabled = true;
                _computers.Refresh();
                UpdateSummary();
                _status.Text = string.Format("Unattended deployment started for {0} PC(s). No PowerShell credential prompt is required; progress appears here automatically.", package.TargetCount);
            }, _deploy, "Preparing...");
        }

        private void ConfigureDeploymentAccess()
        {
            OfficeLanDeploymentCredential existing;
            _service.TryLoadSavedDeploymentCredential(out existing);
            OfficeLanDeploymentCredential credential = PromptForDeploymentCredential(existing);
            if (credential == null)
                return;
            if (credential.RememberOnServer)
            {
                _service.SaveDeploymentCredential(credential);
                _status.Text = "Administrator access saved securely for unattended LAN deployments from this Windows account.";
            }
            else
            {
                _service.ForgetSavedDeploymentCredential();
                _status.Text = "Saved LAN deployment administrator access was removed.";
            }
            credential.Password = string.Empty;
            if (existing != null)
                existing.Password = string.Empty;
        }

        private OfficeLanDeploymentCredential PromptForDeploymentCredential(OfficeLanDeploymentCredential existing = null)
        {
            using (var dialog = new LanDeploymentCredentialDialog(existing))
                return dialog.ShowDialog(this) == DialogResult.OK ? dialog.Credential : null;
        }

        private void RefreshDeploymentProgress()
        {
            if (_activePackage == null)
                return;
            try
            {
                IList<OfficeLanDeploymentProgress> progress = _service.ReadDeploymentProgress(_activePackage);
                if (progress.Count == 0)
                    return;
                foreach (OfficeLanDeploymentProgress update in progress)
                {
                    OfficeLanComputer row = _allRows.FirstOrDefault(item => string.Equals(item.HostName, update.Computer, StringComparison.OrdinalIgnoreCase) || string.Equals(item.IpAddress, update.Computer, StringComparison.OrdinalIgnoreCase));
                    if (row == null)
                        continue;
                    row.CurrentStage = update.Stage;
                    row.ProgressPercent = update.ProgressPercent;
                    row.LastResult = update.Detail;
                    row.ManagementState = update.Status == "Failed" ? "Failed" : update.Status == "Completed" ? "Ready" : "Installing";
                }
                _service.PersistDeploymentProgress(_activePackage.JobPublicId, progress);
                _computers.Refresh();
                UpdateSummary();
                bool finished = progress.Count >= _activePackage.TargetCount && progress.All(item => item.Status == "Completed" || item.Status == "Failed" || item.Status == "Cancelled");
                if (finished)
                {
                    _deploymentMonitor.Stop();
                    _cancelDeployment.Enabled = false;
                    int failed = progress.Count(item => item.Status == "Failed");
                    _status.Text = failed == 0 ? "Deployment completed successfully on every selected terminal." : failed + " terminal(s) failed. Select a failed row for the exact reason, correct it, and retry.";
                }
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("OfficeLanControlForm.RefreshDeploymentProgress", ex);
            }
        }

        private async Task RetryFailedAsync()
        {
            List<OfficeLanComputer> failed = _allRows.Where(item => item.ManagementState == "Failed" && item.IsReachable && !item.IsLocalComputer).ToList();
            if (failed.Count == 0)
            {
                MessageBox.Show(this, "There are no reachable failed terminals to retry.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            foreach (OfficeLanComputer item in _allRows) item.Selected = failed.Contains(item);
            _filter.SelectedIndex = 0;
            ApplyFilter();
            await DeploySelectedAsync();
        }

        private void CancelDeployment()
        {
            if (_activePackage == null || string.IsNullOrWhiteSpace(_activePackage.FolderPath))
                return;
            try
            {
                File.WriteAllText(Path.Combine(_activePackage.FolderPath, "cancel.requested"), DateTime.UtcNow.ToString("o"));
                _cancelDeployment.Enabled = false;
                _status.Text = "Cancellation requested. The current terminal is allowed to finish safely; remaining terminals will be marked Cancelled.";
            }
            catch (Exception ex)
            {
                ShowError("Could not request deployment cancellation.", ex);
            }
        }

        private void QueueBatchCommand(string commandType, string description)
        {
            _computers.EndEdit();
            List<Guid> nodeIds = _allRows.Where(item => item.Selected && item.NodePublicId.HasValue).Select(item => item.NodePublicId.Value).Distinct().ToList();
            if (nodeIds.Count == 0)
            {
                OfficeLanComputer current = _computers.CurrentRow == null ? null : _computers.CurrentRow.DataBoundItem as OfficeLanComputer;
                if (current != null && current.NodePublicId.HasValue)
                    nodeIds.Add(current.NodePublicId.Value);
            }
            if (nodeIds.Count == 0)
            {
                MessageBox.Show(this, "Select at least one enrolled ServoERP terminal first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                int count = _service.QueueBatch(nodeIds, commandType, Environment.UserName);
                MessageBox.Show(this, description + " queued for " + count + " terminal(s). The terminal agent processes it even when ServoERP is closed.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ShowError("Could not queue the LAN command.", ex);
            }
        }

        private void QueuePilotRollout()
        {
            _computers.EndEdit();
            List<Guid> nodeIds = _allRows.Where(item => item.Selected && item.NodePublicId.HasValue).Select(item => item.NodePublicId.Value).Distinct().ToList();
            if (nodeIds.Count == 0)
            {
                MessageBox.Show(this, "Select one or more enrolled terminals first. The first selected terminal becomes the pilot.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DateTime? scheduled = PromptForSchedule(this);
            if (!scheduled.HasValue)
                return;
            try
            {
                int count = _service.QueuePilotUpdate(nodeIds, Environment.UserName, scheduled.Value.ToUniversalTime());
                MessageBox.Show(this, string.Format("Pilot rollout scheduled for {0}. The first terminal updates first; the remaining {1} terminal(s) are released only after the pilot reports success.",
                    scheduled.Value.ToString("dd/MM/yyyy HH:mm"), Math.Max(0, count - 1)), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ShowError("Could not schedule the pilot rollout.", ex);
            }
        }

        private void ApplyFilter()
        {
            string selected = _filter.SelectedItem as string ?? "All terminals";
            IEnumerable<OfficeLanComputer> filtered = _allRows;
            if (selected == "Online") filtered = filtered.Where(item => item.IsReachable || item.ConnectionStatus == "Online");
            else if (selected == "Need installation") filtered = filtered.Where(item => item.ManagementState == "Needs installation");
            else if (selected == "Update available") filtered = filtered.Where(item => item.ManagementState == "Update available");
            else if (selected == "Installing") filtered = filtered.Where(item => item.ManagementState == "Installing");
            else if (selected == "Failed") filtered = filtered.Where(item => item.ManagementState == "Failed");
            else if (selected == "Offline") filtered = filtered.Where(item => item.ManagementState == "Offline");
            _rows = new BindingList<OfficeLanComputer>(filtered.ToList());
            _computers.DataSource = _rows;
        }

        private void UpdateSummary()
        {
            SetSummary("Total", _allRows.Count);
            SetSummary("Online", _allRows.Count(item => item.IsReachable || item.ConnectionStatus == "Online"));
            SetSummary("Install", _allRows.Count(item => item.ManagementState == "Needs installation"));
            SetSummary("Update", _allRows.Count(item => item.ManagementState == "Update available"));
            SetSummary("Installing", _allRows.Count(item => item.ManagementState == "Installing"));
            SetSummary("Failed", _allRows.Count(item => item.ManagementState == "Failed"));
            int ready = _allRows.Count(item => item.ReadinessStatus == "Ready" && !item.IsLocalComputer);
            int preparation = _allRows.Count(item => item.ReadinessStatus == "Needs preparation" && !item.IsLocalComputer);
            _summary.Text = string.Format("{0} terminal(s) visible; {1} ready; {2} require preparation. Agent-managed terminals remain visible while ServoERP is closed.", _allRows.Count, ready, preparation);
        }

        private void SetSummary(string key, int value)
        {
            Label label;
            if (_summaryValues.TryGetValue(key, out label))
                label.Text = value.ToString();
        }

        private void ShowSelectedDetails()
        {
            OfficeLanComputer computer = _computers.CurrentRow == null ? null : _computers.CurrentRow.DataBoundItem as OfficeLanComputer;
            _readinessDetails.Items.Clear();
            if (computer == null)
            {
                _detailTitle.Text = "Readiness details — select a terminal";
                return;
            }
            _detailTitle.Text = "Readiness details — " + computer.HostName;
            foreach (OfficeLanReadinessCheck check in computer.ReadinessChecks ?? new List<OfficeLanReadinessCheck>())
            {
                string detail = check.Detail + (string.IsNullOrWhiteSpace(check.Recommendation) ? string.Empty : "  Fix: " + check.Recommendation);
                var item = new ListViewItem(check.Name ?? check.CheckKey);
                item.SubItems.Add(check.Status);
                item.SubItems.Add(detail);
                item.ForeColor = check.Status == "Failed" ? DS.Red600 : check.Status == "Passed" ? DS.Teal600 : DS.Slate700;
                _readinessDetails.Items.Add(item);
            }
            if (_readinessDetails.Items.Count == 0)
            {
                var item = new ListViewItem("Not checked");
                item.SubItems.Add("Pending");
                item.SubItems.Add("Select this terminal and choose Run readiness.");
                _readinessDetails.Items.Add(item);
            }
        }

        private void ResetCancellation()
        {
            _operationCancellation?.Cancel();
            _operationCancellation?.Dispose();
            _operationCancellation = new CancellationTokenSource();
        }

        private static string PromptForTerminal(IWin32Window owner)
        {
            using (var dialog = new Form { Text = "Add terminal", StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(430, 150), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false })
            using (var input = new TextBox { Left = 20, Top = 48, Width = 390 })
            using (var ok = new Button { Text = "Add terminal", DialogResult = DialogResult.OK, Left = 270, Top = 100, Width = 140 })
            {
                dialog.Controls.Add(new Label { Text = "Terminal hostname or IPv4 address", AutoSize = true, Left = 20, Top = 22 });
                dialog.Controls.Add(input);
                dialog.Controls.Add(ok);
                dialog.AcceptButton = ok;
                return dialog.ShowDialog(owner) == DialogResult.OK ? input.Text.Trim() : string.Empty;
            }
        }

        private static DateTime? PromptForSchedule(IWin32Window owner)
        {
            using (var dialog = new Form { Text = "Schedule pilot rollout", StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(430, 160), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false })
            using (var picker = new DateTimePicker { Left = 20, Top = 50, Width = 390, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy HH:mm", MinDate = DateTime.Now.AddMinutes(-1), Value = DateTime.Now.AddMinutes(5) })
            using (var ok = new Button { Text = "Schedule rollout", DialogResult = DialogResult.OK, Left = 270, Top = 108, Width = 140 })
            {
                dialog.Controls.Add(new Label { Text = "Start the pilot terminal at", AutoSize = true, Left = 20, Top = 23 });
                dialog.Controls.Add(picker);
                dialog.Controls.Add(ok);
                dialog.AcceptButton = ok;
                return dialog.ShowDialog(owner) == DialogResult.OK ? picker.Value : (DateTime?)null;
            }
        }

        public void LoadPreviewDataForVisualTest()
        {
            _allRows = new List<OfficeLanComputer>
            {
                Preview("SERVER-PC", "192.168.1.10", "Server PC", "1.1.440.0", "Ready", "Passed", "Managed", 100, "Just now", true, true),
                Preview("ACCOUNTS-PC", "192.168.1.21", "Ready", "1.1.440.0", "Ready", "Passed", "Managed", 100, "Just now", true, false),
                Preview("FRONT-DESK", "192.168.1.27", "Needs installation", string.Empty, "Needs preparation", "Passed", "Preflight complete", 0, "Detected now", true, false),
                Preview("STORE-PC", "192.168.1.31", "Update available", "1.1.438.0", "Ready", "Passed", "Installer copied", 40, "1 min ago", true, false),
                Preview("SERVICE-DESK", "192.168.1.36", "Installing", "1.1.439.0", "Ready", "Passed", "Installing", 68, "Just now", true, false),
                Preview("DAD-PC", "192.168.1.42", "Failed", string.Empty, "Blocked", "Failed", "Failed", 100, "4 min ago", false, false)
            };
            _allRows[1].Selected = true;
            _allRows[2].Selected = true;
            _allRows[2].ReadinessChecks = new List<OfficeLanReadinessCheck>
            {
                new OfficeLanReadinessCheck { Name = "Network reachability", Status = "Passed", Detail = "The terminal responds on the office network." },
                new OfficeLanReadinessCheck { Name = "Remote management", Status = "Needs preparation", Detail = "WinRM is not currently reachable.", Recommendation = "LAN Control will attempt the approved WMI bootstrap." },
                new OfficeLanReadinessCheck { Name = "Shared SQL connection", Status = "Passed", Detail = "The configured SQL login can open HVAC_PRO." }
            };
            _installerPath.Text = "Built-in ServoERP.Terminal.Setup.1.1.440.0.exe";
            ApplyFilter();
            UpdateSummary();
            if (_computers.Rows.Count > 2)
            {
                _computers.ClearSelection();
                _computers.CurrentCell = _computers.Rows[2].Cells[1];
                _computers.Rows[2].Selected = true;
            }
            ShowSelectedDetails();
        }

        internal void CaptureDeploymentAccessDialogForVisualTest(string outputPath)
        {
            using (var dialog = new LanDeploymentCredentialDialog(new OfficeLanDeploymentCredential
            {
                UserName = @"OFFICE\ServoDeploy",
                Password = "visual-test-only",
                RememberOnServer = true
            }))
            {
                dialog.Show(this);
                Application.DoEvents();
                using (var bitmap = new Bitmap(dialog.Width, dialog.Height))
                {
                    dialog.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                    bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
                }
                dialog.Close();
            }
        }

        private static OfficeLanComputer Preview(string host, string ip, string state, string version, string readiness, string sql, string stage, int progress, string lastSeen, bool reachable, bool local)
        {
            return new OfficeLanComputer
            {
                HostName = host, IpAddress = ip, ManagementState = state, AppVersion = version,
                TargetVersion = "1.1.440.0", ReadinessStatus = readiness, SqlStatus = sql,
                CurrentStage = stage, ProgressPercent = progress, LastSeenDisplay = lastSeen,
                IsReachable = reachable, IsLocalComputer = local, IsEnrolled = state != "Needs installation",
                SupportsRemoteManagement = readiness == "Ready",
                LastResult = state == "Failed" ? "SQL login failed — correct credentials and retry" : "Healthy"
            };
        }

        private sealed class LanDeploymentCredentialDialog : ServoERP.Infrastructure.ServoFormBase
        {
            private readonly TextBox _userName = new TextBox();
            private readonly TextBox _password = new TextBox();
            private readonly CheckBox _remember = new CheckBox();

            public OfficeLanDeploymentCredential Credential { get; private set; }

            public LanDeploymentCredentialDialog(OfficeLanDeploymentCredential existing)
            {
                Text = "LAN deployment administrator access";
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                ShowInTaskbar = false;
                ClientSize = new Size(520, 300);
                BackColor = DS.BgPage;

                Controls.Add(new Label
                {
                    Text = "Administrator access for terminal PCs",
                    Font = DS.H2,
                    ForeColor = DS.Slate900,
                    AutoSize = true,
                    Location = new Point(24, 20)
                });
                Controls.Add(new Label
                {
                    Text = "Use an account that Windows already authorizes as an administrator on each selected PC. LAN Control cannot bypass Windows security.",
                    Font = DS.Body,
                    ForeColor = DS.Slate600,
                    Location = new Point(24, 57),
                    Size = new Size(470, 48)
                });

                Controls.Add(new Label { Text = "Windows account", Font = DS.SmallBold, ForeColor = DS.Slate700, AutoSize = true, Location = new Point(24, 113) });
                _userName.Location = new Point(24, 134);
                _userName.Size = new Size(470, 26);
                _userName.Text = existing == null ? Environment.UserName : existing.UserName;
                Controls.Add(_userName);

                Controls.Add(new Label { Text = "Password", Font = DS.SmallBold, ForeColor = DS.Slate700, AutoSize = true, Location = new Point(24, 170) });
                _password.Location = new Point(24, 191);
                _password.Size = new Size(470, 26);
                _password.UseSystemPasswordChar = true;
                _password.Text = existing == null ? string.Empty : existing.Password;
                Controls.Add(_password);

                _remember.Text = "Remember securely for unattended deployments from this server";
                _remember.Font = DS.Small;
                _remember.ForeColor = DS.Slate700;
                _remember.AutoSize = true;
                _remember.Checked = existing != null;
                _remember.Location = new Point(24, 229);
                Controls.Add(_remember);

                var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(296, 260), Size = new Size(94, 32) };
                var use = new Button { Text = "Use access", Location = new Point(400, 260), Size = new Size(94, 32) };
                use.Click += (sender, args) => AcceptCredential();
                Controls.Add(cancel);
                Controls.Add(use);
                AcceptButton = use;
                CancelButton = cancel;
            }

            private void AcceptCredential()
            {
                if (string.IsNullOrWhiteSpace(_userName.Text) || string.IsNullOrEmpty(_password.Text))
                {
                    MessageBox.Show(this, "Enter the Windows administrator account and password used by the selected terminal PCs.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                Credential = new OfficeLanDeploymentCredential
                {
                    UserName = _userName.Text.Trim(),
                    Password = _password.Text,
                    RememberOnServer = _remember.Checked
                };
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }
}

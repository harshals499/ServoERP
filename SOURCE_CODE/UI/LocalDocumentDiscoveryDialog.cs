using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class LocalDocumentDiscoveryDialog : ServoERP.Infrastructure.ServoFormBase
    {
        private sealed class DriveChoice
        {
            public string Root { get; set; }
            public override string ToString() => Root;
        }
        private readonly ComboBox _drive = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 95 };
        private readonly TextBox _nameFilter = new TextBox { Width = 170 };
        private readonly CheckBox _skipSystem = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new CheckBox { Text = "Skip system/cache folders", AutoSize = true, Checked = true });
        private readonly Button _scan = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Button { Text = "Scan selected drive", AutoSize = true });
        private readonly Button _cancelScan = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Button { Text = "Cancel scan", AutoSize = true, Enabled = false });
        private readonly Button _review = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Button { Text = "Extract selected for review", AutoSize = true });
        private readonly Label _summary = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label { Dock = DockStyle.Fill, Padding = new Padding(8), Text = "Choose a local drive and start a scan. PDF, JPG, PNG and XLSX files are detected without opening their contents. No business records are saved." });
        private readonly DataGridView _grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false,
            AllowUserToDeleteRows = false, RowHeadersVisible = false, BackgroundColor = DS.White, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None };
        private readonly ProgressBar _progress = new ProgressBar { Dock = DockStyle.Fill, Style = ProgressBarStyle.Marquee, Visible = false };
        private CancellationTokenSource _scanToken;
        private bool _scanning;
        private bool _initializing = true;
        public LocalDocumentScanResult Result { get; private set; }
        public List<LocalDocumentCandidate> SelectedFiles { get; private set; } = new List<LocalDocumentCandidate>();
        protected override bool EnableWorkspaceHeader => false;

        public LocalDocumentDiscoveryDialog(LocalDocumentScanResult previous = null)
        {
            global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(this, "ServoERP · Detect documents on a local drive");
            Size = new Size(1180, 740); MinimumSize = new Size(980, 640); StartPosition = FormStartPosition.CenterParent;
            Font = new Font(global::HVAC_Pro_Desktop.Services.LanguageManager.GetUiFontFamily(), 9); BackColor = DS.BgPage;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 5, ColumnCount = 1 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 105)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 8));
            Controls.Add(root);
            var options = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, AutoScroll = true };
            options.Controls.Add(global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label { Text = "Local drive", AutoSize = true, Margin = new Padding(4, 10, 4, 4) })); options.Controls.Add(_drive);
            options.Controls.Add(global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label { Text = "Filename contains", AutoSize = true, Margin = new Padding(12, 10, 4, 4) })); options.Controls.Add(_nameFilter);
            options.Controls.Add(_skipSystem); options.Controls.Add(_scan); options.Controls.Add(_cancelScan);
            root.Controls.Add(options, 0, 0); root.Controls.Add(_grid, 0, 1); root.Controls.Add(_summary, 0, 2); root.Controls.Add(_progress, 0, 4);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
            var selectAll = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Button { Text = "Select next 50", AutoSize = true }); var clear = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Button { Text = "Clear selection", AutoSize = true });
            var close = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel });
            actions.Controls.Add(selectAll); actions.Controls.Add(clear); actions.Controls.Add(_review); actions.Controls.Add(close); root.Controls.Add(actions, 0, 3);
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "Selected", HeaderText = global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Review"), Name = "Selected", Width = 65, MinimumWidth = 65 });
            AddColumn("FileName", "File", 230); AddColumn("Format", "Format", 80); AddColumn("Status", "Detection status", 250);
            AddColumn("SizeBytes", "Bytes", 100); AddColumn("ModifiedUtc", "Modified (UTC)", 150); AddColumn("FullPath", "Location", 320);
            _grid.Columns["SizeBytes"].DefaultCellStyle.Format="N0";_grid.Columns["ModifiedUtc"].DefaultCellStyle.Format="dd/MM/yyyy HH:mm";
            _scan.Click += async (s, e) => await Scan(); _cancelScan.Click += (s, e) => _scanToken?.Cancel();
            selectAll.Click += (s, e) => SelectEligible(true); clear.Click += (s, e) => SelectEligible(false);
            _review.Click += (s, e) => AcceptSelection();
            _drive.SelectedIndexChanged += (s, e) => ClearResultsOnOptionsChange(); _nameFilter.TextChanged += (s, e) => ClearResultsOnOptionsChange(); _skipSystem.CheckedChanged += (s, e) => ClearResultsOnOptionsChange();
            _grid.CellContentClick += (s, e) => { if (e.RowIndex >= 0 && e.ColumnIndex == 0) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            _grid.DataBindingComplete += (s, e) => { foreach (DataGridViewRow row in _grid.Rows) if (row.DataBoundItem is LocalDocumentCandidate file) row.Cells[0].ReadOnly = !file.CanReview; if(IsHandleCreated)BeginInvoke((Action)ApplyColumnFormats);else ApplyColumnFormats(); };
            foreach (var drive in LocalDocumentDiscoveryService.GetLocalDrives()) _drive.Items.Add(new DriveChoice { Root = drive.Name });
            if (_drive.Items.Count > 0) _drive.SelectedIndex = 0;
            if (previous != null)
            {
                for (int i = 0; i < _drive.Items.Count; i++) if (((DriveChoice)_drive.Items[i]).Root.Equals(previous.RootPath, StringComparison.OrdinalIgnoreCase)) _drive.SelectedIndex = i;
                _nameFilter.Text = previous.NameFilter; _skipSystem.Checked = previous.SystemFoldersSkipped; Bind(previous);
            }
            _initializing = false;
            FormClosing += (s, e) => { if (_scanning) { e.Cancel = true; _scanToken.Cancel(); global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_summary, "Stopping discovery safely…"); } };
        }
        private void AddColumn(string property, string title, int width) => _grid.Columns.Add(new DataGridViewTextBoxColumn
            { DataPropertyName = property, Name = property, HeaderText = title, Width = width, MinimumWidth = width, ReadOnly = true });
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);_grid.CellFormatting+=FormatModificationDate;BeginInvoke((Action)ApplyColumnFormats);
        }
        private void FormatModificationDate(object sender,DataGridViewCellFormattingEventArgs e)
        {
            if(e.RowIndex>=0&&e.ColumnIndex>=0&&_grid.Columns[e.ColumnIndex].Name=="ModifiedUtc"&&_grid.Rows[e.RowIndex].DataBoundItem is LocalDocumentCandidate file)
            {e.Value=file.ModifiedUtc.ToString("dd/MM/yyyy HH:mm",System.Globalization.CultureInfo.InvariantCulture);e.FormattingApplied=true;}
        }
        private void ApplyColumnFormats()
        {
            _grid.Columns["ModifiedUtc"].DefaultCellStyle.Format="dd/MM/yyyy HH:mm";
            _grid.Columns["SizeBytes"].DefaultCellStyle.Format="N0";
            _summary.Font=Font;
        }
        private void ClearResultsOnOptionsChange()
        {
            if (_initializing || _scanning) return;
            Result = null; _grid.DataSource = null; global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_summary, "Options changed. Scan the selected drive to discover documents with these settings.");
        }
        private async Task Scan()
        {
            if (_scanning) return;
            if (!(_drive.SelectedItem is DriveChoice choice)) { global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_summary, "No local fixed or removable drive is available."); return; }
            _scanning = true; _scanToken = new CancellationTokenSource(); _progress.Visible = true;
            _drive.Enabled = _nameFilter.Enabled = _skipSystem.Enabled = _scan.Enabled = _grid.Enabled = _review.Enabled = false; _cancelScan.Enabled = true;
            Result = null; _grid.DataSource = null;
            global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_summary, "Scanning the selected drive… You can cancel discovery. No files are changed and no business records are saved.");
            try
            {
                var progress = new Progress<LocalDocumentScanProgress>(p => {if(_scanning)global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_summary, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Scanning… {0} folders, {1} files, {2} candidates, {3} locations skipped. No records saved.", p.FoldersVisited, p.FilesVisited, p.CandidatesFound, p.SkippedLocations));});
                var result = await new LocalDocumentDiscoveryService().ScanDriveAsync(choice.Root, _nameFilter.Text.Trim(), _skipSystem.Checked, progress, _scanToken.Token);
                Bind(result);
            }
            catch (OperationCanceledException) { global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_summary, "Drive scan cancelled. No files were changed and no business records were saved. Start another scan when ready."); }
            catch (Exception ex)
            {
                Serilog.Log.Warning("Local drive discovery failed with {ErrorType}", ex.GetType().Name);
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_summary, (ex is InvalidOperationException ? ex.Message : global::HVAC_Pro_Desktop.Services.LanguageManager.Get("The drive could not be scanned. Check that it is connected and accessible, then retry.")));
            }
            finally
            {
                _scanning = false; _scanToken.Dispose(); _scanToken = null; _progress.Visible = false;
                _drive.Enabled = _nameFilter.Enabled = _skipSystem.Enabled = _scan.Enabled = _grid.Enabled = _review.Enabled = true; _cancelScan.Enabled = false;
            }
        }
        private void Bind(LocalDocumentScanResult result)
        {
            Result = result; _grid.DataSource = new BindingList<LocalDocumentCandidate>(result.Files); global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_summary, result.Summary);
        }
        private void SelectEligible(bool value)
        {
            if (_scanning || Result == null) return;
            foreach (var candidate in Result.Files) candidate.Selected = false;
            if(value)foreach(var candidate in Result.Files.Where(f=>f.CanReview&&!(f.Status??"").StartsWith("Needs attention:",StringComparison.OrdinalIgnoreCase)).Take(50))candidate.Selected=true;
            _grid.Refresh();
        }
        private void AcceptSelection()
        {
            if (_scanning || Result == null) return;
            _grid.EndEdit();
            var selected = Result.Files.Where(f => f.Selected && f.CanReview).ToList();
            if (selected.Count == 0) { global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_summary, "Select at least one eligible document to classify and extract for review."); return; }
            if (selected.Count > 50) { global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_summary, "Select at most 50 files per extraction batch. The detected-file list is retained for the next batch."); return; }
            SelectedFiles = selected; DialogResult = DialogResult.OK; Close();
        }
    }
}

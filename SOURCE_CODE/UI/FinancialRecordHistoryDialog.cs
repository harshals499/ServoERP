using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Dapper;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Services;
using ServoERP.Infrastructure;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class FinancialRecordHistoryDialog : ServoFormBase
    {
        private readonly string _module;
        private readonly int? _recordId;
        private readonly DataGridView _history = Grid();
        private readonly DataGridView _values = Grid();
        private readonly DataGridView _archives = Grid();
        private readonly TabControl _tabs = new TabControl { Dock = DockStyle.Fill };
        private readonly Label _status = new Label { Dock = DockStyle.Bottom, Height = 36, Text = "Posted invoices remain locked. Archived posted balances remain in financial reports." };
        private sealed class AuditEntry
        {
            public long AuditId { get; set; }
            public DateTime ChangedUtc { get; set; }
            public string Module { get; set; }
            public int RecordId { get; set; }
            public string Action { get; set; }
            public string UserName { get; set; }
            public string OldValues { get; set; }
            public string NewValues { get; set; }
        }
        public FinancialRecordHistoryDialog(string module, int? recordId = null, bool archives = false)
        {
            if (module != "Invoices" && module != "Jobs") throw new ArgumentException("Unsupported module.");
            _module = module; _recordId = recordId;
            Text = BrandingService.WindowTitle(module + " - Record History");
            Size = new Size(1080, 720); MinimumSize = new Size(880, 520);
            var historyTab = new TabPage("Audit history");
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, Size = new Size(1000, 580), SplitterDistance = 250, Panel1MinSize = 120, Panel2MinSize = 160 };
            split.Panel1.Controls.Add(_history); split.Panel2.Controls.Add(_values); historyTab.Controls.Add(split);
            var archiveTab = new TabPage("Archived records");
            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42 };
            var restore = new Button { Text = "Restore selected", AutoSize = true };
            restore.Click += async (s, e) => await RestoreSelected();
            var view = new Button { Text = "View selected history", AutoSize = true };
            view.Click += (s, e) =>
            {
                if (_archives.CurrentRow == null) return;
                int id = Convert.ToInt32(_archives.CurrentRow.Cells["RecordId"].Value);
                using (var dialog = new FinancialRecordHistoryDialog(_module, id)) dialog.ShowDialog(this);
            };
            actions.Controls.AddRange(new Control[] { restore, view }); archiveTab.Controls.Add(_archives); archiveTab.Controls.Add(actions);
            _tabs.TabPages.AddRange(new[] { historyTab, archiveTab });
            Controls.Add(_tabs); Controls.Add(_status);
            _history.SelectionChanged += (s, e) => ShowChangedValues();
            Shown += async (s, e) => await Reload();
            if (archives) _tabs.SelectedIndex = 1;
        }
        private static DataGridView Grid()
        {
            return new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false, RowHeadersVisible = false, AutoGenerateColumns = true };
        }
        private async Task Reload()
        {
            try
            {
                SessionManager.DemandPermission(_module == "Jobs" ? "WorkOrders" : _module, "View");
                var records = await Task.Run(() => LoadRecords());
                if (!IsDisposed) Bind(records.Item1, records.Item2);

            }
            catch (Exception ex)
            {
                AppLogger.LogError("FinancialRecordHistoryDialog.Reload", ex);
                _status.Text = "Record history could not be loaded. Please refresh or contact ServoERP support.";
            }
        }
        private Tuple<List<AuditEntry>, DataTable> LoadRecords()
        {
            using (var connection = DatabaseConnectionFactory.CreateConnection())
            {
                connection.Open();
                var audit = connection.Query<AuditEntry>(@"SELECT TOP(500) AuditId,ChangedUtc,Module,RecordId,Action,UserName,OldValues,NewValues FROM dbo.FinancialRecordAudit
WHERE (Module=@Module AND (@Id IS NULL OR RecordId=@Id)) OR
(@Module='Invoices' AND Module='Invoice lines' AND (@Id IS NULL OR TRY_CONVERT(int,JSON_VALUE(COALESCE(NewValues,OldValues),'$.InvoiceID'))=@Id))
ORDER BY AuditId DESC", new { Module = _module, Id = _recordId }).ToList();
                string sql = _module == "Invoices"
                    ? "SELECT TOP(500) InvoiceID RecordId,InvoiceNumber Reference,IsPosted,TotalAmount,BalanceDue,DeletedUtc,DeletedByName FROM dbo.Invoices WHERE IsDeleted=1 ORDER BY DeletedUtc DESC"
                    : "SELECT TOP(500) JobID RecordId,JobNumber Reference,Title,Status,InvoiceId,DeletedUtc,DeletedByName FROM dbo.Jobs WHERE IsDeleted=1 ORDER BY DeletedUtc DESC";
                var table = new DataTable(); using (var reader = connection.ExecuteReader(sql)) table.Load(reader);
                return Tuple.Create(audit, table);
            }
        }
        private void Bind(List<AuditEntry> entries, DataTable archives)
        {
            _history.DataSource = entries;
            _history.Columns["OldValues"].Visible = false; _history.Columns["NewValues"].Visible = false;
            _history.Columns["ChangedUtc"].HeaderText = "Changed (UTC)";
            _history.Columns["ChangedUtc"].DefaultCellStyle.Format = "dd/MM/yyyy HH:mm:ss";
            _history.Columns["UserName"].HeaderText = "Changed by";
            _archives.DataSource = archives;
            _archives.Columns["RecordId"].HeaderText = "Record no.";
            _archives.Columns["DeletedByName"].HeaderText = "Archived by";
            _archives.Columns["DeletedUtc"].HeaderText = "Archived (UTC)";
            if (_archives.Columns.Contains("IsPosted")) _archives.Columns["IsPosted"].HeaderText = "Posted";
            if (_archives.Columns.Contains("TotalAmount")) _archives.Columns["TotalAmount"].HeaderText = "Total";
            if (_archives.Columns.Contains("BalanceDue")) _archives.Columns["BalanceDue"].HeaderText = "Balance due";
            if (_archives.Columns.Contains("DeletedUtc")) _archives.Columns["DeletedUtc"].DefaultCellStyle.Format = "dd/MM/yyyy HH:mm:ss";
            _status.Text = "Posted invoices remain locked. Archived posted balances remain in financial reports.";
            ShowChangedValues();
        }
        private void ShowChangedValues()
        {
            var entry = _history.CurrentRow?.DataBoundItem as AuditEntry;
            var table = new DataTable(); table.Columns.Add("Field"); table.Columns.Add("Old value"); table.Columns.Add("New value");
            if (entry != null)
            {
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var oldValues = string.IsNullOrEmpty(entry.OldValues) ? new Dictionary<string, object>() : serializer.Deserialize<Dictionary<string, object>>(entry.OldValues);
                var newValues = string.IsNullOrEmpty(entry.NewValues) ? new Dictionary<string, object>() : serializer.Deserialize<Dictionary<string, object>>(entry.NewValues);
                foreach (string key in oldValues.Keys.Union(newValues.Keys).OrderBy(k => k))
                {
                    object oldValue, newValue; oldValues.TryGetValue(key, out oldValue); newValues.TryGetValue(key, out newValue);
                    string before = Convert.ToString(oldValue, CultureInfo.GetCultureInfo("en-IN"));
                    string after = Convert.ToString(newValue, CultureInfo.GetCultureInfo("en-IN"));
                    if (before != after || entry.OldValues == null || entry.NewValues == null) table.Rows.Add(key, before, after);
                }
            }
            _values.DataSource = table;
        }
        private async Task RestoreSelected()
        {
            if (_archives.CurrentRow == null) return;
            int id = Convert.ToInt32(_archives.CurrentRow.Cells["RecordId"].Value);
            if (!ServoConfirmDialog.Show((Control)this, "Restore archived " + (_module == "Invoices" ? "invoice" : "job"),
                "The record and its existing links will return to active lists. Posted invoices remain locked. Draft stock reservations are recreated on the next save.")) return;
            try { await Task.Run(() => FinancialRecordProtection.Restore(_module, id)); await Reload(); }
            catch (Exception ex) { AppLogger.LogError("FinancialRecordHistoryDialog.Restore", ex); _status.Text = "Restore failed. " + ex.Message; }
        }
        internal void LoadForVisualTest()
        {
            var records = LoadRecords(); Bind(records.Item1, records.Item2);
        }
    }
}

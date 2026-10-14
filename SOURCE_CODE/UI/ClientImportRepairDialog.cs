using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Dapper;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using ServoERP.Infrastructure;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class ClientImportRepairDialog : ServoFormBase
    {
        private sealed class QuoteIdentity
        {
            public int? ClientID { get; set; }
            public string QuotationNumber { get; set; }
            public decimal? BidValue { get; set; }
        }
        private readonly Label _status = new Label();
        private readonly DataGridView _records = new DataGridView();
        private readonly ComboBox _target = new ComboBox();
        private readonly Button _repair = new Button();
        private readonly Button _archive = new Button();
        private readonly CheckBox _showAll = new CheckBox { Text = "Show all clients for manual review", AutoSize = true, Dock = DockStyle.Left };
        private sealed class LinkedCounts
        {
            public int ClientID { get; set; }
            public int Invoices { get; set; }
            public int Jobs { get; set; }
        }
        public bool ChangesApplied { get; private set; }

        public ClientImportRepairDialog()
        {
            Text = "Review Unknown Client Names";
            Size = new Size(960, 600);
            MinimumSize = new Size(800, 480);
            StartPosition = FormStartPosition.CenterParent;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(16) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Review numbers, quotation text and clients missing contact details. These are suggestions, not proof of an invalid client. Select unwanted names to delete from client lists; existing invoices and jobs stay linked. Nothing is deleted automatically." }, 0, 0);
            _records.Dock = DockStyle.Fill;
            _records.ReadOnly = true;
            _records.AllowUserToAddRows = false;
            _records.AllowUserToDeleteRows = false;
            _records.MultiSelect = true;
            _records.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _records.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _records.Columns.Add("ClientId", "ID");
            _records.Columns.Add("Name", "Suspected client");
            _records.Columns.Add("Reason", "Review reason");
            _records.Columns.Add("Invoices", "Invoices");
            _records.Columns.Add("Jobs", "Jobs");
            _records.Columns.Add("Quotes", "Linked quotations / amounts (INR)");
            _records.Columns["Quotes"].FillWeight = 150;
            _records.Columns["ClientId"].FillWeight = 35;
            _records.Columns["Invoices"].FillWeight = 45;
            _records.Columns["Jobs"].FillWeight = 45;
            var selectionPanel = new Panel { Dock = DockStyle.Fill };
            selectionPanel.Controls.Add(_records);
            selectionPanel.Controls.Add(WorkspaceActionUi.CreateSelectionActions(_records));
            var recordFilter = new TextBox { Name = "ImportedClientRecordFilter", Dock = DockStyle.Fill };
            var filterBar = new Panel { Dock = DockStyle.Top, Height = 38, Padding = new Padding(0, 3, 0, 6) };
            var filterLabel = new Label { Text = "Filter records", Dock = DockStyle.Left, Width = 98, TextAlign = ContentAlignment.MiddleLeft };
            var clearFilter = WorkspaceActionUi.CreateClearFilters(() => recordFilter.Clear());
            clearFilter.Dock = DockStyle.Right;
            filterBar.Controls.Add(recordFilter); filterBar.Controls.Add(clearFilter); filterBar.Controls.Add(filterLabel);
            recordFilter.TextChanged += (sender, args) =>
            {
                _records.CurrentCell = null;
                _records.ClearSelection();
                string query = recordFilter.Text.Trim();
                foreach (DataGridViewRow row in _records.Rows)
                    row.Visible = string.IsNullOrEmpty(query) || row.Cells.Cast<DataGridViewCell>().Any(cell => Convert.ToString(cell.Value).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
            };
            selectionPanel.Controls.Add(filterBar);
            var reviewOptions = new Panel { Dock = DockStyle.Top, Height = 30 };
            reviewOptions.Controls.Add(_showAll);
            selectionPanel.Controls.Add(reviewOptions);
            _showAll.CheckedChanged += async (sender, args) => await ReloadAsync();
            root.Controls.Add(selectionPanel, 0, 1);
            _target.DropDownStyle = ComboBoxStyle.DropDownList;
            _target.Dock = DockStyle.Fill;
            _target.DisplayMember = "CompanyName";
            _target.ValueMember = "ClientID";
            var targetPanel = new Panel { Dock = DockStyle.Fill };
            _status.Text = "Loading records...";
            _status.Dock = DockStyle.Top;
            _status.Height = 23;
            _target.Dock = DockStyle.Bottom;
            targetPanel.Controls.Add(_target);
            targetPanel.Controls.Add(_status);
            root.Controls.Add(targetPanel, 0, 2);
            _repair.Text = "Relink selected records and archive";
            _repair.Dock = DockStyle.Right;
            _repair.Width = 290;
            _repair.Click += async (sender, args) => await RepairAsync();
            _archive.Text = "Delete selected names";
            _archive.Dock = DockStyle.Right;
            _archive.Width = 190;
            _archive.Click += async (sender, args) => await ArchiveAsync();
            var actionBar = new Panel { Dock = DockStyle.Fill };
            var close = DS.GhostBtn("Close", 100, 34);
            close.Dock = DockStyle.Left;
            close.Click += (sender, args) => Close();
            actionBar.Controls.Add(close); actionBar.Controls.Add(_repair); actionBar.Controls.Add(_archive);
            root.Controls.Add(actionBar, 0, 3);
            Controls.Add(root);
            Shown += async (sender, args) => await ReloadAsync();
        }

        private async Task ReloadAsync()
        {
            _repair.Enabled = false;
            _archive.Enabled = false;
            _showAll.Enabled = false;
            try
            {
                var payload = await Task.Run(() =>
                {
                    using (var connection = new DatabaseManager().GetConnection())
                    {
                        connection.Open();
                        SmartImportDuplicateDetector.EnsureArchiveSchema(connection);
                        var clients = connection.Query<B2BClient>(@"SELECT * FROM B2BClients c WHERE NOT EXISTS
(SELECT 1 FROM DuplicateMergeArchive a WHERE a.ModuleName='Clients' AND a.DuplicateRecordID=CONVERT(varchar(30),c.ClientID)) ORDER BY CompanyName").ToList();
                        var quotations = connection.Query<QuoteIdentity>(@"SELECT ClientID, QuotationNumber, BidValue FROM Quotations").ToList();
                        var counts = connection.Query<LinkedCounts>(@"SELECT c.ClientID,
(SELECT COUNT(*) FROM dbo.FinancialInvoices i WHERE i.ClientID=c.ClientID) Invoices,
(SELECT COUNT(*) FROM dbo.ActiveJobs j WHERE j.ClientID=c.ClientID) Jobs FROM B2BClients c").ToDictionary(c => c.ClientID);
                        return new { clients, quotations, counts };
                    }
                });
                if (IsDisposed) return;
                _records.Rows.Clear();
                foreach (var client in payload.clients.Where(c => _showAll.Checked || ReviewReason(c) != null))
                {
                    string quotes = string.Join("; ", payload.quotations.Where(q => q.ClientID != null && (int)q.ClientID == client.ClientID)
                        .Select(q => (string)q.QuotationNumber + " / " + Convert.ToDecimal(q.BidValue ?? 0m).ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("en-IN"))));
                    LinkedCounts counts;
                    payload.counts.TryGetValue(client.ClientID, out counts);
                    _records.Rows.Add(client.ClientID, client.CompanyName, ReviewReason(client) ?? "Manual review", counts == null ? 0 : counts.Invoices, counts == null ? 0 : counts.Jobs, quotes.Length == 0 ? "No linked quotations" : quotes);
                }
                _records.ClearSelection();
                _target.DataSource = payload.clients.Where(c => !SmartImportDuplicateDetector.IsSuspectClientName(c.CompanyName) && c.IsActive).ToList();
                _target.SelectedIndex = -1;
                _status.Text = _records.Rows.Count + " client(s) for review. Delete selected names, or choose a correct client below to relink:";
                _repair.Enabled = _records.Rows.Count > 0;
                _archive.Enabled = _records.Rows.Count > 0;
            }
            catch (Exception ex) { AppRuntime.ShowRecoverableError(Text, "Loading client import review", ex); }
            finally { if (!IsDisposed) _showAll.Enabled = true; }
        }

        private static string ReviewReason(B2BClient client)
        {
            if (SmartImportDuplicateDetector.IsSuspectClientName(client.CompanyName))
                return "Number, heading or quotation text";
            if (string.IsNullOrWhiteSpace(client.PrimaryContact) && string.IsNullOrWhiteSpace(client.Phone)
                && string.IsNullOrWhiteSpace(client.Email) && string.IsNullOrWhiteSpace(client.GSTNumber))
                return "No contact, phone, email or GSTIN";
            return null;
        }

        private async Task ArchiveAsync()
        {
            var rows = _records.SelectedRows.Cast<DataGridViewRow>().Where(row => row.Visible).ToList();
            if (rows.Count == 0)
            {
                MessageBox.Show(this, "Select the unwanted client names first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string names = string.Join("\n", rows.Take(12).Select(row => Convert.ToString(row.Cells["Name"].Value)));
            if (rows.Count > 12) names += "\nAnd " + (rows.Count - 12) + " more selected names.";
            if (!ServoConfirmDialog.Show(this, "Delete " + rows.Count + " selected client name(s) from client lists?",
                names + "\n\nThe clients will be archived. Existing invoices, jobs, payments, sites and contracts keep their original client links and amounts.")) return;
            _archive.Enabled = _repair.Enabled = _showAll.Enabled = false;
            try
            {
                SessionManager.DemandPermission("Clients", "Delete");
                var ids = rows.Select(row => Convert.ToInt32(row.Cells["ClientId"].Value)).ToList();
                await Task.Run(() =>
                {
                    var service = new ClientService();
                    foreach (int id in ids) { service.DeleteClient(id); ChangesApplied = true; }
                });
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(Text, "Archiving selected client names", ex);
                await ReloadAsync();
            }
        }

        private async Task RepairAsync()
        {
            var client = _target.SelectedItem as B2BClient;
            var ids = _records.SelectedRows.Cast<DataGridViewRow>().Select(row => Convert.ToString(row.Cells["ClientId"].Value)).ToList();
            if (client == null || ids.Count == 0)
            {
                MessageBox.Show(this, "Select suspected records and the correct client first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!ServoConfirmDialog.Show(this, "Relink " + ids.Count + " suspected client record(s) to " + client.CompanyName + " and archive them?",
                "All linked business records move to this client. Quotation numbers and amounts remain unchanged. Review the selected documents before confirming.")) return;
            _repair.Enabled = _archive.Enabled = _showAll.Enabled = false;
            try
            {
                SessionManager.DemandPermission("Clients", "Edit");
                await Task.Run(() => new SmartImportDuplicateCleanupService().MergeAndArchive(ExcelImportModule.Clients, client.ClientID.ToString(), ids));
                ChangesApplied = true;
                AppDataCache.RemovePrefix("clients:");
                await ReloadAsync();
            }
            catch (Exception ex) { AppRuntime.ShowRecoverableError(Text, "Repairing client links", ex); await ReloadAsync(); }
        }
    }
}

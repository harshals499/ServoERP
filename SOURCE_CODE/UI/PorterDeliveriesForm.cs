using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public class PorterDeliveriesForm : DeferredPageControl
    {
        private const string PorterUrl = "https://porter.in/enterprise";
        private readonly PorterDeliveryService _service = new PorterDeliveryService();
        private readonly ClientService _clients = new ClientService();
        private readonly SiteService _sites = new SiteService();
        private List<PorterDelivery> _items = new List<PorterDelivery>();
        private int _editingId;

        private DataGridView _grid;
        private TextBox _search, _booking, _pickup, _drop, _contact, _phone, _vehicle, _job, _estimated, _final, _driver, _driverPhone, _tracking, _notes;
        private ComboBox _status, _client, _site, _filterStatus;
        private DateTimePicker _scheduled;
        private Label _activeKpi, _todayKpi, _deliveredKpi, _spendKpi;

        public PorterDeliveriesForm()
        {
            Dock = DockStyle.Fill;
            BackColor = DS.BgPage;
            BuildUi();
            EnableDeferredLoad(LoadData, ShowLoadError);
        }

        public override void OnShellActivated()
        {
            if (DeferredLoadCompleted) LoadData();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22, 18, 22, 22), ColumnCount = 1, RowCount = 4, BackColor = DS.BgPage };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 106));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 404));
            var titles = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            titles.Controls.Add(new Label { Text = "Porter Deliveries", AutoSize = true, Font = new Font("Segoe UI Semibold", 18f), ForeColor = DS.Slate900 });
            titles.Controls.Add(new Label { Text = "Book with Porter, then record and track the delivery in ServoERP.", AutoSize = true, Font = new Font("Segoe UI", 9f), ForeColor = DS.Slate600 });
            header.Controls.Add(titles, 0, 0);
            var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(0, 8, 0, 20) };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
            var bookButton = ActionButton("Book on Porter", true, BookOnPorter); bookButton.Dock = DockStyle.Fill;
            var newButton = ActionButton("New Delivery", false, (s, e) => ClearEditor()); newButton.Dock = DockStyle.Fill;
            var refreshButton = ActionButton("Refresh", false, (s, e) => LoadData()); refreshButton.Dock = DockStyle.Fill;
            actions.Controls.Add(bookButton, 0, 0); actions.Controls.Add(newButton, 1, 0); actions.Controls.Add(refreshButton, 2, 0);
            header.Controls.Add(actions, 1, 0);
            root.Controls.Add(header, 0, 0);

            var kpis = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Padding = new Padding(0, 6, 0, 8) };
            for (int i = 0; i < 4; i++) kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            _activeKpi = Kpi(kpis, 0, "ACTIVE DELIVERIES");
            _todayKpi = Kpi(kpis, 1, "SCHEDULED TODAY");
            _deliveredKpi = Kpi(kpis, 2, "DELIVERED");
            _spendKpi = Kpi(kpis, 3, "TOTAL SPEND");
            root.Controls.Add(kpis, 0, 1);

            var filters = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0, 8, 0, 8) };
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            _search = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10f) };
            _search.TextChanged += (s, e) => BindGrid();
            _filterStatus = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _filterStatus.Items.AddRange(new object[] { "All statuses", "Booked", "Assigned", "Picked Up", "In Transit", "Delivered", "Cancelled" });
            _filterStatus.SelectedIndex = 0; _filterStatus.SelectedIndexChanged += (s, e) => BindGrid();
            filters.Controls.Add(_search, 0, 0); filters.Controls.Add(_filterStatus, 1, 0);
            root.Controls.Add(filters, 0, 2);

            var body = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel2, BackColor = DS.Border };
            body.HandleCreated += (s, e) => ApplyBodySplit(body);
            body.SizeChanged += (s, e) => ApplyBodySplit(body);
            body.Panel1.Padding = new Padding(0, 0, 8, 0); body.Panel2.Padding = new Padding(8, 0, 0, 0);
            _grid = BuildGrid(); body.Panel1.Controls.Add(_grid);
            body.Panel2.Controls.Add(BuildEditor());
            root.Controls.Add(body, 0, 3);
        }

        private static void ApplyBodySplit(SplitContainer body)
        {
            if (body == null || body.Width < 700)
                return;

            int editorWidth = Math.Min(400, Math.Max(360, body.Width / 3));
            int distance = Math.Max(360, body.Width - editorWidth - body.SplitterWidth);
            int maximum = body.Width - body.SplitterWidth - 260;
            if (maximum >= 360)
                body.SplitterDistance = Math.Min(distance, maximum);
        }

        private Control BuildEditor()
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(16) };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, ColumnCount = 2, RowCount = 15 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layout.Controls.Add(new Label { Text = "Delivery details", AutoSize = true, Font = new Font("Segoe UI Semibold", 13f), ForeColor = DS.Slate900, Margin = new Padding(0, 0, 0, 10) }, 0, 0);
            layout.SetColumnSpan(layout.GetControlFromPosition(0, 0), 2);
            _booking = AddField(layout, "Porter booking ID *", 1, 0);
            _status = AddCombo(layout, "Status", 1, 1, new[] { "Booked", "Assigned", "Picked Up", "In Transit", "Delivered", "Cancelled" });
            _client = AddCombo(layout, "Client", 2, 0, null); _client.SelectedIndexChanged += ClientChanged;
            _site = AddCombo(layout, "Site", 2, 1, null);
            _pickup = AddField(layout, "Pickup address *", 3, 0);
            _drop = AddField(layout, "Drop address *", 3, 1);
            _contact = AddField(layout, "Contact name", 4, 0); _phone = AddField(layout, "Contact phone", 4, 1);
            _vehicle = AddField(layout, "Vehicle type", 5, 0); _job = AddField(layout, "Linked job ID", 5, 1);
            AddLabel(layout, "Scheduled date", 6, 0);
            _scheduled = new DateTimePicker { Dock = DockStyle.Top, Format = DateTimePickerFormat.Custom, CustomFormat = "dd MMM yyyy  hh:mm tt", ShowCheckBox = true, Height = 32, Margin = new Padding(0, 0, 8, 10) };
            layout.Controls.Add(_scheduled, 0, 7); layout.SetColumnSpan(_scheduled, 2);
            _estimated = AddField(layout, "Estimated amount (₹)", 8, 0); _final = AddField(layout, "Final amount (₹)", 8, 1);
            _driver = AddField(layout, "Driver name", 9, 0); _driverPhone = AddField(layout, "Driver phone", 9, 1);
            _tracking = AddField(layout, "Tracking link", 10, 0); layout.SetColumnSpan(_tracking, 2);
            AddLabel(layout, "Notes", 12, 0);
            _notes = new TextBox { Multiline = true, Height = 58, Dock = DockStyle.Top, ScrollBars = ScrollBars.Vertical, Margin = new Padding(0, 0, 8, 10) };
            layout.Controls.Add(_notes, 0, 13); layout.SetColumnSpan(_notes, 2);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            buttons.Controls.Add(ActionButton("Save Delivery", true, Save)); buttons.Controls.Add(ActionButton("Clear", false, (s, e) => ClearEditor()));
            layout.Controls.Add(buttons, 0, 14); layout.SetColumnSpan(buttons, 2);
            card.Controls.Add(layout); return card;
        }

        private DataGridView BuildGrid()
        {
            var grid = new DataGridView { Dock = DockStyle.Fill, BackgroundColor = Color.White, BorderStyle = BorderStyle.FixedSingle, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoGenerateColumns = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(241, 245, 249), ForeColor = DS.Slate900, Font = new Font("Segoe UI Semibold", 9f), Padding = new Padding(4), SelectionBackColor = Color.FromArgb(241, 245, 249) };
            grid.EnableHeadersVisualStyles = false; grid.RowTemplate.Height = 38;
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "BookingReference", HeaderText = "BOOKING ID", FillWeight = 18 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ClientName", HeaderText = "CLIENT / SITE", FillWeight = 24 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Route", HeaderText = "ROUTE", FillWeight = 34 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Scheduled", HeaderText = "SCHEDULED", FillWeight = 18 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Status", HeaderText = "STATUS", FillWeight = 15 });
            grid.CellClick += GridCellClick; return grid;
        }

        private void LoadData()
        {
            _items = _service.GetAll();
            var clientItems = _clients.GetAllClients().OrderBy(x => x.CompanyName).ToList();
            _client.DataSource = clientItems; _client.DisplayMember = "CompanyName"; _client.ValueMember = "ClientID"; _client.SelectedIndex = -1;
            BindSites(null); BindGrid(); UpdateKpis();
        }

        private void BindGrid()
        {
            string query = (_search.Text ?? string.Empty).Trim(); string status = _filterStatus.SelectedItem as string;
            IEnumerable<PorterDelivery> filtered = _items;
            if (!string.IsNullOrEmpty(query)) filtered = filtered.Where(x => Contains(x.BookingReference, query) || Contains(x.ClientName, query) || Contains(x.SiteName, query) || Contains(x.PickupAddress, query) || Contains(x.DropAddress, query));
            if (!string.IsNullOrEmpty(status) && status != "All statuses") filtered = filtered.Where(x => x.Status == status);
            _grid.DataSource = filtered.Select(x => new { Item = x, x.BookingReference, ClientName = Join(x.ClientName, x.SiteName), Route = JoinRoute(x.PickupAddress, x.DropAddress), Scheduled = x.ScheduledAt.HasValue ? x.ScheduledAt.Value.ToString("dd MMM yyyy, hh:mm tt") : "Not scheduled", x.Status }).ToList();
        }

        private void UpdateKpis()
        {
            _activeKpi.Text = _items.Count(x => x.Status != "Delivered" && x.Status != "Cancelled").ToString();
            _todayKpi.Text = _items.Count(x => x.ScheduledAt.HasValue && x.ScheduledAt.Value.Date == DateTime.Today).ToString();
            _deliveredKpi.Text = _items.Count(x => x.Status == "Delivered").ToString();
            _spendKpi.Text = "₹" + _items.Sum(x => x.FinalAmount).ToString("N0");
        }

        private void Save(object sender, EventArgs e)
        {
            try
            {
                var item = new PorterDelivery { PorterDeliveryId = _editingId, BookingReference = _booking.Text, Status = Convert.ToString(_status.SelectedItem), ClientId = SelectedId(_client), SiteId = SelectedId(_site), LinkedJobId = ParseNullableInt(_job.Text), PickupAddress = _pickup.Text, DropAddress = _drop.Text, ContactName = _contact.Text, ContactPhone = _phone.Text, VehicleType = _vehicle.Text, ScheduledAt = _scheduled.Checked ? (DateTime?)_scheduled.Value : null, EstimatedAmount = ParseMoney(_estimated.Text), FinalAmount = ParseMoney(_final.Text), DriverName = _driver.Text, DriverPhone = _driverPhone.Text, TrackingUrl = _tracking.Text, Notes = _notes.Text };
                _service.Save(item); LoadData(); ClearEditor(); MessageBox.Show("Porter delivery saved.", "ServoERP", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Unable to save delivery", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void BookOnPorter(object sender, EventArgs e)
        {
            ClearEditor();
            Process.Start(new ProcessStartInfo { FileName = PorterUrl, UseShellExecute = true });
            _booking.Focus();
            MessageBox.Show("Complete the booking on Porter, then paste the returned booking ID here and save the delivery.", "Record Porter booking", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void GridCellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            object source = _grid.Rows[e.RowIndex].DataBoundItem; var prop = source.GetType().GetProperty("Item");
            var x = prop == null ? null : prop.GetValue(source, null) as PorterDelivery; if (x == null) return;
            _editingId = x.PorterDeliveryId; _booking.Text = x.BookingReference; _status.SelectedItem = x.Status; SelectValue(_client, x.ClientId); BindSites(x.ClientId); SelectValue(_site, x.SiteId);
            _pickup.Text = x.PickupAddress; _drop.Text = x.DropAddress; _contact.Text = x.ContactName; _phone.Text = x.ContactPhone; _vehicle.Text = x.VehicleType; _job.Text = x.LinkedJobId.HasValue ? x.LinkedJobId.ToString() : string.Empty;
            _scheduled.Checked = x.ScheduledAt.HasValue; if (x.ScheduledAt.HasValue) _scheduled.Value = x.ScheduledAt.Value;
            _estimated.Text = x.EstimatedAmount.ToString("0.##"); _final.Text = x.FinalAmount.ToString("0.##"); _driver.Text = x.DriverName; _driverPhone.Text = x.DriverPhone; _tracking.Text = x.TrackingUrl; _notes.Text = x.Notes;
        }

        private void ClearEditor()
        {
            _editingId = 0; foreach (TextBox box in new[] { _booking, _pickup, _drop, _contact, _phone, _vehicle, _job, _estimated, _final, _driver, _driverPhone, _tracking, _notes }) box.Clear();
            _status.SelectedIndex = 0; _client.SelectedIndex = -1; BindSites(null); _scheduled.Checked = false;
        }

        private void ClientChanged(object sender, EventArgs e) { BindSites(SelectedId(_client)); }
        private void BindSites(int? clientId) { var data = _sites.GetAll().Where(x => !clientId.HasValue || x.ClientID == clientId.Value).OrderBy(x => x.SiteName).ToList(); _site.DataSource = data; _site.DisplayMember = "DisplayName"; _site.ValueMember = "SiteID"; _site.SelectedIndex = -1; }
        private void ShowLoadError(Exception ex) { MessageBox.Show("Porter Deliveries could not be loaded. " + ex.Message, "ServoERP", MessageBoxButtons.OK, MessageBoxIcon.Warning); }

        private static Button ActionButton(string text, bool primary, EventHandler click) { var b = new Button { Text = text, Height = 38, MinimumSize = new Size(90, 38), FlatStyle = FlatStyle.Flat, BackColor = primary ? DS.Primary600 : Color.White, ForeColor = primary ? Color.White : DS.Slate900, Font = new Font("Segoe UI Semibold", 9f), Margin = new Padding(6, 0, 0, 0), Cursor = Cursors.Hand }; b.FlatAppearance.BorderColor = primary ? DS.Primary600 : DS.Border; b.Click += click; return b; }
        private static Label Kpi(TableLayoutPanel host, int column, string caption) { var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(column == 0 ? 0 : 6, 0, column == 3 ? 0 : 6, 0), Padding = new Padding(14, 10, 14, 10) }; var value = new Label { Text = "0", Dock = DockStyle.Top, Height = 36, Font = new Font("Segoe UI Semibold", 17f), ForeColor = DS.Slate900 }; panel.Controls.Add(new Label { Text = caption, Dock = DockStyle.Bottom, Height = 24, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI Semibold", 8f), ForeColor = DS.Slate600 }); panel.Controls.Add(value); host.Controls.Add(panel, column, 0); return value; }
        private static void AddLabel(TableLayoutPanel p, string text, int row, int col) { p.Controls.Add(new Label { Text = text, AutoSize = true, ForeColor = DS.Slate600, Font = new Font("Segoe UI Semibold", 8.5f), Margin = new Padding(0, 2, 0, 3) }, col, row); }
        private static TextBox AddField(TableLayoutPanel p, string label, int row, int col) { var host = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, RowCount = 2, Margin = new Padding(0, 0, col == 0 ? 8 : 0, 8) }; host.RowStyles.Add(new RowStyle(SizeType.AutoSize)); host.RowStyles.Add(new RowStyle(SizeType.Absolute, 31)); host.Controls.Add(new Label { Text = label, AutoSize = true, ForeColor = DS.Slate600, Font = new Font("Segoe UI Semibold", 8.5f) }, 0, 0); var box = new TextBox { Dock = DockStyle.Fill }; host.Controls.Add(box, 0, 1); p.Controls.Add(host, col, row); return box; }
        private static ComboBox AddCombo(TableLayoutPanel p, string label, int row, int col, string[] values) { var host = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, RowCount = 2, Margin = new Padding(0, 0, col == 0 ? 8 : 0, 8) }; host.Controls.Add(new Label { Text = label, AutoSize = true, ForeColor = DS.Slate600, Font = new Font("Segoe UI Semibold", 8.5f) }, 0, 0); var combo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList }; if (values != null) combo.Items.AddRange(values); if (combo.Items.Count > 0) combo.SelectedIndex = 0; host.Controls.Add(combo, 0, 1); p.Controls.Add(host, col, row); return combo; }
        private static int? SelectedId(ComboBox combo) { if (combo.SelectedValue == null || combo.SelectedValue is DataRowView) return null; int value; return int.TryParse(Convert.ToString(combo.SelectedValue), out value) ? (int?)value : null; }
        private static void SelectValue(ComboBox combo, int? value) { combo.SelectedIndex = -1; if (value.HasValue) combo.SelectedValue = value.Value; }
        private static int? ParseNullableInt(string text) { int value; return int.TryParse(text, out value) ? (int?)value : null; }
        private static decimal ParseMoney(string text) { decimal value; return decimal.TryParse(text, out value) ? value : 0m; }
        private static bool Contains(string value, string query) { return (value ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0; }
        private static string Join(string a, string b) { return string.Join(" / ", new[] { a, b }.Where(x => !string.IsNullOrWhiteSpace(x))); }
        private static string JoinRoute(string a, string b) { return (a ?? string.Empty) + "  →  " + (b ?? string.Empty); }
    }
}

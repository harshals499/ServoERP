using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public class ClientDetailPage : BaseUserControl
    {
        private static readonly Color Teal = ColorTranslator.FromHtml("#1D9E75");
        private static readonly Color TealBg = ColorTranslator.FromHtml("#E1F5EE");
        private static readonly Color TealText = ColorTranslator.FromHtml("#0F6E56");
        private static readonly Color Border = DS.Border;
        private static readonly Color Surface = Color.FromArgb(248, 248, 248);
        private static readonly Color TextMain = Color.FromArgb(24, 24, 27);
        private static readonly Color TextMuted = Color.FromArgb(101, 112, 128);
        private static readonly Color RedText = ColorTranslator.FromHtml("#A32D2D");
        private static readonly Color AmberText = ColorTranslator.FromHtml("#854F0B");

        private readonly ClientService _clientService = new ClientService();
        private readonly SiteService _siteService = new SiteService();
        private readonly ContractService _contractService = new ContractService();
        private readonly JobService _jobService = new JobService();
        private readonly InvoiceService _invoiceService = new InvoiceService();
        private readonly SiteTimelineService _siteTimelineService = new SiteTimelineService();
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();
        private readonly ToolTip _toolTip = new ToolTip();

        private B2BClient _client;
        private FlowLayoutPanel _leftStack;
        private FlowLayoutPanel _rightStack;
        private Label _titleLabel;
        private Label _savedLabel;
        private DataGridView _teamGrid;
        private FlowLayoutPanel _sitesFlow;

        private TextBox _txtCompany;
        private TextBox _txtIndustry;
        private TextBox _txtGst;
        private TextBox _txtPan;
        private TextBox _txtAddress;
        private TextBox _txtCity;
        private TextBox _txtTerms;
        private TextBox _txtLimit;
        private TextBox _txtPrimary;
        private TextBox _txtSecondary;
        private TextBox _txtPhone;
        private TextBox _txtEmail;

        public int ClientId { get; set; }
        public int HighlightSiteId { get; set; }
        public Action<int> OnBackToClients { get; set; }
        public Action OnBackToDashboard { get; set; }

        public ClientDetailPage()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9f);
            BuildLayout();
            SalesUiPolishService.ApplyAfterRebuild(this, "Client Detail");
        }

        public void LoadClient(int clientId)
        {
            ClientId = clientId;
            LoadClient();
        }

        public void LoadClient()
        {
            if (ClientId <= 0)
                throw new InvalidOperationException("Client id is required.");

            try
            {
                _client = _clientService.GetClientById(ClientId);
                if (_client == null)
                    throw new Exception("Client not found.");
                RenderClient();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("ClientDetailPage.LoadClient", ex);
                throw;
            }
        }

        private void BuildLayout()
        {
            Controls.Clear();

            Panel top = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Color.White, Padding = new Padding(12, 8, 12, 8) };
            top.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, top.Height - 1, top.Width, top.Height - 1);
            FlowLayoutPanel nav = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 244, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.White, Padding = new Padding(0) };
            Button dashboard = Button("<- Dashboard", Color.White, TextMain, 116);
            dashboard.FlatAppearance.BorderColor = Border;
            dashboard.Click += (s, e) => OnBackToDashboard?.Invoke();
            Button back = Button("<- Clients", Color.White, TextMain, 92);
            back.FlatAppearance.BorderColor = Border;
            back.Click += (s, e) => OnBackToClients?.Invoke(ClientId);
            nav.Controls.Add(dashboard);
            nav.Controls.Add(back);
            _titleLabel = new Label { Text = "Client", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = TextMain };
            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 198, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Color.White };
            Button save = Button("Save changes", Teal, Color.White, 112);
            Button print = Button("Print", Color.White, TextMain, 70);
            save.Click += (s, e) => SaveClient();
            print.Click += (s, e) => PrintClientProfile();
            actions.Controls.Add(save);
            actions.Controls.Add(print);
            top.Controls.Add(_titleLabel);
            top.Controls.Add(actions);
            top.Controls.Add(nav);

            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 40, BackColor = Color.White, Padding = new Padding(8, 5, 12, 5) };
            footer.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, 0, footer.Width, 0);
            _savedLabel = new Label { Dock = DockStyle.Right, Width = 80, ForeColor = TealText, TextAlign = ContentAlignment.MiddleLeft };
            Button footerSave = Button("Save changes", Teal, Color.White, 112);
            footerSave.Dock = DockStyle.Right;
            footerSave.Click += (s, e) => SaveClient();
            Button cancel = Button("Cancel", Color.White, TextMain, 82);
            cancel.Dock = DockStyle.Right;
            cancel.FlatAppearance.BorderColor = Border;
            cancel.Click += (s, e) => OnBackToClients?.Invoke(ClientId);
            footer.Controls.Add(_savedLabel);
            footer.Controls.Add(footerSave);
            footer.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 8 });
            footer.Controls.Add(cancel);

            TableLayoutPanel body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Color.White };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260f));
            _leftStack = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(18, 14, 18, 20), BackColor = Color.White };
            _rightStack = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(12), BackColor = Surface };
            _leftStack.Resize += (s, e) => ResizeClientDetailCards(_leftStack);
            _rightStack.Resize += (s, e) => ResizeClientDetailCards(_rightStack);
            body.Controls.Add(_leftStack, 0, 0);
            body.Controls.Add(_rightStack, 1, 0);

            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(top);
        }

        private void RenderClient()
        {
            if (_client == null)
                return;

            _titleLabel.Text = _client.CompanyName ?? "Client";
            _leftStack.SuspendLayout();
            _rightStack.SuspendLayout();
            _leftStack.Controls.Clear();
            _rightStack.Controls.Clear();

            _leftStack.Controls.Add(BuildIdentitySection());
            _leftStack.Controls.Add(BuildStatsSection());
            _leftStack.Controls.Add(BuildStageSection());
            _leftStack.Controls.Add(BuildCompanyDetailsSection());
            _leftStack.Controls.Add(BuildBillingSection());
            _leftStack.Controls.Add(BuildContactSection());
            _leftStack.Controls.Add(BuildTeamSection());
            _leftStack.Controls.Add(BuildSitesSection());

            _rightStack.Controls.Add(BuildNotesCard());
            _rightStack.Controls.Add(BuildSiteTimelineCard());

            _leftStack.ResumeLayout();
            _rightStack.ResumeLayout();
            ResizeClientDetailCards(_leftStack);
            ResizeClientDetailCards(_rightStack);
            SalesUiPolishService.ApplyAfterRebuild(this, "Client Detail");
        }

        private Panel BuildIdentitySection()
        {
            Panel card = Section(760, 104);
            Panel avatar = Avatar(Initials(_client.CompanyName), 48);
            avatar.Location = new Point(14, 16);
            card.Controls.Add(avatar);
            card.Controls.Add(new Label { Text = _client.CompanyName ?? "Client", Location = new Point(78, 14), Size = new Size(420, 24), Font = new Font("Segoe UI", 16f, FontStyle.Bold), ForeColor = TextMain, AutoEllipsis = true, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right });
            card.Controls.Add(new Label { Text = Safe(_client.IndustryType, "-") + " · " + Safe(_client.City, "-"), Location = new Point(80, 42), Size = new Size(420, 18), ForeColor = TextMuted, AutoEllipsis = true, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right });
            int x = 80;
            foreach (string tag in SplitTags(_client.Tags).Take(5))
            {
                Label pill = MakeTag(tag);
                pill.Location = new Point(x, 67);
                card.Controls.Add(pill);
                x += pill.Width + 6;
            }
            return card;
        }

        private Panel BuildStatsSection()
        {
            Panel card = Section(760, 72);
            ClientStats stats = LoadStats();
            AddStat(card, 0, "Open jobs", stats.OpenJobs, stats.OverdueJobs > 0 ? stats.OverdueJobs + " overdue" : "None", stats.OverdueJobs > 0 ? RedText : TextMuted);
            AddStat(card, 250, "Active contracts", stats.ActiveContracts, stats.ActiveContracts == "0" ? "None" : "Active", stats.ActiveContracts == "0" ? TextMuted : TealText);
            AddStat(card, 500, "Outstanding", stats.Outstanding, stats.OutstandingRaw <= 0 ? "Clear" : stats.Outstanding, stats.OutstandingRaw <= 0 ? TealText : RedText);
            return card;
        }

        private Panel BuildStageSection()
        {
            Panel card = Section(760, 86);
            card.Controls.Add(new Label { Text = "Client status", Location = new Point(14, 8), Size = new Size(220, 22), Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = TextMain });
            string[] stages = ClientStatusOptions();
            string current = GetClientStatus(_client);
            for (int i = 0; i < stages.Length; i++)
            {
                string stage = stages[i];
                Button step = Button((stage == current ? "● " : "○ ") + stage, Color.White, stage == current ? TealText : TextMuted, stage == "Blacklisted" ? 124 : 104);
                step.Location = new Point(14 + (i * 138), 40);
                step.Tag = stage;
                step.FlatAppearance.BorderColor = stage == current ? Teal : Border;
                step.Click += (s, e) => MoveStage((string)((Button)s).Tag);
                card.Controls.Add(step);
            }
            return card;
        }

        private Control BuildCompanyDetailsSection()
        {
            AccordionPanel card = AccordionSection("Company details", 760, 236, true);
            TableLayoutPanel grid = Grid(2, 3, 220);
            _txtCompany = Field(grid, "Company name", _client.CompanyName, 0, 0);
            _txtIndustry = Field(grid, "Industry", _client.IndustryType, 0, 1);
            _txtGst = Field(grid, "GSTIN", _client.GSTNumber, 1, 0);
            _txtPan = Field(grid, "PAN", _client.PANNumber, 1, 1);
            _txtAddress = Field(grid, "Billing address", _client.BillingAddress, 2, 0);
            _txtCity = Field(grid, "City", _client.City, 2, 1);
            card.ContentPanel.Controls.Add(grid);
            return card;
        }

        private Control BuildBillingSection()
        {
            AccordionPanel card = AccordionSection("Billing & payment", 760, 96, true);
            TableLayoutPanel grid = Grid(3, 1, 76);
            Field(grid, "City", _client.City, 0, 0);
            _txtTerms = Field(grid, "Payment terms (days)", _client.PaymentTermsDays.ToString(), 0, 1);
            _txtLimit = Field(grid, "Credit limit", _client.CreditLimit.ToString("0.##"), 0, 2);
            card.ContentPanel.Controls.Add(grid);
            return card;
        }

        private Control BuildContactSection()
        {
            AccordionPanel card = AccordionSection("Contact information", 760, 166, true);
            TableLayoutPanel grid = Grid(2, 2, 148);
            _txtPrimary = Field(grid, "Primary contact", _client.PrimaryContact, 0, 0);
            _txtSecondary = Field(grid, "Secondary contact", _client.SecondaryContact, 0, 1);
            _txtPhone = Field(grid, "Phone", _client.Phone, 1, 0);
            _txtEmail = Field(grid, "Email", _client.Email, 1, 1);
            card.ContentPanel.Controls.Add(grid);
            return card;
        }

        private Control BuildTeamSection()
        {
            List<ClientTeamMember> members = SafeTeamMembers();
            AccordionPanel card = AccordionSection("Company team", 760, 270, true, members.Count.ToString());
            Button add = Button("+ Add contact", Color.White, TealText, 112);
            add.Location = new Point(card.Width - 132, 8);
            add.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            add.FlatAppearance.BorderColor = Border;
            add.Click += (s, e) => _teamGrid.Rows.Add(0, ClientId, "", "", "", "", false, true);
            card.ContentPanel.Controls.Add(add);
            _teamGrid = new DataGridView
            {
                Location = new Point(14, 42),
                Size = new Size(728, 178),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                RowHeadersVisible = false
            };
            _teamGrid.Columns.Add("Id", "Id");
            _teamGrid.Columns.Add("ClientId", "ClientId");
            _teamGrid.Columns.Add("EmployeeName", "Name");
            _teamGrid.Columns.Add("Position", "Position");
            _teamGrid.Columns.Add("EmailId", "Email");
            _teamGrid.Columns.Add("ContactNo", "Phone");
            _teamGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsPrimary", HeaderText = "Primary" });
            _teamGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsActive", HeaderText = "Active" });
            _teamGrid.Columns["Id"].Visible = false;
            _teamGrid.Columns["ClientId"].Visible = false;
            GridTheme.Apply(_teamGrid);
            foreach (ClientTeamMember member in members)
                _teamGrid.Rows.Add(member.Id, member.ClientId, member.EmployeeName, member.Position, member.EmailId, member.ContactNo, member.IsPrimary, member.IsActive);
            Button saveTeam = Button("Save team", Teal, Color.White, 96);
            saveTeam.Location = new Point(14, 228);
            saveTeam.Click += (s, e) => SaveTeam();
            card.ContentPanel.Controls.Add(_teamGrid);
            card.ContentPanel.Controls.Add(saveTeam);
            return card;
        }

        private Control BuildSitesSection()
        {
            List<ClientSite> sites = SafeSites();
            AccordionPanel card = AccordionSection("Sites", 760, 126, true, sites.Count.ToString());
            Button add = Button("+ Add site", Color.White, TealText, 92);
            add.Location = new Point(card.Width - 112, 8);
            add.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            add.FlatAppearance.BorderColor = Border;
            add.Click += (s, e) => AddSite();
            _sitesFlow = new FlowLayoutPanel { Location = new Point(14, 42), Size = new Size(728, 74), WrapContents = true, AutoScroll = true, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            foreach (ClientSite site in sites)
                _sitesFlow.Controls.Add(SiteChip(site));
            card.ContentPanel.Controls.Add(add);
            card.ContentPanel.Controls.Add(_sitesFlow);
            return card;
        }

        private Control BuildNotesCard()
        {
            AccordionPanel card = SideCard(236, 214, "Sticky notes");
            int y = 8;
            foreach (ClientNote note in ParseNotes(_client.Notes).Take(3))
            {
                Label item = new Label { Text = Safe(note.Title, "Note") + "\r\n" + Safe(note.Body, ""), Location = new Point(14, y), Size = new Size(208, 52), BackColor = Color.White, ForeColor = TextMuted, Padding = new Padding(6), AutoEllipsis = true, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                item.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(Border), 0, 0, item.Width - 1, item.Height - 1);
                card.ContentPanel.Controls.Add(item);
                y += 58;
            }
            TextBox add = new TextBox { Location = new Point(14, Math.Min(202, y)), Width = 208, Text = "+ Add a note...", Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            add.Enter += (s, e) => { if (add.Text == "+ Add a note...") add.Text = ""; };
            add.Leave += (s, e) => SaveNote(add);
            add.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { SaveNote(add); e.SuppressKeyPress = true; } };
            card.ContentPanel.Controls.Add(add);
            return card;
        }

        private Control BuildSiteTimelineCard()
        {
            List<ClientSite> sites = SafeSites();
            ClientSite selectedSite = sites.FirstOrDefault(s => s.SiteID == HighlightSiteId) ?? sites.FirstOrDefault();
            string title = selectedSite == null ? "Site timeline" : "Timeline: " + Safe(selectedSite.SiteName, "Site");
            AccordionPanel card = SideCard(236, 252, title);

            if (selectedSite == null)
            {
                card.ContentPanel.Controls.Add(new Label
                {
                    Text = "No site is available for this client.",
                    Location = new Point(14, 12),
                    Size = new Size(208, 40),
                    ForeColor = TextMuted,
                    AutoEllipsis = true,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                });
                return card;
            }

            int y = 8;
            foreach (SiteTimelineItem item in _siteTimelineService.GetTimeline(selectedSite.SiteID, 5))
            {
                Label icon = new Label { Text = ActivityLetter(item.EventType), Location = new Point(14, y + 2), Size = new Size(22, 22), BackColor = TealBg, ForeColor = TealText, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8f, FontStyle.Bold) };
                Label titleLabel = new Label { Text = Safe(item.Title, item.EventType), Location = new Point(44, y), Size = new Size(170, 18), Font = new Font("Segoe UI", 8.3f, FontStyle.Bold), ForeColor = TextMain, AutoEllipsis = true, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                Label metaLabel = new Label { Text = item.EventDate == DateTime.MinValue ? item.EventType : item.EventDate.ToString("dd/MM/yyyy") + " | " + item.EventType, Location = new Point(44, y + 18), Size = new Size(170, 16), Font = new Font("Segoe UI", 7.5f), ForeColor = TextMuted, AutoEllipsis = true, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                NavigationHelper.MakeLink(icon, () => NavigationHelper.OpenTimelineItem(this, item));
                NavigationHelper.MakeLink(titleLabel, () => NavigationHelper.OpenTimelineItem(this, item));
                NavigationHelper.MakeLink(metaLabel, () => NavigationHelper.OpenTimelineItem(this, item));
                card.ContentPanel.Controls.Add(icon);
                card.ContentPanel.Controls.Add(titleLabel);
                card.ContentPanel.Controls.Add(metaLabel);
                y += 42;
            }

            if (y == 8)
            {
                card.ContentPanel.Controls.Add(new Label
                {
                    Text = "No jobs, invoices, purchases, service tickets, or contracts yet.",
                    Location = new Point(14, 12),
                    Size = new Size(208, 54),
                    ForeColor = TextMuted,
                    AutoEllipsis = true,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                });
            }

            return card;
        }

        private void SaveClient()
        {
            if (_client == null)
                return;
            try
            {
                _client.CompanyName = _txtCompany.Text.Trim();
                _client.IndustryType = _txtIndustry.Text.Trim();
                _client.GSTNumber = _txtGst.Text.Trim();
                _client.PANNumber = _txtPan.Text.Trim();
                _client.BillingAddress = _txtAddress.Text.Trim();
                _client.City = _txtCity.Text.Trim();
                _client.PaymentTermsDays = ParseInt(_txtTerms.Text);
                _client.CreditLimit = ParseDecimal(_txtLimit.Text);
                _client.PrimaryContact = _txtPrimary.Text.Trim();
                _client.SecondaryContact = _txtSecondary.Text.Trim();
                _client.Phone = _txtPhone.Text.Trim();
                _client.Email = _txtEmail.Text.Trim();
                _clientService.UpdateClient(_client);
                _clientService.LogActivity(new ClientActivity { ClientId = ClientId, ActivityType = "Note", Title = "Profile updated", Detail = "Client profile changes saved." });
                _savedLabel.Text = "Saved";
            }
            catch (Exception ex)
            {
                AppLogger.LogError("ClientDetailPage.SaveClient", ex);
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Clients"), "Saving client", ex);
            }
        }

        private void PrintClientProfile()
        {
            try
            {
                using (var preview = new HtmlPreviewDialog("Client Profile - " + Safe(_client.CompanyName, "Client"), BuildClientProfileHtml()))
                    preview.ShowDialog(this);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("ClientDetailPage.PrintClientProfile", ex);
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Clients"), "Printing client profile", ex);
            }
        }

        private string BuildClientProfileHtml()
        {
            ClientStats stats = LoadStats();
            List<ClientSite> sites = SafeSites();
            List<ClientTeamMember> team = SafeTeamMembers();
            string rows = string.Join("", sites.Select(s => "<tr><td>" + H(s.SiteName) + "</td><td>" + H(s.City) + "</td><td>" + H(s.Address) + "</td></tr>"));
            if (string.IsNullOrEmpty(rows))
                rows = "<tr><td colspan='3' class='muted'>No sites configured.</td></tr>";
            string contacts = string.Join("", team.Select(t => "<tr><td>" + H(t.EmployeeName) + "</td><td>" + H(t.Position) + "</td><td>" + H(t.EmailId) + "</td><td>" + H(t.ContactNo) + "</td></tr>"));
            if (string.IsNullOrEmpty(contacts))
                contacts = "<tr><td colspan='4' class='muted'>No team contacts configured.</td></tr>";

            return @"<!doctype html><html><head><meta charset='utf-8'><style>
body{font-family:'Segoe UI',Arial,sans-serif;margin:34px;color:#0f172a;background:#fff}
h1{font-size:28px;margin:0 0 6px} h2{font-size:16px;margin:26px 0 10px;color:#4338ca}
.muted{color:#64748b}.header{display:flex;justify-content:space-between;border-bottom:1px solid #e2e8f0;padding-bottom:18px}
.badge{background:#dcfce7;color:#166534;border-radius:999px;padding:5px 10px;font-weight:700;font-size:12px}
.grid{display:grid;grid-template-columns:repeat(3,1fr);gap:12px;margin:20px 0}
.metric{border:1px solid #e2e8f0;border-radius:10px;padding:14px;background:#f8fafc}.metric b{display:block;font-size:20px}
table{width:100%;border-collapse:collapse;margin-top:8px}th,td{border:1px solid #e2e8f0;padding:9px;text-align:left;font-size:13px}th{background:#f1f5f9}
.kv{display:grid;grid-template-columns:180px 1fr;gap:8px;font-size:13px}.kv div{padding:5px 0;border-bottom:1px solid #f1f5f9}
li{margin:0 0 10px}li span{float:right;color:#64748b;font-size:12px}p{margin:4px 0 0;color:#475569}
@media print{button{display:none}body{margin:18px}}
</style></head><body>
<div class='header'><div><h1>" + H(_client.CompanyName) + @"</h1><div class='muted'>" + H(_client.IndustryType) + " - " + H(_client.City) + @"</div></div><div class='badge'>" + H(NormalizeStage(_client.RelationshipStage)) + @"</div></div>
<div class='grid'><div class='metric'><span>Open jobs</span><b>" + H(stats.OpenJobs) + @"</b><small>" + H(stats.OverdueJobs > 0 ? stats.OverdueJobs + " overdue" : "None overdue") + @"</small></div><div class='metric'><span>Active contracts</span><b>" + H(stats.ActiveContracts) + @"</b></div><div class='metric'><span>Outstanding</span><b>" + H(stats.Outstanding) + @"</b></div></div>
<h2>Company</h2><div class='kv'><div>GSTIN</div><div>" + H(_client.GSTNumber) + @"</div><div>PAN</div><div>" + H(_client.PANNumber) + @"</div><div>Phone</div><div>" + H(_client.Phone) + @"</div><div>Email</div><div>" + H(_client.Email) + @"</div><div>Billing address</div><div>" + H(_client.BillingAddress) + @"</div></div>
<h2>Sites</h2><table><thead><tr><th>Site</th><th>City</th><th>Address</th></tr></thead><tbody>" + rows + @"</tbody></table>
<h2>Contacts</h2><table><thead><tr><th>Name</th><th>Role</th><th>Email</th><th>Phone</th></tr></thead><tbody>" + contacts + @"</tbody></table>
</body></html>";
        }

        private static string H(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }

        private void SaveTeam()
        {
            try
            {
                foreach (DataGridViewRow row in _teamGrid.Rows)
                {
                    if (row.IsNewRow)
                        continue;
                    string name = Convert.ToString(row.Cells["EmployeeName"].Value);
                    if (string.IsNullOrWhiteSpace(name))
                        continue;
                    _clientService.SaveTeamMember(new ClientTeamMember
                    {
                        Id = ParseInt(Convert.ToString(row.Cells["Id"].Value)),
                        ClientId = ClientId,
                        EmployeeName = name.Trim(),
                        Position = Convert.ToString(row.Cells["Position"].Value),
                        EmailId = Convert.ToString(row.Cells["EmailId"].Value),
                        ContactNo = Convert.ToString(row.Cells["ContactNo"].Value),
                        IsPrimary = Convert.ToBoolean(row.Cells["IsPrimary"].Value ?? false),
                        IsActive = Convert.ToBoolean(row.Cells["IsActive"].Value ?? true)
                    });
                }
                _savedLabel.Text = "Saved";
            }
            catch (Exception ex)
            {
                AppLogger.LogError("ClientDetailPage.SaveTeam", ex);
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Clients"), "Saving team", ex);
            }
        }

        private void MoveStage(string stage)
        {
            if (MessageBox.Show("Change client status to " + stage + "?", "Client status", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            ApplyLifecycleLocal(_client, stage);
            _clientService.UpdateLifecycleStatus(ClientId, stage);
            RenderClient();
        }

        private void AddSite()
        {
            SitePromptResult site = PromptSiteDetails();
            if (site == null || string.IsNullOrWhiteSpace(site.SiteName))
                return;
            try
            {
                _siteService.Create(new ClientSite
                {
                    ClientID = ClientId,
                    SiteName = site.SiteName.Trim(),
                    Address = (site.Address ?? string.Empty).Trim(),
                    City = string.IsNullOrWhiteSpace(site.City) ? _client.City : site.City.Trim()
                });
                RenderClient();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("ClientDetailPage.AddSite", ex);
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Clients"), "Adding site", ex);
            }
        }

        private Control SiteChip(ClientSite site)
        {
            Button chip = Button(Safe(site.SiteName, "Site") + " · " + Safe(site.City, "-") + " x", Color.White, TextMain, 180);
            if (site.SiteID == HighlightSiteId)
            {
                chip.Text = Safe(site.SiteName, "Site") + " · " + Safe(site.City, "-");
                chip.BackColor = TealBg;
                chip.ForeColor = TealText;
                chip.FlatAppearance.BorderColor = Teal;
            }
            else
            {
                chip.FlatAppearance.BorderColor = Border;
            }
            if (site.SiteID == HighlightSiteId)
            {
                chip.Click += (s, e) =>
                {
                    HighlightSiteId = 0;
                    RenderClient();
                };
            }
            else
            {
                chip.Click += (s, e) =>
                {
                    if (MessageBox.Show("Remove site " + site.SiteName + "?", "Sites", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                        return;
                    _siteService.Delete(site.SiteID);
                    RenderClient();
                };
            }
            return chip;
        }

        private ClientStats LoadStats()
        {
            ClientStats stats = new ClientStats { OpenJobs = "-", ActiveContracts = "-", Outstanding = "-" };
            try
            {
                List<Job> jobs = _jobService.GetAll().Where(j => j.ClientID == ClientId).ToList();
                stats.OpenJobs = jobs.Count(j => !IsClosedJob(j)).ToString();
                stats.OverdueJobs = jobs.Count(j => j.IsOverdue && !IsClosedJob(j));
            }
            catch (Exception ex) { AppLogger.LogError("ClientDetailPage.LoadStats.Jobs", ex); }
            try
            {
                stats.ActiveContracts = _contractService.GetContractsByClient(ClientId).Count(c => string.Equals(c.ContractStatus, "Active", StringComparison.OrdinalIgnoreCase) && c.EndDate >= DateTime.Today).ToString();
            }
            catch (Exception ex) { AppLogger.LogError("ClientDetailPage.LoadStats.Contracts", ex); }
            try
            {
                stats.OutstandingRaw = _invoiceService.GetInvoicesForClient(ClientId).Sum(i => i.BalanceDue);
                stats.Outstanding = IndiaFormatHelper.FormatCurrency(stats.OutstandingRaw);
            }
            catch (Exception ex) { AppLogger.LogError("ClientDetailPage.LoadStats.Invoices", ex); }
            return stats;
        }

        private List<ClientTeamMember> SafeTeamMembers()
        {
            try { return _clientService.GetTeamMembers(ClientId); }
            catch (Exception ex) { AppLogger.LogError("ClientDetailPage.SafeTeamMembers", ex); return new List<ClientTeamMember>(); }
        }

        private List<ClientSite> SafeSites()
        {
            try { return _siteService.GetByClientId(ClientId); }
            catch (Exception ex) { AppLogger.LogError("ClientDetailPage.SafeSites", ex); return new List<ClientSite>(); }
        }

        private void SaveNote(TextBox box)
        {
            if (box == null || string.IsNullOrWhiteSpace(box.Text) || box.Text == "+ Add a note...")
                return;
            List<ClientNote> notes = ParseNotes(_client.Notes);
            notes.Insert(0, new ClientNote { Title = "Note", Body = box.Text.Trim(), CreatedAt = DateTime.Now, CreatedBy = SessionManager.CurrentUser == null ? "System" : SessionManager.CurrentUser.DisplayName });
            _client.Notes = _json.Serialize(notes);
            _clientService.UpdateClient(_client);
            _clientService.LogActivity(new ClientActivity { ClientId = ClientId, ActivityType = "Note", Title = "Note", Detail = box.Text.Trim() });
            RenderClient();
        }

        private List<ClientNote> ParseNotes(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new List<ClientNote>();

            string value = raw.Trim();
            if (!value.StartsWith("[") && !value.StartsWith("{"))
            {
                return new List<ClientNote>
                {
                    new ClientNote
                    {
                        Title = "Imported Note",
                        Body = value,
                        CreatedAt = DateTime.Now,
                        CreatedBy = "Imported"
                    }
                };
            }

            try { return _json.Deserialize<List<ClientNote>>(value) ?? new List<ClientNote>(); }
            catch (Exception ex) { AppLogger.LogError("ClientDetailPage.ParseNotes", ex); return new List<ClientNote>(); }
        }

        private Panel Section(int width, int height)
        {
            Panel panel = new Panel { Width = width, Height = height, BackColor = Color.White, Margin = new Padding(0, 0, 0, 12) };
            panel.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, panel.Height - 1, panel.Width, panel.Height - 1);
            panel.Resize += (s, e) => { if (panel.Parent != null) panel.Width = Math.Max(640, panel.Parent.ClientSize.Width - 38); };
            return panel;
        }

        private AccordionPanel AccordionSection(string title, int width, int contentHeight, bool expanded)
        {
            return AccordionSection(title, width, contentHeight, expanded, null);
        }

        private AccordionPanel AccordionSection(string title, int width, int contentHeight, bool expanded, string badgeText)
        {
            AccordionPanel panel = new AccordionPanel
            {
                Width = width,
                Height = 36 + contentHeight,
                HeaderText = title,
                HeaderTextColor = TextMain,
                HeaderBorderColor = Border,
                HeaderFontSize = 10,
                ShowBadge = !string.IsNullOrWhiteSpace(badgeText),
                BadgeText = badgeText ?? string.Empty,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, 12)
            };
            panel.ContentPanel.BackColor = Color.White;
            panel.Resize += (s, e) =>
            {
                if (panel.Parent != null)
                    panel.Width = Math.Max(640, panel.Parent.ClientSize.Width - 38);
                ResizeFixedChildren(panel.ContentPanel);
            };
            panel.SetExpanded(expanded);
            return panel;
        }

        private AccordionPanel SideCard(int width, int contentHeight, string title)
        {
            AccordionPanel card = new AccordionPanel
            {
                Width = width,
                Height = 36 + contentHeight,
                HeaderText = title,
                HeaderTextColor = TextMain,
                HeaderBorderColor = Border,
                HeaderBackground = Surface,
                HeaderFontSize = 10,
                BackColor = Surface,
                Margin = new Padding(0, 0, 0, 10)
            };
            card.ContentPanel.BackColor = Surface;
            card.Resize += (s, e) => ResizeFixedChildren(card.ContentPanel);
            card.SetExpanded(true);
            return card;
        }

        private Label Header(string text)
        {
            return new Label { Text = text, Location = new Point(14, 8), Size = new Size(400, 22), Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = TextMain };
        }

        private TableLayoutPanel Grid(int columns, int rows, int height)
        {
            TableLayoutPanel grid = new TableLayoutPanel { Location = new Point(14, 10), Size = new Size(728, Math.Max(height, rows * 70 + 8)), ColumnCount = columns, RowCount = rows, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            for (int i = 0; i < columns; i++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
            for (int i = 0; i < rows; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 70f));
            return grid;
        }

        private TextBox Field(TableLayoutPanel grid, string label, string value, int row, int col)
        {
            Panel wrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 12, 10) };
            Label fieldLabel = new Label
            {
                Location = new Point(0, 0),
                Size = new Size(120, 22),
                Text = label,
                ForeColor = Color.FromArgb(65, 74, 90),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            TextBox input = new TextBox
            {
                Location = new Point(0, 30),
                Height = 28,
                BorderStyle = BorderStyle.FixedSingle,
                Text = value ?? string.Empty,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                ForeColor = TextMain,
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
            };
            wrap.Resize += (s, e) =>
            {
                int width = Math.Max(80, wrap.ClientSize.Width - 12);
                fieldLabel.Width = width;
                input.Width = width;
            };
            wrap.Controls.Add(input);
            wrap.Controls.Add(fieldLabel);
            grid.Controls.Add(wrap, col, row);
            return input;
        }

        private void AddStat(Panel card, int x, string label, string value, string note, Color noteColor)
        {
            card.Controls.Add(new Label { Text = value, Location = new Point(x + 14, 10), Size = new Size(160, 22), Font = new Font("Segoe UI", 14f, FontStyle.Bold), ForeColor = TextMain, AutoEllipsis = true });
            card.Controls.Add(new Label { Text = label, Location = new Point(x + 14, 34), Size = new Size(160, 14), ForeColor = TextMuted, Font = new Font("Segoe UI", 7.5f), AutoEllipsis = true });
            card.Controls.Add(new Label { Text = note, Location = new Point(x + 14, 48), Size = new Size(160, 14), ForeColor = noteColor, Font = new Font("Segoe UI", 7.5f), AutoEllipsis = true });
        }

        private static Button Button(string text, Color back, Color fore, int width)
        {
            Button b = new Button { Text = text, Width = width, Height = 30, BackColor = back, ForeColor = fore, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Cursor = Cursors.Hand };
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = back == Color.White ? Border : back;
            return b;
        }

        private static Panel Avatar(string text, int size)
        {
            Panel p = new Panel { Size = new Size(size, size), BackColor = Teal };
            p.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (Brush b = new SolidBrush(Color.White))
                    e.Graphics.DrawString(text, new Font("Segoe UI", 10f, FontStyle.Bold), b, new RectangleF(0, 0, size, size), sf);
            };
            return p;
        }

        private static Label MakeTag(string text)
        {
            return new Label { Text = text, Width = Math.Max(60, Math.Min(140, text.Length * 7 + 20)), Height = 22, TextAlign = ContentAlignment.MiddleCenter, BackColor = TealBg, ForeColor = TealText, Font = new Font("Segoe UI", 8f), AutoEllipsis = true };
        }

        private static void ResizeClientDetailCards(FlowLayoutPanel stack)
        {
            if (stack == null || stack.IsDisposed)
                return;

            int width = Math.Max(220, stack.ClientSize.Width - stack.Padding.Left - stack.Padding.Right - 24);
            foreach (Control control in stack.Controls)
            {
                if (control.Dock == DockStyle.None)
                    control.Width = Math.Max(control.MinimumSize.Width, width);
                ResizeFixedChildren(control);
            }
        }

        private static void ResizeFixedChildren(Control parent)
        {
            if (parent == null || parent.IsDisposed)
                return;

            int available = Math.Max(80, parent.ClientSize.Width - 28);
            foreach (Control child in parent.Controls)
            {
                if (child.Dock == DockStyle.None && child.Left >= 0 && child.Width > available / 2)
                    child.Width = Math.Max(80, parent.ClientSize.Width - child.Left - 14);

                ResizeFixedChildren(child);
            }
        }

        private static bool IsClosedJob(Job job)
        {
            string status = ((job.Status ?? "") + " " + (job.PipelineStatus ?? "")).ToLowerInvariant();
            return status.Contains("closed") || status.Contains("invoice");
        }

        private static string NormalizeStage(string stage)
        {
            string s = (stage ?? "").Trim();
            if (string.Equals(s, "Active", StringComparison.OrdinalIgnoreCase)) return "Active";
            if (s.IndexOf("black", StringComparison.OrdinalIgnoreCase) >= 0) return "Blacklisted";
            if (s.IndexOf("hold", StringComparison.OrdinalIgnoreCase) >= 0) return "On Hold";
            if (string.Equals(s, "Qualified", StringComparison.OrdinalIgnoreCase)) return "Qualified";
            if (string.Equals(s, "Active AMC", StringComparison.OrdinalIgnoreCase)) return "Active AMC";
            if (string.Equals(s, "Renewal Due", StringComparison.OrdinalIgnoreCase)) return "Renewal Due";
            if (string.Equals(s, "Inactive", StringComparison.OrdinalIgnoreCase)) return "Inactive";
            return "Prospect";
        }

        /// <summary>Returns the supported client lifecycle statuses shared by client status controls.</summary>
        private static string[] ClientStatusOptions()
        {
            return new[] { "Active", "Prospect", "On Hold", "Inactive", "Blacklisted" };
        }

        /// <summary>Returns the visible lifecycle status for a client record.</summary>
        private static string GetClientStatus(B2BClient client)
        {
            if (client == null)
                return "Inactive";
            string stage = client.RelationshipStage ?? string.Empty;
            if (stage.IndexOf("black", StringComparison.OrdinalIgnoreCase) >= 0) return "Blacklisted";
            if (!client.IsActive) return stage.IndexOf("hold", StringComparison.OrdinalIgnoreCase) >= 0 ? "On Hold" : "Inactive";
            if (stage.IndexOf("prospect", StringComparison.OrdinalIgnoreCase) >= 0 || stage.IndexOf("lead", StringComparison.OrdinalIgnoreCase) >= 0) return "Prospect";
            return "Active";
        }

        /// <summary>Applies lifecycle status semantics to the current in-memory client record.</summary>
        private static void ApplyLifecycleLocal(B2BClient client, string status)
        {
            if (client == null)
                return;
            string value = string.IsNullOrWhiteSpace(status) ? "Active" : status.Trim();
            client.RelationshipStage = value;
            client.IsActive = value == "Active" || value == "Prospect";
        }

        private static List<string> SplitTags(string tags)
        {
            return (tags ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
        }

        private static string Initials(string value)
        {
            string[] parts = (value ?? "CL").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "CL";
            if (parts.Length == 1) return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
            return (parts[0][0].ToString() + parts[1][0].ToString()).ToUpperInvariant();
        }

        private static string Safe(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string ActivityLetter(string type)
        {
            string t = (type ?? "C").Trim();
            return t.Length == 0 ? "C" : t.Substring(0, 1).ToUpperInvariant();
        }

        private static int ParseInt(string text)
        {
            int value;
            return int.TryParse((text ?? "").Trim(), out value) ? value : 0;
        }

        private static decimal ParseDecimal(string text)
        {
            decimal value;
            return decimal.TryParse((text ?? "").Replace(",", "").Trim(), out value) ? value : 0m;
        }

        private static string Prompt(string title, string label)
        {
            using (Form dialog = ServoModalForm.Create(title, 360, 150))
            {
                dialog.Controls.Add(new Label { Text = label, Location = new Point(14, 14), Width = 310 });
                TextBox box = new TextBox { Location = new Point(14, 38), Width = 310 };
                Button ok = Button("OK", Teal, Color.White, 78);
                ok.Location = new Point(166, 74);
                ok.DialogResult = DialogResult.OK;
                Button cancel = Button("Cancel", Color.White, TextMain, 78);
                cancel.Location = new Point(250, 74);
                cancel.DialogResult = DialogResult.Cancel;
                dialog.Controls.Add(box);
                dialog.Controls.Add(ok);
                dialog.Controls.Add(cancel);
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;
                return dialog.ShowDialog() == DialogResult.OK ? box.Text : string.Empty;
            }
        }

        private SitePromptResult PromptSiteDetails()
        {
            using (Form dialog = ServoModalForm.Create("Add site", 420, 280))
            {
                dialog.Controls.Add(new Label { Text = "Site name", Location = new Point(16, 16), Width = 360, ForeColor = TextMain });
                TextBox txtName = new TextBox { Location = new Point(16, 40), Width = 360, Text = string.Empty };

                dialog.Controls.Add(new Label { Text = "Site address", Location = new Point(16, 76), Width = 360, ForeColor = TextMain });
                TextBox txtAddress = new TextBox { Location = new Point(16, 100), Width = 360, Height = 56, Multiline = true, ScrollBars = ScrollBars.Vertical, Text = string.Empty };

                dialog.Controls.Add(new Label { Text = "City", Location = new Point(16, 166), Width = 360, ForeColor = TextMain });
                TextBox txtCity = new TextBox { Location = new Point(16, 190), Width = 360, Text = Safe(_client == null ? null : _client.City, string.Empty) };

                Button ok = Button("Save site", Teal, Color.White, 92);
                ok.Location = new Point(192, 224);
                ok.DialogResult = DialogResult.OK;
                Button cancel = Button("Cancel", Color.White, TextMain, 78);
                cancel.Location = new Point(298, 224);
                cancel.DialogResult = DialogResult.Cancel;

                dialog.Controls.Add(txtName);
                dialog.Controls.Add(txtAddress);
                dialog.Controls.Add(txtCity);
                dialog.Controls.Add(ok);
                dialog.Controls.Add(cancel);
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return null;

                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    MessageBox.Show("Site name is required.", "Sites", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return null;
                }

                return new SitePromptResult
                {
                    SiteName = txtName.Text,
                    Address = txtAddress.Text,
                    City = txtCity.Text
                };
            }
        }

        private sealed class ClientStats
        {
            public string OpenJobs;
            public int OverdueJobs;
            public string ActiveContracts;
            public string Outstanding;
            public decimal OutstandingRaw;
        }

        private sealed class ClientNote
        {
            public string Title { get; set; }
            public string Body { get; set; }
            public DateTime CreatedAt { get; set; }
            public string CreatedBy { get; set; }
        }

        private sealed class SitePromptResult
        {
            public string SiteName { get; set; }
            public string Address { get; set; }
            public string City { get; set; }
        }
    }
}



using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public class ClientDetailPage : UserControl
    {
        private static readonly Color Teal = ColorTranslator.FromHtml("#1D9E75");
        private static readonly Color TealBg = ColorTranslator.FromHtml("#E1F5EE");
        private static readonly Color TealText = ColorTranslator.FromHtml("#0F6E56");
        private static readonly Color Border = Color.FromArgb(230, 230, 230);
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

        public ClientDetailPage()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9f);
            BuildLayout();
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
            Button back = Button("<- Clients", Color.White, TextMain, 92);
            back.Dock = DockStyle.Left;
            back.FlatAppearance.BorderColor = Border;
            back.Click += (s, e) => OnBackToClients?.Invoke(ClientId);
            Label sep = new Label { Text = "/", Dock = DockStyle.Left, Width = 24, TextAlign = ContentAlignment.MiddleCenter, ForeColor = TextMuted };
            _titleLabel = new Label { Text = "Client", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = TextMain };
            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 300, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Color.White };
            Button save = Button("Save changes", Teal, Color.White, 112);
            Button print = Button("Print", Color.White, TextMain, 70);
            Button log = Button("+ Log activity", Color.White, TextMain, 112);
            save.Click += (s, e) => SaveClient();
            print.Click += (s, e) => MessageBox.Show("Print preview is not wired for client profiles yet.", "Clients", MessageBoxButtons.OK, MessageBoxIcon.Information);
            log.Click += (s, e) => ClientUi.ShowActivityModal(this, ClientId, RenderClient);
            actions.Controls.Add(save);
            actions.Controls.Add(print);
            actions.Controls.Add(log);
            top.Controls.Add(_titleLabel);
            top.Controls.Add(actions);
            top.Controls.Add(sep);
            top.Controls.Add(back);

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
            _rightStack.Controls.Add(BuildRecentActivityCard());
            _rightStack.Controls.Add(BuildSiteTimelineCard());

            _leftStack.ResumeLayout();
            _rightStack.ResumeLayout();
        }

        private Panel BuildIdentitySection()
        {
            Panel card = Section(760, 104);
            Panel avatar = Avatar(Initials(_client.CompanyName), 48);
            avatar.Location = new Point(14, 16);
            card.Controls.Add(avatar);
            card.Controls.Add(new Label { Text = _client.CompanyName ?? "Client", Location = new Point(78, 14), Size = new Size(420, 24), Font = new Font("Segoe UI", 16f, FontStyle.Bold), ForeColor = TextMain });
            card.Controls.Add(new Label { Text = Safe(_client.IndustryType, "-") + " · " + Safe(_client.City, "-"), Location = new Point(80, 42), Size = new Size(420, 18), ForeColor = TextMuted });
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
            card.Controls.Add(new Label { Text = "Relationship stage", Location = new Point(14, 8), Size = new Size(220, 22), Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = TextMain });
            string[] stages = { "Prospect", "Qualified", "Active AMC", "Renewal Due", "Inactive" };
            string current = NormalizeStage(_client.RelationshipStage);
            for (int i = 0; i < stages.Length; i++)
            {
                string stage = stages[i];
                Button step = Button((stage == current ? "● " : "○ ") + stage, Color.White, stage == current ? TealText : TextMuted, i == 2 || i == 3 ? 130 : 104);
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
            _sitesFlow = new FlowLayoutPanel { Location = new Point(14, 42), Size = new Size(728, 74), WrapContents = true, AutoScroll = true };
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
                Label item = new Label { Text = Safe(note.Title, "Note") + "\r\n" + Safe(note.Body, ""), Location = new Point(14, y), Size = new Size(208, 52), BackColor = Color.White, ForeColor = TextMuted, Padding = new Padding(6) };
                item.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(Border), 0, 0, item.Width - 1, item.Height - 1);
                card.ContentPanel.Controls.Add(item);
                y += 58;
            }
            TextBox add = new TextBox { Location = new Point(14, Math.Min(202, y)), Width = 208, Text = "+ Add a note..." };
            add.Enter += (s, e) => { if (add.Text == "+ Add a note...") add.Text = ""; };
            add.Leave += (s, e) => SaveNote(add);
            add.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { SaveNote(add); e.SuppressKeyPress = true; } };
            card.ContentPanel.Controls.Add(add);
            return card;
        }

        private Control BuildRecentActivityCard()
        {
            AccordionPanel card = SideCard(236, 224, "Recent activity");
            int y = 8;
            foreach (ClientActivity activity in SafeActivities().Take(5))
            {
                card.ContentPanel.Controls.Add(new Label { Text = ActivityLetter(activity.ActivityType), Location = new Point(14, y + 3), Size = new Size(22, 22), BackColor = TealBg, ForeColor = TealText, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8f, FontStyle.Bold) });
                card.ContentPanel.Controls.Add(new Label { Text = Safe(activity.Title, activity.ActivityType), Location = new Point(44, y), Size = new Size(170, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = TextMain });
                card.ContentPanel.Controls.Add(new Label { Text = activity.CreatedAt.ToString("dd/MM/yyyy"), Location = new Point(44, y + 18), Size = new Size(170, 16), Font = new Font("Segoe UI", 7.5f), ForeColor = TextMuted });
                y += 40;
            }
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
                    ForeColor = TextMuted
                });
                return card;
            }

            int y = 8;
            foreach (SiteTimelineItem item in _siteTimelineService.GetTimeline(selectedSite.SiteID, 5))
            {
                card.ContentPanel.Controls.Add(new Label { Text = ActivityLetter(item.EventType), Location = new Point(14, y + 2), Size = new Size(22, 22), BackColor = TealBg, ForeColor = TealText, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8f, FontStyle.Bold) });
                card.ContentPanel.Controls.Add(new Label { Text = Safe(item.Title, item.EventType), Location = new Point(44, y), Size = new Size(170, 18), Font = new Font("Segoe UI", 8.3f, FontStyle.Bold), ForeColor = TextMain });
                card.ContentPanel.Controls.Add(new Label { Text = item.EventDate == DateTime.MinValue ? item.EventType : item.EventDate.ToString("dd/MM/yyyy") + " | " + item.EventType, Location = new Point(44, y + 18), Size = new Size(170, 16), Font = new Font("Segoe UI", 7.5f), ForeColor = TextMuted });
                y += 42;
            }

            if (y == 8)
            {
                card.ContentPanel.Controls.Add(new Label
                {
                    Text = "No jobs, invoices, purchases, service tickets, or contracts yet.",
                    Location = new Point(14, 12),
                    Size = new Size(208, 54),
                    ForeColor = TextMuted
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
            if (MessageBox.Show("Move to " + stage + "?", "Relationship stage", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            _client.RelationshipStage = stage;
            _clientService.UpdateClient(_client);
            RenderClient();
        }

        private void AddSite()
        {
            string name = Prompt("Add site", "Site name");
            if (string.IsNullOrWhiteSpace(name))
                return;
            try
            {
                _siteService.Create(new ClientSite { ClientID = ClientId, SiteName = name.Trim(), City = _client.City });
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
                chip.Click += (s, e) => { };
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

        private List<ClientActivity> SafeActivities()
        {
            try { return _clientService.GetActivities(ClientId, "All"); }
            catch (Exception ex) { AppLogger.LogError("ClientDetailPage.SafeActivities", ex); return new List<ClientActivity>(); }
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
            try { return string.IsNullOrWhiteSpace(raw) ? new List<ClientNote>() : (_json.Deserialize<List<ClientNote>>(raw) ?? new List<ClientNote>()); }
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
            card.SetExpanded(true);
            return card;
        }

        private Label Header(string text)
        {
            return new Label { Text = text, Location = new Point(14, 8), Size = new Size(400, 22), Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = TextMain };
        }

        private TableLayoutPanel Grid(int columns, int rows, int height)
        {
            TableLayoutPanel grid = new TableLayoutPanel { Location = new Point(14, 10), Size = new Size(728, Math.Max(height, rows * 70 + 8)), ColumnCount = columns, RowCount = rows };
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
            card.Controls.Add(new Label { Text = value, Location = new Point(x + 14, 10), Size = new Size(160, 22), Font = new Font("Segoe UI", 14f, FontStyle.Bold), ForeColor = TextMain });
            card.Controls.Add(new Label { Text = label, Location = new Point(x + 14, 34), Size = new Size(160, 14), ForeColor = TextMuted, Font = new Font("Segoe UI", 7.5f) });
            card.Controls.Add(new Label { Text = note, Location = new Point(x + 14, 48), Size = new Size(160, 14), ForeColor = noteColor, Font = new Font("Segoe UI", 7.5f) });
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
            return new Label { Text = text, Width = Math.Max(60, Math.Min(140, text.Length * 7 + 20)), Height = 22, TextAlign = ContentAlignment.MiddleCenter, BackColor = TealBg, ForeColor = TealText, Font = new Font("Segoe UI", 8f) };
        }

        private static bool IsClosedJob(Job job)
        {
            string status = ((job.Status ?? "") + " " + (job.PipelineStatus ?? "")).ToLowerInvariant();
            return status.Contains("closed") || status.Contains("invoice");
        }

        private static string NormalizeStage(string stage)
        {
            string s = (stage ?? "").Trim();
            if (string.Equals(s, "Qualified", StringComparison.OrdinalIgnoreCase)) return "Qualified";
            if (string.Equals(s, "Active AMC", StringComparison.OrdinalIgnoreCase)) return "Active AMC";
            if (string.Equals(s, "Renewal Due", StringComparison.OrdinalIgnoreCase)) return "Renewal Due";
            if (string.Equals(s, "Inactive", StringComparison.OrdinalIgnoreCase)) return "Inactive";
            return "Prospect";
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
            using (Form dialog = new Form { Text = title, Width = 360, Height = 150, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false })
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
    }
}

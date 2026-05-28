using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using Microsoft.Web.WebView2.WinForms;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class WhatsAppHubForm : UserControl
    {
        private static readonly Color WhatsAppGreen = Color.FromArgb(22, 163, 74);
        private static readonly Color WhatsAppGreenDark = Color.FromArgb(5, 150, 105);
        private static readonly Color WhatsAppGreenLight = Color.FromArgb(220, 252, 231);
        private static readonly Color BubbleGreen = Color.FromArgb(220, 252, 231);
        private static readonly Color NoticeBack = Color.FromArgb(255, 251, 235);
        private static readonly Color NoticeBorder = Color.FromArgb(253, 230, 138);

        private readonly WhatsAppHubService _service = new WhatsAppHubService();
        private readonly List<Button> _tabButtons = new List<Button>();
        private readonly List<Button> _templateButtons = new List<Button>();
        private readonly Panel _conversationList = new Panel();
        private readonly Panel _chatHost = new Panel();
        private readonly Panel _detailHost = new Panel();
        private readonly TextBox _conversationSearch = new TextBox();
        private readonly TextBox _globalSearch = new TextBox();
        private readonly TextBox _messageInput = new TextBox();
        private readonly Label _conversationCount = new Label();
        private readonly Label _webStatus = new Label();
        private WebView2 _whatsAppWeb;
        private Button _markSentButton;
        private WhatsAppQuickActionContext _pendingContext;

        private List<WhatsAppContact> _contacts = new List<WhatsAppContact>();
        private List<WhatsAppTemplate> _templates = new List<WhatsAppTemplate>();
        private WhatsAppContact _selectedContact;
        private WhatsAppTemplate _selectedTemplate;
        private string _activeTab = "All";

        public WhatsAppHubForm()
        {
            Dock = DockStyle.Fill;
            BackColor = DS.BgPage;
            Font = DS.Body;
            DoubleBuffered = true;
            Load += (s, e) => LoadHubData();
            BuildLayout();
        }

        private void BuildLayout()
        {
            Controls.Clear();

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = DS.BgPage,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(14, 14, 14, 12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildBody(), 0, 1);
            Controls.Add(root);
        }

        private Control BuildHeader()
        {
            Panel header = new Panel { Dock = DockStyle.Fill, BackColor = DS.BgPage };

            Label icon = ModernIconSystem.Badge(ModernIconKind.Phone, 38, WhatsAppGreenLight, WhatsAppGreen, 12);
            icon.Location = new Point(2, 8);
            header.Controls.Add(icon);

            Label title = new Label
            {
                Text = "WhatsApp Hub",
                AutoSize = true,
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                ForeColor = DS.Slate950,
                Location = new Point(52, 7)
            };
            header.Controls.Add(title);

            Label subtitle = new Label
            {
                Text = "Prepare WhatsApp messages for clients, vendors and team. Open chats manually through WhatsApp Web or browser links.",
                AutoSize = true,
                Font = DS.Small,
                ForeColor = DS.Slate600,
                Location = new Point(53, 36)
            };
            header.Controls.Add(subtitle);

            Button settings = IconOnlyButton(ModernIconKind.Settings, DS.Slate700, DS.White);
            settings.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            settings.Location = new Point(header.Width - 42, 8);
            settings.Click += (s, e) => MessageBox.Show("WhatsApp Hub uses embedded WhatsApp Web or browser deep links only. ServoERP does not auto-send messages, scrape chats, or claim live sync without the official API.", "WhatsApp Hub", MessageBoxButtons.OK, MessageBoxIcon.Information);
            header.Controls.Add(settings);

            Button refresh = IconOnlyButton(ModernIconKind.Refresh, DS.Slate700, DS.White);
            refresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            refresh.Location = new Point(header.Width - 92, 8);
            refresh.Click += (s, e) => LoadHubData();
            header.Controls.Add(refresh);

            Button newMessage = DS.PrimaryBtn("New Message", 138, 38);
            newMessage.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            newMessage.Location = new Point(header.Width - 238, 8);
            ModernIconSystem.AddButtonIcon(newMessage, ModernIconKind.Email);
            newMessage.Click += (s, e) => MessageBox.Show("Select a contact from Conversations to start a WhatsApp message.", "New Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
            header.Controls.Add(newMessage);

            Panel searchHost = SearchBox(_globalSearch, "Search customer, mobile, invoice, job...");
            searchHost.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            searchHost.Size = new Size(330, 38);
            searchHost.Location = new Point(header.Width - 582, 8);
            header.Controls.Add(searchHost);

            header.Resize += (s, e) =>
            {
                settings.Location = new Point(header.Width - 42, 8);
                refresh.Location = new Point(header.Width - 92, 8);
                newMessage.Location = new Point(header.Width - 238, 8);
                searchHost.Location = new Point(Math.Max(360, header.Width - 582), 8);
            };

            return header;
        }

        private Control BuildBody()
        {
            Panel body = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = DS.BgPage
            };

            Panel leftCard = Card(new Padding(14), 12);
            BuildConversationColumn(leftCard);

            Panel chatCard = Card(new Padding(0), 12);
            _chatHost.Dock = DockStyle.Fill;
            _chatHost.BackColor = DS.White;
            chatCard.Controls.Add(_chatHost);

            _detailHost.Dock = DockStyle.Fill;
            _detailHost.BackColor = DS.BgPage;
            _detailHost.Margin = Padding.Empty;
            _detailHost.Padding = Padding.Empty;

            Panel leftWrap = new Panel { Dock = DockStyle.Left, Width = 376, Padding = new Padding(0, 0, 12, 0), BackColor = DS.BgPage };
            Panel rightWrap = new Panel { Dock = DockStyle.Right, Width = 360, Padding = new Padding(0), BackColor = DS.BgPage };
            Panel chatWrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 12, 0), BackColor = DS.BgPage };
            leftCard.Dock = DockStyle.Fill;
            chatCard.Dock = DockStyle.Fill;
            leftWrap.Controls.Add(leftCard);
            rightWrap.Controls.Add(_detailHost);
            chatWrap.Controls.Add(chatCard);

            body.Controls.Add(chatWrap);
            body.Controls.Add(rightWrap);
            body.Controls.Add(leftWrap);
            body.Resize += (s, e) =>
            {
                int width = body.ClientSize.Width;
                leftWrap.Width = width < 1280 ? 330 : 376;
                rightWrap.Width = width < 1280 ? 320 : 360;
            };
            return body;
        }

        private void BuildConversationColumn(Panel host)
        {
            host.Controls.Clear();

            Panel titleRow = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Color.Transparent };
            Label title = new Label { Text = "Contacts", AutoSize = true, Font = DS.H3, ForeColor = DS.Slate900, Location = new Point(0, 7) };
            titleRow.Controls.Add(title);
            _conversationCount.AutoSize = false;
            _conversationCount.Size = new Size(32, 22);
            _conversationCount.Location = new Point(112, 5);
            _conversationCount.TextAlign = ContentAlignment.MiddleCenter;
            _conversationCount.Font = DS.Caption;
            _conversationCount.ForeColor = Color.White;
            _conversationCount.BackColor = WhatsAppGreen;
            DS.Rounded(_conversationCount, 10);
            titleRow.Controls.Add(_conversationCount);
            host.Controls.Add(titleRow);

            FlowLayoutPanel tabs = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = false, BackColor = Color.Transparent };
            foreach (string tab in new[] { "All", "Unread", "Clients", "Vendors", "Team" })
            {
                Button button = TabButton(tab);
                tabs.Controls.Add(button);
                _tabButtons.Add(button);
            }
            host.Controls.Add(tabs);

            TableLayoutPanel searchRow = new TableLayoutPanel { Dock = DockStyle.Top, Height = 48, ColumnCount = 2, BackColor = Color.Transparent };
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
            Panel search = SearchBox(_conversationSearch, "Search or start new chat...");
            _conversationSearch.TextChanged += (s, e) => RenderConversations();
            searchRow.Controls.Add(search, 0, 0);
            Button filter = IconOnlyButton(ModernIconKind.Filter, DS.Slate700, DS.White);
            filter.Margin = new Padding(8, 0, 0, 0);
            filter.Dock = DockStyle.Fill;
            searchRow.Controls.Add(filter, 1, 0);
            host.Controls.Add(searchRow);

            LinkLabel loadMore = new LinkLabel
            {
                Dock = DockStyle.Bottom,
                Text = "Manual WhatsApp actions only",
                Height = 34,
                TextAlign = ContentAlignment.MiddleCenter,
                LinkColor = DS.Primary600,
                Font = DS.SmallBold
            };
            host.Controls.Add(loadMore);

            _conversationList.Dock = DockStyle.Fill;
            _conversationList.AutoScroll = true;
            _conversationList.BackColor = Color.Transparent;
            host.Controls.Add(_conversationList);
            _conversationList.BringToFront();
        }

        private void LoadHubData()
        {
            Cursor = Cursors.WaitCursor;
            try
            {
                _contacts = _service.LoadContacts()
                    .OrderByDescending(c => !string.IsNullOrWhiteSpace(c.Phone))
                    .ThenByDescending(c => c.LastMessageAt)
                    .ToList();
                _templates = _service.LoadTemplates();
                _selectedContact = _selectedContact == null
                    ? _contacts.FirstOrDefault()
                    : _contacts.FirstOrDefault(c => c.SourceType == _selectedContact.SourceType && c.SourceId == _selectedContact.SourceId) ?? _contacts.FirstOrDefault();
                _selectedTemplate = _templates.FirstOrDefault(t => t.TemplateType == "Payment reminder") ?? _templates.FirstOrDefault();

                _conversationCount.Text = _contacts.Count.ToString();
                RenderTabs();
                RenderConversations();
                RenderChat();
                RenderDetails();
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void RenderTabs()
        {
            foreach (Button button in _tabButtons)
            {
                bool active = string.Equals((string)button.Tag, _activeTab, StringComparison.OrdinalIgnoreCase);
                button.BackColor = active ? DS.Primary50 : DS.White;
                button.ForeColor = active ? DS.Primary700 : DS.Slate700;
                button.FlatAppearance.BorderColor = active ? DS.Primary100 : DS.White;
            }
        }

        private void RenderConversations()
        {
            _conversationList.SuspendLayout();
            _conversationList.Controls.Clear();

            IEnumerable<WhatsAppContact> filtered = _contacts;
            if (_activeTab == "Unread")
                filtered = filtered.Where(c => c.UnreadCount > 0);
            else if (_activeTab == "Clients")
                filtered = filtered.Where(c => c.SourceType == "Client");
            else if (_activeTab == "Vendors")
                filtered = filtered.Where(c => c.SourceType == "Vendor");
            else if (_activeTab == "Team")
                filtered = filtered.Where(c => c.SourceType == "Team");

            string query = _conversationSearch.Text.Trim();
            if (!string.IsNullOrWhiteSpace(query) && !query.StartsWith("Search ", StringComparison.OrdinalIgnoreCase))
                filtered = filtered.Where(c => (c.Name ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || (c.Phone ?? string.Empty).Contains(query));

            List<WhatsAppContact> visible = filtered.Take(18).ToList();
            if (visible.Count == 0)
            {
                Panel empty = BuildConversationEmptyState(HasConversationFilters());
                empty.Location = new Point(0, 14);
                empty.Width = Math.Max(300, _conversationList.ClientSize.Width - 18);
                empty.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                _conversationList.Controls.Add(empty);
                _conversationList.ResumeLayout();
                return;
            }

            int y = 2;
            foreach (WhatsAppContact contact in visible)
            {
                Panel row = ConversationRow(contact);
                row.Location = new Point(0, y);
                row.Width = Math.Max(300, _conversationList.ClientSize.Width - 18);
                row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                _conversationList.Controls.Add(row);
                y += row.Height + 8;
            }

            _conversationList.ResumeLayout();
        }

        private bool HasConversationFilters()
        {
            string query = (_conversationSearch.Text ?? string.Empty).Trim();
            return !string.Equals(_activeTab, "All", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(query) && !query.StartsWith("Search ", StringComparison.OrdinalIgnoreCase));
        }

        private Panel BuildConversationEmptyState(bool filtered)
        {
            Panel panel = new Panel { Height = 176, BackColor = DS.White };
            DS.Rounded(panel, 12);
            panel.Paint += (s, e) => DrawBorder((Control)s, e.Graphics, DS.Border, 12);
            Label icon = ModernIconSystem.Badge(ModernIconKind.Phone, 42, WhatsAppGreenLight, WhatsAppGreen, 12);
            icon.Location = new Point(22, 24);
            Label title = new Label
            {
                Text = filtered ? "No contacts match" : "No WhatsApp contacts",
                Location = new Point(76, 26),
                Size = new Size(210, 22),
                Font = DS.BodyBold,
                ForeColor = DS.Slate900
            };
            Label hint = new Label
            {
                Text = filtered ? "Clear the current search or tab to return to the full contact list." : "Clients, vendors, and team contacts will appear here once available.",
                Location = new Point(76, 52),
                Size = new Size(210, 44),
                Font = DS.Small,
                ForeColor = DS.Slate600
            };
            Button clear = DS.GhostBtn("Clear Filters", 120, 32);
            clear.Location = new Point(76, 108);
            clear.Visible = filtered;
            clear.Click += (s, e) =>
            {
                _conversationSearch.Clear();
                _activeTab = "All";
                RenderTabs();
                RenderConversations();
            };
            panel.Controls.Add(icon);
            panel.Controls.Add(title);
            panel.Controls.Add(hint);
            panel.Controls.Add(clear);
            return panel;
        }

        private Panel ConversationRow(WhatsAppContact contact)
        {
            bool active = ReferenceEquals(contact, _selectedContact) || (_selectedContact != null && contact.SourceType == _selectedContact.SourceType && contact.SourceId == _selectedContact.SourceId);
            Panel row = new Panel
            {
                Height = 72,
                BackColor = active ? Color.FromArgb(241, 245, 249) : DS.White,
                Cursor = Cursors.Hand,
                Padding = new Padding(10)
            };
            DS.Rounded(row, 10);
            row.Click += (s, e) => SelectContact(contact);

            Label avatar = Avatar(contact.Name, 42, ContactColor(contact));
            avatar.Location = new Point(10, 14);
            row.Controls.Add(avatar);

            Label name = new Label { Text = contact.Name, AutoSize = false, Location = new Point(62, 13), Size = new Size(185, 20), Font = DS.BodyBold, ForeColor = DS.Slate900 };
            Label last = new Label { Text = contact.LastMessage, AutoSize = false, Location = new Point(62, 36), Size = new Size(208, 18), Font = DS.Small, ForeColor = DS.Slate600 };
            Label time = new Label { Text = FormatTime(contact.LastMessageAt), AutoSize = false, TextAlign = ContentAlignment.MiddleRight, Location = new Point(row.Width - 78, 13), Size = new Size(68, 18), Font = DS.Caption, ForeColor = DS.Slate500, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            row.Controls.Add(name);
            row.Controls.Add(last);
            row.Controls.Add(time);

            if (contact.UnreadCount > 0)
            {
                Label unread = new Label { Text = contact.UnreadCount.ToString(), AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Font = DS.Caption, ForeColor = Color.White, BackColor = WhatsAppGreen, Size = new Size(20, 20), Location = new Point(row.Width - 30, 40), Anchor = AnchorStyles.Top | AnchorStyles.Right };
                DS.Rounded(unread, 10);
                row.Controls.Add(unread);
            }
            return row;
        }

        private void SelectContact(WhatsAppContact contact)
        {
            _selectedContact = contact;
            RenderConversations();
            RenderChat();
            RenderDetails();
        }

        private void RenderChat()
        {
            _chatHost.SuspendLayout();
            if (_whatsAppWeb != null)
            {
                _whatsAppWeb.Dispose();
                _whatsAppWeb = null;
            }
            _chatHost.Controls.Clear();

            Panel header = new Panel { Dock = DockStyle.Top, Height = 88, Padding = new Padding(20, 16, 20, 10), BackColor = DS.White };
            if (_selectedContact != null)
            {
                Label avatar = Avatar(_selectedContact.Name, 52, ContactColor(_selectedContact));
                avatar.Location = new Point(20, 18);
                header.Controls.Add(avatar);
                Label name = new Label { Text = _selectedContact.Name, AutoSize = true, Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = DS.Slate950, Location = new Point(84, 18) };
                Label phone = new Label { Text = PrettyPhone(_selectedContact.Phone), AutoSize = true, Font = DS.Small, ForeColor = DS.Slate600, Location = new Point(86, 48) };
                header.Controls.Add(name);
                header.Controls.Add(phone);
            }

            Button more = IconOnlyButton(ModernIconKind.Settings, DS.Slate700, DS.White);
            more.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            more.Location = new Point(header.Width - 58, 22);
            Button video = IconOnlyButton(ModernIconKind.Activity, DS.Slate700, DS.White);
            video.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            video.Location = new Point(header.Width - 106, 22);
            Button call = IconOnlyButton(ModernIconKind.Phone, DS.Slate700, DS.White);
            call.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            call.Location = new Point(header.Width - 154, 22);
            header.Controls.Add(call);
            header.Controls.Add(video);
            header.Controls.Add(more);
            header.Resize += (s, e) =>
            {
                more.Location = new Point(header.Width - 58, 22);
                video.Location = new Point(header.Width - 106, 22);
                call.Location = new Point(header.Width - 154, 22);
            };
            _chatHost.Controls.Add(header);

            Panel composer = BuildComposer();
            composer.Dock = DockStyle.Bottom;
            _chatHost.Controls.Add(composer);

            Panel browserPanel = BuildWhatsAppWebPanel();
            _chatHost.Controls.Add(browserPanel);
            browserPanel.BringToFront();
            composer.BringToFront();

            ApplyTemplate(_selectedTemplate, false);
            _chatHost.ResumeLayout();
        }

        private Panel BuildComposer()
        {
            Panel host = new Panel { Height = 128, Padding = new Padding(20, 10, 20, 14), BackColor = DS.White };

            TableLayoutPanel inputRow = new TableLayoutPanel { Dock = DockStyle.Top, Height = 48, ColumnCount = 6, BackColor = DS.White };
            inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
            inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
            inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
            inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56));
            inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
            inputRow.Controls.Add(IconOnlyButton(ModernIconKind.Status, DS.Slate600, DS.White), 0, 0);
            inputRow.Controls.Add(IconOnlyButton(ModernIconKind.Document, DS.Slate600, DS.White), 1, 0);

            Panel messageHost = new Panel { Dock = DockStyle.Fill, BackColor = DS.White, Padding = new Padding(12, 8, 12, 6), Margin = new Padding(4, 0, 4, 0) };
            DS.Rounded(messageHost, 10);
            _messageInput.BorderStyle = BorderStyle.None;
            _messageInput.Dock = DockStyle.Fill;
            _messageInput.Multiline = true;
            _messageInput.Font = DS.Body;
            _messageInput.ForeColor = DS.Slate800;
            _messageInput.BackColor = DS.White;
            messageHost.Paint += (s, e) => DrawBorder((Control)s, e.Graphics, DS.Border, 10);
            messageHost.Controls.Add(_messageInput);
            inputRow.Controls.Add(messageHost, 2, 0);
            inputRow.Controls.Add(IconOnlyButton(ModernIconKind.Phone, DS.Slate600, DS.White), 3, 0);

            Button send = IconOnlyButton(ModernIconKind.Email, Color.White, WhatsAppGreen, 44);
            send.Click += SendCurrentMessage;
            inputRow.Controls.Add(send, 4, 0);
            _markSentButton = DS.GhostBtn("Mark as Sent", 108, 36);
            _markSentButton.Enabled = false;
            _markSentButton.Margin = new Padding(6, 6, 0, 6);
            _markSentButton.Click += (s, e) => MarkPendingMessageSent();
            inputRow.Controls.Add(_markSentButton, 5, 0);
            host.Controls.Add(inputRow);

            FlowLayoutPanel templates = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = DS.White };
            string[] quick = { "Send Invoice", "Send Quotation", "Payment Reminder", "AMC Reminder", "Job Update", "More" };
            foreach (string title in quick)
            {
                Button button = DS.GhostBtn(title, title == "Payment Reminder" ? 138 : 118, 32);
                button.Font = DS.Caption;
                button.Margin = new Padding(0, 7, 8, 0);
                ModernIconSystem.AddButtonIcon(button, ModernIconSystem.KindForTitle(title));
                button.Click += (s, e) => SelectTemplateFromButton(((Button)s).Text);
                templates.Controls.Add(button);
                _templateButtons.Add(button);
            }
            host.Controls.Add(templates);
            return host;
        }

        private void SelectTemplateFromButton(string buttonText)
        {
            string text = (buttonText ?? string.Empty).Replace("&", string.Empty);
            WhatsAppTemplate match = _templates.FirstOrDefault(t => text.IndexOf(t.TemplateType, StringComparison.OrdinalIgnoreCase) >= 0 || t.TemplateType.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);
            if (match == null && text.IndexOf("Invoice", StringComparison.OrdinalIgnoreCase) >= 0)
                match = _templates.FirstOrDefault(t => t.TemplateType == "Invoice sent");
            if (match == null && text.IndexOf("Quotation", StringComparison.OrdinalIgnoreCase) >= 0)
                match = _templates.FirstOrDefault(t => t.TemplateType == "Quotation sent");
            if (match == null && text.IndexOf("Job", StringComparison.OrdinalIgnoreCase) >= 0)
                match = _templates.FirstOrDefault(t => t.TemplateType == "Job scheduled");
            if (match == null && text.IndexOf("AMC", StringComparison.OrdinalIgnoreCase) >= 0)
                match = _templates.FirstOrDefault(t => t.TemplateType == "AMC visit reminder");
            if (match == null)
                match = _templates.FirstOrDefault(t => t.TemplateType == "General update");
            ApplyTemplate(match, true);
        }

        private void ApplyTemplate(WhatsAppTemplate template, bool focus)
        {
            if (template == null)
                return;
            _selectedTemplate = template;
            _messageInput.Text = _service.RenderTemplate(template, _selectedContact);
            if (focus)
                _messageInput.Focus();
        }

        private void SendCurrentMessage(object sender, EventArgs e)
        {
            try
            {
                if (_selectedContact == null)
                    throw new InvalidOperationException("Select a contact before opening WhatsApp.");

                _pendingContext = BuildContext(_selectedContact, _selectedTemplate, _messageInput.Text);
                string url = _service.BuildWhatsAppWebUrl(_pendingContext.Phone, _pendingContext.Message);
                _service.LogAction(_service.BuildLog(_pendingContext, "Prepared"));
                _service.LogAction(_service.BuildLog(_pendingContext, "Opened"));
                NavigateWhatsAppWeb(url);
                if (_markSentButton != null)
                    _markSentButton.Enabled = true;
                _webStatus.Text = "Opened prefilled chat. Review and send manually in WhatsApp.";
                RenderDetails();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "WhatsApp Hub", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private WhatsAppQuickActionContext BuildContext(WhatsAppContact contact, WhatsAppTemplate template, string message)
        {
            return new WhatsAppQuickActionContext
            {
                Module = contact == null ? "Contact" : contact.SourceType,
                SourceId = contact == null ? 0 : contact.SourceId,
                ContactName = contact == null ? string.Empty : contact.Name,
                Phone = contact == null ? string.Empty : contact.Phone,
                TemplateType = template == null ? "General update" : template.TemplateType,
                Message = message ?? string.Empty,
                LinkedRecordType = template == null ? string.Empty : ResolveRecordType(template.TemplateType),
                LinkedRecord = template == null ? string.Empty : ResolveRecordName(template.TemplateType),
                LinkedRecordId = 0
            };
        }

        private void MarkPendingMessageSent()
        {
            if (_pendingContext == null)
                return;

            _pendingContext.Message = _messageInput.Text ?? string.Empty;
            _service.LogAction(_service.BuildLog(_pendingContext, "User Confirmed Sent"));
            _webStatus.Text = "Marked as sent by user confirmation.";
            if (_markSentButton != null)
                _markSentButton.Enabled = false;
            RenderDetails();
        }

        private Panel BuildWhatsAppWebPanel()
        {
            Panel host = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(250, 252, 255), Padding = new Padding(16, 12, 16, 12) };

            Panel notice = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = NoticeBack, Padding = new Padding(12, 9, 12, 8) };
            DS.Rounded(notice, 8);
            notice.Paint += (s, e) => DrawBorder((Control)s, e.Graphics, NoticeBorder, 8);
            Label noticeText = new Label
            {
                Text = "Embedded WhatsApp Web. Scan the QR code if prompted. ServoERP only opens prefilled chats; you send manually.",
                Dock = DockStyle.Fill,
                Font = DS.Small,
                ForeColor = DS.Slate800,
                TextAlign = ContentAlignment.MiddleLeft
            };
            notice.Controls.Add(noticeText);
            host.Controls.Add(notice);

            _webStatus.Dock = DockStyle.Bottom;
            _webStatus.Height = 28;
            _webStatus.Font = DS.Small;
            _webStatus.ForeColor = DS.Slate600;
            _webStatus.Text = "Loading WhatsApp Web...";
            host.Controls.Add(_webStatus);

            Panel browserHost = new Panel { Dock = DockStyle.Fill, BackColor = DS.White, Padding = new Padding(0), Margin = new Padding(0, 10, 0, 8) };
            host.Controls.Add(browserHost);
            browserHost.BringToFront();

            _whatsAppWeb = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.White };
            browserHost.Controls.Add(_whatsAppWeb);
            BeginInvoke((Action)(async () => await InitializeWhatsAppWebAsync()));

            return host;
        }

        private async System.Threading.Tasks.Task InitializeWhatsAppWebAsync()
        {
            if (_whatsAppWeb == null || _whatsAppWeb.IsDisposed)
                return;

            if (!WebView2RuntimeHelper.IsRuntimeAvailable(out _, out string message))
            {
                _webStatus.Text = "WebView2 runtime is not available. Use the browser link actions instead.";
                AppLogger.LogInfo("WhatsAppHubForm WebView2 unavailable: " + message);
                return;
            }

            try
            {
                await _whatsAppWeb.EnsureCoreWebView2Async(await WebView2RuntimeHelper.CreateEnvironmentAsync());
                _whatsAppWeb.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                _whatsAppWeb.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _whatsAppWeb.CoreWebView2.NavigationCompleted += (s, e) =>
                {
                    _webStatus.Text = e.IsSuccess ? "WhatsApp Web ready. Session is stored in the ServoERP WebView2 user data folder." : "WhatsApp Web navigation failed.";
                };
                _whatsAppWeb.Source = new Uri("https://web.whatsapp.com/");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("WhatsAppHubForm.InitializeWhatsAppWebAsync", ex);
                _webStatus.Text = "Could not load embedded WhatsApp Web.";
            }
        }

        private void NavigateWhatsAppWeb(string url)
        {
            if (_whatsAppWeb != null && _whatsAppWeb.CoreWebView2 != null)
            {
                _whatsAppWeb.CoreWebView2.Navigate(url);
                return;
            }

            InitializeWhatsAppWebAsync().ContinueWith(_ =>
            {
                try
                {
                    if (!IsDisposed && _whatsAppWeb != null && _whatsAppWeb.CoreWebView2 != null)
                        BeginInvoke((Action)(() => _whatsAppWeb.CoreWebView2.Navigate(url)));
                }
                catch { }
            });
        }

        private void RenderDetails()
        {
            _detailHost.SuspendLayout();
            _detailHost.Controls.Clear();
            _detailHost.AutoScroll = true;
            _detailHost.BackColor = DS.BgPage;

            int y = 0;
            foreach (Control card in new[] { ContactCard(), QuickActionsCard(), OpenInvoicesCard(), RecentActionsCard(), LinkedRecordsCard() })
            {
                card.Location = new Point(0, y);
                card.Width = Math.Max(280, _detailHost.ClientSize.Width - 8);
                card.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                _detailHost.Controls.Add(card);
                y += card.Height + 12;
            }
            _detailHost.ResumeLayout();
        }

        private Control ContactCard()
        {
            Panel card = RightCard("Customer Details", 168);
            if (_selectedContact == null)
                return card;

            Label avatar = Avatar(_selectedContact.Name, 48, ContactColor(_selectedContact));
            avatar.Location = new Point(16, 52);
            card.Controls.Add(avatar);
            Label name = new Label { Text = _selectedContact.Name, AutoSize = false, Location = new Point(76, 48), Size = new Size(190, 22), Font = DS.BodyBold, ForeColor = DS.Slate950 };
            Label phone = DetailLine(ModernIconKind.Phone, PrettyPhone(_selectedContact.Phone), 76, 75);
            Label email = DetailLine(ModernIconKind.Email, FirstNonEmpty(_selectedContact.Email, "No email saved"), 76, 100);
            Label location = DetailLine(ModernIconKind.Location, FirstNonEmpty(_selectedContact.Location, "Location not saved"), 76, 125);
            Button edit = DS.GhostBtn("Edit", 78, 30);
            edit.Location = new Point(card.Width - 96, 52);
            edit.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            ModernIconSystem.AddButtonIcon(edit, ModernIconKind.Preference);
            card.Controls.Add(name);
            card.Controls.Add(phone);
            card.Controls.Add(email);
            card.Controls.Add(location);
            card.Controls.Add(edit);
            return card;
        }

        private Control QuickActionsCard()
        {
            Panel card = RightCard("Quick Actions", 150);
            TableLayoutPanel grid = new TableLayoutPanel { Location = new Point(16, 52), Size = new Size(310, 78), ColumnCount = 5, RowCount = 1, BackColor = Color.Transparent, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            for (int i = 0; i < 5; i++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            AddQuickAction(grid, "Send Invoice", ModernIconKind.Invoice, 0);
            AddQuickAction(grid, "Send Quotation", ModernIconKind.Document, 1);
            AddQuickAction(grid, "Payment Reminder", ModernIconKind.Payment, 2);
            AddQuickAction(grid, "AMC Reminder", ModernIconKind.Contract, 3);
            AddQuickAction(grid, "Job Update", ModernIconKind.Job, 4);
            card.Controls.Add(grid);
            card.Resize += (s, e) => grid.Width = Math.Max(280, card.Width - 32);
            return card;
        }

        private void AddQuickAction(TableLayoutPanel grid, string title, ModernIconKind iconKind, int column)
        {
            Panel item = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Cursor = Cursors.Hand };
            Label icon = ModernIconSystem.Badge(iconKind, 38, DS.Green50, WhatsAppGreen, 10);
            icon.Location = new Point(10, 2);
            Label text = new Label { Text = title, AutoSize = false, TextAlign = ContentAlignment.TopCenter, Font = DS.Caption, ForeColor = DS.Slate800, Location = new Point(0, 46), Size = new Size(58, 28) };
            item.Controls.Add(icon);
            item.Controls.Add(text);
            item.Click += (s, e) => SelectTemplateFromButton(title);
            grid.Controls.Add(item, column, 0);
        }

        private Control OpenInvoicesCard()
        {
            Panel card = RightCard("Open Invoices", 218);
            LinkLabel view = new LinkLabel { Text = "View all", AutoSize = true, Location = new Point(card.Width - 70, 20), Anchor = AnchorStyles.Top | AnchorStyles.Right, LinkColor = DS.Primary600, Font = DS.Caption };
            card.Controls.Add(view);

            AddInvoiceRow(card, "INV-0245", "Due on 20 May 2025", "Rs 48,750", "PENDING", DS.Amber50, DS.Amber600, 54);
            AddInvoiceRow(card, "INV-0241", "Overdue by 5 days", "Rs 36,400", "OVERDUE", DS.Red50, DS.Red600, 100);
            AddInvoiceRow(card, "INV-0238", "Paid on 02 May 2025", "Rs 15,000", "PAID", DS.Green50, DS.Green600, 146);
            return card;
        }

        private void AddInvoiceRow(Panel card, string no, string meta, string amount, string status, Color back, Color fore, int y)
        {
            Label inv = new Label { Text = no, Location = new Point(16, y), Size = new Size(85, 20), Font = DS.BodyBold, ForeColor = DS.Slate900 };
            Label date = new Label { Text = meta, Location = new Point(16, y + 20), Size = new Size(140, 18), Font = DS.Caption, ForeColor = status == "OVERDUE" ? DS.Red600 : DS.Slate600 };
            Label amt = new Label { Text = amount, Location = new Point(card.Width - 150, y), Size = new Size(72, 20), Font = DS.BodyBold, ForeColor = DS.Slate900, TextAlign = ContentAlignment.MiddleRight, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            Label pill = new Label { Text = status, Location = new Point(card.Width - 70, y + 2), Size = new Size(58, 22), Font = DS.Caption, ForeColor = fore, BackColor = back, TextAlign = ContentAlignment.MiddleCenter, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            DS.Rounded(pill, 7);
            card.Controls.Add(inv);
            card.Controls.Add(date);
            card.Controls.Add(amt);
            card.Controls.Add(pill);
        }

        private Control RecentActionsCard()
        {
            Panel card = RightCard("Recent WhatsApp Actions", 212);
            LinkLabel view = new LinkLabel { Text = "View all", AutoSize = true, Location = new Point(card.Width - 70, 20), Anchor = AnchorStyles.Top | AnchorStyles.Right, LinkColor = DS.Primary600, Font = DS.Caption };
            card.Controls.Add(view);
            List<WhatsAppActionLog> logs = _service.LoadRecentActions(3);
            if (logs.Count == 0)
            {
                AddActionRow(card, "No manual actions yet", "Open a prepared WhatsApp chat", "-", "Prepared", 58);
                return card;
            }

            int y = 58;
            foreach (WhatsAppActionLog log in logs)
            {
                AddActionRow(card, log.TemplateType, log.ActionDate.ToString("dd MMM HH:mm"), FirstNonEmpty(log.LinkedRecord, log.Module), NormalizeActionStatus(log.Status), y);
                y += 46;
            }
            return card;
        }

        private void AddActionRow(Panel card, string title, string meta, string record, string statusText, int y)
        {
            Label icon = ModernIconSystem.Icon(ModernIconKind.Document, 15, DS.Slate700);
            icon.Size = new Size(20, 20);
            icon.Location = new Point(16, y + 2);
            Label lbl = new Label { Text = title, Location = new Point(43, y), Size = new Size(130, 20), Font = DS.SmallBold, ForeColor = DS.Slate900 };
            Label sub = new Label { Text = meta, Location = new Point(43, y + 20), Size = new Size(115, 18), Font = DS.Caption, ForeColor = DS.Slate500 };
            Label rec = new Label { Text = record, Location = new Point(card.Width - 126, y), Size = new Size(74, 20), Font = DS.Small, ForeColor = DS.Slate800, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            Label status = new Label { Text = statusText, Location = new Point(card.Width - 98, y + 20), Size = new Size(90, 20), Font = DS.Caption, ForeColor = DS.Green600, BackColor = DS.Green50, TextAlign = ContentAlignment.MiddleCenter, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            DS.Rounded(status, 7);
            card.Controls.Add(icon);
            card.Controls.Add(lbl);
            card.Controls.Add(sub);
            card.Controls.Add(rec);
            card.Controls.Add(status);
        }

        private Control LinkedRecordsCard()
        {
            Panel card = RightCard("Linked Records", 132);
            TableLayoutPanel grid = new TableLayoutPanel { Location = new Point(16, 50), Size = new Size(310, 62), ColumnCount = 4, RowCount = 1, BackColor = Color.Transparent, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            for (int i = 0; i < 4; i++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            AddLinkedMetric(grid, "Invoices", _selectedContact == null ? 0 : _selectedContact.InvoiceCount, ModernIconKind.Invoice, 0);
            AddLinkedMetric(grid, "Quotes", _selectedContact == null ? 0 : _selectedContact.QuoteCount, ModernIconKind.Document, 1);
            AddLinkedMetric(grid, "Jobs", _selectedContact == null ? 0 : _selectedContact.JobCount, ModernIconKind.Job, 2);
            AddLinkedMetric(grid, "Contracts", _selectedContact == null ? 0 : _selectedContact.ContractCount, ModernIconKind.Contract, 3);
            card.Controls.Add(grid);
            card.Resize += (s, e) => grid.Width = Math.Max(280, card.Width - 32);
            return card;
        }

        private void AddLinkedMetric(TableLayoutPanel grid, string title, int value, ModernIconKind kind, int column)
        {
            Panel item = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            Label icon = ModernIconSystem.Badge(kind, 28, DS.Primary50, DS.Primary600, 8);
            icon.Location = new Point(24, 0);
            Label label = new Label { Text = title, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Location = new Point(0, 30), Size = new Size(76, 16), Font = DS.Caption, ForeColor = DS.Slate600 };
            Label count = new Label { Text = value.ToString(), AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Location = new Point(0, 46), Size = new Size(76, 16), Font = DS.SmallBold, ForeColor = DS.Slate900 };
            item.Controls.Add(icon);
            item.Controls.Add(label);
            item.Controls.Add(count);
            grid.Controls.Add(item, column, 0);
        }

        private Panel RightCard(string title, int height)
        {
            Panel card = Card(new Padding(16), 12);
            card.Width = Math.Max(300, _detailHost.ClientSize.Width - 6);
            card.Height = height;
            card.Margin = new Padding(0, 0, 0, 12);
            Label label = new Label { Text = title, AutoSize = true, Font = DS.H3, ForeColor = DS.Slate900, Location = new Point(16, 18) };
            card.Controls.Add(label);
            return card;
        }

        private Control NoticeBanner()
        {
            Panel banner = new Panel { Width = 560, Height = 56, BackColor = NoticeBack, Margin = new Padding(0, 0, 0, 14), Padding = new Padding(12, 9, 12, 8) };
            DS.Rounded(banner, 8);
            banner.Paint += (s, e) => DrawBorder((Control)s, e.Graphics, NoticeBorder, 8);
            Label icon = ModernIconSystem.Icon(ModernIconKind.Alert, 16, DS.Amber600);
            icon.Size = new Size(22, 22);
            icon.Location = new Point(10, 16);
            Label text = new Label { Text = "This is not the official WhatsApp. Messages are opened in your WhatsApp Web / Desktop.", Location = new Point(40, 10), Size = new Size(480, 20), Font = DS.Small, ForeColor = DS.Slate800 };
            LinkLabel more = new LinkLabel { Text = "Learn more", Location = new Point(40, 30), Size = new Size(100, 18), Font = DS.Small, LinkColor = DS.Primary600 };
            Label close = new Label { Text = "×", Location = new Point(532, 13), Size = new Size(18, 18), Font = DS.BodyBold, ForeColor = DS.Slate500, TextAlign = ContentAlignment.MiddleCenter };
            banner.Controls.Add(icon);
            banner.Controls.Add(text);
            banner.Controls.Add(more);
            banner.Controls.Add(close);
            return banner;
        }

        private Control DatePill(string text)
        {
            Label label = new Label { Text = text, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Font = DS.SmallBold, ForeColor = DS.Slate700, BackColor = DS.White, Width = 96, Height = 28, Margin = new Padding(230, 0, 0, 14) };
            DS.Rounded(label, 10);
            return label;
        }

        private Control MessageBubble(string text, bool outgoing, string time)
        {
            Panel bubble = new Panel
            {
                Width = 430,
                Height = Math.Max(56, 42 + (text.Length / 44) * 20),
                BackColor = outgoing ? BubbleGreen : DS.White,
                Margin = outgoing ? new Padding(250, 0, 0, 10) : new Padding(0, 0, 0, 10),
                Padding = new Padding(14)
            };
            DS.Rounded(bubble, 10);
            bubble.Paint += (s, e) => DrawBorder((Control)s, e.Graphics, outgoing ? Color.FromArgb(187, 247, 208) : DS.Border, 10);
            Label message = new Label { Text = text, AutoSize = false, Location = new Point(14, 12), Size = new Size(360, bubble.Height - 30), Font = DS.Body, ForeColor = DS.Slate900 };
            Label clock = new Label { Text = time + (outgoing ? "  ✓✓" : string.Empty), AutoSize = false, Location = new Point(bubble.Width - 104, bubble.Height - 24), Size = new Size(94, 16), TextAlign = ContentAlignment.MiddleRight, Font = DS.Caption, ForeColor = outgoing ? DS.Primary600 : DS.Slate500 };
            bubble.Controls.Add(message);
            bubble.Controls.Add(clock);
            return bubble;
        }

        private Control DocumentBubble(string fileName, string meta, string time)
        {
            Panel bubble = new Panel { Width = 380, Height = 74, BackColor = BubbleGreen, Margin = new Padding(300, 0, 0, 12), Padding = new Padding(12) };
            DS.Rounded(bubble, 10);
            bubble.Paint += (s, e) => DrawBorder((Control)s, e.Graphics, Color.FromArgb(187, 247, 208), 10);
            Label pdf = new Label { Text = "PDF", Location = new Point(14, 16), Size = new Size(48, 44), Font = DS.SmallBold, ForeColor = Color.White, BackColor = DS.Red500, TextAlign = ContentAlignment.MiddleCenter };
            DS.Rounded(pdf, 8);
            Label name = new Label { Text = fileName, Location = new Point(76, 14), Size = new Size(160, 20), Font = DS.BodyBold, ForeColor = DS.Slate900 };
            Label details = new Label { Text = meta, Location = new Point(76, 37), Size = new Size(140, 20), Font = DS.Small, ForeColor = DS.Slate600 };
            Label clock = new Label { Text = time + "  ✓✓", Location = new Point(270, 46), Size = new Size(96, 18), TextAlign = ContentAlignment.MiddleRight, Font = DS.Caption, ForeColor = DS.Primary600 };
            bubble.Controls.Add(pdf);
            bubble.Controls.Add(name);
            bubble.Controls.Add(details);
            bubble.Controls.Add(clock);
            return bubble;
        }

        private Button TabButton(string text)
        {
            int width = text == "All" ? 46 : text == "Unread" ? 64 : text == "Clients" ? 58 : text == "Vendors" ? 66 : 50;
            Button button = DS.GhostBtn(text, width, 30);
            button.Tag = text;
            button.Margin = new Padding(0, 0, 4, 0);
            button.Padding = Padding.Empty;
            button.Font = new Font("Segoe UI", 6.8f, FontStyle.Bold);
            button.Click += (s, e) =>
            {
                _activeTab = (string)((Button)s).Tag;
                RenderTabs();
                RenderConversations();
            };
            return button;
        }

        private Panel SearchBox(TextBox textBox, string placeholder)
        {
            Panel host = new Panel { Dock = DockStyle.Fill, BackColor = DS.White, Padding = new Padding(36, 9, 10, 6), Margin = new Padding(0, 0, 0, 8) };
            DS.Rounded(host, 9);
            host.Paint += (s, e) => DrawBorder((Control)s, e.Graphics, DS.Border, 9);
            Label icon = ModernIconSystem.Icon(ModernIconKind.Search, 15, DS.Slate500);
            icon.Size = new Size(22, 22);
            icon.Location = new Point(10, 8);
            textBox.BorderStyle = BorderStyle.None;
            textBox.Dock = DockStyle.Fill;
            textBox.Font = DS.Small;
            textBox.ForeColor = DS.Slate700;
            textBox.BackColor = DS.White;
            if (string.IsNullOrWhiteSpace(textBox.Text))
                textBox.Text = string.Empty;
            textBox.Tag = placeholder;
            host.Controls.Add(textBox);
            host.Controls.Add(icon);
            return host;
        }

        private Button IconOnlyButton(ModernIconKind kind, Color foreColor, Color backColor, int size = 36)
        {
            Button button = new Button
            {
                Width = size,
                Height = size,
                BackColor = backColor,
                ForeColor = foreColor,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Image = ModernIconSystem.IconBitmap(kind, Math.Max(15, size - 18), foreColor),
                ImageAlign = ContentAlignment.MiddleCenter,
                UseVisualStyleBackColor = false,
                Margin = Padding.Empty
            };
            button.FlatAppearance.BorderSize = backColor == WhatsAppGreen ? 0 : 1;
            button.FlatAppearance.BorderColor = backColor == WhatsAppGreen ? backColor : DS.Border;
            button.FlatAppearance.MouseOverBackColor = backColor == WhatsAppGreen ? WhatsAppGreenDark : DS.Slate50;
            DS.Rounded(button, size / 3);
            return button;
        }

        private Panel Card(Padding padding, int radius)
        {
            Panel card = new Panel { Dock = DockStyle.Fill, BackColor = DS.White, Padding = padding };
            DS.Rounded(card, radius);
            card.Paint += (s, e) => DrawBorder((Control)s, e.Graphics, DS.Border, radius);
            return card;
        }

        private Label Avatar(string name, int size, Color color)
        {
            Label avatar = new Label
            {
                Text = Initials(name),
                Size = new Size(size, size),
                BackColor = color,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", size >= 48 ? 12f : 10f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            DS.Rounded(avatar, size / 2);
            return avatar;
        }

        private Label DetailLine(ModernIconKind kind, string text, int x, int y)
        {
            Label label = new Label { Text = ModernIconSystem.Icon(kind, 12, DS.Slate500).Text + "  " + text, AutoSize = false, Location = new Point(x, y), Size = new Size(240, 20), Font = DS.Small, ForeColor = DS.Slate700 };
            return label;
        }

        private static void DrawBorder(Control control, Graphics graphics, Color color, int radius)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(color))
            using (GraphicsPath path = RoundedPath(new Rectangle(0, 0, control.Width - 1, control.Height - 1), radius))
                graphics.DrawPath(pen, path);
        }

        private static GraphicsPath RoundedPath(Rectangle rect, int radius)
        {
            int diameter = Math.Max(1, radius * 2);
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static string Initials(string name)
        {
            string[] parts = (name ?? "WA").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return "WA";
            if (parts.Length == 1)
                return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
            return (parts[0][0].ToString() + parts[1][0].ToString()).ToUpperInvariant();
        }

        private static Color ContactColor(WhatsAppContact contact)
        {
            string type = contact == null ? string.Empty : contact.SourceType;
            if (type == "Vendor") return Color.FromArgb(124, 58, 237);
            if (type == "Team") return Color.FromArgb(14, 116, 144);
            return WhatsAppGreen;
        }

        private static string FormatTime(DateTime value)
        {
            if (value.Date == DateTime.Today)
                return value.ToString("h:mm tt");
            if (value.Date == DateTime.Today.AddDays(-1))
                return "Yesterday";
            return value.ToString("dd/MM/yyyy");
        }

        private static string PrettyPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return "Phone not saved";
            return phone.StartsWith("+") ? phone : "+91 " + phone;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            return string.Empty;
        }

        private static string NormalizeActionStatus(string status)
        {
            string value = status ?? string.Empty;
            if (value.IndexOf("Confirmed", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Sent";
            if (value.IndexOf("Opened", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Opened";
            if (value.IndexOf("Prepared", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Prepared";
            return string.IsNullOrWhiteSpace(value) ? "Prepared" : value;
        }

        private static string ResolveRecordType(string templateType)
        {
            string type = templateType ?? string.Empty;
            if (type.IndexOf("Invoice", StringComparison.OrdinalIgnoreCase) >= 0 || type.IndexOf("Payment", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Invoice";
            if (type.IndexOf("Quotation", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Quotation";
            if (type.IndexOf("AMC", StringComparison.OrdinalIgnoreCase) >= 0)
                return "AMC";
            if (type.IndexOf("Job", StringComparison.OrdinalIgnoreCase) >= 0 || type.IndexOf("Technician", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Job";
            return string.Empty;
        }

        private static string ResolveRecordName(string templateType)
        {
            string type = ResolveRecordType(templateType);
            if (type == "Invoice") return "Invoice reminder";
            if (type == "Quotation") return "Quotation follow-up";
            if (type == "AMC") return "AMC renewal";
            if (type == "Job") return "Service job update";
            return string.Empty;
        }
    }
}

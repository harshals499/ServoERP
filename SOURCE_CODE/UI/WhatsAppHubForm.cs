using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using Microsoft.Web.WebView2.WinForms;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class WhatsAppHubForm : BaseUserControl
    {
        private static readonly Color WhatsAppGreen = Color.FromArgb(22, 163, 74);
        private static readonly Color WhatsAppGreenDark = Color.FromArgb(5, 150, 105);
        private static readonly Color WhatsAppGreenLight = Color.FromArgb(220, 252, 231);
        private static readonly Color BubbleGreen = Color.FromArgb(220, 252, 231);
        private static readonly Color NoticeBack = Color.FromArgb(255, 251, 235);
        private static readonly Color NoticeBorder = DS.Border;

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
        private readonly Label _selectedTemplateLabel = new Label();
        private readonly Label _selectedContactMeta = new Label();
        private readonly ToolTip _toolTip = new ToolTip();
        private WebView2 _whatsAppWeb;
        private Button _markSentButton;
        private WhatsAppQuickActionContext _pendingContext;
        private BackgroundWorker _loadWorker;

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
            Load += (s, e) => QueueHubDataLoad();
            BuildLayout();
            RenderLoadingState();
            PageHeaderPolishService.Apply(this);
            UIHelper.ApplyInputStyles(Controls);
            InputOutlineService.ApplyToTree(this);
        }

        private void QueueHubDataLoad()
        {
            var timer = new Timer { Interval = 1500 };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                timer.Dispose();
                if (!IsDisposed && Visible)
                    LoadHubData();
            };
            timer.Start();
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
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildBody(), 0, 1);
            Controls.Add(root);
        }

        private Control BuildHeader()
        {
            Panel header = new Panel { Dock = DockStyle.Fill, BackColor = DS.BgPage };

            Label icon = ModernIconSystem.Badge(ModernIconKind.Phone, 42, WhatsAppGreenLight, WhatsAppGreen, 12);
            icon.Location = new Point(2, 10);
            header.Controls.Add(icon);

            Label title = new Label
            {
                Text = "WhatsApp Hub",
                AutoSize = true,
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                ForeColor = DS.Slate950,
                Location = new Point(56, 7)
            };
            header.Controls.Add(title);

            Label subtitle = new Label
            {
                Text = "Connected WhatsApp Web workspace for client, supplier, and team message follow-up.",
                AutoSize = true,
                Font = DS.Small,
                ForeColor = DS.Slate600,
                Location = new Point(57, 38)
            };
            header.Controls.Add(subtitle);

            Button settings = IconOnlyButton(ModernIconKind.Settings, DS.Slate700, DS.White, 38, "WhatsApp settings");
            settings.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            settings.Location = new Point(header.Width - 44, 12);
            settings.Click += (s, e) => MessageBox.Show("WhatsApp Hub uses embedded WhatsApp Web or browser deep links only. ServoERP does not auto-send messages, scrape chats, or claim live sync without the official API.", "WhatsApp Hub", MessageBoxButtons.OK, MessageBoxIcon.Information);
            header.Controls.Add(settings);

            Button refresh = IconOnlyButton(ModernIconKind.Refresh, DS.Slate700, DS.White, 38, "Refresh WhatsApp contacts");
            refresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            refresh.Location = new Point(header.Width - 94, 12);
            refresh.Click += (s, e) => { LoadHubData(); ReloadWhatsAppHome(); };
            header.Controls.Add(refresh);

            Button newMessage = DS.PrimaryBtn("Open WhatsApp Web", 168, 38);
            newMessage.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            newMessage.Location = new Point(header.Width - 270, 12);
            ModernIconSystem.AddButtonIcon(newMessage, ModernIconKind.Email);
            newMessage.Click += (s, e) => ReloadWhatsAppHome();
            header.Controls.Add(newMessage);

            Panel searchHost = SearchBox(_globalSearch, "Search name, mobile, invoice, job...");
            searchHost.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            searchHost.Size = new Size(340, 40);
            searchHost.Location = new Point(header.Width - 626, 12);
            _globalSearch.TextChanged += (s, e) => ApplyGlobalSearch();
            header.Controls.Add(searchHost);

            header.Resize += (s, e) =>
            {
                settings.Location = new Point(header.Width - 44, 12);
                refresh.Location = new Point(header.Width - 94, 12);
                newMessage.Location = new Point(header.Width - 270, 12);
                searchHost.Location = new Point(Math.Max(380, header.Width - 626), 12);
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
                leftWrap.Width = width < 1280 ? 330 : 388;
                rightWrap.Width = width < 1280 ? 314 : 354;
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

            TableLayoutPanel tabs = new TableLayoutPanel { Dock = DockStyle.Top, Height = 38, ColumnCount = 5, RowCount = 1, BackColor = Color.Transparent, Padding = new Padding(0), Margin = Padding.Empty };
            for (int i = 0; i < 5; i++)
                tabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            int tabColumn = 0;
            foreach (string tab in new[] { "All", "Unread", "Clients", "Vendors", "Team" })
            {
                Button button = TabButton(tab);
                tabs.Controls.Add(button, tabColumn++, 0);
                _tabButtons.Add(button);
            }
            host.Controls.Add(tabs);

            TableLayoutPanel searchRow = new TableLayoutPanel { Dock = DockStyle.Top, Height = 48, ColumnCount = 2, BackColor = Color.Transparent };
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
            Panel search = SearchBox(_conversationSearch, "Search or start new chat...");
            _conversationSearch.TextChanged += (s, e) => RenderConversations();
            searchRow.Controls.Add(search, 0, 0);
            Button filter = IconOnlyButton(ModernIconKind.Filter, DS.Slate700, DS.White, 36, "Filter WhatsApp contacts");
            filter.Margin = new Padding(8, 0, 0, 0);
            filter.Dock = DockStyle.Fill;
            filter.Click += (s, e) => ShowConversationFilterMenu(filter);
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
            if (_loadWorker != null && _loadWorker.IsBusy)
                return;

            Cursor = Cursors.WaitCursor;
            SetLoadingText("Loading contacts...");

            _loadWorker = CreateWorker();
            _loadWorker.DoWork += (s, e) =>
            {
                List<WhatsAppContact> contacts = _service.LoadContacts()
                    .OrderByDescending(c => !string.IsNullOrWhiteSpace(c.Phone))
                    .ThenByDescending(c => c.LastMessageAt)
                    .ToList();
                List<WhatsAppTemplate> templates = _service.LoadTemplates();
                e.Result = new WhatsAppHubLoadResult { Contacts = contacts, Templates = templates };
            };
            _loadWorker.RunWorkerCompleted += (s, e) =>
            {
                if (e.Error != null)
                {
                    AppLogger.LogError("WhatsAppHubForm.LoadHubData", e.Error);
                    RunOnUI(() =>
                    {
                        Cursor = Cursors.Default;
                        SetLoadingText("Could not load contacts. Click refresh to try again.");
                    });
                    ShowError( "Failed to load WhatsApp contacts. Please try again.", e.Error);
                    return;
                }
                if (e.Cancelled) return;

                RunOnUI(() =>
                {
                    Cursor = Cursors.Default;
                    var result = e.Result as WhatsAppHubLoadResult ?? new WhatsAppHubLoadResult();
                    _contacts = result.Contacts ?? new List<WhatsAppContact>();
                    _templates = result.Templates ?? new List<WhatsAppTemplate>();
                    _selectedContact = _selectedContact == null
                        ? _contacts.FirstOrDefault()
                        : _contacts.FirstOrDefault(c => c.SourceType == _selectedContact.SourceType && c.SourceId == _selectedContact.SourceId) ?? _contacts.FirstOrDefault();
                    _selectedTemplate = _templates.FirstOrDefault(t => t.TemplateType == "Payment reminder") ?? _templates.FirstOrDefault();

                    _conversationCount.Text = _contacts.Count.ToString();
                    RenderTabs();
                    RenderConversations();
                    RenderChat();
                    RenderDetails();
                });
            };
            _loadWorker.RunWorkerAsync();
        }

        private void RenderLoadingState()
        {
            _conversationCount.Text = "...";
            SetLoadingText("Loading contacts...");
            _detailHost.Controls.Clear();
        }

        private void SetLoadingText(string text)
        {
            if (_chatHost == null)
                return;

            _chatHost.SuspendLayout();
            _chatHost.Controls.Clear();
            Label loading = new Label
            {
                Dock = DockStyle.Fill,
                Text = text,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = DS.BodyBold,
                ForeColor = DS.Slate600,
                BackColor = DS.White
            };
            _chatHost.Controls.Add(loading);
            _chatHost.ResumeLayout();
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

        private void ApplyGlobalSearch()
        {
            string query = (_globalSearch.Text ?? string.Empty).Trim();
            if (!string.Equals(_conversationSearch.Text, query, StringComparison.Ordinal))
                _conversationSearch.Text = query;
            RenderConversations();
        }

        private void ShowConversationFilterMenu(Control owner)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            foreach (string tab in new[] { "All", "Unread", "Clients", "Vendors", "Team" })
            {
                ToolStripMenuItem item = new ToolStripMenuItem(tab) { Checked = string.Equals(_activeTab, tab, StringComparison.OrdinalIgnoreCase) };
                item.Click += (s, e) =>
                {
                    _activeTab = tab;
                    RenderTabs();
                    RenderConversations();
                };
                menu.Items.Add(item);
            }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Clear Search", null, (s, e) =>
            {
                _conversationSearch.Clear();
                _globalSearch.Clear();
                _activeTab = "All";
                RenderTabs();
                RenderConversations();
            });
            menu.Show(owner, new Point(0, owner.Height));
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
                Label name = new Label { Text = _selectedContact.Name, AutoSize = false, Size = new Size(Math.Max(260, header.Width - 270), 24), Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = DS.Slate950, Location = new Point(84, 18), AutoEllipsis = true };
                Label phone = new Label { Text = PrettyPhone(_selectedContact.Phone), AutoSize = false, Size = new Size(Math.Max(220, header.Width - 300), 18), Font = DS.Small, ForeColor = DS.Slate600, Location = new Point(86, 48), AutoEllipsis = true };
                name.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                phone.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                header.Controls.Add(name);
                header.Controls.Add(phone);
            }

            Button more = IconOnlyButton(ModernIconKind.Settings, DS.Slate700, DS.White, 36, "Chat settings");
            more.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            more.Location = new Point(header.Width - 58, 22);
            Button video = IconOnlyButton(ModernIconKind.Document, DS.Slate700, DS.White, 36, "Open related record");
            video.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            video.Location = new Point(header.Width - 106, 22);
            Button call = IconOnlyButton(ModernIconKind.Phone, DS.Slate700, DS.White, 36, "Call contact");
            call.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            call.Location = new Point(header.Width - 154, 22);
            more.Click += (s, e) => ShowChatSettingsMenu(more);
            video.Click += (s, e) => OpenRelatedRecordHint();
            call.Click += (s, e) => OpenPhoneDialer();
            header.Controls.Add(call);
            header.Controls.Add(video);
            header.Controls.Add(more);
            header.Resize += (s, e) =>
            {
                more.Location = new Point(header.Width - 58, 22);
                video.Location = new Point(header.Width - 106, 22);
                call.Location = new Point(header.Width - 154, 22);
                foreach (Control control in header.Controls)
                {
                    if (control is Label label && label.Location.X >= 84)
                        label.Width = Math.Max(220, header.Width - 270);
                }
            };
            _chatHost.Controls.Add(header);

            Panel composer = BuildComposer();
            composer.Dock = DockStyle.Bottom;
            _chatHost.Controls.Add(composer);

            Panel browserPanel = HasUsablePhone(_selectedContact) ? BuildWhatsAppWebPanel() : BuildMissingPhonePanel();
            _chatHost.Controls.Add(browserPanel);
            browserPanel.BringToFront();
            composer.BringToFront();

            ApplyTemplate(_selectedTemplate, false);
            _chatHost.ResumeLayout();
        }

        private Panel BuildComposer()
        {
            Panel host = new Panel { Height = 96, Padding = new Padding(20, 6, 20, 8), BackColor = DS.White };

            _messageInput.BorderStyle = BorderStyle.None;
            _messageInput.Visible = false;
            _messageInput.Multiline = true;
            _messageInput.Font = DS.Body;
            _messageInput.Size = new Size(1, 1);
            host.Controls.Add(_messageInput);

            FlowLayoutPanel actionRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = false, BackColor = DS.White };
            Button templatePicker = IconOnlyButton(ModernIconKind.Status, DS.Slate600, DS.White, 34, "Choose saved template");
            Button document = IconOnlyButton(ModernIconKind.Document, DS.Slate600, DS.White, 34, "Attach document reference");
            Button phone = IconOnlyButton(ModernIconKind.Phone, DS.Slate600, DS.White, 34, "Contact phone status");
            templatePicker.Margin = new Padding(0, 2, 8, 2);
            document.Margin = new Padding(0, 2, 8, 2);
            phone.Margin = new Padding(0, 2, 8, 2);
            templatePicker.Click += (s, e) => ShowTemplatePickerMenu(templatePicker);
            document.Click += (s, e) => AttachDocumentReference();
            phone.Click += (s, e) => ShowPhoneStatus();

            Button send = IconOnlyButton(ModernIconKind.Email, Color.White, WhatsAppGreen, 38, "Open prefilled WhatsApp chat");
            send.Margin = new Padding(0, 0, 8, 0);
            if (!HasUsablePhone(_selectedContact))
            {
                send.Enabled = false;
                send.BackColor = DS.Slate200;
                send.ForeColor = DS.Slate500;
                send.Image = ModernIconSystem.IconBitmap(ModernIconKind.Email, 26, DS.Slate500);
                send.FlatAppearance.BorderSize = 1;
                send.FlatAppearance.BorderColor = DS.Border;
                send.FlatAppearance.MouseOverBackColor = DS.Slate200;
                _toolTip.SetToolTip(send, "Add a phone number before opening WhatsApp");
            }
            send.Click += SendCurrentMessage;
            _markSentButton = DS.GhostBtn("Mark Sent", 90, 32);
            _markSentButton.Enabled = false;
            _markSentButton.Margin = new Padding(0, 3, 8, 3);
            _markSentButton.Click += (s, e) => MarkPendingMessageSent();
            Button copyMessage = DS.GhostBtn("Copy Message", 118, 32);
            copyMessage.Margin = new Padding(0, 3, 8, 3);
            copyMessage.Click += (s, e) => CopyPreparedMessage();
            actionRow.Controls.Add(templatePicker);
            actionRow.Controls.Add(document);
            actionRow.Controls.Add(phone);
            actionRow.Controls.Add(send);
            actionRow.Controls.Add(_markSentButton);
            actionRow.Controls.Add(copyMessage);
            host.Controls.Add(actionRow);

            FlowLayoutPanel templates = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = false, BackColor = DS.White };
            string[] quick = { "Send Invoice", "Send Quotation", "Payment Reminder", "AMC Reminder", "Job Update", "More" };
            foreach (string title in quick)
            {
                Button button = DS.GhostBtn(title, title == "Payment Reminder" ? 130 : 108, 30);
                button.Font = DS.Caption;
                button.Margin = new Padding(0, 5, 6, 0);
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

        private void ShowTemplatePickerMenu(Control owner)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            foreach (WhatsAppTemplate template in _templates)
            {
                WhatsAppTemplate selected = template;
                ToolStripMenuItem item = new ToolStripMenuItem(template.Title ?? template.TemplateType);
                item.Checked = _selectedTemplate != null && string.Equals(_selectedTemplate.TemplateType, template.TemplateType, StringComparison.OrdinalIgnoreCase);
                item.Click += (s, e) => ApplyTemplate(selected, false);
                menu.Items.Add(item);
            }

            if (menu.Items.Count == 0)
                menu.Items.Add("No templates loaded").Enabled = false;
            menu.Show(owner, new Point(0, owner.Height));
        }

        private void AttachDocumentReference()
        {
            if (_selectedContact == null)
            {
                MessageBox.Show("Select a WhatsApp contact before attaching a document reference.", "WhatsApp Hub", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string reference = ResolveRecordName(_selectedTemplate == null ? string.Empty : _selectedTemplate.TemplateType);
            if (string.IsNullOrWhiteSpace(reference))
                reference = "linked ServoERP document";
            string suffix = Environment.NewLine + Environment.NewLine + "Reference: " + reference;
            if ((_messageInput.Text ?? string.Empty).IndexOf(suffix, StringComparison.OrdinalIgnoreCase) < 0)
                _messageInput.Text = (_messageInput.Text ?? string.Empty).TrimEnd() + suffix;
            _webStatus.Text = "Document reference added to prepared message.";
        }

        private void ShowPhoneStatus()
        {
            if (_selectedContact == null)
            {
                MessageBox.Show("No contact selected.", "WhatsApp Hub", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string normalized = _service.ValidatePhone(_selectedContact.Phone);
            MessageBox.Show(
                "Contact: " + _selectedContact.Name + Environment.NewLine +
                "Saved phone: " + PrettyPhone(_selectedContact.Phone) + Environment.NewLine +
                "WhatsApp number: " + (string.IsNullOrWhiteSpace(normalized) ? "Invalid or missing" : normalized),
                "Phone Status",
                MessageBoxButtons.OK,
                string.IsNullOrWhiteSpace(normalized) ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        private void ApplyTemplate(WhatsAppTemplate template, bool focus)
        {
            if (template == null)
                return;
            _selectedTemplate = template;
            _messageInput.Text = _service.RenderTemplate(template, _selectedContact);
            _selectedTemplateLabel.Text = "Template: " + (template.Title ?? template.TemplateType ?? "General update");
            if (focus && _messageInput.Visible)
                _messageInput.Focus();
        }

        private void SendCurrentMessage(object sender, EventArgs e)
        {
            try
            {
                if (_selectedContact == null)
                    throw new InvalidOperationException("Select a contact before opening WhatsApp.");
                if (!HasUsablePhone(_selectedContact))
                    throw new InvalidOperationException("This contact does not have a saved phone number. Add a mobile number before opening WhatsApp.");

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
            Panel host = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(250, 252, 255), Padding = new Padding(16, 12, 16, 8) };

            Panel notice = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = NoticeBack, Padding = new Padding(12, 7, 12, 7) };
            DS.Rounded(notice, 8);
            notice.Paint += (s, e) => DrawBorder((Control)s, e.Graphics, NoticeBorder, 8);
            Label noticeText = new Label
            {
                Text = "WhatsApp Web runs inside ServoERP. Scan the QR code once, then open prefilled chats from the send button.",
                Dock = DockStyle.Fill,
                Font = DS.Small,
                ForeColor = DS.Slate800,
                TextAlign = ContentAlignment.MiddleLeft
            };
            notice.Controls.Add(noticeText);
            host.Controls.Add(notice);

            _webStatus.Dock = DockStyle.Bottom;
            _webStatus.Height = 24;
            _webStatus.Font = DS.Small;
            _webStatus.ForeColor = DS.Slate600;
            _webStatus.Text = "WhatsApp Web will start when you open a chat.";
            host.Controls.Add(_webStatus);

            Panel browserHost = new Panel { Dock = DockStyle.Fill, BackColor = DS.White, Padding = new Padding(0), Margin = new Padding(0, 8, 0, 4) };
            host.Controls.Add(browserHost);
            browserHost.BringToFront();

            _whatsAppWeb = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.White };
            browserHost.Controls.Add(_whatsAppWeb);

            return host;
        }

        private Panel BuildManualWhatsAppPanel()
        {
            Panel host = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(250, 252, 255), Padding = new Padding(28) };
            Panel card = new Panel { Width = 520, Height = 190, BackColor = DS.White, Padding = new Padding(24) };
            DS.Rounded(card, 12);
            card.Paint += (s, e) => DrawBorder((Control)s, e.Graphics, DS.Border, 12);
            Label title = new Label { Text = "Ready to open WhatsApp", Dock = DockStyle.Top, Height = 34, Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = DS.Slate900 };
            Label body = new Label
            {
                Text = "Choose a template, review the message, then click send to open the prefilled WhatsApp Web chat. The embedded browser starts only when needed.",
                Dock = DockStyle.Top,
                Height = 74,
                Font = DS.Body,
                ForeColor = DS.Slate700
            };
            Label hint = new Label { Text = "ServoERP prepares the message; the operator sends it manually.", Dock = DockStyle.Top, Height = 28, Font = DS.Small, ForeColor = DS.Slate600 };
            card.Controls.Add(hint);
            card.Controls.Add(body);
            card.Controls.Add(title);
            host.Controls.Add(card);
            host.Resize += (s, e) => card.Location = new Point(Math.Max(20, (host.ClientSize.Width - card.Width) / 2), Math.Max(30, (host.ClientSize.Height - card.Height) / 2));
            card.Location = new Point(Math.Max(20, (host.ClientSize.Width - card.Width) / 2), Math.Max(30, (host.ClientSize.Height - card.Height) / 2));
            _webStatus.Text = "WhatsApp Web will load when a chat is opened.";
            return host;
        }

        private Panel BuildMissingPhonePanel()
        {
            Panel host = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(250, 252, 255), Padding = new Padding(28) };
            Panel card = new Panel { Width = 480, Height = 170, BackColor = DS.White, Padding = new Padding(24) };
            DS.Rounded(card, 12);
            card.Paint += (s, e) => DrawBorder((Control)s, e.Graphics, DS.Border, 12);
            Label title = new Label { Text = "Phone number required", Dock = DockStyle.Top, Height = 30, Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = DS.Slate900 };
            Label body = new Label
            {
                Text = "This contact cannot be opened in WhatsApp because no phone number is saved. Add the mobile number in the client, vendor, or employee record, then refresh this hub.",
                Dock = DockStyle.Top,
                Height = 70,
                Font = DS.Body,
                ForeColor = DS.Slate700
            };
            Label hint = new Label { Text = "Sending is disabled until the phone number is available.", Dock = DockStyle.Top, Height = 24, Font = DS.Small, ForeColor = DS.Red600 };
            card.Controls.Add(hint);
            card.Controls.Add(body);
            card.Controls.Add(title);
            host.Controls.Add(card);
            host.Resize += (s, e) => card.Location = new Point(Math.Max(20, (host.ClientSize.Width - card.Width) / 2), Math.Max(30, (host.ClientSize.Height - card.Height) / 2));
            card.Location = new Point(Math.Max(20, (host.ClientSize.Width - card.Width) / 2), Math.Max(30, (host.ClientSize.Height - card.Height) / 2));
            _webStatus.Text = "Phone number missing.";
            return host;
        }

        private async System.Threading.Tasks.Task InitializeWhatsAppWebAsync()
        {
            if (IsDisposed || _whatsAppWeb == null || _whatsAppWeb.IsDisposed)
                return;

            if (!WebView2RuntimeHelper.IsRuntimeAvailable(out _, out string message))
            {
                SetWebStatus("WebView2 runtime is not available. Use the browser link actions instead.");
                AppLogger.LogInfo("WhatsAppHubForm WebView2 unavailable: " + message);
                return;
            }

            try
            {
                await _whatsAppWeb.EnsureCoreWebView2Async(await WebView2RuntimeHelper.CreateEnvironmentAsync());
                if (IsDisposed || _whatsAppWeb == null || _whatsAppWeb.IsDisposed || _whatsAppWeb.CoreWebView2 == null)
                    return;
                _whatsAppWeb.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                _whatsAppWeb.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _whatsAppWeb.CoreWebView2.NavigationCompleted += (s, e) =>
                {
                    SetWebStatus(e.IsSuccess ? "WhatsApp Web ready. Session is stored in the ServoERP WebView2 user data folder." : "WhatsApp Web navigation failed.");
                };
                _whatsAppWeb.Source = new Uri("https://web.whatsapp.com/");
            }
            catch (COMException ex)
            {
                const int OPERATION_ABORTED = unchecked((int)0x80004004);
                if (ex.ErrorCode != OPERATION_ABORTED || (!IsDisposed && _whatsAppWeb != null && !_whatsAppWeb.IsDisposed))
                    AppLogger.LogError("WhatsAppHubForm.InitializeWhatsAppWebAsync", ex);
                SetWebStatus("Could not load embedded WhatsApp Web.");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("WhatsAppHubForm.InitializeWhatsAppWebAsync", ex);
                SetWebStatus("Could not load embedded WhatsApp Web.");
            }
        }

        private void SetWebStatus(string text)
        {
            try
            {
                if (IsDisposed || _webStatus == null || _webStatus.IsDisposed)
                    return;
                if (_webStatus.InvokeRequired)
                {
                    if (_webStatus.IsHandleCreated)
                        _webStatus.BeginInvoke((Action)(() => SetWebStatus(text)));
                    return;
                }
                _webStatus.Text = text;
            }
            catch { }
        }

        private void NavigateWhatsAppWeb(string url)
        {
            if (_whatsAppWeb == null || _whatsAppWeb.IsDisposed)
            {
                RenderChat();
                Control manualPanel = _chatHost.Controls.Cast<Control>().FirstOrDefault(c => c.Dock == DockStyle.Fill);
                if (manualPanel != null)
                    _chatHost.Controls.Remove(manualPanel);
                Panel webPanel = BuildWhatsAppWebPanel();
                _chatHost.Controls.Add(webPanel);
                webPanel.BringToFront();
            }

            if (_whatsAppWeb != null && _whatsAppWeb.CoreWebView2 != null)
            {
                _whatsAppWeb.CoreWebView2.Navigate(url);
                return;
            }

            InitializeWhatsAppWebAsync().ContinueWith(_ =>
            {
                try
                {
                    if (!IsDisposed && IsHandleCreated && _whatsAppWeb != null && _whatsAppWeb.CoreWebView2 != null)
                        BeginInvoke((Action)(() => _whatsAppWeb.CoreWebView2.Navigate(url)));
                }
                catch { }
            });
        }

        private void ReloadWhatsAppHome()
        {
            if (_whatsAppWeb == null || _whatsAppWeb.IsDisposed)
            {
                RenderChat();
                return;
            }

            if (_whatsAppWeb.CoreWebView2 != null)
            {
                _whatsAppWeb.CoreWebView2.Navigate("https://web.whatsapp.com/");
                SetWebStatus("Opening WhatsApp Web home.");
                return;
            }

            BeginInvoke((Action)(async () => await InitializeWhatsAppWebAsync()));
        }

        private void ShowChatSettingsMenu(Control owner)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Reload WhatsApp Web", null, (s, e) => ReloadWhatsAppHome());
            menu.Items.Add("Open Chat in Browser", null, (s, e) => OpenSelectedChatInBrowser());
            menu.Items.Add("Copy Prepared Message", null, (s, e) => CopyPreparedMessage());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Refresh Contacts", null, (s, e) => LoadHubData());
            menu.Show(owner, new Point(0, owner.Height));
        }

        private void OpenSelectedChatInBrowser()
        {
            try
            {
                if (!HasUsablePhone(_selectedContact))
                    throw new InvalidOperationException("Add a valid phone number before opening WhatsApp.");
                string url = _service.BuildWhatsAppWebUrl(_selectedContact.Phone, _messageInput.Text);
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                _webStatus.Text = "Opened WhatsApp chat in external browser.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "WhatsApp Hub", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void CopyPreparedMessage()
        {
            string text = _messageInput.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                return;
            Clipboard.SetText(text);
            _webStatus.Text = "Prepared message copied.";
        }

        private void OpenRelatedRecordHint()
        {
            if (_selectedContact == null)
            {
                MessageBox.Show("Select a WhatsApp contact first.", "WhatsApp Hub", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MessageBox.Show(
                _selectedContact.SourceType + " record: " + _selectedContact.Name + Environment.NewLine +
                "Linked counts: " + _selectedContact.InvoiceCount + " invoices, " + _selectedContact.QuoteCount + " quotes, " + _selectedContact.JobCount + " jobs, " + _selectedContact.ContractCount + " contracts." + Environment.NewLine + Environment.NewLine +
                "Open the related module from the main navigation to inspect the full record.",
                "Related Records",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void OpenPhoneDialer()
        {
            try
            {
                if (!HasUsablePhone(_selectedContact))
                    throw new InvalidOperationException("This contact does not have a saved phone number.");
                string phone = _service.ValidatePhone(_selectedContact.Phone);
                Process.Start(new ProcessStartInfo { FileName = "tel:" + phone, UseShellExecute = true });
                _webStatus.Text = "Opened phone dialer for " + PrettyPhone(_selectedContact.Phone) + ".";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "WhatsApp Hub", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RenderDetails()
        {
            _detailHost.SuspendLayout();
            _detailHost.Controls.Clear();
            _detailHost.AutoScroll = true;
            _detailHost.BackColor = DS.BgPage;

            int y = 0;
            foreach (Control card in new[] { ContactCard(), QuickActionsCard(), OpenInvoicesCard(), LinkedRecordsCard() })
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
            edit.Click += (s, e) => ShowContactEditGuidance();
            card.Controls.Add(name);
            card.Controls.Add(phone);
            card.Controls.Add(email);
            card.Controls.Add(location);
            card.Controls.Add(edit);
            return card;
        }

        private void ShowContactEditGuidance()
        {
            if (_selectedContact == null)
            {
                MessageBox.Show("Select a WhatsApp contact first.", "WhatsApp Hub", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MessageBox.Show(
                "Edit this " + _selectedContact.SourceType.ToLowerInvariant() + " from its master record." + Environment.NewLine + Environment.NewLine +
                "Name: " + _selectedContact.Name + Environment.NewLine +
                "Phone: " + PrettyPhone(_selectedContact.Phone) + Environment.NewLine +
                "Email: " + FirstNonEmpty(_selectedContact.Email, "Not saved"),
                "Edit Contact",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
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
            icon.Click += (s, e) => SelectTemplateFromButton(title);
            text.Click += (s, e) => SelectTemplateFromButton(title);
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
            button.Dock = DockStyle.Fill;
            button.Tag = text;
            button.Margin = new Padding(0, 0, 4, 6);
            button.Padding = Padding.Empty;
            button.Font = new Font("Segoe UI", 7.4f, FontStyle.Bold);
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

        private Button IconOnlyButton(ModernIconKind kind, Color foreColor, Color backColor, int size = 36, string tooltip = null)
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
            if (!string.IsNullOrWhiteSpace(tooltip))
            {
                button.AccessibleName = tooltip;
                _toolTip.SetToolTip(button, tooltip);
            }
            return button;
        }

        private static bool HasUsablePhone(WhatsAppContact contact)
        {
            return contact != null && !string.IsNullOrWhiteSpace(contact.Phone);
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

        private sealed class WhatsAppHubLoadResult
        {
            public List<WhatsAppContact> Contacts { get; set; }
            public List<WhatsAppTemplate> Templates { get; set; }
        }
    }
}




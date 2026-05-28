using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.AI;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    /// <summary>
    /// Enterprise Copilot surface. This form is intentionally read-only: it suggests actions but never writes ERP records.
    /// </summary>
    public class AiAssistantForm : Form
    {
        private readonly Func<string> _moduleResolver;
        private readonly AiAssistantService _assistant = new AiAssistantService();
        private FlowLayoutPanel _chat;
        private TextBox _txtPrompt;
        private ComboBox _cmbMode;
        private Label _lblContext;
        private Label _lblStatus;
        private Button _btnSend;
        private Button _btnCancel;
        private Button _btnRegenerate;
        private string _lastPrompt;
        private string _lastResponse;
        private CancellationTokenSource _requestCts;
        private Panel _suggestionCard;

        private sealed class PromptSuggestion
        {
            public PromptSuggestion(string label, string prompt)
            {
                Label = label;
                Prompt = prompt;
            }

            public string Label { get; private set; }
            public string Prompt { get; private set; }
        }

        public AiAssistantForm(Func<string> moduleResolver)
        {
            _moduleResolver = moduleResolver;
            BuildLayout();
            Shown += async (s, e) => await CheckLocalAiAsync();
            FormClosed += (s, e) => CancelActiveRequest();
        }

        private void BuildLayout()
        {
            Text = "ServoERP Copilot";
            StartPosition = FormStartPosition.Manual;
            Size = new Size(440, 760);
            MinimumSize = new Size(390, 560);
            BackColor = DS.BgPage;
            Font = DS.Body;

            Panel header = new Panel { Dock = DockStyle.Top, Height = 128, BackColor = DS.White, Padding = new Padding(18, 14, 18, 12) };
            header.Paint += (s, e) =>
            {
                using (var pen = new Pen(DS.Border))
                    e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };

            Label title = new Label
            {
                Text = "ServoERP Copilot",
                Location = new Point(18, 14),
                Size = new Size(240, 26),
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                ForeColor = DS.Slate900
            };
            Label subtitle = new Label
            {
                Text = "Local HVAC ERP assistant",
                Location = new Point(18, 42),
                Size = new Size(240, 20),
                Font = DS.Small,
                ForeColor = DS.Slate500
            };

            Button clear = DS.GhostBtn("Clear", 72, 30);
            clear.Location = new Point(254, 16);
            clear.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            clear.Click += (s, e) => ClearChat();

            Button copy = DS.GhostBtn("Copy", 72, 30);
            copy.Location = new Point(334, 16);
            copy.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            copy.Click += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(_lastResponse))
                    Clipboard.SetText(_lastResponse);
            };

            _cmbMode = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(18, 82),
                Size = new Size(190, 25),
                Font = DS.Small
            };
            _cmbMode.Items.AddRange(new object[]
            {
                "General ERP Help",
                "Client Assistant",
                "Quotation Assistant",
                "Invoice Assistant",
                "Job/Technician Assistant",
                "Inventory Assistant",
                "Vendor Assistant",
                "Analytics Explainer"
            });
            _cmbMode.SelectedIndex = 0;
            _cmbMode.SelectedIndexChanged += (s, e) => RefreshSuggestedPrompts();

            _lblContext = new Label
            {
                Location = new Point(220, 83),
                Size = new Size(190, 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = DS.Small,
                ForeColor = DS.Primary700,
                TextAlign = ContentAlignment.MiddleRight
            };

            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            header.Controls.Add(clear);
            header.Controls.Add(copy);
            header.Controls.Add(_cmbMode);
            header.Controls.Add(_lblContext);
            header.Resize += (s, e) =>
            {
                copy.Left = Math.Max(236, header.ClientSize.Width - copy.Width - 18);
                clear.Left = copy.Left - clear.Width - 8;
                _lblContext.Left = _cmbMode.Right + 12;
                _lblContext.Width = Math.Max(120, header.ClientSize.Width - _lblContext.Left - 18);
            };

            _chat = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(16),
                BackColor = DS.BgPage
            };
            _chat.Resize += (s, e) => ReflowChatBubbles();

            Panel chips = BuildQuickChips();

            Panel input = new Panel { Dock = DockStyle.Bottom, Height = 122, BackColor = DS.White, Padding = new Padding(16, 10, 16, 12) };
            input.Paint += (s, e) =>
            {
                using (var pen = new Pen(DS.Border))
                    e.Graphics.DrawLine(pen, 0, 0, input.Width, 0);
            };

            _lblStatus = new Label { Dock = DockStyle.Top, Height = 20, Font = DS.Caption, ForeColor = DS.Slate500 };
            _txtPrompt = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 48,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = DS.Body,
                BorderStyle = BorderStyle.FixedSingle
            };
            _txtPrompt.KeyDown += async (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    await SendAsync(null);
                }
            };

            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = DS.White };
            _btnSend = DS.PrimaryBtn("Send", 82, 32);
            _btnSend.Click += async (s, e) => await SendAsync(null);
            _btnCancel = DS.GhostBtn("Cancel", 82, 32);
            _btnCancel.Enabled = false;
            _btnCancel.Click += (s, e) => CancelActiveRequest();
            _btnRegenerate = DS.GhostBtn("Regenerate", 104, 32);
            _btnRegenerate.Click += async (s, e) => await SendAsync(_lastPrompt);
            actions.Controls.Add(_btnSend);
            actions.Controls.Add(_btnCancel);
            actions.Controls.Add(_btnRegenerate);

            input.Controls.Add(_txtPrompt);
            input.Controls.Add(_lblStatus);
            input.Controls.Add(actions);

            Controls.Add(_chat);
            Controls.Add(chips);
            Controls.Add(input);
            Controls.Add(header);

            RefreshContextLabel();
            AddAssistantBubble("Local AI is read-only in this first version. Ask about clients, invoices, quotations, delayed jobs, vendors, stock, or payments. For write actions I will show a preview only.");
            AddSuggestedPromptCard();
        }

        private Panel BuildQuickChips()
        {
            Panel wrap = new Panel { Dock = DockStyle.Top, Height = 158, BackColor = DS.BgPage, Padding = new Padding(14, 10, 14, 8) };
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, AutoScroll = false, BackColor = DS.BgPage };
            string[] chips =
            {
                "Summarize this client",
                "Draft quotation",
                "Explain invoice",
                "Find delayed jobs",
                "Suggest vendor",
                "Create payment reminder",
                "Summarize site history",
                "Explain dashboard numbers"
            };
            foreach (string chip in chips)
            {
                Button b = DS.GhostBtn(chip, Math.Min(188, Math.Max(128, chip.Length * 7 + 24)), 28);
                b.Font = DS.SmallBold;
                b.Margin = new Padding(0, 0, 8, 8);
                b.Click += async (s, e) => await SendAsync(ExpandQuickActionPrompt(chip));
                flow.Controls.Add(b);
            }
            wrap.Controls.Add(flow);
            return wrap;
        }

        private string ExpandQuickActionPrompt(string chip)
        {
            switch (chip)
            {
                case "Summarize this client":
                    return "Summarize the selected client using ServoERP context. Include sites, recent jobs, invoices, payments, risks, and next actions. If no client is selected, tell me what to select.";
                case "Draft quotation":
                    return "Draft a customer quotation suggestion for the selected client, site, or job. Keep it preview-only and list assumptions, scope, exclusions, materials, labor, taxes, and approval steps.";
                case "Explain invoice":
                    return "Explain the selected invoice in simple business terms. Cover customer, job, status, subtotal, taxes, payments, balance, due date, and what needs attention.";
                case "Find delayed jobs":
                    return "Find delayed or at-risk jobs from the available ServoERP context. Explain why each job needs attention and suggest the next follow-up.";
                case "Suggest vendor":
                    return "Suggest a vendor for the selected material or purchase requirement using available ServoERP vendor and inventory context. Do not create a purchase order.";
                case "Create payment reminder":
                    return "Create a polite payment reminder draft only for overdue receivables with a positive balance. Do not send, update, or mark anything.";
                case "Summarize site history":
                    return "Summarize the selected site history using available jobs, maintenance visits, quotations, invoices, assets, and open issues.";
                case "Explain dashboard numbers":
                    return "Explain today's dashboard numbers and list the top 3 operational or financial issues to check first.";
                default:
                    return chip;
            }
        }

        private void AddSuggestedPromptCard()
        {
            RemoveSuggestedPromptCard();
            PromptSuggestion[] prompts = GetSuggestedPrompts();

            Panel card = new Panel
            {
                Width = Math.Max(300, _chat.ClientSize.Width - 46),
                Height = 82 + (prompts.Length * 38),
                BackColor = DS.White,
                Padding = new Padding(12),
                Margin = new Padding(0, 0, 0, 10)
            };
            DS.Rounded(card, 8);

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = DS.White,
                Padding = new Padding(0)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            Label title = new Label
            {
                Text = "Suggested prompts",
                Dock = DockStyle.Fill,
                Font = DS.BodyBold,
                ForeColor = DS.Slate900,
                TextAlign = ContentAlignment.MiddleLeft
            };

            Label hint = new Label
            {
                Text = "Click one to ask with the current page context.",
                Dock = DockStyle.Fill,
                Font = DS.Caption,
                ForeColor = DS.Slate500,
                TextAlign = ContentAlignment.MiddleLeft
            };

            FlowLayoutPanel list = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = DS.White
            };

            foreach (PromptSuggestion prompt in prompts)
            {
                Button promptButton = DS.GhostBtn(prompt.Label, Math.Max(260, card.Width - 36), 32);
                promptButton.TextAlign = ContentAlignment.MiddleLeft;
                promptButton.Font = DS.Small;
                promptButton.Margin = new Padding(0, 0, 0, 6);
                promptButton.Tag = prompt.Prompt;
                promptButton.Click += async (s, e) =>
                {
                    Button clicked = s as Button;
                    string selectedPrompt = clicked == null ? prompt.Prompt : Convert.ToString(clicked.Tag);
                    await SendAsync(selectedPrompt);
                };
                list.Controls.Add(promptButton);
            }

            layout.Controls.Add(title, 0, 0);
            layout.Controls.Add(hint, 0, 1);
            layout.Controls.Add(list, 0, 2);
            card.Controls.Add(layout);
            _chat.Controls.Add(card);
            _suggestionCard = card;
            ScrollToBottom();
        }

        private void RefreshSuggestedPrompts()
        {
            if (_chat == null || _chat.IsDisposed)
                return;

            RefreshContextLabel();
            AddSuggestedPromptCard();
        }

        private void RemoveSuggestedPromptCard()
        {
            if (_suggestionCard == null)
                return;

            if (_chat != null && !_chat.IsDisposed && _chat.Controls.Contains(_suggestionCard))
                _chat.Controls.Remove(_suggestionCard);

            _suggestionCard.Dispose();
            _suggestionCard = null;
        }

        private PromptSuggestion[] GetSuggestedPrompts()
        {
            string mode = _cmbMode.SelectedItem == null ? "General ERP Help" : _cmbMode.SelectedItem.ToString();
            string module = ResolveCurrentModule();

            if (mode == "Client Assistant")
            {
                return new[]
                {
                    Prompt("Client summary", "Summarize the selected client, sites, open jobs, unpaid invoices, and next follow-up."),
                    Prompt("Client risks", "What risks or delays should I know about for this client?"),
                    Prompt("Client update draft", "Draft a short client update message from the current ServoERP context.")
                };
            }

            if (mode == "Quotation Assistant")
            {
                return new[]
                {
                    Prompt("Customer quotation draft", "Draft a customer quotation suggestion for the selected job or site."),
                    Prompt("Quotation readiness check", "Explain which quotation items look incomplete before I send it."),
                    Prompt("Vendor RFQ draft", "Create a procurement quotation request for vendors, preview only.")
                };
            }

            if (mode == "Invoice Assistant")
            {
                return new[]
                {
                    Prompt("Explain selected invoice", "Explain the selected invoice and what still needs to be paid."),
                    Prompt("Overdue receivables", "Summarize overdue receivables with positive balances only."),
                    Prompt("Payment reminder draft", "Draft a payment reminder without sending or updating anything.")
                };
            }

            if (mode == "Job/Technician Assistant")
            {
                return new[]
                {
                    Prompt("Delayed jobs", "Find delayed jobs and suggest the next follow-up for each."),
                    Prompt("Technician workload", "Summarize technician workload and urgent jobs from available context."),
                    Prompt("Job blockers", "Explain what is blocking the selected job from completion.")
                };
            }

            if (mode == "Inventory Assistant")
            {
                return new[]
                {
                    Prompt("Low stock risks", "List low-stock or reorder-risk inventory items from available context."),
                    Prompt("Material plan", "Suggest material planning for the selected job, preview only."),
                    Prompt("Parts affecting delivery", "Explain which parts may affect job delivery timelines.")
                };
            }

            if (mode == "Vendor Assistant")
            {
                return new[]
                {
                    Prompt("Suggest vendor", "Suggest vendors for the selected purchase requirement, without creating a PO."),
                    Prompt("Compare vendors", "Compare vendor options using available price, lead time, and reliability context."),
                    Prompt("Vendor quotation request", "Draft a vendor quotation request for materials, preview only.")
                };
            }

            if (mode == "Analytics Explainer")
            {
                return new[]
                {
                    Prompt("Dashboard priorities", "Explain today's dashboard numbers and the top 3 issues to check."),
                    Prompt("Business risk summary", "Summarize revenue, receivables, delayed jobs, and inventory risks."),
                    Prompt("Recent changes", "Tell me what changed recently in " + module + " and why it matters.")
                };
            }

            return new[]
            {
                Prompt("Dashboard priorities", "Explain today's dashboard numbers and list the top 3 issues to check first."),
                Prompt("Client or site summary", "Summarize the selected client or site. If nothing is selected, tell me what to select."),
                Prompt("Delayed job follow-up", "Find delayed jobs and explain the next follow-up for each."),
                Prompt("Quotation draft", "Draft a customer quotation suggestion, preview only."),
                Prompt("Payment reminder draft", "Draft a payment reminder only for overdue positive-balance receivables."),
                Prompt("Vendor suggestion", "Suggest a vendor for the selected material without creating a purchase order.")
            };
        }

        private static PromptSuggestion Prompt(string label, string prompt)
        {
            return new PromptSuggestion(label, prompt);
        }

        private async Task CheckLocalAiAsync()
        {
            RefreshContextLabel();
            SetBusy(true, "Checking local AI...");
            try
            {
                bool ok = await _assistant.IsLocalAiReachableAsync(CancellationToken.None);
                _lblStatus.Text = ok
                    ? "Local AI connected."
                    : "Local AI is not running. Please install/start Ollama and pull a model like llama3.1 or qwen2.5.";
                _lblStatus.ForeColor = ok ? DS.Teal600 : DS.Amber600;
            }
            finally
            {
                SetBusy(false, _lblStatus.Text);
            }
        }

        private async Task SendAsync(string overridePrompt)
        {
            string prompt = string.IsNullOrWhiteSpace(overridePrompt) ? _txtPrompt.Text.Trim() : overridePrompt.Trim();
            if (string.IsNullOrWhiteSpace(prompt))
                return;

            CancelActiveRequest();
            _requestCts = new CancellationTokenSource();
            _lastPrompt = prompt;
            _txtPrompt.Clear();
            RefreshContextLabel();
            AddUserBubble(prompt);
            SetBusy(true, "Thinking locally...");

            try
            {
                var request = new AiAssistantRequest
                {
                    UserMessage = prompt,
                    QuickAction = overridePrompt,
                    Mode = _cmbMode.SelectedItem == null ? "General ERP Help" : _cmbMode.SelectedItem.ToString(),
                    CurrentModule = ResolveCurrentModule()
                };

                AiAssistantResponse response = await _assistant.AskAsync(request, _requestCts.Token);
                _lastResponse = response.Answer;
                if (response.IsError)
                    AddAssistantBubble(response.Answer, true);
                else
                    AddAssistantBubble(response.Answer);

                if (response.SuggestedActions != null)
                {
                    foreach (AiSuggestedAction action in response.SuggestedActions)
                        AddConfirmationCard(action);
                }
            }
            finally
            {
                SetBusy(false, "Ready.");
                _requestCts?.Dispose();
                _requestCts = null;
            }
        }

        private void AddUserBubble(string text)
        {
            AddBubble(text, true, false);
        }

        private void AddAssistantBubble(string text, bool error = false)
        {
            AddBubble(text, false, error);
        }

        private void AddBubble(string text, bool user, bool error)
        {
            Panel bubble = new Panel
            {
                Width = Math.Max(300, _chat.ClientSize.Width - 46),
                AutoSize = true,
                BackColor = user ? DS.Primary600 : (error ? DS.Red50 : DS.White),
                Padding = new Padding(12),
                Margin = new Padding(0, 0, 0, 10)
            };
            DS.Rounded(bubble, 8);

            Label label = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(Math.Max(260, bubble.Width - 26), 0),
                Font = DS.Body,
                ForeColor = user ? Color.White : (error ? DS.Red600 : DS.Slate800),
                Text = text ?? string.Empty
            };
            bubble.Controls.Add(label);
            _chat.Controls.Add(bubble);
            ScrollToBottom();
        }

        private void AddConfirmationCard(AiSuggestedAction action)
        {
            if (action == null)
                return;

            Panel card = new Panel
            {
                Width = Math.Max(300, _chat.ClientSize.Width - 46),
                Height = action.IsWriteAction ? 104 : 86,
                BackColor = DS.Amber50,
                Padding = new Padding(12),
                Margin = new Padding(0, 0, 0, 10)
            };
            DS.Rounded(card, 8);
            card.Controls.Add(new Label { Text = action.Title ?? "Suggested action", Location = new Point(12, 10), Size = new Size(card.Width - 24, 20), Font = DS.BodyBold, ForeColor = DS.Slate900 });
            card.Controls.Add(new Label { Text = action.Description ?? "Preview only.", Location = new Point(12, 34), Size = new Size(card.Width - 24, 34), Font = DS.Small, ForeColor = DS.Slate700 });
            if (action.IsWriteAction)
            {
                Button preview = DS.GhostBtn("Preview only", 110, 28);
                preview.Location = new Point(12, 70);
                preview.Click += (s, e) => MessageBox.Show(this, "This AI module does not write ERP records yet. Use the existing " + (action.TargetModule ?? "module") + " workflow after reviewing the suggestion.", "ServoERP Copilot", MessageBoxButtons.OK, MessageBoxIcon.Information);
                card.Controls.Add(preview);
            }
            _chat.Controls.Add(card);
            ScrollToBottom();
        }

        private void ReflowChatBubbles()
        {
            foreach (Control control in _chat.Controls)
            {
                control.Width = Math.Max(300, _chat.ClientSize.Width - 46);
                foreach (Control child in control.Controls)
                {
                    Label label = child as Label;
                    if (label != null)
                        label.MaximumSize = new Size(Math.Max(260, control.Width - 26), 0);

                    FlowLayoutPanel flow = child as FlowLayoutPanel;
                    if (flow != null)
                    {
                        flow.Width = Math.Max(260, control.Width - 24);
                        foreach (Control prompt in flow.Controls)
                            prompt.Width = Math.Max(260, flow.Width - 4);
                    }
                }
            }
        }

        private void ScrollToBottom()
        {
            if (_chat.Controls.Count == 0)
                return;
            _chat.ScrollControlIntoView(_chat.Controls[_chat.Controls.Count - 1]);
        }

        private void ClearChat()
        {
            _chat.Controls.Clear();
            _suggestionCard = null;
            _lastPrompt = null;
            _lastResponse = null;
            AddAssistantBubble("Chat cleared. Local AI remains read-only and uses limited ServoERP context.");
            AddSuggestedPromptCard();
        }

        private void SetBusy(bool busy, string status)
        {
            _btnSend.Enabled = !busy;
            _btnCancel.Enabled = busy;
            _btnRegenerate.Enabled = !busy && !string.IsNullOrWhiteSpace(_lastPrompt);
            _lblStatus.Text = status;
            _lblStatus.ForeColor = busy ? DS.Primary700 : DS.Slate500;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private void CancelActiveRequest()
        {
            try
            {
                if (_requestCts != null && !_requestCts.IsCancellationRequested)
                    _requestCts.Cancel();
            }
            catch
            {
            }
        }

        private void RefreshContextLabel()
        {
            _lblContext.Text = "Context: " + ResolveCurrentModule();
        }

        private string ResolveCurrentModule()
        {
            try
            {
                string module = _moduleResolver == null ? null : _moduleResolver();
                return string.IsNullOrWhiteSpace(module) ? "Dashboard" : module;
            }
            catch
            {
                return "Dashboard";
            }
        }
    }
}

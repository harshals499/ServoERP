using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class WhatsAppQuickActionDialog : ServoERP.Infrastructure.ServoFormBase
    {
        private readonly WhatsAppHubService _service = new WhatsAppHubService();
        private readonly WhatsAppQuickActionContext _context;
        private readonly TextBox _message;
        private readonly Label _status;
        private bool _opened;

        public WhatsAppQuickActionDialog(WhatsAppQuickActionContext context)
        {
            _context = context ?? new WhatsAppQuickActionContext();
            Text = BrandingService.WindowTitle("WhatsApp Quick Action");
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = true;
            MinimizeBox = false;
            Size = new Size(620, 430);
            MinimumSize = new Size(560, 420);
            BackColor = DS.BgPage;
            Font = DS.Body;

            Label title = new Label
            {
                Text = "WhatsApp message",
                Location = new Point(22, 18),
                Size = new Size(360, 28),
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = DS.Slate900,
                AutoEllipsis = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(title);

            Label meta = new Label
            {
                Text = BuildMetaText(),
                Location = new Point(24, 50),
                Size = new Size(560, 38),
                Font = DS.Small,
                ForeColor = DS.Slate600,
                AutoEllipsis = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(meta);

            _message = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(24, 102),
                Size = new Size(554, 188),
                Text = _context.Message ?? string.Empty,
                Font = new Font("Segoe UI", 9.5f),
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(_message);

            Label notice = new Label
            {
                Text = "ServoERP opens WhatsApp with prefilled text only. Review and send manually in WhatsApp. No chats are scraped or synced.",
                Location = new Point(24, 300),
                Size = new Size(554, 34),
                Font = DS.Small,
                ForeColor = DS.Slate700,
                AutoEllipsis = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(notice);

            _status = new Label
            {
                Text = "Status: Prepared",
                Location = new Point(24, 342),
                Size = new Size(260, 24),
                Font = DS.SmallBold,
                ForeColor = DS.Teal600,
                AutoEllipsis = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(_status);

            Button copy = MakeButton("Copy message", DS.White, DS.Slate700, 118, true);
            Button open = MakeButton("Open WhatsApp Chat", DS.Primary600, Color.White, 154, false);
            Button sent = MakeButton("Mark as Sent", DS.Teal600, Color.White, 118, false);
            Button close = MakeButton("Close", DS.White, DS.Slate700, 90, true);

            copy.Location = new Point(24, 372);
            open.Location = new Point(152, 372);
            sent.Location = new Point(316, 372);
            close.Location = new Point(488, 372);

            copy.Click += (s, e) => CopyMessage();
            open.Click += (s, e) => OpenWhatsApp();
            sent.Click += (s, e) => MarkSent();
            close.Click += (s, e) => Close();

            Controls.Add(copy);
            Controls.Add(open);
            Controls.Add(sent);
            Controls.Add(close);
            Resize += (s, e) => LayoutBottomControls(notice, copy, open, sent, close);
            LayoutBottomControls(notice, copy, open, sent, close);

            Log("Prepared");
        }

        private void LayoutBottomControls(Label notice, Button copy, Button open, Button sent, Button close)
        {
            int margin = 24;
            int bottom = ClientSize.Height - 24;
            int buttonTop = bottom - close.Height;
            int statusTop = buttonTop - 30;
            int noticeTop = statusTop - 42;

            _message.Height = Math.Max(120, noticeTop - _message.Top - 10);
            notice.SetBounds(margin, noticeTop, Math.Max(120, ClientSize.Width - (margin * 2)), 34);
            _status.SetBounds(margin, statusTop, Math.Max(120, ClientSize.Width - 300), 24);

            close.Location = new Point(ClientSize.Width - margin - close.Width, buttonTop);
            sent.Location = new Point(close.Left - 10 - sent.Width, buttonTop);
            open.Location = new Point(sent.Left - 10 - open.Width, buttonTop);
            copy.Location = new Point(margin, buttonTop);
        }

        public static void ShowFor(IWin32Window owner, WhatsAppQuickActionContext context)
        {
            using (var dialog = new WhatsAppQuickActionDialog(context))
                dialog.ShowDialog(owner);
        }

        private string BuildMetaText()
        {
            string phone = string.IsNullOrWhiteSpace(_context.Phone) ? "No phone saved" : _context.Phone;
            string record = string.IsNullOrWhiteSpace(_context.LinkedRecord) ? _context.Module : _context.LinkedRecord;
            return (_context.ContactName ?? "Contact") + " | " + phone + " | " + (_context.TemplateType ?? "General update") + " | " + record;
        }

        private void CopyMessage()
        {
            if (UIHelper.TrySetClipboardText(this, _message.Text ?? string.Empty, BrandingService.WindowTitle("WhatsApp")))
            {
                _status.Text = "Status: Prepared, copied";
                Log("Prepared");
            }
        }

        private void OpenWhatsApp()
        {
            _context.Message = _message.Text ?? string.Empty;
            string url = _service.BuildWhatsAppUrl(_context.Phone, _context.Message);
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            _opened = true;
            _status.Text = "Status: Opened";
            Log("Opened");
        }

        private void MarkSent()
        {
            if (!_opened)
            {
                DialogResult result = MessageBox.Show(this, "WhatsApp has not been opened from this dialog yet. Mark as sent anyway?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result != DialogResult.Yes)
                    return;
            }

            _context.Message = _message.Text ?? string.Empty;
            _status.Text = "Status: User Confirmed Sent";
            Log("User Confirmed Sent");
            MessageBox.Show(this, "WhatsApp activity marked as sent.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void Log(string status)
        {
            _context.Message = _message.Text ?? _context.Message ?? string.Empty;
            _service.LogAction(_service.BuildLog(_context, status));
        }

        private static Button MakeButton(string text, Color back, Color fore, int width, bool outline)
        {
            Button button = new Button
            {
                Text = text,
                Width = width,
                Height = 34,
                BackColor = back,
                ForeColor = fore,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = outline ? 1 : 0;
            button.FlatAppearance.BorderColor = DS.BorderStrong;
            return button;
        }
    }
}


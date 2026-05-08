using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class NotificationCenterDialog : Form
    {
        private readonly ListBox _list;
        private readonly Action<string> _navigate;
        private readonly NotificationCenterService _service = new NotificationCenterService();
        private List<FoundationNotification> _notifications = new List<FoundationNotification>();

        public NotificationCenterDialog(Action<string> navigate)
        {
            _navigate = navigate;
            Text = BrandingService.WindowTitle("Notifications");
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(680, 500);
            MinimumSize = new Size(540, 360);
            BackColor = DS.BgPage;

            var header = new Label
            {
                Text = "Operational exceptions needing attention",
                Dock = DockStyle.Top,
                Height = 38,
                Padding = new Padding(12, 10, 12, 0),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = DS.Slate900
            };

            _list = new ListBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5f), IntegralHeight = false };
            _list.DoubleClick += (s, e) => OpenSelected();

            var footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(10, 8, 10, 8),
                BackColor = DS.BgPage
            };
            footer.Controls.Add(MakeButton("Open", (s, e) => OpenSelected()));
            footer.Controls.Add(MakeButton("Dismiss", (s, e) => DismissSelected()));
            footer.Controls.Add(MakeButton("Refresh", (s, e) => LoadNotifications()));

            Controls.Add(_list);
            Controls.Add(footer);
            Controls.Add(header);
            Load += (s, e) => LoadNotifications();
        }

        private void LoadNotifications()
        {
            try
            {
                _list.Items.Clear();
                _notifications = _service.GetActiveNotifications(80);
                if (_notifications.Count == 0)
                {
                    _list.Items.Add("No active operational notifications.");
                    return;
                }

                foreach (FoundationNotification n in _notifications)
                    _list.Items.Add(n.Severity + " | " + n.Module + " | " + n.Title + " | " + n.Detail);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("NotificationCenterDialog.LoadNotifications", ex);
                _list.Items.Add("Notifications could not be loaded: " + ex.Message);
            }
        }

        private void OpenSelected()
        {
            int index = _list.SelectedIndex;
            if (index < 0 || index >= _notifications.Count)
                return;

            FoundationNotification selected = _notifications[index];
            Close();
            _navigate?.Invoke(selected.PageKey);
        }

        private void DismissSelected()
        {
            int index = _list.SelectedIndex;
            if (index < 0 || index >= _notifications.Count)
                return;

            _service.Dismiss(_notifications[index]);
            LoadNotifications();
        }

        private static Button MakeButton(string text, EventHandler onClick)
        {
            var button = new Button
            {
                Text = text,
                Width = 92,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = DS.Slate900
            };
            button.Click += onClick;
            return button;
        }
    }
}

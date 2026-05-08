using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    public class ClientCardControl : Panel
    {
        private bool _selected;
        private readonly Label _avatar;
        private readonly Label _name;
        private readonly Label _type;
        private readonly Label _city;
        private readonly Label _status;
        private readonly Panel _progress;
        private readonly Panel _progressTrack;

        public int ClientId { get; private set; }

        public bool IsSelected
        {
            get { return _selected; }
            set
            {
                _selected = value;
                BackColor = value ? DS.Primary50 : Color.White;
                Invalidate();
            }
        }

        public ClientCardControl()
        {
            Height = 92;
            Width = 278;
            Margin = new Padding(0, 0, 0, 10);
            Padding = new Padding(12);
            BackColor = Color.White;
            Cursor = Cursors.Hand;
            DoubleBuffered = true;

            _avatar = new Label
            {
                Location = new Point(14, 16),
                Size = new Size(42, 42),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = DS.Teal600
            };
            DS.Rounded(_avatar, 21);

            _name = new Label { Location = new Point(68, 12), Size = new Size(178, 20), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = DS.Slate900 };
            _type = new Label { Location = new Point(68, 32), Size = new Size(178, 18), Font = new Font("Segoe UI", 8f), ForeColor = DS.Slate700 };
            _city = new Label { Location = new Point(68, 49), Size = new Size(178, 16), Font = new Font("Segoe UI", 8f), ForeColor = DS.Slate600 };
            _status = new Label { Location = new Point(80, 67), Size = new Size(88, 16), Font = new Font("Segoe UI", 7.5f), ForeColor = DS.Green600 };
            _progressTrack = new Panel { Location = new Point(204, 72), Size = new Size(52, 5), BackColor = DS.Slate200 };
            _progress = new Panel { Location = new Point(0, 0), Size = new Size(28, 5), BackColor = DS.Teal600 };
            DS.Rounded(_progressTrack, 3);
            DS.Rounded(_progress, 3);
            _progressTrack.Controls.Add(_progress);

            Controls.Add(_avatar);
            Controls.Add(_name);
            Controls.Add(_type);
            Controls.Add(_city);
            Controls.Add(_status);
            Controls.Add(_progressTrack);

            foreach (Control child in Controls)
            {
                child.Cursor = Cursors.Hand;
                child.Click += (s, e) => OnClick(e);
            }
        }

        public void Bind(int clientId, string initials, string name, string type, string city, bool active, int progress)
        {
            ClientId = clientId;
            _avatar.Text = initials;
            _name.Text = name;
            _type.Text = type;
            _city.Text = city;
            _status.Text = active ? "●  Active" : "●  Inactive";
            _status.ForeColor = active ? DS.Green600 : DS.Slate500;
            _avatar.BackColor = active ? DS.Teal600 : DS.Slate400;
            _progress.Width = Math.Max(8, Math.Min(_progressTrack.Width, progress));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color border = IsSelected ? DS.Primary600 : DS.Border;
            using (GraphicsPath path = DS.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 8))
            using (Pen pen = new Pen(border, IsSelected ? 2 : 1))
                e.Graphics.DrawPath(pen, path);
        }
    }

    public class ClientHeaderControl : Panel
    {
        private readonly Label _avatar;
        private readonly Label _name;
        private readonly Label _meta;
        private readonly Label _badge;
        private readonly TableLayoutPanel _actions;

        public event EventHandler LogActivityClicked;
        public event EventHandler AddJobClicked;
        public event EventHandler CreateInvoiceClicked;
        public event EventHandler EditProfileClicked;
        public event EventHandler MoreClicked;

        public ClientHeaderControl()
        {
            Height = 94;
            Dock = DockStyle.Top;
            BackColor = Color.White;
            Padding = new Padding(14);
            DoubleBuffered = true;

            _avatar = new Label { Location = new Point(18, 18), Size = new Size(54, 54), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 14f, FontStyle.Bold), ForeColor = Color.White, BackColor = DS.Teal600 };
            DS.Rounded(_avatar, 27);
            _name = new Label { Location = new Point(86, 20), Size = new Size(400, 24), Font = new Font("Segoe UI", 14f, FontStyle.Bold), ForeColor = DS.Slate900 };
            _meta = new Label { Location = new Point(88, 48), Size = new Size(430, 18), Font = new Font("Segoe UI", 8.5f), ForeColor = DS.Slate600 };
            _badge = new Label { Location = new Point(88, 67), Size = new Size(60, 20), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), ForeColor = Color.FromArgb(21, 128, 61), BackColor = DS.Green50 };
            DS.Rounded(_badge, 10);

            _actions = new TableLayoutPanel { Dock = DockStyle.Right, Width = 540, Height = 54, Padding = new Padding(0, 6, 0, 0), ColumnCount = 5, RowCount = 1, BackColor = Color.Transparent };
            for (int i = 0; i < 5; i++)
                _actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            _actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            AddAction("Log Activity", "□", (s, e) => LogActivityClicked?.Invoke(this, EventArgs.Empty));
            AddAction("Add Job", "+", (s, e) => AddJobClicked?.Invoke(this, EventArgs.Empty));
            AddAction("Create Invoice", "▤", (s, e) => CreateInvoiceClicked?.Invoke(this, EventArgs.Empty));
            AddAction("Edit Profile", "✎", (s, e) => EditProfileClicked?.Invoke(this, EventArgs.Empty));
            AddAction("More", "…", (s, e) => MoreClicked?.Invoke(this, EventArgs.Empty));

            Controls.Add(_actions);
            Controls.Add(_avatar);
            Controls.Add(_name);
            Controls.Add(_meta);
            Controls.Add(_badge);
            Resize += (s, e) => LayoutActionBar();
            LayoutActionBar();
        }

        public void Bind(string initials, string name, string category, string city, bool active)
        {
            _avatar.Text = initials;
            _name.Text = name;
            _meta.Text = category + " - " + city;
            _badge.Text = active ? "Active" : "Inactive";
            _badge.BackColor = active ? DS.Green50 : DS.Slate100;
            _badge.ForeColor = active ? DS.Green600 : DS.Slate600;
        }

        private void AddAction(string text, string icon, EventHandler handler)
        {
            var button = new Button
            {
                Text = icon + "\r\n" + text,
                Dock = DockStyle.Fill,
                Margin = new Padding(3, 0, 3, 0),
                BackColor = Color.White,
                ForeColor = DS.Slate800,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderColor = DS.Border;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseOverBackColor = DS.Primary50;
            button.Text = GetActionText(text);
            button.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Padding = new Padding(4, 0, 4, 0);
            button.Click += handler;
            DS.Rounded(button, 7);
            _actions.Controls.Add(button);
        }

        private void LayoutActionBar()
        {
            int available = Math.Max(300, Width - 500);
            bool compact = available < 460;
            _actions.Width = compact ? Math.Max(300, available) : 540;
            _actions.Height = 54;
            _actions.Top = 20;
            _name.Width = Math.Max(200, Width - _actions.Width - 116);
            _meta.Width = _name.Width;

            string[] full = { "Log Activity", "+ Add Job", "Create Invoice", "Profile", "... More" };
            string[] small = { "Log", "+ Job", "Invoice", "Profile", "More" };
            for (int i = 0; i < _actions.Controls.Count && i < full.Length; i++)
            {
                Button button = _actions.Controls[i] as Button;
                if (button == null) continue;
                button.Text = compact ? small[i] : full[i];
                button.Font = new Font("Segoe UI", compact ? 8f : 8.25f, FontStyle.Bold);
                button.Padding = new Padding(2, 0, 2, 0);
            }
        }

        private static string GetActionText(string text)
        {
            switch (text)
            {
                case "Add Job": return "+ Add Job";
                case "Create Invoice": return "Create Invoice";
                case "Edit Profile": return "Profile";
                case "More": return "... More";
                default: return text;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = DS.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 8))
            using (Pen pen = new Pen(DS.Border))
                e.Graphics.DrawPath(pen, path);
        }
    }

    public class LifecyclePipelineControl : Panel
    {
        private readonly FlowLayoutPanel _flow;
        private string _stage = "Prospect";
        public event EventHandler<string> StageClicked;

        public LifecyclePipelineControl()
        {
            Height = 112;
            Dock = DockStyle.Top;
            BackColor = Color.White;
            DoubleBuffered = true;
            Controls.Add(new Label { Text = "Lifecycle Pipeline", Location = new Point(18, 16), Size = new Size(240, 20), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = DS.Slate900 });
            _flow = new FlowLayoutPanel { Location = new Point(18, 46), Height = 46, Width = 860, WrapContents = false, BackColor = Color.Transparent };
            Controls.Add(_flow);
            Resize += (s, e) => _flow.Width = Math.Max(420, Width - 34);
        }

        public void Bind(string stage)
        {
            _stage = string.IsNullOrWhiteSpace(stage) ? "Prospect" : stage;
            _flow.Controls.Clear();
            string[] stages = { "Prospect", "Qualified", "Active AMC", "Renewal Due", "Inactive" };
            for (int i = 0; i < stages.Length; i++)
            {
                string item = stages[i];
                Button btn = new Button
                {
                    Text = item,
                    Tag = item,
                    Width = item.Length > 10 ? 142 : 118,
                    Height = 38,
                    Margin = new Padding(0, 0, 8, 0),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 8.5f, string.Equals(item, _stage, StringComparison.OrdinalIgnoreCase) ? FontStyle.Bold : FontStyle.Regular),
                    Cursor = Cursors.Hand
                };
                ApplyStageStyle(btn, item);
                btn.Click += (s, e) => StageClicked?.Invoke(this, (string)((Button)s).Tag);
                _flow.Controls.Add(btn);
                if (i < stages.Length - 1)
                    _flow.Controls.Add(new Label { Text = "→", Width = 18, Height = 38, TextAlign = ContentAlignment.MiddleCenter, ForeColor = DS.Slate400, Margin = new Padding(0, 0, 8, 0) });
            }
        }

        private void ApplyStageStyle(Button button, string stage)
        {
            bool active = string.Equals(stage, _stage, StringComparison.OrdinalIgnoreCase);
            if (active)
            {
                button.BackColor = DS.Primary600;
                button.ForeColor = Color.White;
                button.FlatAppearance.BorderColor = DS.Primary600;
            }
            else if (stage == "Active AMC")
            {
                button.BackColor = DS.Green50;
                button.ForeColor = DS.Green600;
                button.FlatAppearance.BorderColor = Color.FromArgb(187, 247, 208);
            }
            else if (stage == "Renewal Due")
            {
                button.BackColor = DS.Amber50;
                button.ForeColor = DS.Amber600;
                button.FlatAppearance.BorderColor = Color.FromArgb(253, 230, 138);
            }
            else
            {
                button.BackColor = DS.Slate50;
                button.ForeColor = DS.Slate700;
                button.FlatAppearance.BorderColor = DS.Border;
            }
            DS.Rounded(button, 7);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = DS.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 8))
            using (Pen pen = new Pen(DS.Border))
                e.Graphics.DrawPath(pen, path);
        }
    }

    public class KpiCardControl : Panel
    {
        private readonly Label _label;
        private readonly Label _value;
        private readonly LinkLabel _link;
        private readonly Label _icon;
        public event EventHandler ActionClicked;

        public KpiCardControl()
        {
            Height = 104;
            BackColor = Color.White;
            DoubleBuffered = true;
            _label = new Label { Location = new Point(16, 14), Size = new Size(132, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = DS.Slate900 };
            _value = new Label { Location = new Point(16, 38), Size = new Size(150, 30), Font = new Font("Segoe UI", 18f, FontStyle.Bold), ForeColor = DS.Slate900 };
            _link = new LinkLabel { Location = new Point(16, 76), Size = new Size(128, 18), Font = new Font("Segoe UI", 8.5f), LinkColor = DS.Primary600, ActiveLinkColor = DS.Primary700 };
            _icon = new Label { Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(150, 18), Size = new Size(28, 28), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 15f), ForeColor = DS.Slate500 };
            _link.Click += (s, e) => ActionClicked?.Invoke(this, EventArgs.Empty);
            Controls.Add(_label);
            Controls.Add(_value);
            Controls.Add(_link);
            Controls.Add(_icon);
            Resize += (s, e) => _icon.Left = Width - 44;
        }

        public void Bind(string label, string value, string link, string icon, Color valueColor)
        {
            _label.Text = label;
            _value.Text = value;
            _link.Text = link + " →";
            _icon.Text = icon;
            _value.ForeColor = valueColor;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = DS.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 8))
            using (Pen pen = new Pen(DS.Border))
                e.Graphics.DrawPath(pen, path);
        }
    }

    public class ActivityTimelineControl : Panel
    {
        private readonly FlowLayoutPanel _items;

        public ActivityTimelineControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            _items = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(18, 14, 14, 14), BackColor = Color.White };
            Controls.Add(_items);
        }

        public void Bind(IEnumerable<ActivityTimelineItem> items)
        {
            _items.Controls.Clear();
            foreach (ActivityTimelineItem item in items)
                _items.Controls.Add(CreateRow(item));
        }

        private Control CreateRow(ActivityTimelineItem item)
        {
            Panel row = new Panel { Width = 360, Height = 92, Margin = new Padding(0, 0, 0, 8), BackColor = Color.White };
            Label icon = new Label { Location = new Point(2, 8), Size = new Size(28, 28), Text = item.Icon, TextAlign = ContentAlignment.MiddleCenter, BackColor = item.IconBackColor, ForeColor = Color.White, Font = new Font("Segoe UI", 8f, FontStyle.Bold) };
            DS.Rounded(icon, 14);
            row.Controls.Add(icon);
            row.Controls.Add(new Label { Text = item.Title, Location = new Point(42, 4), Size = new Size(300, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = DS.Slate900 });
            row.Controls.Add(new Label { Text = item.When, Location = new Point(42, 22), Size = new Size(300, 16), Font = new Font("Segoe UI", 7.5f), ForeColor = DS.Slate500 });
            row.Controls.Add(new Label { Text = item.Description, Location = new Point(42, 40), Size = new Size(300, 32), Font = new Font("Segoe UI", 8f), ForeColor = DS.Slate700 });
            LinkLabel link = new LinkLabel { Text = item.ActionText, Location = new Point(42, 72), Size = new Size(160, 18), Font = new Font("Segoe UI", 8f), LinkColor = DS.Primary600 };
            link.Click += (s, e) => item.Action?.Invoke();
            row.Controls.Add(link);
            return row;
        }
    }

    public class ClientSummaryTableControl : Panel
    {
        private readonly DataGridView _grid;
        private readonly Label _total;

        public ClientSummaryTableControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Padding = new Padding(18, 24, 18, 18);
            _grid = new DataGridView
            {
                Dock = DockStyle.Top,
                Height = 218,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = DS.Border,
                EnableHeadersVisualStyles = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Stage", Width = 110 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Details", Width = 180 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Expected Value", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            GridTheme.Apply(_grid);
            _total = new Label { Dock = DockStyle.Top, Height = 34, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = DS.Primary600 };
            Controls.Add(_total);
            Controls.Add(_grid);
        }

        public void Bind(IEnumerable<ClientSummaryRow> rows, string total)
        {
            _grid.Rows.Clear();
            foreach (ClientSummaryRow row in rows)
                _grid.Rows.Add(row.Stage, row.Details, row.ExpectedValue);
            _total.Text = "Total Expected Value     " + total;
        }
    }

    public sealed class ActivityTimelineItem
    {
        public string Icon { get; set; }
        public Color IconBackColor { get; set; }
        public string When { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string ActionText { get; set; }
        public Action Action { get; set; }
    }

    public sealed class ClientSummaryRow
    {
        public string Stage { get; set; }
        public string Details { get; set; }
        public string ExpectedValue { get; set; }
    }
}

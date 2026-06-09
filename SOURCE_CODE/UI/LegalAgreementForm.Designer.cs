using System.Drawing;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    public partial class LegalAgreementForm
    {
        private Panel _headerPanel;
        private Panel _contentPanel;
        private Panel _noticePanel;
        private Label _lblTitle;
        private Label _lblSubtitle;
        private Label _lblBadge;
        private Label _lblNoticeTitle;
        private Label _lblNoticeText;
        private TabControl _tabs;
        private TabPage _tabEula;
        private TabPage _tabPrivacy;
        private TabPage _tabDataProcessing;
        private TabPage _tabDisclaimer;
        private RichTextBox _rtbEula;
        private RichTextBox _rtbPrivacy;
        private RichTextBox _rtbDataProcessing;
        private RichTextBox _rtbDisclaimer;
        private Panel _bottomPanel;
        private CheckBox _chkAccept;
        private Button _btnAccept;
        private Button _btnDecline;
        private Button _btnClose;

        /// <summary>Initializes legal agreement form controls.</summary>
        private void InitializeComponent()
        {
            _headerPanel = new Panel();
            _contentPanel = new Panel();
            _noticePanel = new Panel();
            _lblTitle = new Label();
            _lblSubtitle = new Label();
            _lblBadge = new Label();
            _lblNoticeTitle = new Label();
            _lblNoticeText = new Label();
            _tabs = new TabControl();
            _tabEula = new TabPage();
            _tabPrivacy = new TabPage();
            _tabDataProcessing = new TabPage();
            _tabDisclaimer = new TabPage();
            _rtbEula = CreateLegalTextBox();
            _rtbPrivacy = CreateLegalTextBox();
            _rtbDataProcessing = CreateLegalTextBox();
            _rtbDisclaimer = CreateLegalTextBox();
            _bottomPanel = new Panel();
            _chkAccept = new CheckBox();
            _btnAccept = new Button();
            _btnDecline = new Button();
            _btnClose = new Button();
            SuspendLayout();

            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = DS.Slate50;
            ClientSize = new Size(960, 700);
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "ServoERP - Legal Agreements";

            _headerPanel.BackColor = DS.Primary700;
            _headerPanel.Dock = DockStyle.Top;
            _headerPanel.Height = 118;
            _headerPanel.Paint += OnHeaderPanelPaint;

            _lblTitle.AutoSize = false;
            _lblTitle.Font = new Font("Segoe UI", 18f, FontStyle.Bold);
            _lblTitle.ForeColor = Color.White;
            _lblTitle.Location = new Point(30, 22);
            _lblTitle.Size = new Size(590, 34);
            _lblTitle.Text = "ServoERP Legal Agreements";

            _lblSubtitle.AutoSize = false;
            _lblSubtitle.Font = new Font("Segoe UI", 10f);
            _lblSubtitle.ForeColor = DS.Primary100;
            _lblSubtitle.Location = new Point(32, 60);
            _lblSubtitle.Size = new Size(690, 42);
            _lblSubtitle.Text = "Review the EULA, Privacy Policy, Data Processing Policy, and Disclaimer before using ServoERP for your business operations.";

            _lblBadge.AutoSize = false;
            _lblBadge.BackColor = Color.White;
            _lblBadge.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            _lblBadge.ForeColor = DS.Teal600;
            _lblBadge.Location = new Point(702, 30);
            _lblBadge.Size = new Size(220, 30);
            _lblBadge.Text = "Made in India | Local-first ERP";
            _lblBadge.TextAlign = ContentAlignment.MiddleCenter;

            _contentPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _contentPanel.BackColor = Color.White;
            _contentPanel.Location = new Point(24, 138);
            _contentPanel.Size = new Size(912, 452);
            _contentPanel.Paint += OnSurfacePaint;

            _noticePanel.BackColor = DS.Slate50;
            _noticePanel.Location = new Point(20, 18);
            _noticePanel.Size = new Size(872, 58);
            _noticePanel.Paint += OnSurfacePaint;

            _lblNoticeTitle.AutoSize = false;
            _lblNoticeTitle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            _lblNoticeTitle.ForeColor = DS.Slate900;
            _lblNoticeTitle.Location = new Point(18, 8);
            _lblNoticeTitle.Size = new Size(280, 20);
            _lblNoticeTitle.Text = "Review before continuing";

            _lblNoticeText.AutoSize = false;
            _lblNoticeText.Font = new Font("Segoe UI", 9f);
            _lblNoticeText.ForeColor = DS.Slate600;
            _lblNoticeText.Location = new Point(18, 30);
            _lblNoticeText.Size = new Size(824, 20);
            _lblNoticeText.Text = "ServoERP stores business data locally and asks for one-time acceptance of these India-aligned legal terms.";

            _tabs.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            _tabs.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _tabs.ItemSize = new Size(170, 34);
            _tabs.Location = new Point(20, 92);
            _tabs.Size = new Size(872, 340);
            _tabs.SizeMode = TabSizeMode.Fixed;
            _tabs.DrawItem += OnTabsDrawItem;

            ConfigureTab(_tabEula, "EULA", _rtbEula);
            ConfigureTab(_tabPrivacy, "Privacy Policy", _rtbPrivacy);
            ConfigureTab(_tabDataProcessing, "Data Processing Policy", _rtbDataProcessing);
            ConfigureTab(_tabDisclaimer, "Disclaimer", _rtbDisclaimer);
            _tabs.TabPages.AddRange(new[] { _tabEula, _tabPrivacy, _tabDataProcessing, _tabDisclaimer });

            _bottomPanel.Dock = DockStyle.Bottom;
            _bottomPanel.Height = 88;
            _bottomPanel.BackColor = Color.White;
            _bottomPanel.Paint += OnBottomPanelPaint;

            _chkAccept.AutoSize = false;
            _chkAccept.Font = new Font("Segoe UI", 9.5f);
            _chkAccept.ForeColor = DS.Slate800;
            _chkAccept.Location = new Point(28, 22);
            _chkAccept.Size = new Size(585, 42);
            _chkAccept.Text = "I have read and agree to all the above terms and conditions";
            _chkAccept.CheckedChanged += OnAcceptCheckedChanged;

            _btnAccept.BackColor = DS.Slate300;
            _btnAccept.Enabled = false;
            _btnAccept.FlatStyle = FlatStyle.Flat;
            _btnAccept.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _btnAccept.ForeColor = Color.White;
            _btnAccept.Location = new Point(710, 26);
            _btnAccept.Size = new Size(96, 38);
            _btnAccept.Text = "Accept";
            _btnAccept.UseVisualStyleBackColor = false;
            _btnAccept.FlatAppearance.BorderSize = 0;
            _btnAccept.Click += OnAcceptClick;

            _btnDecline.BackColor = Color.White;
            _btnDecline.FlatStyle = FlatStyle.Flat;
            _btnDecline.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _btnDecline.ForeColor = DS.Slate700;
            _btnDecline.Location = new Point(820, 26);
            _btnDecline.Size = new Size(104, 38);
            _btnDecline.Text = "Decline";
            _btnDecline.UseVisualStyleBackColor = false;
            _btnDecline.FlatAppearance.BorderColor = DS.Border;
            _btnDecline.Click += OnDeclineClick;

            _btnClose.BackColor = Color.White;
            _btnClose.FlatStyle = FlatStyle.Flat;
            _btnClose.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _btnClose.ForeColor = DS.Slate700;
            _btnClose.Location = new Point(820, 26);
            _btnClose.Size = new Size(104, 38);
            _btnClose.Text = "Close";
            _btnClose.UseVisualStyleBackColor = false;
            _btnClose.Visible = false;
            _btnClose.FlatAppearance.BorderColor = DS.Border;
            _btnClose.Click += OnCloseClick;

            _headerPanel.Controls.Add(_lblBadge);
            _headerPanel.Controls.Add(_lblSubtitle);
            _headerPanel.Controls.Add(_lblTitle);
            _noticePanel.Controls.Add(_lblNoticeTitle);
            _noticePanel.Controls.Add(_lblNoticeText);
            _contentPanel.Controls.Add(_noticePanel);
            _contentPanel.Controls.Add(_tabs);
            _bottomPanel.Controls.Add(_chkAccept);
            _bottomPanel.Controls.Add(_btnAccept);
            _bottomPanel.Controls.Add(_btnDecline);
            _bottomPanel.Controls.Add(_btnClose);

            Controls.Add(_contentPanel);
            Controls.Add(_headerPanel);
            Controls.Add(_bottomPanel);
            ResumeLayout(false);
        }

        /// <summary>Creates a read-only legal text viewer.</summary>
        private static RichTextBox CreateLegalTextBox()
        {
            return new RichTextBox
            {
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                DetectUrls = true,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10f),
                ForeColor = DS.Slate800,
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = true
            };
        }

        /// <summary>Configures a legal document tab and attaches its viewer.</summary>
        private static void ConfigureTab(TabPage tab, string title, RichTextBox textBox)
        {
            tab.BackColor = Color.White;
            tab.Padding = new Padding(18);
            tab.Text = title;
            tab.Controls.Add(textBox);
        }

        /// <summary>Draws the legal agreement tab headers with ServoERP styling.</summary>
        private void OnTabsDrawItem(object sender, DrawItemEventArgs e)
        {
            bool selected = e.Index == _tabs.SelectedIndex;
            Rectangle bounds = e.Bounds;
            Color back = selected ? Color.White : DS.Slate100;
            Color fore = selected ? DS.Primary700 : DS.Slate600;

            using (SolidBrush brush = new SolidBrush(back))
                e.Graphics.FillRectangle(brush, bounds);

            using (Font tabFont = new Font("Segoe UI", 8.8f, FontStyle.Bold))
            {
                TextRenderer.DrawText(
                    e.Graphics,
                    _tabs.TabPages[e.Index].Text,
                    tabFont,
                    bounds,
                    fore,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            if (selected)
            {
                using (Pen pen = new Pen(DS.Primary600, 3))
                    e.Graphics.DrawLine(pen, bounds.Left + 14, bounds.Bottom - 3, bounds.Right - 14, bounds.Bottom - 3);
            }
        }

        /// <summary>Draws the header accent line.</summary>
        private void OnHeaderPanelPaint(object sender, PaintEventArgs e)
        {
            using (Pen pen = new Pen(DS.Teal500, 4))
                e.Graphics.DrawLine(pen, 0, _headerPanel.Height - 3, _headerPanel.Width, _headerPanel.Height - 3);
        }

        /// <summary>Draws a light border around white and notice surfaces.</summary>
        private static void OnSurfacePaint(object sender, PaintEventArgs e)
        {
            Control control = sender as Control;
            if (control == null)
                return;

            using (Pen pen = new Pen(DS.Slate200))
                e.Graphics.DrawRectangle(pen, 0, 0, control.Width - 1, control.Height - 1);
        }

        /// <summary>Draws the action bar separator.</summary>
        private void OnBottomPanelPaint(object sender, PaintEventArgs e)
        {
            using (Pen pen = new Pen(DS.Slate200))
                e.Graphics.DrawLine(pen, 0, 0, _bottomPanel.Width, 0);
        }
    }
}

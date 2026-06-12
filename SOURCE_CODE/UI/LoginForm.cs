using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.UI.Controls;
using HVAC_Pro_Desktop.UI.Helpers;

namespace HVAC_Pro_Desktop.UI
{
    public class LoginForm : Form
    {
        private const int TitleBarHeight = 54;
        private readonly AuthService _authService = new AuthService();
        private readonly RememberedLoginService _rememberedLogin = new RememberedLoginService();
        private readonly Timer _spinnerTimer = new Timer();

        private Panel _titleBar;
        private Panel _leftPanel;
        private Panel _rightPanel;
        private Panel _loginCard;
        private Panel _footer;
        private ModernTextBox _txtUsername;
        private ModernTextBox _txtPassword;
        private Button _btnPasswordToggle;
        private Button _btnMaximizeToggle;
        private CheckBox _chkRememberMe;
        private Button _btnSignIn;
        private Label _lblError;
        private Label _lblSpinner;
        private bool _signInHover;
        private bool _signInPressed;
        private bool _passwordVisible;
        private float _spinnerPhase;
        private Bitmap _brandPanelCache;
        private Size _brandPanelCacheSize;
        private bool _paintingBrandCache;
        private Size _lastWindowRegionSize;
        private FormWindowState _lastWindowRegionState;

        private enum WindowButtonKind
        {
            Minimize,
            Maximize,
            Close
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED: reduce whole-form flicker in VM/RDP environments.
                return cp;
            }
        }

        public LoginForm()
        {
            Text = BrandingService.WindowTitle("Login");
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1180, 760);
            MinimumSize = new Size(940, 660);
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.FromArgb(245, 249, 255);
            Font = new Font("Segoe UI", 9f);
            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;
            DoubleBuffered = true;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            SuspendLayout();
            BuildLayout();
            LayoutScaler.ApplyGlobalScale(this);
            ResumeLayout(true);
            WireEvents();
            LoadSavedLoginState();

            _spinnerTimer.Interval = 120;
            _spinnerTimer.Tick += (s, e) =>
            {
                _spinnerPhase += 0.25f;
                if (_lblSpinner.Visible)
                    _lblSpinner.Text = SpinnerFrame();
            };
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            FitToWorkingArea();
            ApplyRoundedWindow();
            await TryAutoLoginAsync();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyRoundedWindow();
            LayoutTitleBarControls();
            LayoutMainAreas();
            UpdateWindowControlState();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _spinnerTimer.Stop();
            _spinnerTimer.Dispose();
            DisposeBrandPanelCache();
            base.OnFormClosed(e);
        }

        private void BuildLayout()
        {
            _titleBar = new BufferedPanel { Dock = DockStyle.Top, Height = TitleBarHeight, BackColor = BackColor };
            Controls.Add(_titleBar);
            BuildTitleBar();

            _footer = new BufferedPanel { Dock = DockStyle.Bottom, Height = 62, BackColor = Color.FromArgb(248, 251, 255) };
            _footer.Paint += DrawFooter;
            Controls.Add(_footer);

            _leftPanel = new BufferedPanel { BackColor = Color.FromArgb(239, 246, 255) };
            _leftPanel.Paint += DrawBrandPanelLite;
            Controls.Add(_leftPanel);

            _rightPanel = new BufferedPanel { BackColor = BackColor };
            Controls.Add(_rightPanel);

            BuildLoginCardV2();
            BuildFooterLabelsV2();
            LayoutMainAreas();
        }

        private void BuildTitleBar()
        {
            _titleBar.MouseDown += DragWindow;

            _titleBar.Controls.Add(new Label
            {
                Text = "ServoERP",
                Location = new Point(24, 13),
                Size = new Size(180, 28),
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42)
            });

            AddWindowButton(WindowButtonKind.Close, 0, Close, "Close window");
            AddWindowButton("□", 1, ToggleMaximize);
            AddWindowButton("\u2212", 2, () => WindowState = FormWindowState.Minimized, "Minimize window", false);
        }

        private void LayoutTitleBarControls()
        {
            if (_titleBar == null)
                return;

            foreach (Control control in _titleBar.Controls)
            {
                var button = control as Button;
                if (button == null || !(button.Tag is WindowButtonKind))
                    continue;

                WindowButtonKind kind = (WindowButtonKind)button.Tag;
                int slot = kind == WindowButtonKind.Close ? 0 : kind == WindowButtonKind.Maximize ? 1 : 2;
                button.Size = new Size(38, 34);
                button.Location = new Point(Math.Max(0, _titleBar.Width - 52 - (slot * 46)), 10);
            }
        }

        private Button AddWindowButton(string text, int slot, Action action, string accessibleName, bool closeButton)
        {
            WindowButtonKind kind = closeButton ? WindowButtonKind.Close : (slot == 2 ? WindowButtonKind.Minimize : WindowButtonKind.Maximize);
            return AddWindowButton(kind, slot, action, accessibleName);
        }

        private Button AddWindowButton(WindowButtonKind kind, int slot, Action action, string accessibleName)
        {
            var button = new Button
            {
                Text = string.Empty,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(ClientSize.Width - 52 - (slot * 46), 10),
                Size = new Size(38, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(15, 23, 42),
                AccessibleName = accessibleName,
                AccessibleDescription = accessibleName,
                TabStop = false,
                Tag = kind
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.Transparent;
            button.FlatAppearance.MouseDownBackColor = Color.Transparent;
            button.Paint += DrawWindowControlButton;
            button.MouseEnter += (s, e) => button.Invalidate();
            button.MouseLeave += (s, e) => button.Invalidate();
            button.MouseDown += (s, e) => button.Invalidate();
            button.MouseUp += (s, e) => button.Invalidate();
            button.Click += (s, e) => action();
            _titleBar.Controls.Add(button);
            return button;
        }

        private void AddWindowButton(string text, int slot, Action action)
        {
            if (slot == 1)
            {
                _btnMaximizeToggle = AddWindowButton(WindowButtonKind.Maximize, slot, action, "Maximize window");
                return;
            }

            AddWindowButton(slot == 2 ? WindowButtonKind.Minimize : WindowButtonKind.Close, slot, action, slot == 2 ? "Minimize window" : "Close window");
        }

        private void BuildLoginCardV2()
        {
            _loginCard = new BufferedPanel { BackColor = Color.White };
            _loginCard.Paint += DrawLoginCard;
            _rightPanel.Controls.Add(_loginCard);

            _loginCard.Controls.Add(new Label
            {
                Text = "Sign In",
                Location = new Point(56, 36),
                Size = new Size(440, 56),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 25f, FontStyle.Bold),
                ForeColor = Color.FromArgb(8, 22, 61)
            });

            _loginCard.Controls.Add(new Label
            {
                Text = "Use your ServoERP account to continue.",
                Location = new Point(56, 92),
                Size = new Size(440, 30),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 10.5f),
                ForeColor = Color.FromArgb(71, 85, 105)
            });

            _loginCard.Controls.Add(FieldLabel("Username / Email", 56, 136));
            _txtUsername = new ModernTextBox
            {
                Location = new Point(56, 164),
                Size = new Size(440, 52),
                Placeholder = "Enter your username or email",
                LeadingIcon = '\uE77B'
            };
            _loginCard.Controls.Add(_txtUsername);

            _loginCard.Controls.Add(FieldLabel("Password", 56, 232));
            _txtPassword = new ModernTextBox
            {
                Location = new Point(56, 260),
                Size = new Size(440, 52),
                Placeholder = "Enter your password",
                UseSystemPasswordChar = true,
                LeadingIcon = '\uE72E'
            };
            _txtPassword.Padding = new Padding(52, 0, 78, 0);
            _loginCard.Controls.Add(_txtPassword);

            _btnPasswordToggle = GhostButton(string.Empty, 424, 270, 58, 32);
            ConfigurePasswordToggleButton(_btnPasswordToggle);
            _btnPasswordToggle.Click += (s, e) => TogglePasswordVisibility();
            _loginCard.Controls.Add(_btnPasswordToggle);
            _btnPasswordToggle.BringToFront();

            _chkRememberMe = new CheckBox
            {
                Text = "Remember me",
                Location = new Point(56, 332),
                Size = new Size(160, 28),
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(8, 22, 61)
            };
            _loginCard.Controls.Add(_chkRememberMe);

            var forgot = LinkLabel("Forgot password?", 346, 328, 150, 30);
            forgot.Click += (s, e) => ShowForgotPasswordDialog();
            _loginCard.Controls.Add(forgot);

            _lblError = new Label
            {
                Location = new Point(56, 368),
                Size = new Size(440, 28),
                ForeColor = Color.FromArgb(220, 38, 38),
                Font = new Font("Segoe UI", 9.5f),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _loginCard.Controls.Add(_lblError);

            _btnSignIn = new Button
            {
                Text = "Sign In",
                Location = new Point(56, 404),
                Size = new Size(440, 54),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 91, 224),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold)
            };
            _btnSignIn.FlatAppearance.BorderSize = 0;
            _btnSignIn.FlatAppearance.MouseOverBackColor = Color.FromArgb(29, 78, 216);
            _btnSignIn.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 64, 175);
            _btnSignIn.Click += async (s, e) => await AttemptLoginAsync();
            _loginCard.Controls.Add(_btnSignIn);
            StylePrimaryButton(_btnSignIn);

            _lblSpinner = new Label
            {
                Text = "",
                Location = new Point(72, 417),
                Size = new Size(34, 28),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0, 91, 224),
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                Visible = false
            };
            _loginCard.Controls.Add(_lblSpinner);

            AddDivider(56, 464, 440);

            var demoButton = SocialButton("Continue as Demo", 56, 488);
            demoButton.Size = new Size(210, 46);
            demoButton.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            demoButton.Click += (s, e) => ShowToast("Demo access can be enabled from administrator settings.", false);
            _loginCard.Controls.Add(demoButton);

            var createButton = SocialButton("Create Account", 286, 488);
            createButton.Size = new Size(210, 46);
            createButton.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            createButton.ForeColor = Color.FromArgb(0, 92, 255);
            createButton.FlatAppearance.BorderColor = Color.FromArgb(0, 92, 255);
            createButton.Click += (s, e) => ShowCreateAccountDialog();
            _loginCard.Controls.Add(createButton);
        }

        private void BuildLoginCardLegacy()
        {
            _loginCard = new BufferedPanel { BackColor = Color.White };
            _loginCard.Paint += DrawLoginCard;
            _rightPanel.Controls.Add(_loginCard);

            var language = new Button
            {
                Text = "◎  English",
                Location = new Point(518, 28),
                Size = new Size(140, 42),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(8, 22, 61),
                Font = new Font("Segoe UI", 10f)
            };
            language.FlatAppearance.BorderColor = DS.Border;
            language.FlatAppearance.MouseOverBackColor = Color.FromArgb(239, 246, 255);
            _loginCard.Controls.Add(language);

            _loginCard.Controls.Add(new Label
            {
                Text = "Welcome Back!",
                Location = new Point(0, 94),
                Size = new Size(690, 48),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 24f, FontStyle.Bold),
                ForeColor = Color.FromArgb(8, 22, 61)
            });
            _loginCard.Controls.Add(new Panel { Location = new Point(315, 151), Size = new Size(60, 2), BackColor = Color.FromArgb(249, 115, 22) });
            _loginCard.Controls.Add(new Label
            {
                Text = "Sign in to your ServoERP account",
                Location = new Point(0, 172),
                Size = new Size(690, 34),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 13f),
                ForeColor = Color.FromArgb(30, 41, 82)
            });

            _loginCard.Controls.Add(FieldLabel("Username / Email", 58, 232));
            _txtUsername = new ModernTextBox
            {
                Location = new Point(58, 262),
                Size = new Size(574, 56),
                Placeholder = "Enter your username or email",
                LeadingIcon = '\uE77B'
            };
            _loginCard.Controls.Add(_txtUsername);

            _loginCard.Controls.Add(FieldLabel("Password", 58, 350));
            _txtPassword = new ModernTextBox
            {
                Location = new Point(58, 380),
                Size = new Size(574, 56),
                Placeholder = "Enter your password",
                UseSystemPasswordChar = true,
                LeadingIcon = '\uE72E'
            };
            _loginCard.Controls.Add(_txtPassword);

            _btnPasswordToggle = GhostButton("○", 590, 392, 34, 32);
            _btnPasswordToggle.Click += (s, e) => TogglePasswordVisibility();
            _loginCard.Controls.Add(_btnPasswordToggle);

            _chkRememberMe = new CheckBox
            {
                Text = "Remember me",
                Location = new Point(58, 462),
                Size = new Size(160, 28),
                Font = new Font("Segoe UI", 10.5f),
                ForeColor = Color.FromArgb(8, 22, 61)
            };
            _loginCard.Controls.Add(_chkRememberMe);

            var forgot = LinkLabel("Forgot Password?", 482, 458, 150, 30);
            forgot.Click += (s, e) => ShowForgotPasswordDialog();
            _loginCard.Controls.Add(forgot);

            _lblError = new Label
            {
                Location = new Point(58, 486),
                Size = new Size(574, 24),
                ForeColor = Color.FromArgb(220, 38, 38),
                Font = new Font("Segoe UI", 9.5f),
                TextAlign = ContentAlignment.MiddleCenter
            };
            _loginCard.Controls.Add(_lblError);

            _btnSignIn = new Button
            {
                Text = "Sign In  ->",
                Location = new Point(58, 492),
                Size = new Size(574, 56),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 91, 224),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 14f, FontStyle.Bold)
            };
            _btnSignIn.FlatAppearance.BorderSize = 0;
            _btnSignIn.FlatAppearance.MouseOverBackColor = Color.FromArgb(29, 78, 216);
            _btnSignIn.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 64, 175);
            _btnSignIn.Click += async (s, e) => await AttemptLoginAsync();
            _loginCard.Controls.Add(_btnSignIn);

            _lblSpinner = new Label
            {
                Text = "",
                Location = new Point(74, 507),
                Size = new Size(34, 28),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0, 91, 224),
                Font = new Font("Segoe UI Symbol", 14f),
                Visible = false
            };
            _loginCard.Controls.Add(_lblSpinner);

            AddDivider(58, 574, 250);
            _loginCard.Controls.Add(new Label { Text = "OR", Location = new Point(316, 560), Size = new Size(58, 30), TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(82, 103, 143), Font = new Font("Segoe UI", 9.5f) });
            AddDivider(382, 574, 250);

            var createButton = SocialButton("Create New Account", 58, 596);
            createButton.Size = new Size(574, 50);
            createButton.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            createButton.ForeColor = Color.FromArgb(0, 92, 255);
            createButton.FlatAppearance.BorderColor = Color.FromArgb(0, 92, 255);
            createButton.Click += (s, e) => ShowCreateAccountDialog();
            _loginCard.Controls.Add(createButton);

            _loginCard.Controls.Add(new Label
            {
                Text = "New to ServoERP?",
                Location = new Point(230, 650),
                Size = new Size(126, 28),
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.FromArgb(71, 85, 125),
                Font = new Font("Segoe UI", 10f)
            });
            var createAccount = LinkLabel("Create an account", 360, 650, 132, 28);
            createAccount.TextAlign = ContentAlignment.MiddleCenter;
            createAccount.Click += (s, e) => ShowCreateAccountDialog();
            _loginCard.Controls.Add(createAccount);

            BuildTrustStrip();
        }

        private void BuildTrustStrip()
        {
            string[] titles = { "Secure Connection", "Your Data is Safe", "Trusted Support" };
            string[] subtitles = { "SSL Encrypted", "Protected & Secure", "We're Here to Help" };
            string[] icons = { "\uE72E", "\uE83D", "\uE95B" };
            int[] x = { 58, 280, 486 };
            for (int i = 0; i < titles.Length; i++)
            {
                _loginCard.Controls.Add(new Label
                {
                    Text = icons[i],
                    Location = new Point(x[i], 676),
                    Size = new Size(38, 38),
                    Font = new Font("Segoe MDL2 Assets", 20f),
                    ForeColor = Color.FromArgb(0, 65, 170),
                    TextAlign = ContentAlignment.MiddleCenter
                });
                _loginCard.Controls.Add(new Label
                {
                    Text = titles[i],
                    Location = new Point(x[i] + 46, 674),
                    Size = new Size(128, 20),
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(8, 22, 61)
                });
                _loginCard.Controls.Add(new Label
                {
                    Text = subtitles[i],
                    Location = new Point(x[i] + 46, 694),
                    Size = new Size(136, 20),
                    Font = new Font("Segoe UI", 8.5f),
                    ForeColor = Color.FromArgb(30, 41, 82)
                });
                if (i > 0)
                    _loginCard.Controls.Add(new Panel { Location = new Point(x[i] - 16, 674), Size = new Size(1, 42), BackColor = Color.FromArgb(226, 232, 240) });
            }
        }

        private void BuildLoginCard()
        {
            _loginCard = new BufferedPanel { BackColor = Color.White };
            _loginCard.Paint += DrawLoginCard;
            _rightPanel.Controls.Add(_loginCard);

            var language = new Button
            {
                Text = "◎  English  ˅",
                Location = new Point(520, 38),
                Size = new Size(158, 44),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(30, 64, 175),
                Font = new Font("Segoe UI", 10f)
            };
            language.FlatAppearance.BorderColor = DS.Border;
            language.FlatAppearance.MouseOverBackColor = Color.FromArgb(239, 246, 255);
            _loginCard.Controls.Add(language);

            _loginCard.Controls.Add(new Label
            {
                Text = "Welcome Back!",
                Location = new Point(0, 130),
                Size = new Size(720, 48),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 25f, FontStyle.Bold),
                ForeColor = Color.FromArgb(8, 22, 61)
            });
            _loginCard.Controls.Add(new Label
            {
                Text = "Sign in to your ServoERP account",
                Location = new Point(0, 178),
                Size = new Size(720, 34),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 13f),
                ForeColor = Color.FromArgb(71, 85, 125)
            });

            _loginCard.Controls.Add(FieldLabel("Username / Email", 78, 258));
            _txtUsername = new ModernTextBox
            {
                Location = new Point(78, 294),
                Size = new Size(564, 58),
                Placeholder = "Enter your username or email",
                LeadingIcon = '♙'
            };
            _loginCard.Controls.Add(_txtUsername);

            _loginCard.Controls.Add(FieldLabel("Password", 78, 394));
            _txtPassword = new ModernTextBox
            {
                Location = new Point(78, 430),
                Size = new Size(564, 58),
                Placeholder = "Enter your password",
                UseSystemPasswordChar = true,
                LeadingIcon = '▣'
            };
            _loginCard.Controls.Add(_txtPassword);

            _btnPasswordToggle = GhostButton("◉", 604, 443, 34, 32);
            _btnPasswordToggle.Click += (s, e) => TogglePasswordVisibility();
            _loginCard.Controls.Add(_btnPasswordToggle);

            _chkRememberMe = new CheckBox
            {
                Text = "Remember me",
                Location = new Point(78, 522),
                Size = new Size(160, 28),
                Font = new Font("Segoe UI", 10.5f),
                ForeColor = Color.FromArgb(8, 22, 61)
            };
            _loginCard.Controls.Add(_chkRememberMe);

            var forgot = LinkLabel("Forgot Password?", 500, 518, 150, 30);
            forgot.Click += (s, e) => ShowForgotPasswordDialog();
            _loginCard.Controls.Add(forgot);

            _btnSignIn = new Button
            {
                Text = "→  Sign In",
                Location = new Point(78, 574),
                Size = new Size(564, 68),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 15f, FontStyle.Bold)
            };
            _btnSignIn.FlatAppearance.BorderSize = 0;
            _btnSignIn.FlatAppearance.MouseOverBackColor = Color.FromArgb(29, 78, 216);
            _btnSignIn.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 64, 175);
            _btnSignIn.Click += async (s, e) => await AttemptLoginAsync();
            _loginCard.Controls.Add(_btnSignIn);

            _lblSpinner = new Label
            {
                Text = "",
                Location = new Point(92, 595),
                Size = new Size(34, 28),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(37, 99, 235),
                Font = new Font("Segoe UI Symbol", 14f),
                Visible = false
            };
            _loginCard.Controls.Add(_lblSpinner);

            AddDivider(78, 686, 224);
            _loginCard.Controls.Add(new Label { Text = "or sign in with", Location = new Point(304, 674), Size = new Size(116, 32), TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(82, 103, 143), Font = new Font("Segoe UI", 10f) });
            AddDivider(420, 686, 222);

            var microsoft = SocialButton("■  Microsoft", 78, 720);
            microsoft.Click += (s, e) => ShowToast("Microsoft sign-in is ready for Azure AD wiring.", false);
            _loginCard.Controls.Add(microsoft);

            var google = SocialButton("G  Google", 360, 720);
            google.Click += (s, e) => ShowToast("Google sign-in is ready for OAuth wiring.", false);
            _loginCard.Controls.Add(google);

            var createAccount = LinkLabel("Create account", 236, 804, 112, 30);
            createAccount.TextAlign = ContentAlignment.MiddleCenter;
            createAccount.Click += (s, e) => ShowCreateAccountDialog();
            _loginCard.Controls.Add(createAccount);

            var contact = LinkLabel("Need access? Ask Administrator", 356, 804, 210, 30);
            contact.TextAlign = ContentAlignment.MiddleCenter;
            contact.Click += (s, e) => ShowToast("An Admin can approve and create local ServoERP users.", false);
            _loginCard.Controls.Add(contact);

            _lblError = new Label
            {
                Location = new Point(78, 646),
                Size = new Size(564, 28),
                ForeColor = Color.FromArgb(220, 38, 38),
                Font = new Font("Segoe UI", 9.5f),
                TextAlign = ContentAlignment.MiddleCenter
            };
            _loginCard.Controls.Add(_lblError);
        }

        private void BuildFooterLabels()
        {
            _footer.Controls.Add(new Label
            {
                Text = "✓  Secure Connection\r\nSSL Encrypted",
                Location = new Point(44, 19),
                Size = new Size(260, 48),
                ForeColor = Color.FromArgb(15, 72, 145),
                Font = new Font("Segoe UI", 10f)
            });
            _footer.Controls.Add(new Label
            {
                Text = "Version " + Assembly.GetExecutingAssembly().GetName().Version,
                Anchor = AnchorStyles.Top,
                Location = new Point(590, 30),
                Size = new Size(220, 28),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(82, 103, 143),
                Font = new Font("Segoe UI", 10.5f)
            });
            _footer.Controls.Add(new Label
            {
                Text = "☎  Need Help?\r\nSupport Center",
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(ClientSize.Width - 250, 19),
                Size = new Size(210, 48),
                ForeColor = Color.FromArgb(15, 72, 145),
                Font = new Font("Segoe UI", 10f)
            });
        }

        private void BuildFooterLabelsV2()
        {
            _footer.Controls.Add(new Label
            {
                Text = "\uE72E",
                Location = new Point(30, 18),
                Size = new Size(30, 30),
                Font = new Font("Segoe MDL2 Assets", 16f),
                ForeColor = Color.FromArgb(22, 163, 74),
                TextAlign = ContentAlignment.MiddleCenter
            });
            _footer.Controls.Add(new Label
            {
                Text = "Secure encrypted local connection",
                Location = new Point(66, 16),
                Size = new Size(320, 34),
                ForeColor = Color.FromArgb(8, 44, 118),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            });
            _footer.Controls.Add(new Label
            {
                Text = "Version " + Assembly.GetExecutingAssembly().GetName().Version + "  |  Licensed workstation",
                Dock = DockStyle.Right,
                Width = 420,
                Padding = new Padding(0, 0, 36, 0),
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.FromArgb(71, 85, 105),
                Font = new Font("Segoe UI", 9.5f)
            });
        }

        private void BuildFooterLabelsV2Legacy()
        {
            _footer.Controls.Add(new Label
            {
                Text = "\uE83D",
                Location = new Point(28, 22),
                Size = new Size(36, 36),
                Font = new Font("Segoe MDL2 Assets", 20f),
                ForeColor = Color.FromArgb(22, 163, 74),
                TextAlign = ContentAlignment.MiddleCenter
            });
            _footer.Controls.Add(new Label
            {
                Text = "Secure Connection\r\nSSL Encrypted",
                Location = new Point(70, 19),
                Size = new Size(260, 48),
                ForeColor = Color.FromArgb(8, 44, 118),
                Font = new Font("Segoe UI", 10f)
            });
            _footer.Controls.Add(new Label
            {
                Text = "Version " + Assembly.GetExecutingAssembly().GetName().Version + "\r\n© ServoERP. All rights reserved.",
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(ClientSize.Width - 330, 19),
                Size = new Size(290, 48),
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.FromArgb(30, 41, 82),
                Font = new Font("Segoe UI", 10f)
            });
        }

        private void LayoutMainAreas()
        {
            if (_leftPanel == null || _rightPanel == null)
                return;

            LayoutTitleBarControls();
            int availableTop = TitleBarHeight;
            int availableHeight = Math.Max(300, ClientSize.Height - TitleBarHeight - _footer.Height);
            int leftWidth = Math.Max(410, (int)(ClientSize.Width * 0.40));
            _leftPanel.Bounds = new Rectangle(0, availableTop, leftWidth, availableHeight);
            _rightPanel.Bounds = new Rectangle(leftWidth, availableTop, ClientSize.Width - leftWidth, availableHeight);

            int cardWidth = Math.Min(560, Math.Max(520, _rightPanel.Width - 128));
            int cardHeight = Math.Min(620, Math.Max(540, _rightPanel.Height - 28));
            _loginCard.Bounds = new Rectangle((_rightPanel.Width - cardWidth) / 2, (_rightPanel.Height - cardHeight) / 2, cardWidth, cardHeight);
            LayoutPasswordToggle();
        }

        private void LayoutPasswordToggle()
        {
            if (_txtPassword == null || _btnPasswordToggle == null)
                return;

            _btnPasswordToggle.Size = new Size(46, 32);
            _btnPasswordToggle.Location = new Point(_txtPassword.Right - _btnPasswordToggle.Width - 12, _txtPassword.Top + (_txtPassword.Height - _btnPasswordToggle.Height) / 2);
            _btnPasswordToggle.BringToFront();
        }

        private async Task TryAutoLoginAsync()
        {
            RememberedSessionDto session;
            if (!_rememberedLogin.TryLoad(out session))
                return;

            SetBusy(true, "Resuming secure session...");
            LoginResultDto result = await _authService.TryResumeSessionAsync(session);
            SetBusy(false, null);

            if (result.Success)
                ShowMainShell();
            else
                _rememberedLogin.Clear();
        }

        private async Task AttemptLoginAsync()
        {
            string username = (_txtUsername.Text ?? string.Empty).Trim();
            string password = _txtPassword.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ShowToast("Enter username and password.", true);
                return;
            }

            SetBusy(true, "Signing in securely...");
            LoginResultDto result = await _authService.LoginAsync(username, password, _chkRememberMe.Checked);
            SetBusy(false, null);

            if (!result.Success)
            {
                ShowToast(string.IsNullOrWhiteSpace(result.ErrorMessage) ? "Invalid username or password" : result.ErrorMessage, true);
                ShakeForm();
                return;
            }

            if (_chkRememberMe.Checked && result.SessionId.HasValue)
            {
                _rememberedLogin.Save(new RememberedSessionDto
                {
                    SessionId = result.SessionId.Value,
                    Username = result.User.Username,
                    SessionToken = result.SessionToken,
                    RefreshToken = result.RefreshToken,
                    ExpiresAt = result.ExpiresAt ?? DateTime.Now.AddDays(30)
                });
            }
            else
            {
                _rememberedLogin.Clear();
            }

            if (result.RequiresPasswordChange)
            {
                using (var change = new ChangePasswordForm(result.User.UserId, true))
                {
                    if (change.ShowDialog(this) != DialogResult.OK)
                    {
                        _authService.Logout();
                        ShowToast("Password change is required before continuing.", true);
                        return;
                    }
                }
            }

            ShowMainShell();
        }

        private void ShowCreateAccountDialog()
        {
            using (var dialog = new CreateAccountForm())
            {
                if (dialog.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.CreatedUsername))
                {
                    _txtUsername.Text = dialog.CreatedUsername;
                    _txtPassword.Text = string.Empty;
                    _txtPassword.Focus();
                    ShowToast("Account created. Enter the password to sign in.", false);
                }
            }
        }

        private void ShowForgotPasswordDialog()
        {
            using (var dialog = new ChangePasswordForm(_txtUsername?.Text ?? string.Empty))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _txtPassword.Text = string.Empty;
                    _txtPassword.Focus();
                    ShowToast("Password reset. Sign in with your new password.", false);
                }
            }
        }

        private void ShowMainShell()
        {
            if (!LegalAgreementForm.EnsureAccepted(this))
                return;

            Hide();
            try
            {
                using (var main = new MainForm())
                    main.ShowDialog(this);
            }
            finally
            {
                if (!IsDisposed)
                {
                    _txtPassword.Text = string.Empty;
                    _lblError.Text = string.Empty;
                    Close();
                }
            }
        }

        private void LoadSavedLoginState()
        {
            RememberedSessionDto session;
            if (_rememberedLogin.TryLoad(out session))
            {
                _chkRememberMe.Checked = true;
                _txtUsername.Text = session.Username;
            }
        }

        private void SetBusy(bool busy, string message)
        {
            _txtUsername.Enabled = !busy;
            _txtPassword.Enabled = !busy;
            _chkRememberMe.Enabled = !busy;
            _btnSignIn.Enabled = !busy;
            _btnSignIn.Text = busy ? "   Signing In..." : "Sign In";
            if (!busy)
                _btnSignIn.Text = "Sign In";
            _lblSpinner.Visible = busy;
            if (busy && !_spinnerTimer.Enabled)
                _spinnerTimer.Start();
            else if (!busy && _spinnerTimer.Enabled)
                _spinnerTimer.Stop();
            _lblError.ForeColor = busy ? Color.FromArgb(37, 99, 235) : Color.FromArgb(220, 38, 38);
            _lblError.Text = message ?? string.Empty;
        }

        private void ShowToast(string message, bool error)
        {
            _lblError.ForeColor = error ? Color.FromArgb(220, 38, 38) : Color.FromArgb(37, 99, 235);
            _lblError.Text = message ?? string.Empty;
        }

        private void TogglePasswordVisibility()
        {
            _passwordVisible = !_passwordVisible;
            _txtPassword.UseSystemPasswordChar = !_passwordVisible;
            if (_btnPasswordToggle != null)
            {
                _btnPasswordToggle.AccessibleName = _passwordVisible ? "Hide password" : "Show password";
                _btnPasswordToggle.AccessibleDescription = _btnPasswordToggle.AccessibleName;
                _btnPasswordToggle.Invalidate();
            }
        }

        private void ConfigurePasswordToggleButton(Button button)
        {
            if (button == null)
                return;

            button.Text = string.Empty;
            button.TabStop = false;
            button.AccessibleName = "Show password";
            button.AccessibleDescription = "Show password";
            button.MouseEnter += (s, e) => button.Invalidate();
            button.MouseLeave += (s, e) => button.Invalidate();
            button.Paint += (s, e) => DrawPasswordVisibilityIcon(e.Graphics, button.ClientRectangle, !_passwordVisible, button.ClientRectangle.Contains(button.PointToClient(Cursor.Position)));
        }

        private void DrawPasswordVisibilityIcon(Graphics graphics, Rectangle bounds, bool hidden, bool hot)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color color = hot ? Color.FromArgb(30, 58, 138) : Color.FromArgb(100, 116, 139);
            Rectangle eye = new Rectangle(bounds.Left + 13, bounds.Top + 7, bounds.Width - 26, bounds.Height - 14);

            using (Pen pen = new Pen(color, 1.6f))
            using (SolidBrush brush = new SolidBrush(color))
            {
                Point left = new Point(eye.Left, eye.Top + eye.Height / 2);
                Point right = new Point(eye.Right, eye.Top + eye.Height / 2);
                Point top = new Point(eye.Left + eye.Width / 2, eye.Top);
                graphics.DrawBezier(pen, left, new Point(eye.Left + 5, eye.Top), new Point(eye.Right - 5, eye.Top), right);
                graphics.DrawBezier(pen, left, new Point(eye.Left + 5, eye.Bottom), new Point(eye.Right - 5, eye.Bottom), right);
                graphics.FillEllipse(brush, new Rectangle(top.X - 3, top.Y + 6, 7, 7));
                if (hidden)
                    graphics.DrawLine(pen, eye.Left - 2, eye.Bottom + 2, eye.Right + 2, eye.Top - 2);
            }
        }

        private void DrawWindowControlButton(object sender, PaintEventArgs e)
        {
            var button = sender as Button;
            if (button == null)
                return;

            WindowButtonKind kind = button.Tag is WindowButtonKind ? (WindowButtonKind)button.Tag : WindowButtonKind.Maximize;
            bool hot = button.ClientRectangle.Contains(button.PointToClient(Cursor.Position));
            bool pressed = (Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left && hot;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color back = kind == WindowButtonKind.Close && hot
                ? Color.FromArgb(232, 17, 35)
                : hot
                    ? Color.FromArgb(226, 232, 240)
                    : Color.Transparent;
            Color icon = kind == WindowButtonKind.Close && hot
                ? Color.White
                : Color.FromArgb(15, 23, 42);

            if (pressed && kind != WindowButtonKind.Close)
                back = Color.FromArgb(203, 213, 225);

            using (SolidBrush fill = new SolidBrush(back))
            using (GraphicsPath path = Rounded(new Rectangle(2, 2, button.Width - 4, button.Height - 4), 8))
                e.Graphics.FillPath(fill, path);

            Rectangle r = new Rectangle((button.Width - 12) / 2, (button.Height - 12) / 2, 12, 12);
            using (Pen pen = new Pen(icon, 1.7f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                if (kind == WindowButtonKind.Minimize)
                {
                    e.Graphics.DrawLine(pen, r.Left + 1, r.Top + 8, r.Right - 1, r.Top + 8);
                }
                else if (kind == WindowButtonKind.Close)
                {
                    e.Graphics.DrawLine(pen, r.Left + 2, r.Top + 2, r.Right - 2, r.Bottom - 2);
                    e.Graphics.DrawLine(pen, r.Right - 2, r.Top + 2, r.Left + 2, r.Bottom - 2);
                }
                else if (WindowState == FormWindowState.Maximized)
                {
                    e.Graphics.DrawRectangle(pen, r.Left + 3, r.Top + 1, 7, 7);
                    e.Graphics.DrawRectangle(pen, r.Left + 1, r.Top + 4, 7, 7);
                }
                else
                {
                    e.Graphics.DrawRectangle(pen, r.Left + 1, r.Top + 1, 10, 10);
                }
            }
        }

        private void WireEvents()
        {
            KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && _btnSignIn.Enabled)
                {
                    e.SuppressKeyPress = true;
                    await AttemptLoginAsync();
                }
            };
        }

        private void DragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;
            ReleaseCapture();
            SendMessage(Handle, 0xA1, 0x2, 0);
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
            UpdateWindowControlState();
        }

        private void UpdateWindowControlState()
        {
            if (_btnMaximizeToggle == null)
                return;

            bool maximized = WindowState == FormWindowState.Maximized;
            _btnMaximizeToggle.AccessibleName = maximized ? "Restore window" : "Maximize window";
            _btnMaximizeToggle.AccessibleDescription = _btnMaximizeToggle.AccessibleName;
            _btnMaximizeToggle.Invalidate();
        }

        private void FitToWorkingArea()
        {
            Rectangle work = Screen.FromControl(this).WorkingArea;
            int targetWidth = Math.Min(Width, Math.Max(MinimumSize.Width, work.Width - 24));
            int targetHeight = Math.Min(Height, Math.Max(MinimumSize.Height, work.Height - 24));
            if (Width != targetWidth || Height != targetHeight)
                Size = new Size(targetWidth, targetHeight);

            Location = new Point(
                work.Left + Math.Max(0, (work.Width - Width) / 2),
                work.Top + Math.Max(0, (work.Height - Height) / 2));
        }

        private void ApplyRoundedWindow()
        {
            if (_lastWindowRegionSize == Size && _lastWindowRegionState == WindowState)
                return;

            _lastWindowRegionSize = Size;
            _lastWindowRegionState = WindowState;

            if (WindowState == FormWindowState.Maximized)
            {
                Region = null;
                return;
            }

            using (GraphicsPath path = Rounded(new Rectangle(0, 0, Width, Height), 14))
                Region = new Region(path);
        }

        private void DrawBrandPanelLite(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = _leftPanel.ClientRectangle;

            using (LinearGradientBrush bg = new LinearGradientBrush(r, Color.FromArgb(8, 32, 78), Color.FromArgb(0, 91, 224), 115f))
                g.FillRectangle(bg, r);

            using (SolidBrush rail = new SolidBrush(Color.FromArgb(249, 115, 22)))
                g.FillRectangle(rail, 0, 0, 6, r.Height);

            using (SolidBrush glow = new SolidBrush(Color.FromArgb(24, 255, 255, 255)))
            {
                g.FillEllipse(glow, r.Width - 190, 48, 260, 260);
                g.FillEllipse(glow, -120, r.Height - 220, 260, 260);
            }

            int x = 58;
            DrawLeftAligned(g, "ServoERP", new Font("Segoe UI", 30f, FontStyle.Bold), new Rectangle(x, 60, r.Width - 112, 54), Color.White);
            DrawLeftAligned(g, "Manage every service call, contract, and invoice. Built for India.", new Font("Segoe UI", 11.7f, FontStyle.Bold), new Rectangle(x, 126, r.Width - 112, 86), Color.FromArgb(226, 239, 255));
            DrawLeftAligned(g, "Secure local sign-in for clients, service jobs, billing and field operations.", new Font("Segoe UI", 9.5f), new Rectangle(x, 220, r.Width - 112, 46), Color.FromArgb(203, 213, 225));

            int previewHeight = r.Height < 620 ? 120 : 136;
            Rectangle preview = new Rectangle(x, r.Height < 620 ? 282 : 294, Math.Min(336, r.Width - 116), previewHeight);
            DrawDashboardPreview(g, preview);

            string[] items =
            {
                "Manage Clients & Sites",
                "Dispatch Technicians",
                "GST Invoicing & Quotations",
                "AMC & Service Tracking"
            };

            int y = preview.Bottom + (r.Height < 620 ? 20 : 30);
            for (int i = 0; i < items.Length; i++)
            {
                int rowGap = r.Height < 620 ? 30 : 36;
                Rectangle dot = new Rectangle(x + 2, y + (i * rowGap) + 9, 9, 9);
                using (SolidBrush orange = new SolidBrush(Color.FromArgb(249, 115, 22)))
                    g.FillEllipse(orange, dot);

                DrawLeftAligned(g, items[i], new Font("Segoe UI", 10f, FontStyle.Bold), new Rectangle(x + 28, y + (i * rowGap), r.Width - 124, 28), Color.White);
            }
        }

        private void DrawDashboardPreview(Graphics g, Rectangle rect)
        {
            using (GraphicsPath path = Rounded(rect, 14))
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(236, 255, 255, 255)))
            using (Pen border = new Pen(Color.FromArgb(90, 255, 255, 255)))
            {
                g.FillPath(bg, path);
                g.DrawPath(border, path);
            }

            using (SolidBrush navy = new SolidBrush(Color.FromArgb(8, 44, 118)))
                g.FillRectangle(navy, rect.X + 18, rect.Y + 18, rect.Width - 36, 16);

            Color[] colors = { Color.FromArgb(0, 91, 224), Color.FromArgb(249, 115, 22), Color.FromArgb(22, 163, 74) };
            for (int i = 0; i < 3; i++)
            {
                Rectangle card = new Rectangle(rect.X + 18 + (i * ((rect.Width - 48) / 3)), rect.Y + 48, (rect.Width - 66) / 3, 48);
                using (GraphicsPath cardPath = Rounded(card, 8))
                using (SolidBrush cardBg = new SolidBrush(Color.White))
                    g.FillPath(cardBg, cardPath);
                using (SolidBrush accent = new SolidBrush(colors[i]))
                    g.FillRectangle(accent, card.X + 10, card.Y + 12, card.Width - 20, 5);
                using (SolidBrush line = new SolidBrush(Color.FromArgb(203, 213, 225)))
                    g.FillRectangle(line, card.X + 10, card.Y + 27, card.Width - 32, 4);
            }

            using (Pen chart = new Pen(Color.FromArgb(0, 91, 224), 3f))
                g.DrawLines(chart, new[]
                {
                    new Point(rect.X + 24, rect.Bottom - 34),
                    new Point(rect.X + 82, rect.Bottom - 54),
                    new Point(rect.X + 138, rect.Bottom - 44),
                    new Point(rect.X + 196, rect.Bottom - 68),
                    new Point(rect.X + rect.Width - 28, rect.Bottom - 48)
                });
        }

        private void DrawBrandPanel(object sender, PaintEventArgs e)
        {
            if (!_paintingBrandCache)
            {
                if (_leftPanel.Width <= 0 || _leftPanel.Height <= 0)
                    return;

                if (_brandPanelCache == null || _brandPanelCacheSize != _leftPanel.ClientSize)
                    RebuildBrandPanelCache();

                if (_brandPanelCache != null)
                    e.Graphics.DrawImageUnscaled(_brandPanelCache, 0, 0);
                return;
            }

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = _leftPanel.ClientRectangle;

            using (LinearGradientBrush bg = new LinearGradientBrush(r, Color.FromArgb(245, 249, 255), Color.FromArgb(232, 241, 255), 115f))
                g.FillRectangle(bg, r);

            DrawWaves(g, r);
            DrawServoLogo(g, new Rectangle((r.Width - 360) / 2, 64, 360, 230));

            DrawCentered(g, "ServoERP", new Font("Segoe UI", 40f, FontStyle.Bold), new Rectangle(0, 300, r.Width, 72), Color.FromArgb(8, 22, 61));
            DrawCentered(g, "Smart. Integrated. Powerful.", new Font("Segoe UI", 18f, FontStyle.Bold), new Rectangle(0, 370, r.Width, 36), Color.FromArgb(8, 22, 61));
            DrawCentered(g, "All your HVAC business operations,\r\nconnected in one place.", new Font("Segoe UI", 13f), new Rectangle(0, 412, r.Width, 64), Color.FromArgb(71, 85, 125));

            string[] features = { "Clients", "Invoicing", "Service", "Contracts", "Inventory", "Purchases", "Reports", "Employees" };
            char[] icons = { '♙', '▤', '⚒', '▣', '□', '⌁', '▥', '○' };
            int cardW = 86;
            int cardH = 96;
            int gap = 34;
            int startX = (r.Width - (cardW * 4 + gap * 3)) / 2;
            int y = 506;
            for (int i = 0; i < features.Length; i++)
            {
                int col = i % 4;
                int row = i / 4;
                DrawFeatureCard(g, new Rectangle(startX + col * (cardW + gap), y + row * 126, cardW, cardH), icons[i], features[i]);
            }

            int featureGridBottom = y + 126 + cardH;
            if (r.Height - featureGridBottom >= 112)
                DrawSecurityNote(g, new Rectangle(76, featureGridBottom + 24, r.Width - 150, 58));
        }

        private void RebuildBrandPanelCache()
        {
            DisposeBrandPanelCache();
            _brandPanelCacheSize = _leftPanel.ClientSize;
            _brandPanelCache = new Bitmap(Math.Max(1, _leftPanel.Width), Math.Max(1, _leftPanel.Height));
            _paintingBrandCache = true;
            try
            {
                using (Graphics g = Graphics.FromImage(_brandPanelCache))
                using (PaintEventArgs args = new PaintEventArgs(g, new Rectangle(Point.Empty, _leftPanel.ClientSize)))
                    DrawBrandPanelV2(_leftPanel, args);
            }
            finally
            {
                _paintingBrandCache = false;
            }
        }

        private void DrawBrandPanelV2(object sender, PaintEventArgs e)
        {
            if (!_paintingBrandCache)
            {
                if (_leftPanel.Width <= 0 || _leftPanel.Height <= 0)
                    return;

                if (_brandPanelCache == null || _brandPanelCacheSize != _leftPanel.ClientSize)
                    RebuildBrandPanelCache();

                if (_brandPanelCache != null)
                    e.Graphics.DrawImageUnscaled(_brandPanelCache, 0, 0);
                return;
            }

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = _leftPanel.ClientRectangle;
            using (LinearGradientBrush bg = new LinearGradientBrush(r, Color.FromArgb(247, 251, 255), Color.FromArgb(230, 240, 255), 115f))
                g.FillRectangle(bg, r);

            DrawWaves(g, r);
            DrawDotPattern(g, r);
            DrawServoLogo(g, new Rectangle((r.Width - 260) / 2, 40, 260, 178));
            DrawServoWordmark(g, new Rectangle(0, 222, r.Width, 78));
            using (Pen orange = new Pen(Color.FromArgb(249, 115, 22), 2f))
                g.DrawLine(orange, r.Width / 2 - 32, 306, r.Width / 2 + 32, 306);
            DrawCentered(g, "Service Business Management Software\r\nfor Indian SMEs", new Font("Segoe UI", 15.5f, FontStyle.Bold), new Rectangle(64, 324, r.Width - 128, 58), Color.FromArgb(8, 22, 61));

            using (Pen blue = new Pen(Color.FromArgb(0, 91, 224), 2f))
                g.DrawLine(blue, r.Width / 2 - 30, 392, r.Width / 2 + 30, 392);
            DrawCapabilityRow(g, new Rectangle(90, 416, r.Width - 180, 36));
            DrawCentered(g, "Manage Clients, Jobs, Inventory, Billing, Service,\r\nContracts, Employees and much more.", new Font("Segoe UI", 11.5f), new Rectangle(70, 466, r.Width - 140, 52), Color.FromArgb(30, 41, 82));

            string[] features = { "Clients", "Invoicing", "Service", "Contracts", "Inventory", "Purchases", "Reports", "Employees" };
            char[] icons = { '\uE716', '\uE8A5', '\uE90F', '\uE9D2', '\uE7B8', '\uE7BF', '\uE9D9', '\uE716' };
            int cardW = 86;
            int cardH = 96;
            int gap = 34;
            int startX = (r.Width - (cardW * 4 + gap * 3)) / 2;
            int y = 536;
            if (r.Height < 760)
                y = 514;
            for (int i = 0; i < features.Length; i++)
            {
                int col = i % 4;
                int row = i / 4;
                DrawFeatureCard(g, new Rectangle(startX + col * (cardW + gap), y + row * 116, cardW, cardH), icons[i], features[i]);
            }
        }

        private void DisposeBrandPanelCache()
        {
            if (_brandPanelCache != null)
            {
                _brandPanelCache.Dispose();
                _brandPanelCache = null;
            }

            _brandPanelCacheSize = Size.Empty;
        }

        private void DrawWaves(Graphics g, Rectangle r)
        {
            using (Pen pen = new Pen(Color.FromArgb(70, 187, 211, 255), 2f))
            {
                for (int i = 0; i < 5; i++)
                {
                    float offset = (float)Math.Sin(i) * 10f;
                    using (GraphicsPath wave = new GraphicsPath())
                    {
                        wave.AddBezier(-80, 90 + i * 16 + offset, r.Width * .35f, 10 + i * 10, r.Width * .62f, 150 + offset, r.Width + 80, 38 + i * 12);
                        g.DrawPath(pen, wave);
                    }
                }
            }

            using (SolidBrush glow = new SolidBrush(Color.FromArgb(32, 37, 99, 235)))
            {
                float x = 78;
                float y = 102;
                g.FillEllipse(glow, x, y, 130, 130);
                g.FillEllipse(glow, r.Width - 150, r.Height - 185, 120, 120);
            }
        }

        private void DrawDotPattern(Graphics g, Rectangle r)
        {
            using (SolidBrush dot = new SolidBrush(Color.FromArgb(52, 0, 91, 224)))
            {
                for (int y = 28; y < 250; y += 18)
                {
                    for (int x = 18; x < Math.Min(180, r.Width / 3); x += 18)
                    {
                        int size = Math.Max(2, Math.Min(6, (260 - y) / 34));
                        g.FillEllipse(dot, x, y, size, size);
                    }
                }
            }

            using (GraphicsPath blue = new GraphicsPath())
            {
                blue.AddBezier(-80, r.Height - 210, r.Width * .22f, r.Height - 80, r.Width * .58f, r.Height - 40, r.Width + 120, r.Height - 112);
                blue.AddLine(r.Width + 120, r.Height - 112, r.Width + 120, r.Height + 80);
                blue.AddLine(r.Width + 120, r.Height + 80, -80, r.Height + 80);
                blue.CloseFigure();
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(0, 79, 190)))
                    g.FillPath(brush, blue);
            }

            using (Pen orange = new Pen(Color.FromArgb(249, 115, 22), 10f))
            using (GraphicsPath sweep = new GraphicsPath())
            {
                sweep.AddBezier(-20, r.Height - 86, r.Width * .25f, r.Height - 8, r.Width * .55f, r.Height - 4, r.Width + 90, r.Height - 58);
                g.DrawPath(orange, sweep);
            }
        }

        private void DrawServoWordmark(Graphics g, Rectangle bounds)
        {
            string left = "Servo";
            string right = "ERP";
            using (Font font = new Font("Segoe UI", 45f, FontStyle.Bold))
            {
                SizeF leftSize = g.MeasureString(left, font);
                SizeF rightSize = g.MeasureString(right, font);
                float x = bounds.Left + (bounds.Width - leftSize.Width - rightSize.Width + 10) / 2f;
                using (SolidBrush blue = new SolidBrush(Color.FromArgb(8, 44, 118)))
                using (SolidBrush orange = new SolidBrush(Color.FromArgb(249, 115, 22)))
                {
                    g.DrawString(left, font, blue, x, bounds.Top);
                    g.DrawString(right, font, orange, x + leftSize.Width - 8, bounds.Top);
                }
            }
        }

        private void DrawCapabilityRow(Graphics g, Rectangle rect)
        {
            string[] labels = { "Smart", "Integrated", "Powerful", "Reliable" };
            string[] icons = { "\uE781", "\uECAA", "\uE945", "\uE83D" };
            int segment = Math.Max(1, rect.Width / labels.Length);
            for (int i = 0; i < labels.Length; i++)
            {
                int x = rect.Left + i * segment;
                using (Font iconFont = new Font("Segoe MDL2 Assets", 12f))
                using (Font textFont = new Font("Segoe UI", 10.5f))
                using (Brush blue = new SolidBrush(Color.FromArgb(0, 91, 224)))
                using (Brush text = new SolidBrush(Color.FromArgb(8, 22, 61)))
                {
                    g.DrawString(icons[i], iconFont, blue, x + 4, rect.Top + 7);
                    g.DrawString(labels[i], textFont, text, x + 28, rect.Top + 5);
                }
                if (i > 0)
                    using (Pen p = new Pen(DS.Border))
                        g.DrawLine(p, x - 12, rect.Top + 7, x - 12, rect.Bottom - 8);
            }
        }

        private void DrawServoLogo(Graphics g, Rectangle bounds)
        {
            int cx = bounds.Left + bounds.Width / 2;
            int cy = bounds.Top + bounds.Height / 2;
            Rectangle gear = new Rectangle(cx - 88, cy - 84, 176, 176);
            using (Pen gearPen = new Pen(Color.FromArgb(0, 91, 224), 18f))
                g.DrawArc(gearPen, gear, 140, 250);
            using (Pen orange = new Pen(Color.FromArgb(249, 115, 22), 12f))
                g.DrawArc(orange, new Rectangle(cx - 82, cy - 56, 164, 126), 315, 118);
            using (Font font = new Font("Segoe UI", 98f, FontStyle.Bold))
            using (SolidBrush blue = new SolidBrush(Color.FromArgb(8, 44, 118)))
            {
                SizeF s = g.MeasureString("S", font);
                g.DrawString("S", font, blue, cx - s.Width / 2f + 4, cy - s.Height / 2f - 2);
            }
            using (Pen cold = new Pen(Color.FromArgb(37, 99, 235), 3f))
            {
                int sx = cx - 88;
                int sy = cy + 54;
                g.DrawLine(cold, sx - 18, sy, sx + 18, sy);
                g.DrawLine(cold, sx, sy - 18, sx, sy + 18);
                g.DrawLine(cold, sx - 13, sy - 13, sx + 13, sy + 13);
                g.DrawLine(cold, sx - 13, sy + 13, sx + 13, sy - 13);
            }
        }

        private void DrawFeatureCard(Graphics g, Rectangle rect, char icon, string title)
        {
            using (GraphicsPath path = Rounded(rect, 12))
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(24, 15, 23, 42)))
            using (SolidBrush bg = new SolidBrush(Color.White))
            {
                Rectangle shadowRect = rect;
                shadowRect.Offset(0, 6);
                using (GraphicsPath shadowPath = Rounded(shadowRect, 12))
                    g.FillPath(shadow, shadowPath);
                g.FillPath(bg, path);
            }

            DrawCentered(g, icon.ToString(), new Font("Segoe MDL2 Assets", 22f), new Rectangle(rect.X, rect.Y + 14, rect.Width, 34), Color.FromArgb(0, 91, 224));
            DrawCentered(g, title, new Font("Segoe UI", 9f, FontStyle.Bold), new Rectangle(rect.X, rect.Y + 60, rect.Width, 24), Color.FromArgb(8, 22, 61));
        }

        private void DrawSecurityNote(Graphics g, Rectangle rect)
        {
            using (SolidBrush blue = new SolidBrush(Color.FromArgb(37, 99, 235)))
                g.FillEllipse(blue, rect.X, rect.Y + 8, 42, 42);
            DrawCentered(g, "✓", new Font("Segoe UI", 18f, FontStyle.Bold), new Rectangle(rect.X, rect.Y + 9, 42, 40), Color.White);
            using (Brush b = new SolidBrush(Color.FromArgb(8, 22, 61)))
                g.DrawString("Secure. Reliable. Always.", new Font("Segoe UI", 10.5f, FontStyle.Bold), b, rect.X + 56, rect.Y + 7);
            using (Brush b = new SolidBrush(Color.FromArgb(71, 85, 125)))
                g.DrawString("Your data is protected with enterprise-grade security.", new Font("Segoe UI", 9.5f), b, rect.X + 56, rect.Y + 31);
        }

        private void DrawLoginCard(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle shadowRect = new Rectangle(8, 10, _loginCard.Width - 18, _loginCard.Height - 18);
            Rectangle rect = new Rectangle(0, 0, _loginCard.Width - 10, _loginCard.Height - 10);

            using (GraphicsPath shadowPath = Rounded(shadowRect, 18))
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(22, 15, 23, 42)))
                g.FillPath(shadow, shadowPath);

            using (GraphicsPath path = Rounded(rect, 18))
            using (SolidBrush bg = new SolidBrush(Color.White))
            using (Pen border = new Pen(DS.Border))
            {
                g.FillPath(bg, path);
                g.DrawPath(border, path);
            }
        }

        private void DrawFooter(object sender, PaintEventArgs e)
        {
            using (Pen p = new Pen(DS.Border))
                e.Graphics.DrawLine(p, 0, 0, _footer.Width, 0);
        }

        private static Label FieldLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(240, 28),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59)
            };
        }

        private Button GhostButton(string text, int x, int y, int width, int height)
        {
            var button = new Button { Text = text, Location = new Point(x, y), Size = new Size(width, height), FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Color.FromArgb(82, 103, 143), Font = new Font("Segoe UI Symbol", 12f), TabStop = false };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(239, 246, 255);
            return button;
        }

        private Button SocialButton(string text, int x, int y)
        {
            var button = new Button { Text = text, Location = new Point(x, y), Size = new Size(264, 58), FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Color.FromArgb(8, 22, 61), Font = new Font("Segoe UI", 12f) };
            button.FlatAppearance.BorderColor = DS.Border;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(239, 246, 255);
            return button;
        }

        private void StylePrimaryButton(Button button)
        {
            button.FlatAppearance.BorderSize = 0;
            button.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new Rectangle(0, 0, button.Width - 1, button.Height - 1);
                Color top = _signInPressed ? Color.FromArgb(30, 64, 175) : (_signInHover ? Color.FromArgb(37, 99, 235) : Color.FromArgb(0, 91, 224));
                Color bottom = _signInPressed ? Color.FromArgb(30, 58, 138) : Color.FromArgb(8, 44, 118);
                using (GraphicsPath path = Rounded(rect, 12))
                using (LinearGradientBrush fill = new LinearGradientBrush(rect, top, bottom, 90f))
                {
                    e.Graphics.FillPath(fill, path);
                }

                TextRenderer.DrawText(e.Graphics, button.Text, button.Font, rect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            button.MouseEnter += (s, e) => { _signInHover = true; button.Invalidate(); };
            button.MouseLeave += (s, e) => { _signInHover = false; _signInPressed = false; button.Invalidate(); };
            button.MouseDown += (s, e) => { _signInPressed = true; button.Invalidate(); };
            button.MouseUp += (s, e) => { _signInPressed = false; button.Invalidate(); };
        }

        private Label LinkLabel(string text, int x, int y, int width, int height)
        {
            var label = new Label { Text = text, Location = new Point(x, y), Size = new Size(width, height), ForeColor = Color.FromArgb(0, 92, 255), Cursor = Cursors.Hand, Font = new Font("Segoe UI", 10f), TextAlign = ContentAlignment.MiddleRight };
            label.MouseEnter += (s, e) => label.ForeColor = Color.FromArgb(30, 64, 175);
            label.MouseLeave += (s, e) => label.ForeColor = Color.FromArgb(0, 92, 255);
            return label;
        }

        private void AddDivider(int x, int y, int width)
        {
            var line = new Panel { Location = new Point(x, y), Size = new Size(width, 1), BackColor = Color.FromArgb(226, 232, 240) };
            _loginCard.Controls.Add(line);
        }

        private static void DrawCentered(Graphics g, string text, Font font, Rectangle bounds, Color color)
        {
            using (font)
            using (Brush brush = new SolidBrush(color))
            using (StringFormat format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(text, font, brush, bounds, format);
        }

        private static void DrawLeftAligned(Graphics g, string text, Font font, Rectangle bounds, Color color)
        {
            using (font)
            using (Brush brush = new SolidBrush(color))
            using (StringFormat format = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near, Trimming = StringTrimming.Word })
                g.DrawString(text, font, brush, bounds, format);
        }

        private static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private string SpinnerFrame()
        {
            string[] frames = { "◐", "◓", "◑", "◒" };
            return frames[(int)(_spinnerPhase * 10) % frames.Length];
        }

        private void ShakeForm()
        {
            int originalX = Left;
            for (int i = 0; i < 4; i++)
            {
                Left = originalX - 8;
                Application.DoEvents();
                System.Threading.Thread.Sleep(18);
                Left = originalX + 8;
                Application.DoEvents();
                System.Threading.Thread.Sleep(18);
            }
            Left = originalX;
        }

        private sealed class BufferedPanel : Panel
        {
            public BufferedPanel()
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
                SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.UserPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw,
                    true);
                UpdateStyles();
            }
        }
    }
}

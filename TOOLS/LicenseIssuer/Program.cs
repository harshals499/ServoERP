using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace ServoERP.LicenseIssuer
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args != null && args.Length > 0 && string.Equals(args[0], "--self-test", StringComparison.OrdinalIgnoreCase))
            {
                LicenseIssuerSelfTest.Run();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new LicenseIssuerForm());
        }
    }

    internal sealed class LicenseIssuerForm : Form
    {
        private const string AuthorityRoot = @"C:\ServoERP-LicenseAuthority";
        private const string PrivateKeyPath = AuthorityRoot + @"\private-key.xml";
        private const string PublicKeyPath = AuthorityRoot + @"\public-key.xml";
        private const string LicenseOutputRoot = AuthorityRoot + @"\licenses";
        private const string IssuedHistoryPath = AuthorityRoot + @"\issued-licenses.json";

        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();
        private readonly ComboBox _plan = new ComboBox();
        private readonly TextBox _licenseKey = new TextBox();
        private readonly TextBox _company = new TextBox();
        private readonly TextBox _fingerprint = new TextBox();
        private readonly DateTimePicker _issueDate = new DateTimePicker();
        private readonly DateTimePicker _expiryDate = new DateTimePicker();
        private readonly NumericUpDown _maxDevices = new NumericUpDown();
        private readonly NumericUpDown _maxUsers = new NumericUpDown();
        private readonly NumericUpDown _graceDays = new NumericUpDown();
        private readonly TextBox _support = new TextBox();
        private readonly TextBox _planName = new TextBox();
        private readonly ComboBox _billingCycle = new ComboBox();
        private readonly ComboBox _currency = new ComboBox();
        private readonly NumericUpDown _priceAmount = new NumericUpDown();
        private readonly NumericUpDown _renewalPriceAmount = new NumericUpDown();
        private readonly CheckBox _launchOffer = new CheckBox();
        private readonly TextBox _modules = new TextBox();
        private readonly CheckBox _bindToDevice = new CheckBox();
        private readonly Label _status = new Label();

        public LicenseIssuerForm()
        {
            Text = "ServoERP License Issuer";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(860, 820);
            MinimumSize = new Size(820, 760);
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.FromArgb(248, 250, 252);

            EnsureAuthorityKeys();
            Build();
            ApplyPlanDefaults();
        }

        private void Build()
        {
            Controls.Add(new Label
            {
                Text = "ServoERP License Issuer",
                Location = new Point(24, 18),
                Size = new Size(520, 36),
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42)
            });

            Controls.Add(new Label
            {
                Text = "Private issuing tool. Do not copy this tool or private-key.xml to customer machines.",
                Location = new Point(26, 58),
                Size = new Size(700, 28),
                ForeColor = Color.FromArgb(71, 85, 105)
            });

            AddLabel("Plan", 28, 104);
            _plan.DropDownStyle = ComboBoxStyle.DropDownList;
            _plan.Items.AddRange(new object[] { "Trial", "Standard", "Enterprise" });
            _plan.SelectedIndex = 1;
            _plan.Location = new Point(28, 126);
            _plan.Size = new Size(180, 28);
            _plan.SelectedIndexChanged += (s, e) => ApplyPlanDefaults();
            Controls.Add(_plan);

            AddLabel("License Key", 230, 104);
            _licenseKey.Location = new Point(230, 126);
            _licenseKey.Size = new Size(260, 28);
            Controls.Add(_licenseKey);

            Button generateKey = Button("Generate Key", 504, 124, 120, Color.White, Color.FromArgb(15, 23, 42));
            generateKey.Click += (s, e) => _licenseKey.Text = GenerateLicenseKey(_plan.Text);
            Controls.Add(generateKey);

            AddLabel("Company Name", 28, 168);
            _company.Location = new Point(28, 190);
            _company.Size = new Size(596, 28);
            Controls.Add(_company);

            _bindToDevice.Text = "Bind license to this device fingerprint";
            _bindToDevice.Checked = true;
            _bindToDevice.Location = new Point(28, 232);
            _bindToDevice.Size = new Size(320, 26);
            _bindToDevice.CheckedChanged += (s, e) => _fingerprint.Enabled = _bindToDevice.Checked;
            Controls.Add(_bindToDevice);

            AddLabel("Device Fingerprint", 28, 264);
            _fingerprint.Location = new Point(28, 286);
            _fingerprint.Size = new Size(596, 28);
            Controls.Add(_fingerprint);

            Button paste = Button("Paste", 640, 284, 92, Color.White, Color.FromArgb(15, 23, 42));
            paste.Click += (s, e) => { if (Clipboard.ContainsText()) _fingerprint.Text = Clipboard.GetText().Trim(); };
            Controls.Add(paste);

            AddLabel("Issue Date", 28, 328);
            _issueDate.Format = DateTimePickerFormat.Custom;
            _issueDate.CustomFormat = "dd MMM yyyy";
            _issueDate.Location = new Point(28, 350);
            _issueDate.Size = new Size(160, 28);
            Controls.Add(_issueDate);

            AddLabel("Expiry Date", 210, 328);
            _expiryDate.Format = DateTimePickerFormat.Custom;
            _expiryDate.CustomFormat = "dd MMM yyyy";
            _expiryDate.Location = new Point(210, 350);
            _expiryDate.Size = new Size(160, 28);
            Controls.Add(_expiryDate);

            AddLabel("Max Devices", 392, 328);
            ConfigureNumber(_maxDevices, 392, 350, 1, 999);

            AddLabel("Max Users", 514, 328);
            ConfigureNumber(_maxUsers, 514, 350, 1, 999);

            AddLabel("Grace Days", 636, 328);
            ConfigureNumber(_graceDays, 636, 350, 0, 30);

            AddLabel("Support Level", 28, 392);
            _support.Location = new Point(28, 414);
            _support.Size = new Size(220, 28);
            Controls.Add(_support);

            AddLabel("Public Plan Name", 270, 392);
            _planName.Location = new Point(270, 414);
            _planName.Size = new Size(210, 28);
            Controls.Add(_planName);

            AddLabel("Billing Cycle", 502, 392);
            _billingCycle.DropDownStyle = ComboBoxStyle.DropDownList;
            _billingCycle.Items.AddRange(new object[] { "14 days", "Annual", "Custom" });
            _billingCycle.Location = new Point(502, 414);
            _billingCycle.Size = new Size(120, 28);
            Controls.Add(_billingCycle);

            AddLabel("Currency", 642, 392);
            _currency.DropDownStyle = ComboBoxStyle.DropDownList;
            _currency.Items.AddRange(new object[] { "INR", "USD" });
            _currency.Location = new Point(642, 414);
            _currency.Size = new Size(90, 28);
            Controls.Add(_currency);

            AddLabel("Price", 28, 456);
            ConfigureMoney(_priceAmount, 28, 478);

            AddLabel("Renewal Price", 176, 456);
            ConfigureMoney(_renewalPriceAmount, 176, 478);

            _launchOffer.Text = "Launch offer";
            _launchOffer.Location = new Point(326, 478);
            _launchOffer.Size = new Size(160, 28);
            Controls.Add(_launchOffer);

            AddLabel("Enabled Modules", 28, 520);
            _modules.Multiline = true;
            _modules.ScrollBars = ScrollBars.Vertical;
            _modules.Location = new Point(28, 542);
            _modules.Size = new Size(704, 92);
            Controls.Add(_modules);

            Button issue = Button("Issue Signed License", 28, 662, 180, Color.FromArgb(0, 91, 224), Color.White);
            issue.Click += (s, e) => IssueLicense();
            Controls.Add(issue);

            Button openFolder = Button("Open License Folder", 224, 662, 164, Color.White, Color.FromArgb(15, 23, 42));
            openFolder.Click += (s, e) => System.Diagnostics.Process.Start(LicenseOutputRoot);
            Controls.Add(openFolder);

            Button copyPublic = Button("Copy Public Key", 404, 662, 140, Color.White, Color.FromArgb(15, 23, 42));
            copyPublic.Click += (s, e) => Clipboard.SetText(File.ReadAllText(PublicKeyPath));
            Controls.Add(copyPublic);

            _status.Location = new Point(28, 706);
            _status.Size = new Size(704, 38);
            _status.ForeColor = Color.FromArgb(71, 85, 105);
            Controls.Add(_status);
        }

        private void ApplyPlanDefaults()
        {
            string plan = _plan.Text;
            _issueDate.Value = DateTime.Today;

            if (plan == "Trial")
            {
                _expiryDate.Value = DateTime.Today.AddDays(14);
                _maxDevices.Value = 1;
                _maxUsers.Value = 1;
                _graceDays.Value = 0;
                _support.Text = "Limited";
                _planName.Text = "Trial Download";
                _billingCycle.SelectedItem = "14 days";
                _priceAmount.Value = 0;
                _renewalPriceAmount.Value = 0;
                _launchOffer.Checked = false;
            }
            else if (plan == "Standard")
            {
                _expiryDate.Value = DateTime.Today.AddYears(1);
                _maxDevices.Value = 3;
                _maxUsers.Value = 3;
                _graceDays.Value = 3;
                _support.Text = "Standard";
                _planName.Text = "Standard Download";
                _billingCycle.SelectedItem = "Annual";
                _priceAmount.Value = 10000;
                _renewalPriceAmount.Value = 10000;
                _launchOffer.Checked = false;
            }
            else
            {
                _expiryDate.Value = DateTime.Today.AddYears(1);
                _maxDevices.Value = 10;
                _maxUsers.Value = 10;
                _graceDays.Value = 3;
                _support.Text = "Priority";
                _planName.Text = "Enterprise Download";
                _billingCycle.SelectedItem = "Annual";
                _priceAmount.Value = 25000;
                _renewalPriceAmount.Value = 25000;
                _launchOffer.Checked = false;
            }

            if (_currency.SelectedIndex < 0)
                _currency.SelectedItem = "INR";
            _modules.Text = string.Join(Environment.NewLine, ModulesForPlan(plan));
            if (string.IsNullOrWhiteSpace(_licenseKey.Text))
                _licenseKey.Text = GenerateLicenseKey(plan);
        }

        private void IssueLicense()
        {
            try
            {
                string plan = _plan.Text.Trim();
                string key = _licenseKey.Text.Trim();
                string company = _company.Text.Trim();
                string fingerprint = _bindToDevice.Checked ? _fingerprint.Text.Trim() : string.Empty;

                if (string.IsNullOrWhiteSpace(key))
                    throw new InvalidOperationException("License key is required.");
                if (string.IsNullOrWhiteSpace(company))
                    throw new InvalidOperationException("Company name is required.");
                if (_bindToDevice.Checked && string.IsNullOrWhiteSpace(fingerprint))
                    throw new InvalidOperationException("Device fingerprint is required when binding is enabled.");
                if (_expiryDate.Value.Date < _issueDate.Value.Date)
                    throw new InvalidOperationException("Expiry date cannot be before issue date.");

                var payload = new LicensePayload
                {
                    LicenseKey = key,
                    PlanType = plan,
                    CompanyName = company,
                    MaxCompanies = 1,
                    MaxDevices = (int)_maxDevices.Value,
                    MaxUsers = (int)_maxUsers.Value,
                    IssueDateUtc = DateTime.SpecifyKind(_issueDate.Value.Date, DateTimeKind.Local).ToUniversalTime(),
                    ExpiryDateUtc = DateTime.SpecifyKind(_expiryDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local).ToUniversalTime(),
                    GracePeriodDays = (int)_graceDays.Value,
                    MachineFingerprintHash = fingerprint,
                    ActivatedDeviceId = fingerprint,
                    ActivatedDeviceCount = 1,
                    SupportLevel = _support.Text.Trim(),
                    PlanName = string.IsNullOrWhiteSpace(_planName.Text) ? plan : _planName.Text.Trim(),
                    BillingCycle = _billingCycle.SelectedItem == null ? "Annual" : _billingCycle.SelectedItem.ToString(),
                    Currency = _currency.SelectedItem == null ? "INR" : _currency.SelectedItem.ToString(),
                    PriceAmount = _priceAmount.Value,
                    RenewalPriceAmount = _renewalPriceAmount.Value,
                    IsLaunchOffer = _launchOffer.Checked,
                    EnabledModules = ParseModules(_modules.Text)
                };

                string payloadJson = _serializer.Serialize(payload);
                byte[] payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
                byte[] signatureBytes = Sign(payloadBytes);

                var envelope = new SignedLicenseEnvelope
                {
                    Algorithm = "RS256",
                    Payload = Convert.ToBase64String(payloadBytes),
                    Signature = Convert.ToBase64String(signatureBytes)
                };

                Directory.CreateDirectory(LicenseOutputRoot);
                string safeCompany = MakeSafeFileName(company);
                string outputPath = Path.Combine(LicenseOutputRoot, safeCompany + "-" + key + ".servoerp-license");
                File.WriteAllText(outputPath, _serializer.Serialize(envelope), Encoding.UTF8);
                AppendHistory(payload, outputPath);

                _status.ForeColor = Color.FromArgb(5, 150, 105);
                _status.Text = "License issued: " + outputPath;
                Clipboard.SetText(outputPath);
            }
            catch (Exception ex)
            {
                _status.ForeColor = Color.FromArgb(220, 38, 38);
                _status.Text = ex.Message;
                MessageBox.Show(ex.Message, "License Issuer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static byte[] Sign(byte[] payloadBytes)
        {
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                rsa.PersistKeyInCsp = false;
                rsa.FromXmlString(File.ReadAllText(PrivateKeyPath));
                return rsa.SignData(payloadBytes, CryptoConfig.MapNameToOID("SHA256"));
            }
        }

        private void AppendHistory(LicensePayload payload, string outputPath)
        {
            var history = new List<IssuedLicenseRecord>();
            if (File.Exists(IssuedHistoryPath))
            {
                try
                {
                    history = _serializer.Deserialize<List<IssuedLicenseRecord>>(File.ReadAllText(IssuedHistoryPath)) ?? history;
                }
                catch
                {
                    string backup = IssuedHistoryPath + "." + DateTime.Now.ToString("yyyyMMddHHmmss") + ".bad";
                    File.Copy(IssuedHistoryPath, backup, true);
                }
            }

            history.Add(new IssuedLicenseRecord
            {
                IssuedUtc = DateTime.UtcNow,
                LicenseKey = payload.LicenseKey,
                PlanType = payload.PlanType,
                CompanyName = payload.CompanyName,
                ExpiryDateUtc = payload.ExpiryDateUtc,
                MachineFingerprintHash = payload.MachineFingerprintHash,
                PriceAmount = payload.PriceAmount,
                RenewalPriceAmount = payload.RenewalPriceAmount,
                Currency = payload.Currency,
                BillingCycle = payload.BillingCycle,
                IsLaunchOffer = payload.IsLaunchOffer,
                OutputPath = outputPath
            });

            File.WriteAllText(IssuedHistoryPath, _serializer.Serialize(history), Encoding.UTF8);
        }

        private static void EnsureAuthorityKeys()
        {
            Directory.CreateDirectory(AuthorityRoot);
            Directory.CreateDirectory(LicenseOutputRoot);
            if (File.Exists(PrivateKeyPath) && File.Exists(PublicKeyPath))
                return;

            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                rsa.PersistKeyInCsp = false;
                File.WriteAllText(PrivateKeyPath, rsa.ToXmlString(true), Encoding.ASCII);
                File.WriteAllText(PublicKeyPath, rsa.ToXmlString(false), Encoding.ASCII);
            }

            MessageBox.Show(
                "A new ServoERP license authority key pair was generated. Rebuild ServoERP with this public key before issuing customer licenses.",
                "License Authority Created",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private static string GenerateLicenseKey(string plan)
        {
            string prefix = plan == "Trial" ? "TRL" : plan == "Enterprise" ? "ENT" : "STD";
            return "SERVOERP-" + prefix + "-" + DateTime.Today.Year + "-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
        }

        private static List<string> ModulesForPlan(string plan)
        {
            if (plan == "Trial")
                return new List<string> { "Dashboard", "Clients", "Quotations", "Invoices", "Reports", "Settings", "MasterData" };

            if (plan == "Standard")
                return new List<string>
                {
                    "Dashboard", "Clients", "Contracts", "Invoices", "Payments", "Quotations", "Reports",
                    "Settings", "Vendors", "Purchases", "Inventory", "Employees", "WorkOrders", "ServiceDesk", "MasterData"
                };

            return new List<string>
            {
                "Dashboard", "Clients", "Contracts", "Invoices", "Payments", "Quotations", "Reports",
                "Settings", "Vendors", "Purchases", "Inventory", "Employees", "Payroll",
                "GeoIntelligence", "WorkOrders", "ServiceDesk", "MasterData"
            };
        }

        private static List<string> ParseModules(string text)
        {
            var modules = new List<string>();
            foreach (string raw in (text ?? string.Empty).Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string module = raw.Trim();
                if (module.Length > 0 && !modules.Exists(m => string.Equals(m, module, StringComparison.OrdinalIgnoreCase)))
                    modules.Add(module);
            }
            return modules;
        }

        private static string MakeSafeFileName(string value)
        {
            var builder = new StringBuilder();
            foreach (char c in value)
                builder.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '-' : c);
            return builder.ToString().Trim().Replace(" ", "-");
        }

        private void ConfigureNumber(NumericUpDown number, int x, int y, int min, int max)
        {
            number.Minimum = min;
            number.Maximum = max;
            number.Location = new Point(x, y);
            number.Size = new Size(90, 28);
            Controls.Add(number);
        }

        private void ConfigureMoney(NumericUpDown number, int x, int y)
        {
            number.Minimum = 0;
            number.Maximum = 9999999;
            number.DecimalPlaces = 0;
            number.ThousandsSeparator = true;
            number.Location = new Point(x, y);
            number.Size = new Size(128, 28);
            Controls.Add(number);
        }

        private void AddLabel(string text, int x, int y)
        {
            Controls.Add(new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(180, 20),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(51, 65, 85)
            });
        }

        private static Button Button(string text, int x, int y, int width, Color back, Color fore)
        {
            var button = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = fore
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            button.FlatAppearance.BorderSize = back == Color.White ? 1 : 0;
            return button;
        }
    }

    internal sealed class SignedLicenseEnvelope
    {
        public string Algorithm { get; set; }
        public string Payload { get; set; }
        public string Signature { get; set; }
    }

    internal sealed class LicensePayload
    {
        public string LicenseKey { get; set; }
        public string PlanType { get; set; }
        public string CompanyName { get; set; }
        public int MaxCompanies { get; set; }
        public int MaxDevices { get; set; }
        public int MaxUsers { get; set; }
        public DateTime IssueDateUtc { get; set; }
        public DateTime ExpiryDateUtc { get; set; }
        public int GracePeriodDays { get; set; }
        public string MachineFingerprintHash { get; set; }
        public string ActivatedDeviceId { get; set; }
        public int ActivatedDeviceCount { get; set; }
        public string SupportLevel { get; set; }
        public string PlanName { get; set; }
        public string BillingCycle { get; set; }
        public string Currency { get; set; }
        public decimal PriceAmount { get; set; }
        public decimal RenewalPriceAmount { get; set; }
        public bool IsLaunchOffer { get; set; }
        public List<string> EnabledModules { get; set; }
    }

    internal sealed class IssuedLicenseRecord
    {
        public DateTime IssuedUtc { get; set; }
        public string LicenseKey { get; set; }
        public string PlanType { get; set; }
        public string CompanyName { get; set; }
        public DateTime ExpiryDateUtc { get; set; }
        public string MachineFingerprintHash { get; set; }
        public decimal PriceAmount { get; set; }
        public decimal RenewalPriceAmount { get; set; }
        public string Currency { get; set; }
        public string BillingCycle { get; set; }
        public bool IsLaunchOffer { get; set; }
        public string OutputPath { get; set; }
    }

    internal static class LicenseIssuerSelfTest
    {
        private const string AuthorityRoot = @"C:\ServoERP-LicenseAuthority";
        private const string PrivateKeyPath = AuthorityRoot + @"\private-key.xml";
        private const string PublicKeyPath = AuthorityRoot + @"\public-key.xml";

        public static void Run()
        {
            if (!File.Exists(PrivateKeyPath) || !File.Exists(PublicKeyPath))
                throw new InvalidOperationException("License authority keys are missing.");

            var serializer = new JavaScriptSerializer();
            var payload = new LicensePayload
            {
                LicenseKey = "SERVOERP-SELFTEST-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                PlanType = "Trial",
                CompanyName = "ServoERP Self Test",
                MaxCompanies = 1,
                MaxDevices = 1,
                MaxUsers = 1,
                IssueDateUtc = DateTime.UtcNow,
                ExpiryDateUtc = DateTime.UtcNow.AddDays(14),
                GracePeriodDays = 0,
                MachineFingerprintHash = "SELFTEST",
                ActivatedDeviceId = "SELFTEST",
                ActivatedDeviceCount = 1,
                SupportLevel = "Limited",
                PlanName = "Trial Download",
                BillingCycle = "14 days",
                Currency = "INR",
                PriceAmount = 0,
                RenewalPriceAmount = 0,
                IsLaunchOffer = false,
                EnabledModules = new List<string> { "Dashboard", "Clients", "Settings" }
            };

            byte[] payloadBytes = Encoding.UTF8.GetBytes(serializer.Serialize(payload));
            byte[] signature;
            using (var privateRsa = new RSACryptoServiceProvider(2048))
            {
                privateRsa.PersistKeyInCsp = false;
                privateRsa.FromXmlString(File.ReadAllText(PrivateKeyPath));
                signature = privateRsa.SignData(payloadBytes, CryptoConfig.MapNameToOID("SHA256"));
            }

            bool verified;
            using (var publicRsa = new RSACryptoServiceProvider(2048))
            {
                publicRsa.PersistKeyInCsp = false;
                publicRsa.FromXmlString(File.ReadAllText(PublicKeyPath));
                verified = publicRsa.VerifyData(payloadBytes, CryptoConfig.MapNameToOID("SHA256"), signature);
            }

            if (!verified)
                throw new InvalidOperationException("Self-test failed: signature did not verify.");

            Console.WriteLine("License issuer self-test passed.");
        }
    }
}

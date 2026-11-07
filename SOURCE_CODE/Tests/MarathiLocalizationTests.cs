using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Threading;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.UI;

internal static class MarathiLocalizationTests
{
    private sealed class PreviewLoginForm : LoginForm
    {
        // Do not call LoginForm.OnShown: it attempts remembered-session authentication.
        protected override void OnShown(EventArgs e) { }
    }
    private static int _passed;
    private static readonly Assembly App = typeof(LanguageManager).Assembly;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); _passed++; }
    private static object Preference(string method, params object[] args) => App.GetType("HVAC_Pro_Desktop.Services.LanguagePreferenceService").GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
    private static System.Collections.Generic.IEnumerable<Control> Tree(Control c) { yield return c; foreach (Control child in c.Controls) foreach (var nested in Tree(child)) yield return nested; }

    [STAThread]
    internal static int Run(string[] args)
    {
        string output = args[0]; Directory.CreateDirectory(output);
        try
        {
            Application.EnableVisualStyles();
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-IN");
            string money = (123456.78m).ToString("C");
            LanguageManager.SetLanguage("mr-IN", false);
            Check(LanguageManager.CurrentLanguage == LanguageManager.Marathi && Thread.CurrentThread.CurrentUICulture.Name == "mr-IN", "mr-IN selection and UI culture");
            Check((123456.78m).ToString("C") == money && Thread.CurrentThread.CurrentCulture.Name == "en-IN", "en-IN business formatting unchanged");
            Check(LanguageManager.Get("Save") == "जतन करा", "Marathi satellite lookup");
            Check(LanguageManager.Get("Missing.Resource.123") == "Missing.Resource.123", "unknown key falls back unchanged");
            var resources = new ResourceManager("HVAC_Pro_Desktop.Resources.Localization.Strings", App);
            var neutral = resources.GetResourceSet(CultureInfo.InvariantCulture, true, false);
            var marathi = resources.GetResourceSet(CultureInfo.GetCultureInfo("mr-IN"), true, false);
            foreach (System.Collections.DictionaryEntry entry in neutral)
            {
                string translated = marathi.GetString((string)entry.Key);
                Check(!string.IsNullOrWhiteSpace(translated), "Marathi resource present: " + entry.Key);
                var sourceSlots = System.Text.RegularExpressions.Regex.Matches((string)entry.Value, @"\{([0-9]+)[^{}]*\}").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value).OrderBy(v => v);
                var targetSlots = System.Text.RegularExpressions.Regex.Matches(translated, @"\{([0-9]+)[^{}]*\}").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value).OrderBy(v => v);
                if (sourceSlots.Any()) Check(sourceSlots.SequenceEqual(targetSlots), "resource format placeholders preserved: " + entry.Key);
            }
            LanguageManager.SetLanguage(LanguageManager.Hindi, false);
            Check(LanguageManager.Get("Save Language") == "Save Language", "missing culture resource falls back to neutral English");
            LanguageManager.SetLanguage(LanguageManager.Marathi, false);
            using (var form = new Form())
            {
                var label = new Label { Text = "Save" }; var input = new TextBox { Text = "Save" };
                var combo = new ComboBox(); combo.Items.Add("Save"); combo.SelectedIndex = 0;
                var grid = new DataGridView { AllowUserToAddRows = false }; grid.Columns.Add("Name", "Name"); grid.Rows.Add("Save");
                var menu = new ContextMenuStrip(); menu.Items.Add("Save"); label.ContextMenuStrip = menu;
                form.Controls.AddRange(new Control[] { label, input, combo, grid });
                LanguageManager.ApplyControlTree(form);
                Check(label.Text == LanguageManager.Get("Save"), "label translated");
                grid.Columns.Add("Status", "Status");
                Check(grid.Columns[1].HeaderText == LanguageManager.Get("Status"), "late-created grid column header translated");
                var strip = new ToolStrip(); form.Controls.Add(strip);
                var item = new ToolStripButton { Text = "Save", ToolTipText = "Search", AccessibleName = "Save" }; strip.Items.Add(item);
                Check(item.Text == LanguageManager.Get("Save") && item.ToolTipText == LanguageManager.Get("Search") && item.AccessibleName == LanguageManager.Get("Save"), "late-created toolbar action, tooltip and accessibility translated");
                item.Text = "Cancel"; Check(item.Text == LanguageManager.Get("Cancel"), "dynamic toolbar caption translated");
                var toolInput = new ToolStripTextBox { Text = "Save" }; strip.Items.Add(toolInput);
                Check(toolInput.Text == "Save", "toolbar input preserves business value");
                Check(input.Text == "Save" && combo.SelectedItem.ToString() == "Save" && grid.Rows[0].Cells[0].Value.ToString() == "Save", "editable text, combo business value and grid data untouched");
                Check(grid.Columns[0].HeaderText == LanguageManager.Get("Name") && menu.Items[0].Text == LanguageManager.Get("Save"), "grid header and context menu localized");
                label.Text = "Cancel"; Check(label.Text == LanguageManager.Get("Cancel"), "dynamic status text updates without stale cache");
                var dynamicLabel = new Label { Text = "Search" }; form.Controls.Add(dynamicLabel);
                Check(dynamicLabel.Text == LanguageManager.Get("Search"), "controls added later localized");
                var pretranslated = new Label { Text = LanguageManager.Get("Settings") }; form.Controls.Add(pretranslated);
                LanguageManager.SetLanguage(LanguageManager.English, false); LanguageManager.ApplyControlTree(form);
                Check(label.Text == "Cancel" && pretranslated.Text == "Settings" && dynamicLabel.Text == "Search", "English restoration includes pretranslated captions");
                LanguageManager.SetLanguage(LanguageManager.Marathi, false); LanguageManager.ApplyControlTree(form);
                Check(label.Text == LanguageManager.Get("Cancel"), "repeat language switch preserves current source");
            }
            using (var search = new HVAC_Pro_Desktop.UI.Controls.ModernERPSearchBox { Text = "Save" })
            using (var input = new HVAC_Pro_Desktop.UI.Controls.ModernTextBox { Text = "Save", Placeholder = "Enter your password" })
            {
                LanguageManager.ApplyControlTree(search); LanguageManager.ApplyControlTree(input);
                Check(search.Text == "Save" && input.Text == "Save", "composite search and login inputs preserve business text");
            }
            foreach (string language in new[] { LanguageManager.English, LanguageManager.Marathi })
            {
                LanguageManager.SetLanguage(language, false);
                using (var login = new PreviewLoginForm { StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000), ShowInTaskbar = false })
                {
                    // Preview override prevents authentication while exercising native rendering.
                    login.Show(); Application.DoEvents(); login.PerformLayout();
                    var username = (HVAC_Pro_Desktop.UI.Controls.ModernTextBox)typeof(LoginForm).GetField("_txtUsername", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(login);
                    username.Text = "Save"; LanguageManager.ApplyControlTree(login);
                    Check(username.Text == "Save", "login username untouched: " + language);
                    typeof(LoginForm).GetMethod("ShowToast", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(login, new object[] { "Enter username and password.", true });
                    var error = (Label)typeof(LoginForm).GetField("_lblError", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(login);
                    Check(error.Text == LanguageManager.Get("Enter username and password."), "login validation localized: " + language);
                    username.Text = string.Empty;
                    using (var bitmap = new Bitmap(login.Width, login.Height))
                    {
                        login.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                        bitmap.Save(Path.Combine(output, language == LanguageManager.Marathi ? "login-marathi.png" : "login-english.png"));
                    }
                }
            }
            LanguageManager.SetLanguage(LanguageManager.Marathi, false);
            using (var actionHost = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000), ShowInTaskbar = false })
            {
                var saveAction = new Button { Text = "Save Changes" };
                actionHost.Controls.Add(saveAction); actionHost.Show(); Application.DoEvents();
                LanguageManager.ApplyControlTree(actionHost);
                var findSave = App.GetType("HVAC_Pro_Desktop.UI.WorkspaceActionUi").GetMethod("FindSaveButton", BindingFlags.Static | BindingFlags.NonPublic);
                Check(ReferenceEquals(findSave.Invoke(null, new object[] { actionHost }), saveAction), "Ctrl+S identifies the translated Save action");
                var tab = new TabPage { Text = "Mobile Connection" }; LanguageManager.ApplyControlTree(tab);
                var source = typeof(LanguageManager).GetMethod("GetSourceText", BindingFlags.Static | BindingFlags.NonPublic);
                Check((string)source.Invoke(null, new object[] { tab, tab.Text }) == "Mobile Connection", "translated mobile tab retains neutral save routing key");
                actionHost.Close();
            }
            var getMessage = typeof(LanguageManager).GetMethod("GetMessage", BindingFlags.Static | BindingFlags.NonPublic);
            string rawValidation = "- Client company name is required.\r\n- Payment amount must be greater than zero.\nClient: Save | INV-123";
            string localizedValidation = (string)getMessage.Invoke(null, new object[] { rawValidation });
            Check(localizedValidation == "- " + LanguageManager.Get("Client company name is required.") + "\r\n- " + LanguageManager.Get("Payment amount must be greater than zero.") + "\nClient: Save | INV-123", "validation bullets localized while identifiers and line endings are retained");
            LanguageManager.SetLanguage(LanguageManager.English, false);
            Check((string)getMessage.Invoke(null, new object[] { rawValidation }) == rawValidation, "English alert payload unchanged");
            LanguageManager.SetLanguage(LanguageManager.Marathi, false);
            var alerts = App.GetType("HVAC_Pro_Desktop.Services.LocalizedMessageBox").GetMethods(BindingFlags.Static | BindingFlags.Public).Where(m => m.Name == "Show").ToList();
            foreach (var method in typeof(MessageBox).GetMethods(BindingFlags.Static | BindingFlags.Public).Where(m => m.Name == "Show"))
                Check(alerts.Any(a => a.GetParameters().Select(p => p.ParameterType).SequenceEqual(method.GetParameters().Select(p => p.ParameterType))), "alert overload compatibility: " + method.ToString());
            using (var error = new ServoERP.Infrastructure.ServoErrorDialog("Payment amount must be greater than zero.", new InvalidOperationException("Customer Save / INV-123")))
            {
                Check(Tree(error).OfType<Label>().Any(l => l.Text == LanguageManager.Get("Payment amount must be greater than zero.")), "friendly error message localized");
                Check(Tree(error).OfType<TextBox>().Single().Text.Contains("Customer Save / INV-123"), "technical error details preserved");
            }
            using (var technical = new TextBox { Text = "Save", Font = new Font("Consolas", 9f), ReadOnly = true })
            using (var icon = new Label { Text = "\uE72E", Font = new Font("Segoe MDL2 Assets", 12f) })
            {
                LanguageManager.ApplyControlTree(technical); LanguageManager.ApplyControlTree(icon);
                Check(technical.Font.FontFamily.Name == "Consolas" && technical.Text == "Save" && icon.Font.FontFamily.Name == "Segoe MDL2 Assets", "technical and icon fonts preserved");
            }
            LanguageManager.SetLanguage(LanguageManager.Marathi, false);
            using (var customerName = new Label { Text = "Save" })
            {
                typeof(LanguageManager).GetMethod("DataText", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(typeof(Label)).Invoke(null, new object[] { customerName });
                LanguageManager.ApplyControlTree(customerName);
                Check(customerName.Text == "Save", "data-bearing customer name is not translated even when it matches a caption key");
                customerName.Text = "Cancel";
                LanguageManager.ApplyControlTree(customerName);
                Check(customerName.Text == "Cancel", "changing data-bearing text never reuses a caption translation");
            }
            // Preview-only database scope. Any accidental SQL access targets this explicitly
            // identified QA database, never HVAC_PRO. No schema/save methods are called.
            var diagnostic = App.GetType("HVAC_Pro_Desktop.DAL.DatabaseConnectionFactory").GetProperty("DiagnosticConnectionString", BindingFlags.Static | BindingFlags.NonPublic);
            diagnostic.SetValue(null, @"Data Source=.\SQLEXPRESS;Initial Catalog=ServoERP_MarathiQA_20261107_PreviewOnly;Integrated Security=True;Connect Timeout=1", null);
            try
            {
                var qaConnection = new System.Data.SqlClient.SqlConnectionStringBuilder(HVAC_Pro_Desktop.DAL.DatabaseManager.RequireConfiguredConnectionString());
                if (qaConnection.InitialCatalog != "ServoERP_MarathiQA_20261107_PreviewOnly") throw new InvalidOperationException("Disposable QA database scope is required.");
                // Guarded schema-only fixture DDL intentionally uses ADO.NET.
                using (var fixtureConnection = new System.Data.SqlClient.SqlConnection(qaConnection.ConnectionString))
                {
                    fixtureConnection.Open();
                    using (var fixtureCommand = fixtureConnection.CreateCommand())
                    {
                        fixtureCommand.CommandText = File.ReadAllText(Path.GetFullPath(Path.Combine(args[1], "..", "..", "..", "scripts", "Localization", "MarathiPreviewFixture.sql")));
                        fixtureCommand.ExecuteNonQuery();
                    }
                }
                new HVAC_Pro_Desktop.DAL.DatabaseManager().InitializeDatabase();
                HVAC_Pro_Desktop.Services.SessionManager.SetSession(new HVAC_Pro_Desktop.Models.AppUserDto { UserId = 1, Username = "marathi-qa", DisplayName = "Marathi QA", RoleName = "Admin", IsActive = true });
                foreach (string language in new[] { LanguageManager.English, LanguageManager.Marathi })
                {
                    LanguageManager.SetLanguage(language, false);
                    foreach (string scenario in new[] { "invoice", "reports" })
                    {
                        Console.WriteLine("SCENARIO " + scenario + " / " + language);
                        Control page;
                        if (scenario == "invoice") { var invoice = new InvoiceForm(); invoice.LoadReceivablesPreviewForVisualTest(); page = invoice; }
                        else { var reports = new ReportForm(); reports.LoadProfitabilityPreviewForVisualTest(); page = reports; }
                        using (page)
                        using (var host = new Form { ClientSize = new Size(1440, 940), StartPosition = FormStartPosition.Manual, Location = new Point(-2500, -2500), ShowInTaskbar = false })
                        {
                            page.Dock = DockStyle.Fill; host.Controls.Add(page);
                            LanguageManager.ApplyControlTree(page); host.Show(); Application.DoEvents(); host.PerformLayout();
                            using (var image = new Bitmap(host.Width, host.Height))
                            { host.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(output, scenario + "-" + (language == LanguageManager.Marathi ? "marathi" : "english") + ".png")); }
                            Check(Tree(page).Any(c => c is Button), scenario + " preview renders native workflow actions: " + language);
                            host.Close();
                        }
                    }
                }
            }
            finally { diagnostic.SetValue(null, null, null); }
            string temporary = Path.Combine(output, "temporary-preferences", Guid.NewGuid().ToString("N") + ".txt");
            Check((string)Preference("LoadFrom", temporary, LanguageManager.English) == LanguageManager.English, "missing preference retains default");
            Preference("SaveTo", temporary, LanguageManager.Marathi);
            Check((string)Preference("LoadFrom", temporary, LanguageManager.English) == LanguageManager.Marathi, "Marathi preference survives reload");
            Preference("SaveTo", temporary, LanguageManager.English);
            Check((string)Preference("LoadFrom", temporary, LanguageManager.Marathi) == LanguageManager.English, "English preference atomically replaces Marathi");
            File.WriteAllText(temporary, "invalid");
            Check((string)Preference("LoadFrom", temporary, LanguageManager.Marathi) == LanguageManager.Marathi, "invalid preference uses existing fallback");
            File.Delete(temporary);
            // Construct the actual Settings controls without OnShellActivated, OnLoad or business queries.
            foreach (string language in new[] { LanguageManager.English, LanguageManager.Marathi })
            {
                LanguageManager.SetLanguage(language, false);
                using (var settings = new SettingsForm())
                using (var canvas = new Panel { Size = new Size(1100, 10000) })
                {
                    typeof(SettingsForm).GetMethod("BuildForm", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(settings, new object[] { canvas, false });
                    var selector = Tree(canvas).OfType<ComboBox>().Single(c => c.Name == "ApplicationLanguage");
                    var displayMode = (ComboBox)typeof(SettingsForm).GetField("_cmbDisplayFitMode", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(settings);
                    displayMode.SelectedIndex = 0;
                    Check(displayMode.SelectedItem.ToString() == "Auto detect laptop screens" && displayMode.GetItemText(displayMode.SelectedItem) == LanguageManager.Get("Auto detect laptop screens"), "display choice localized without mutating persisted mode: " + language);
                    Check(selector.Items.Count == 2 && selector.Items[1].ToString() == LanguageManager.Marathi && selector.SelectedItem.ToString() == language, "Settings language selector: " + language);
                    Control card = selector.Parent.Parent;
                    card.Parent = null;
                    using (var host = new Form { Text = "Settings localization preview", ClientSize = new Size(760, 600), StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000), ShowInTaskbar = false })
                    {
                        card.Dock = DockStyle.Fill; host.Controls.Add(card);
                        LanguageManager.ApplyControlTree(host);
                        host.Show(); Application.DoEvents(); host.PerformLayout();
                        Check(selector.Font.FontFamily.Name == (language == LanguageManager.Marathi ? "Nirmala UI" : "Segoe UI"), "language font: " + language);
                        var save = Tree(card).OfType<Button>().Single(c => c.Name == "SaveApplicationLanguage");
                        var size = TextRenderer.MeasureText(save.Text, save.Font);
                        Check(size.Width <= save.ClientSize.Width && size.Height <= save.ClientSize.Height, "Save Language caption fits: " + language);
                        Check(!selector.Bounds.IntersectsWith(save.Bounds), "language input and button do not overlap: " + language);
                        using (var bitmap = new Bitmap(host.ClientSize.Width, host.ClientSize.Height)) { host.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(output, language == LanguageManager.Marathi ? "settings-marathi.png" : "settings-english.png")); }
                        host.Close();
                    }
                }
            }
            foreach (string result in (System.Collections.Generic.IEnumerable<string>)App.GetType("HVAC_Pro_Desktop.Tests.ReleaseNotesSmokeTests").GetMethod("RunAll", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null))
                Check(true, result);
            var version = App.GetName().Version;
            string notesFile = Path.Combine(args[1], "RELEASE_NOTES_" + version.Major + "." + version.Minor + "." + version.Build + ".md");
            using (var stream = App.GetManifestResourceStream("ServoERP.ReleaseNotes.Current.md"))
            using (var reader = new StreamReader(stream))
                Check(reader.ReadToEnd() == File.ReadAllText(notesFile), "embedded notes exactly match versioned build output notes");
            Console.WriteLine("PASS TOTAL " + _passed); return 0;
        }
        catch (Exception ex) { Console.WriteLine("FAIL " + ex); return 1; }
    }
}

// Load the actual Release assembly without leaving a test EXE in the distributable output.
internal static class MarathiLocalizationTestBootstrap
{
    [STAThread]
    private static int Main(string[] args)
    {
        string buildOutput = Path.GetFullPath(args[1]);
        AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) =>
        {
            var name = new AssemblyName(eventArgs.Name);
            string file = Path.Combine(buildOutput, string.IsNullOrEmpty(name.CultureName) ? "" : name.CultureName,
                name.Name + (name.Name == "HVAC_Pro_Desktop" ? ".exe" : ".dll"));
            return File.Exists(file) ? Assembly.LoadFrom(file) : null;
        };
        return MarathiLocalizationTests.Run(args);
    }
}

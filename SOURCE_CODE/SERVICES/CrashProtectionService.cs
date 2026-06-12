using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using HVAC_Pro_Desktop.DAL;

namespace HVAC_Pro_Desktop.Services
{
    public static class CrashProtectionService
    {
        private const int DOUBLE_CLICK_BLOCK_MS = 750;
        private static readonly TimeSpan DatabaseDialogThrottle = TimeSpan.FromSeconds(30);
        private static readonly object Sync = new object();
        private static readonly HashSet<Control> AttachedControls = new HashSet<Control>();
        private static readonly Dictionary<string, int> RecurringFailures = new Dictionary<string, int>();
        private static readonly Dictionary<IntPtr, DateTime> LastButtonClick = new Dictionary<IntPtr, DateTime>();
        private static bool _messageFilterInstalled;
        private static string _lastCrashLogPath;
        private static string _lastUiAction = "(startup)";
        private static DateTime _lastDatabaseDialogUtc = DateTime.MinValue;

        public static string LastCrashLogPath
        {
            get { return _lastCrashLogPath; }
        }

        public static void InstallApplicationHooks()
        {
            if (_messageFilterInstalled)
                return;

            Application.AddMessageFilter(new UiActionMessageFilter());
            _messageFilterInstalled = true;
        }

        public static string LastUiAction
        {
            get { return _lastUiAction; }
        }

        public static void AttachToTree(Control root)
        {
            if (root == null)
                return;

            AttachControl(root);
            foreach (Control child in root.Controls)
                AttachToTree(child);
        }

        public static void LogException(string formName, string action, Exception ex, bool shownToUser)
        {
            if (ex == null)
                return;

            try
            {
                string dir = @"C:\HVAC_PRO_MSE\LOGS";
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "CrashLog_" + DateTime.Now.ToString("yyyyMMdd") + ".txt");
                string key = BuildFailureKey(formName, action, ex);
                int count;
                Logger.Log(formName + "." + action, ex);

                lock (Sync)
                {
                    if (!RecurringFailures.TryGetValue(key, out count))
                        count = 0;
                    count++;
                    RecurringFailures[key] = count;

                    var sb = new StringBuilder();
                    sb.AppendLine("==================================================");
                    sb.AppendLine("Timestamp      : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    sb.AppendLine("Form           : " + SafeText(formName));
                    sb.AppendLine("Action         : " + SafeText(action));
                    sb.AppendLine("Last UI Action : " + SafeText(_lastUiAction));
                    sb.AppendLine("Error Type     : " + ex.GetType().FullName);
                    sb.AppendLine("Shown To User  : " + shownToUser);
                    sb.AppendLine("Recurring Count: " + count);
                    if (count >= 3)
                        sb.AppendLine("Recurring Flag : RECURRING FAILURE - same action failed at least 3 times");
                    sb.AppendLine("Message        : " + SensitiveDataRedactor.Redact(ex.Message));
                    sb.AppendLine("Stack Trace    :");
                    sb.AppendLine(SensitiveDataRedactor.Redact(ex.StackTrace ?? "(no stack trace)"));
                    if (ex.InnerException != null)
                    {
                        sb.AppendLine("Inner Exception:");
                        sb.AppendLine(SensitiveDataRedactor.Redact(ex.InnerException.ToString()));
                    }
                    sb.AppendLine();

                    File.AppendAllText(path, sb.ToString());
                    _lastCrashLogPath = path;
                }
            }
            catch
            {
            }
        }

        public static void ShowFriendlyError(IWin32Window owner, string formName, string action, Exception ex)
        {
            LogException(formName, action, ex, true);

            if (IsDatabaseConnectivityException(ex))
            {
                DatabaseConnectionStateService.RecordOperationFailure(action, ex);
                if (SuppressRepeatedDatabaseDialog())
                    return;

                MessageBox.Show(
                    owner,
                    BuildDatabaseConnectionMessage(),
                    BrandingService.WindowTitle("Database Connection"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            MessageBox.Show(
                owner,
                BuildFriendlyMessage(ex, action),
                BrandingService.WindowTitle("Screen Error"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        public static bool SafeExecute(Action action, string actionName, Control buttonToDisable = null, string loadingText = "Processing...")
        {
            return SafeExecute(buttonToDisable, actionName, buttonToDisable, loadingText, action);
        }

        public static bool SafeExecute(Control owner, string action, Control buttonToDisable, string loadingText, Action work)
        {
            if (work == null)
                return false;

            string originalText = buttonToDisable == null ? string.Empty : buttonToDisable.Text;
            bool originalEnabled = buttonToDisable == null || buttonToDisable.Enabled;
            try
            {
                MarkUiAction(owner, action);
                if (buttonToDisable != null)
                {
                    buttonToDisable.Enabled = false;
                    if (!string.IsNullOrWhiteSpace(loadingText))
                        buttonToDisable.Text = loadingText;
                    buttonToDisable.Refresh();
                }

                work();
                return true;
            }
            catch (ValidationException ex)
            {
                ShowValidationWarning(owner, ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                ShowFriendlyError(owner == null ? null : owner.FindForm(), GetFormName(owner), action, ex);
                return false;
            }
            finally
            {
                if (buttonToDisable != null && !buttonToDisable.IsDisposed)
                {
                    buttonToDisable.Text = originalText;
                    buttonToDisable.Enabled = originalEnabled;
                }
            }
        }

        public static bool SafeExecute(Control owner, string action, Action work)
        {
            if (work == null)
                return false;

            try
            {
                MarkUiAction(owner, action);
                work();
                return true;
            }
            catch (ValidationException ex)
            {
                ShowValidationWarning(owner, ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                ShowFriendlyError(owner == null ? null : owner.FindForm(), GetFormName(owner), action, ex);
                return false;
            }
        }

        public static T SafeExecute<T>(Control owner, string action, Func<T> work, T fallback)
        {
            if (work == null)
                return fallback;

            try
            {
                MarkUiAction(owner, action);
                return work();
            }
            catch (ValidationException ex)
            {
                ShowValidationWarning(owner, ex.Message);
                return fallback;
            }
            catch (Exception ex)
            {
                ShowFriendlyError(owner == null ? null : owner.FindForm(), GetFormName(owner), action, ex);
                return fallback;
            }
        }

        public static bool SafeLoad(Control owner, string formName, Action loadAction, Action retryAction = null)
        {
            if (loadAction == null)
                return false;

            try
            {
                MarkUiAction(owner, formName + " load");
                loadAction();
                return true;
            }
            catch (Exception ex)
            {
                LogException(GetFormName(owner), formName + ".Load", ex, false);
                ShowInlinePageError(owner, formName, retryAction);
                return false;
            }
        }

        public static DialogResult SafeShowDialog(Control owner, string action, Func<Form> createForm)
        {
            if (createForm == null)
                return DialogResult.Cancel;

            try
            {
                MarkUiAction(owner, action);
                using (Form form = createForm())
                {
                    if (form == null)
                        throw new InvalidOperationException("Screen factory returned no form.");

                    IWin32Window window = owner == null ? null : owner.FindForm();
                    return window == null ? form.ShowDialog() : form.ShowDialog(window);
                }
            }
            catch (Exception ex)
            {
                ShowFriendlyError(owner == null ? null : owner.FindForm(), GetFormName(owner), action, ex);
                return DialogResult.Cancel;
            }
        }

        public static void SafeShow(Control owner, string action, Func<Form> createForm)
        {
            if (createForm == null)
                return;

            try
            {
                MarkUiAction(owner, action);
                Form form = createForm();
                if (form == null)
                    throw new InvalidOperationException("Screen factory returned no form.");

                Form parent = owner == null ? null : owner.FindForm();
                if (parent == null)
                    form.Show();
                else
                    form.Show(parent);
            }
            catch (Exception ex)
            {
                ShowFriendlyError(owner == null ? null : owner.FindForm(), GetFormName(owner), action, ex);
            }
        }

        private static void AttachControl(Control control)
        {
            lock (Sync)
            {
                if (AttachedControls.Contains(control))
                    return;
                AttachedControls.Add(control);
            }

            control.Disposed += (s, e) =>
            {
                lock (Sync)
                    AttachedControls.Remove(control);
            };

            control.ControlAdded += (s, e) => AttachToTree(e.Control);

            DataGridView grid = control as DataGridView;
            if (grid != null)
            {
                grid.DataError += (s, e) =>
                {
                    LogException(GetFormName(grid), grid.Name + ".DataError", e.Exception, false);
                    e.Cancel = true;
                    e.ThrowException = false;
                };
            }
        }

        private static void ShowInlinePageError(Control owner, string formName, Action retryAction)
        {
            if (owner == null || owner.IsDisposed)
                return;

            try
            {
                Panel panel = new Panel { Dock = DockStyle.Fill, BackColor = System.Drawing.Color.White, Padding = new Padding(24) };
                Label title = new Label
                {
                    Text = "This page could not load its data.",
                    Dock = DockStyle.Top,
                    Height = 34,
                    Font = new System.Drawing.Font("Segoe UI", 12f, System.Drawing.FontStyle.Bold),
                    ForeColor = System.Drawing.Color.FromArgb(30, 41, 59)
                };
                Label body = new Label
                {
                    Text = "Click Refresh to try again. Your saved data has not been changed.",
                    Dock = DockStyle.Top,
                    Height = 52,
                    Font = new System.Drawing.Font("Segoe UI", 9f),
                    ForeColor = System.Drawing.Color.FromArgb(71, 85, 105)
                };
                Button retry = new Button { Text = "Refresh", Dock = DockStyle.Top, Height = 34 };
                retry.Click += (s, e) =>
                {
                    if (retryAction != null)
                        SafeExecute(owner, formName + " refresh", retryAction);
                };
                panel.Controls.Add(retry);
                panel.Controls.Add(body);
                panel.Controls.Add(title);

                owner.Controls.Clear();
                owner.Controls.Add(panel);
            }
            catch
            {
            }
        }

        private static void ShowValidationWarning(IWin32Window owner, string message)
        {
            MessageBox.Show(
                owner,
                string.IsNullOrWhiteSpace(message) ? "Please check the highlighted fields and try again." : message,
                BrandingService.WindowTitle("Validation"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        private static string BuildFriendlyMessage(Exception ex, string action)
        {
            if (ex is ValidationException)
                return string.IsNullOrWhiteSpace(ex.Message) ? "Please fill in all required fields." : ex.Message;

            if (IsDatabaseConnectivityException(ex))
                return BuildDatabaseConnectionMessage();

            if (ex is DatabaseException)
                return "Could not complete this database action. Check your connection and retry.";

            if (ex is NavigationException)
                return "This record or page could not be opened. Please refresh and try again.";

            if (ex is FileNotFoundException)
                return "The file could not be located. It may have been moved or deleted.";

            if (ex is FileProcessingException || ex is IOException || ex is UnauthorizedAccessException)
                return "The file action could not be completed. Check the file location or permissions and retry.";

            if (ex is InvalidOperationException || ex is ArgumentException)
                return "This action is not available in the current state. Refresh the page and try again.";

            if (ex is NullReferenceException)
                return "A required value was missing. Please refresh and try again.";

            string actionText = string.IsNullOrWhiteSpace(action) ? "this screen" : action.Trim();
            return "Something went wrong with " + actionText + ". Your data has not been lost. Please try again.";
        }

        private static bool IsDatabaseConnectivityException(Exception ex)
        {
            while (ex != null)
            {
                if (ex is SqlException ||
                    ex is DatabaseException ||
                    ex is DatabaseBusinessWriteUnavailableException)
                    return true;

                string message = ex.Message ?? string.Empty;
                if (message.IndexOf("SQL Server", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf("database target", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf("office SQL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf("Business entries are locked", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                ex = ex.InnerException;
            }

            return false;
        }

        private static bool SuppressRepeatedDatabaseDialog()
        {
            lock (Sync)
            {
                DateTime now = DateTime.UtcNow;
                if (now - _lastDatabaseDialogUtc < DatabaseDialogThrottle)
                    return true;

                _lastDatabaseDialogUtc = now;
                return false;
            }
        }

        private static string BuildDatabaseConnectionMessage()
        {
            return "ServoERP cannot reach the office SQL Server right now." + Environment.NewLine + Environment.NewLine +
                   "No business data was saved for this action. Check that the server PC is on, SQL Server is running, and the saved database connection is correct." + Environment.NewLine + Environment.NewLine +
                   "Target: " + GetConfiguredDatabaseTarget() + Environment.NewLine +
                   "Next step: Settings > Help & Support > Database Check, or reopen ServoERP after fixing the server/network.";
        }

        /// <summary>Returns the configured SQL target for support-friendly database errors.</summary>
        private static string GetConfiguredDatabaseTarget()
        {
            try
            {
                string connectionString = DatabaseManager.GetConfiguredConnectionString();
                if (string.IsNullOrWhiteSpace(connectionString))
                    return "(not configured)";

                var builder = new SqlConnectionStringBuilder(connectionString);
                string server = string.IsNullOrWhiteSpace(builder.DataSource) ? "(unknown server)" : builder.DataSource;
                string database = string.IsNullOrWhiteSpace(builder.InitialCatalog) ? "HVAC_PRO" : builder.InitialCatalog;
                return server + " / " + database;
            }
            catch
            {
                return "(not configured)";
            }
        }

        private static string GetFormName(Control control)
        {
            if (control == null)
                return "(application)";

            Form form = control as Form ?? control.FindForm();
            if (form != null)
                return string.IsNullOrWhiteSpace(form.Name) ? form.GetType().Name : form.Name;

            return string.IsNullOrWhiteSpace(control.Name) ? control.GetType().Name : control.Name;
        }

        private static string BuildFailureKey(string formName, string action, Exception ex)
        {
            return SafeText(formName) + "|" + SafeText(action) + "|" + ex.GetType().FullName + "|" + SafeText(SensitiveDataRedactor.Redact(ex.Message));
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(unknown)" : value;
        }

        private static void MarkUiAction(Control control, string action)
        {
            string formName = GetFormName(control);
            string controlName = DescribeControl(control);
            _lastUiAction = SafeText(formName) + " | " + SafeText(controlName) + " | " + SafeText(action);
        }

        private static string DescribeControl(Control control)
        {
            if (control == null)
                return "(no control)";

            string text = control.Text;
            if (string.IsNullOrWhiteSpace(text))
                text = control.Name;
            if (string.IsNullOrWhiteSpace(text))
                text = control.GetType().Name;
            return text.Trim();
        }

        private sealed class UiActionMessageFilter : IMessageFilter
        {
            private const int WM_LBUTTONDOWN = 0x0201;
            private const int WM_LBUTTONDBLCLK = 0x0203;

            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg != WM_LBUTTONDOWN && m.Msg != WM_LBUTTONDBLCLK)
                    return false;

                Control control = Control.FromHandle(m.HWnd);
                if (control != null)
                    MarkUiAction(control, "click");

                if (!(control is Button))
                    return false;

                DateTime now = DateTime.UtcNow;
                lock (Sync)
                {
                    DateTime last;
                    if (LastButtonClick.TryGetValue(m.HWnd, out last) && (now - last).TotalMilliseconds < DOUBLE_CLICK_BLOCK_MS)
                        return true;

                    LastButtonClick[m.HWnd] = now;
                }

                return false;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.UI;

namespace HVAC_Pro_Desktop.Tests
{
    public static class EnterpriseUiSmokeTests
    {
        public static List<string> RunAll()
        {
            var results = new List<string>();
            Type[] coreModuleTypes =
            {
                typeof(DashboardForm),
                typeof(InvoiceForm),
                typeof(InventoryForm),
                typeof(PurchaseForm),
                typeof(ClientManagementForm),
                typeof(VendorForm),
                typeof(PaymentForm),
                typeof(ContractManagementForm),
                typeof(JobManagementForm),
                typeof(PayrollForm),
                typeof(ReportForm),
                typeof(SettingsForm),
                typeof(MasterDataForm)
            };

            Type[] cardMenuPageTypes =
            {
                typeof(TallyIntegrationForm),
                typeof(AMCPage),
                typeof(DashboardForm),
                typeof(InvoiceForm),
                typeof(InventoryForm),
                typeof(PurchaseForm),
                typeof(ClientManagementForm),
                typeof(ClientDetailPage),
                typeof(VendorForm),
                typeof(PaymentForm),
                typeof(ContractManagementForm),
                typeof(JobManagementForm),
                typeof(JobDetailPage),
                typeof(PayrollForm),
                typeof(ReportForm),
                typeof(SettingsForm),
                typeof(MasterDataForm),
                typeof(EmployeeForm),
                typeof(GeoIntelligenceForm),
                typeof(ServiceDeskForm),
                typeof(SLADashboardForm),
                typeof(TenderBidForm),
                typeof(WhatsAppHubForm),
                typeof(BackupSettingsForm),
                typeof(OpenSourceLicenseForm),
                typeof(ModuleCatalogForm),
                typeof(JobWorkflowBoardForm),
                typeof(CompliancePackForm),
                typeof(LegalAgreementForm)
            };

            var coreSet = new HashSet<Type>(coreModuleTypes);
            foreach (Type type in cardMenuPageTypes.Distinct())
            {
                using (Control control = (Control)Activator.CreateInstance(type))
                {
                    control.Size = new Size(1366, 768);
                    control.PerformLayout();
                    LayoutAuditService.AuditAndFix(control);
                    UIHelper.ApplyButtonAlignment(control);
                    InputOutlineService.ApplyToTree(control);
                    GlobalDashboardLayoutService.ApplyToTree(control);
                    GlobalCardContextMenu.ApplyToTree(control);
                    if (coreSet.Contains(type))
                        EnsureNoDeadButtons(control, type.Name);
                    EnsureNoClippedButtons(control, type.Name);
                    EnsureNoTextButtonOverlap(control, type.Name);
                    EnsureButtonRoleStyling(control, type.Name);
                    EnsureNoAwkwardActionButtonGrids(control, type.Name);
                    EnsureSharedTextStyleAndContrast(control, type.Name);
                    EnsureAlignedButtonGroups(control, type.Name);
                    EnsureNoFloatingResetButtons(control, type.Name);
                    EnsureNoResizeGripsOnEditorFields(control, type.Name);
                    EnsureResizableCardsHaveBothGrips(control, type.Name);
                    EnsureVisibleInputOutlines(control, type.Name);
                    EnsureGlobalCardMenus(control, type.Name);
                    control.Size = new Size(1920, 1080);
                    control.PerformLayout();
                    LayoutAuditService.AuditAndFix(control);
                    UIHelper.ApplyButtonAlignment(control);
                    InputOutlineService.ApplyToTree(control);
                    GlobalDashboardLayoutService.ApplyToTree(control);
                    EnsureNoClippedButtons(control, type.Name);
                    EnsureNoTextButtonOverlap(control, type.Name);
                    EnsureButtonRoleStyling(control, type.Name);
                    EnsureNoAwkwardActionButtonGrids(control, type.Name);
                    EnsureSharedTextStyleAndContrast(control, type.Name);
                    EnsureNoFloatingResetButtons(control, type.Name);
                    EnsureNoResizeGripsOnEditorFields(control, type.Name);
                    EnsureResizableCardsHaveBothGrips(control, type.Name);
                    EnsureVisibleInputOutlines(control, type.Name);
                    results.Add(type.Name + " instantiated, scanned, and card menus verified");
                }

                Application.DoEvents();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            foreach (Control control in CreateAdditionalModalSurfaces())
            {
                using (control)
                {
                    control.Size = new Size(1366, 768);
                    control.PerformLayout();
                    LayoutAuditService.AuditAndFix(control);
                    UIHelper.ApplyButtonAlignment(control);
                    InputOutlineService.ApplyToTree(control);
                    EnsureNoClippedButtons(control, control.GetType().Name);
                    EnsureNoTextButtonOverlap(control, control.GetType().Name);
                    EnsureButtonRoleStyling(control, control.GetType().Name);
                    EnsureSharedTextStyleAndContrast(control, control.GetType().Name);
                    EnsureVisibleInputOutlines(control, control.GetType().Name);
                    results.Add(control.GetType().Name + " instantiated and scanned");
                }

                CleanupUiResources();
            }

            return results;
        }

        private static IEnumerable<Control> CreateAdditionalModalSurfaces()
        {
            yield return new AMCDetailPage(0, null);
            yield return new AddAMCEquipmentForm(0);
            yield return new MarkVisitCompleteForm(0);
        }

        public static string WriteReport()
        {
            string dir = Path.Combine(@"C:\HVAC_PRO_MSE", "TEST_RESULTS");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "enterprise-ui-smoke-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
            var lines = new List<string>();
            lines.Add("ServoERP Enterprise UI Smoke Test");
            lines.Add(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            lines.Add("");
            try
            {
                lines.Add("PASS " + ModuleDashboardNavigationSmokeTests.RunAll());
                CleanupUiResources();
                foreach (string result in RunAll())
                    lines.Add("PASS " + result);
                CleanupUiResources();
                foreach (string result in UiQaStateCatalogTests.RunAll())
                    lines.Add(result);
                CleanupUiResources();
                foreach (string result in UiPolicyTests.RunAll())
                    lines.Add(result);
                CleanupUiResources();
                foreach (string result in DataQualitySmokeTests.RunAll())
                    lines.Add("PASS " + result);
                CleanupUiResources();
                foreach (string result in EndToEndWorkflowSmokeTests.RunAll())
                    lines.Add("PASS " + result);
                CleanupUiResources();
                lines.Add("PASS " + StartupInstanceCleanupSmokeTests.RunAll());
                CleanupUiResources();
                foreach (string result in ImportPreflightSmokeTests.RunAll())
                    lines.Add("PASS " + result);
                CleanupUiResources();
                foreach (string result in DashboardCommandCenterSmokeTests.RunAll())
                    lines.Add("PASS " + result);
                CleanupUiResources();
                foreach (string result in InvoiceAnalyticsServiceSmokeTests.RunAll())
                    lines.Add("PASS " + result);
                CleanupUiResources();
                foreach (string result in QuotationAnalyticsServiceSmokeTests.RunAll())
                    lines.Add("PASS " + result);
                CleanupUiResources();
                foreach (string result in SupportCenterOperationsSmokeTests.RunAll())
                    lines.Add("PASS " + result);
                CleanupUiResources();
                foreach (string result in ContextMenuActionAuditTests.RunAll())
                    lines.Add("PASS " + result);
                CleanupUiResources();
                lines.Add("PASS " + UiErrorHandlingSmokeTests.RunAll());
                CleanupUiResources();
                foreach (string result in FullNavigationClickSmokeTests.RunAll())
                    lines.Add("PASS " + result);
                CleanupUiResources();
                foreach (string result in AddAMCSmokeTests.RunAll())
                    lines.Add(result);
            }
            catch (Exception ex)
            {
                lines.Add("FAIL " + ex);
                File.WriteAllLines(path, lines);
                return path;
            }

            File.WriteAllLines(path, lines);
            return path;
        }

        private static void CleanupUiResources()
        {
            Application.DoEvents();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Application.DoEvents();
        }

        private static void EnsureNoDeadButtons(Control root, string moduleName)
        {
            foreach (Button button in EnumerateControls(root).OfType<Button>())
            {
                string text = (button.Text ?? string.Empty).Trim();
                if (button.Enabled && !HasClickHandler(button) && !IsContainerButton(button))
                    throw new InvalidOperationException(moduleName + " has an enabled button without a click handler: " + text);
            }
        }

        private static void EnsureGlobalCardMenus(Control root, string moduleName)
        {
            int detected = GlobalCardContextMenu.CountDetectedCards(root);
            if (detected == 0)
                return;

            int attached = GlobalCardContextMenu.CountAttachedCards(root);
            if (attached < detected)
                throw new InvalidOperationException(moduleName + " has card surfaces without the global context menu. Detected=" + detected + ", attached=" + attached);
        }

        private static void EnsureAlignedButtonGroups(Control root, string moduleName)
        {
            foreach (Control parent in EnumerateControls(root))
            {
                List<Button> buttons = parent.Controls.OfType<Button>().Where(button => button.Visible).ToList();
                if (buttons.Count < 2)
                    continue;

                foreach (List<Button> row in GroupButtonRows(buttons))
                {
                    if (row.Count < 2)
                        continue;

                    bool tableLayout = parent is TableLayoutPanel;
                    int topDelta = tableLayout ? 0 : row.Max(button => button.Top) - row.Min(button => button.Top);
                    int heightDelta = row.Max(button => button.Height) - row.Min(button => button.Height);
                    if (topDelta > 4 || heightDelta > 4)
                    {
                        string names = string.Join(", ", row.Select(button => (button.Text ?? button.Name ?? "button").Replace(Environment.NewLine, " / ")));
                        throw new InvalidOperationException(moduleName + " has a misaligned button row in " + parent.GetType().Name + ": " + names);
                    }
                }
            }
        }

        private static void EnsureNoClippedButtons(Control root, string moduleName)
        {
            foreach (Button button in EnumerateControls(root).OfType<Button>().Where(button => button.Visible))
            {
                string text = (button.Text ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(text) || IsContainerButton(button))
                    continue;

                foreach (string line in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
                {
                    string cleanLine = line.Trim();
                    if (string.IsNullOrWhiteSpace(cleanLine))
                        continue;

                    int requiredWidth = TextRenderer.MeasureText(cleanLine, button.Font).Width + Math.Max(18, button.Padding.Left + button.Padding.Right + 16);
                    if (button.Image != null)
                        requiredWidth += button.Image.Width + 8;

                    if (button.Width + 2 < requiredWidth)
                        throw new InvalidOperationException(moduleName + " has a clipped button: '" + text.Replace(Environment.NewLine, " / ") + "' width=" + button.Width + " required=" + requiredWidth);
                }

                int requiredHeight = TextRenderer.MeasureText(text, button.Font).Height + 10;
                if (button.Height + 2 < requiredHeight)
                    throw new InvalidOperationException(moduleName + " has a button that is too short for its text: '" + text.Replace(Environment.NewLine, " / ") + "' height=" + button.Height + " required=" + requiredHeight);
            }
        }

        private static void EnsureNoTextButtonOverlap(Control root, string moduleName)
        {
            foreach (Control parent in EnumerateControls(root))
            {
                if (parent is FlowLayoutPanel || parent is TableLayoutPanel)
                    continue;

                List<Button> buttons = parent.Controls.OfType<Button>()
                    .Where(button => button.Visible && !IsContainerButton(button) && !IsIconOnlyText(button.Text))
                    .ToList();
                if (buttons.Count == 0)
                    continue;

                foreach (Control textControl in parent.Controls.Cast<Control>().Where(IsInlineTextControl))
                {
                    foreach (Button button in buttons)
                    {
                        if (!textControl.Bounds.IntersectsWith(button.Bounds))
                            continue;

                        throw new InvalidOperationException(
                            moduleName + " has text overlapping a button in " + parent.GetType().Name +
                            ": text='" + (textControl.Text ?? textControl.Name) +
                            "' button='" + (button.Text ?? button.Name) + "'");
                    }
                }
            }
        }

        private static bool IsInlineTextControl(Control control)
        {
            if (control == null || !control.Visible || control.Dock != DockStyle.None || control.AutoSize)
                return false;

            if (control is Button || control is TextBoxBase || control is ComboBox || control is DateTimePicker || control is NumericUpDown)
                return false;

            string text = (control.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text) || IsIconOnlyText(text))
                return false;

            return control is Label || control is LinkLabel || control is CheckBox || control is RadioButton;
        }

        private static void EnsureNoAwkwardActionButtonGrids(Control root, string moduleName)
        {
            foreach (TableLayoutPanel table in EnumerateControls(root).OfType<TableLayoutPanel>())
            {
                List<Button> buttons = table.Controls.OfType<Button>().Where(button => button.Visible && !IsContainerButton(button)).ToList();
                if (buttons.Count < 2)
                    continue;

                bool actionGrid = table.Dock == DockStyle.Fill || table.Height <= 180 || table.Name.IndexOf("action", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!actionGrid)
                    continue;

                bool inputGrid = EnumerateControls(table).Any(control =>
                                    control is TextBoxBase ||
                                    control is ComboBox ||
                                    control is DateTimePicker ||
                                    control is NumericUpDown);
                if (inputGrid)
                    continue;

                int emptyCells = Math.Max(0, (table.ColumnCount * table.RowCount) - buttons.Count);
                if (emptyCells > 0 && table.ColumnCount > 2 && buttons.Count <= 6)
                    throw new InvalidOperationException(moduleName + " has an awkward action button grid with " + emptyCells + " empty slot(s): " + Describe(table));
            }
        }

        private static void EnsureButtonRoleStyling(Control root, string moduleName)
        {
            foreach (Button button in EnumerateControls(root).OfType<Button>().Where(button => button.Visible && !IsContainerButton(button)))
            {
                if (IsCompactUtilityButton(button))
                    continue;

                if (button.Height < 34)
                    throw new InvalidOperationException(moduleName + " has a button below global minimum height: '" + button.Text + "' height=" + button.Height);

                if (button.Width < 100)
                    throw new InvalidOperationException(moduleName + " has a button below global minimum width: '" + button.Text + "' width=" + button.Width);

                ButtonRole role = UIHelper.ResolveButtonRole(button);
                Color expectedBack;
                Color expectedFore;
                Color expectedBorder;
                ResolveExpectedButtonColors(role, out expectedBack, out expectedFore, out expectedBorder);

                if (button.BackColor.ToArgb() != expectedBack.ToArgb())
                    throw new InvalidOperationException(moduleName + " has incorrect " + role + " button background on '" + button.Text + "': " + button.BackColor);
                if (button.ForeColor.ToArgb() != expectedFore.ToArgb())
                    throw new InvalidOperationException(moduleName + " has incorrect " + role + " button text color on '" + button.Text + "': " + button.ForeColor);
                if (button.FlatAppearance.BorderColor.ToArgb() != expectedBorder.ToArgb() && !button.Focused)
                    throw new InvalidOperationException(moduleName + " has incorrect " + role + " button border on '" + button.Text + "': " + button.FlatAppearance.BorderColor);
            }
        }

        private static void ResolveExpectedButtonColors(ButtonRole role, out Color back, out Color fore, out Color border)
        {
            switch (role)
            {
                case ButtonRole.Danger:
                    back = Color.FromArgb(220, 38, 38);
                    fore = Color.White;
                    border = back;
                    return;
                case ButtonRole.Neutral:
                    back = Color.FromArgb(249, 250, 251);
                    fore = Color.FromArgb(55, 65, 81);
                    border = Color.FromArgb(209, 213, 219);
                    return;
                case ButtonRole.Secondary:
                    back = Color.White;
                    fore = Color.FromArgb(17, 24, 39);
                    border = DS.InputBorder;
                    return;
                default:
                    back = Color.FromArgb(37, 99, 235);
                    fore = Color.White;
                    border = back;
                    return;
            }
        }

        private static bool IsCompactUtilityButton(Button button)
        {
            string text = (button == null ? string.Empty : button.Text ?? string.Empty).Trim();
            string metadata = ((button == null ? string.Empty : button.Name ?? string.Empty) + " " +
                               (button == null || button.Tag == null ? string.Empty : button.Tag.ToString()) + " " +
                               (button == null || button.Parent == null ? string.Empty : button.Parent.Name ?? string.Empty) + " " +
                               (button == null || button.Parent == null || button.Parent.Tag == null ? string.Empty : button.Parent.Tag.ToString()))
                .ToUpperInvariant();

            if (metadata.Contains("PAGER") || metadata.Contains("PAGINATION") || metadata.Contains("TAB") ||
                metadata.Contains("CHIP") || metadata.Contains("PIPELINE") || metadata.Contains("STEP") ||
                metadata.Contains("STATUS") || metadata.Contains("BADGE") || metadata.Contains("AVATAR"))
                return true;

            return text.Length <= 3 && button.Width <= 84;
        }

        private static void EnsureSharedTextStyleAndContrast(Control root, string moduleName)
        {
            foreach (Control control in EnumerateControls(root).Where(control => control.Visible))
            {
                string text = (control.Text ?? string.Empty).Trim();
                Button button = control as Button;
                if (!IsTextualControl(control) || string.IsNullOrWhiteSpace(text) || IsIconOnlyText(text) || (button != null && IsContainerButton(button)))
                    continue;

                if (RequiresSegoeUi(control) && !string.Equals(control.Font.FontFamily.Name, "Segoe UI", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(moduleName + " has non-shared UI font on " + Describe(control) + ": " + control.Font.FontFamily.Name);

                Color back = ResolveEffectiveBackColor(control);
                if (back != Color.Empty && back != Color.Transparent && back.GetBrightness() < 0.45f && control.ForeColor.GetBrightness() < 0.55f)
                    throw new InvalidOperationException(moduleName + " has dark text on a dark box: " + Describe(control) + " back=" + back + " fore=" + control.ForeColor);
            }
        }

        private static bool IsTextualControl(Control control)
        {
            return control is Label ||
                   control is Button ||
                   control is CheckBox ||
                   control is RadioButton ||
                   control is GroupBox ||
                   control is TabControl ||
                   control is TabPage;
        }

        private static bool RequiresSegoeUi(Control control)
        {
            if (control == null || control.Font == null)
                return false;

            string family = control.Font.FontFamily.Name;
            return !string.Equals(family, "Consolas", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(family, "Segoe Fluent Icons", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(family, "Segoe MDL2 Assets", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(family, "Marlett", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(family, "Webdings", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(family, "Wingdings", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsIconOnlyText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return true;

            string trimmed = text.Trim();
            return trimmed.Length <= 2 ||
                   trimmed == "..." ||
                   trimmed == "â‹®" ||
                   trimmed == "⋮" ||
                   trimmed == "⋯" ||
                   trimmed == "<" ||
                   trimmed == ">" ||
                   trimmed == "|<" ||
                   trimmed == ">|";
        }

        private static Color ResolveEffectiveBackColor(Control control)
        {
            Control current = control;
            while (current != null)
            {
                Color back = current.BackColor;
                if (back != Color.Empty && back != Color.Transparent)
                    return back;
                current = current.Parent;
            }

            return Color.Empty;
        }

        private static IEnumerable<List<Button>> GroupButtonRows(List<Button> buttons)
        {
            foreach (Button button in buttons.OrderBy(button => button.Top).ThenBy(button => button.Left))
            {
                List<Button> row = buttons
                    .Where(other => Math.Abs((other.Top + other.Height / 2) - (button.Top + button.Height / 2)) <= 8)
                    .OrderBy(other => other.Left)
                    .ToList();

                if (row.Count >= 2 && row[0] == button)
                    yield return row;
            }
        }

        private static void EnsureNoResizeGripsOnEditorFields(Control root, string moduleName)
        {
            foreach (Control grip in EnumerateControls(root).Where(control =>
                string.Equals(control.Name, CardResizeGripService.CornerGripName, StringComparison.Ordinal) ||
                string.Equals(control.Name, CardResizeGripService.HeightGripName, StringComparison.Ordinal)))
            {
                Control parent = grip.Parent;
                if (parent != null && IsEditorFieldContainer(parent))
                    throw new InvalidOperationException(moduleName + " has a dashboard resize grip attached to an editor field container: " + Describe(parent));
            }
        }

        private static void EnsureNoFloatingResetButtons(Control root, string moduleName)
        {
            foreach (Control control in EnumerateControls(root))
            {
                if (string.Equals(control.Name, "__servoerpResetLayoutButton", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(control.Name, "btnResetLayout", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(moduleName + " still has a floating reset layout button.");
            }
        }

        private static void EnsureResizableCardsHaveBothGrips(Control root, string moduleName)
        {
            foreach (Control card in EnumerateControls(root).Where(IsResizableCardSurface))
            {
                if (!CardResizeGripService.HasBothGrips(card))
                    throw new InvalidOperationException(moduleName + " has a resizable card without both corner and bottom height grips: " + Describe(card));
            }
        }

        private static bool IsResizableCardSurface(Control control)
        {
            if (control == null)
                return false;

            if (CardSurfacePolicy.IsResizableCardSurface(control))
                return true;

            return GlobalCardContextMenu.IsAttachedCard(control);
        }

        private static void EnsureVisibleInputOutlines(Control root, string moduleName)
        {
            foreach (Control input in EnumerateControls(root).Where(IsOutlineRequiredInput))
            {
                bool hasHostOutline = HasOutlinedInputHost(input);
                TextBoxBase textBox = input as TextBoxBase;
                if (textBox != null && textBox.BorderStyle == BorderStyle.None && !hasHostOutline)
                    throw new InvalidOperationException(moduleName + " has a TextBox without a visible outline: " + Describe(input));

                NumericUpDown numeric = input as NumericUpDown;
                if (numeric != null && numeric.BorderStyle == BorderStyle.None && !hasHostOutline)
                    throw new InvalidOperationException(moduleName + " has a NumericUpDown without a visible outline: " + Describe(input));

                ComboBox combo = input as ComboBox;
                if (combo != null && combo.FlatStyle == FlatStyle.Flat && !hasHostOutline)
                    throw new InvalidOperationException(moduleName + " has a ComboBox without a visible outline: " + Describe(input));
            }
        }

        private static bool IsOutlineRequiredInput(Control control)
        {
            return control is TextBoxBase ||
                   control is ComboBox ||
                   control is DateTimePicker ||
                   control is NumericUpDown;
        }

        private static bool HasOutlinedInputHost(Control control)
        {
            Control parent = control == null ? null : control.Parent;
            while (parent != null)
            {
                if (InputOutlineService.IsOutlinedInputHost(parent))
                    return true;
                if (parent is Form || parent is UserControl || parent is TabPage)
                    return false;
                parent = parent.Parent;
            }

            return false;
        }

        private static bool IsEditorFieldContainer(Control control)
        {
            if (control == null)
                return false;

            if (GlobalCardContextMenu.IsAttachedCard(control) || CardSurfacePolicy.IsDashboardLayoutCard(control))
                return false;

            if (CountDescendants(control, c => c.GetType().Name.IndexOf("Chart", StringComparison.OrdinalIgnoreCase) >= 0) > 0)
                return false;

            string labels = string.Join(" ", control.Controls.OfType<Label>().Select(label => label.Text ?? string.Empty)).ToUpperInvariant();
            if (ContainsAny(labels, "TREND", "KPI", "VALUE", "AGING", "SUMMARY", "DASHBOARD"))
                return false;

            int inputCount = CountDescendants(control, IsEditableInput);
            if (inputCount == 0)
                return false;

            bool compact = control.Height <= 190;
            bool mostlyInputs = inputCount >= CountDescendants(control, c => c is Button || c is DataGridView || c is ListView);
            bool hasFieldLabel = control.Controls.OfType<Label>().Any(label => !string.IsNullOrWhiteSpace(label.Text) && label.Text.Length <= 80);
            return compact || (hasFieldLabel && mostlyInputs);
        }

        private static bool ContainsAny(string text, params string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(text) || tokens == null)
                return false;

            return tokens.Any(token => text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsEditableInput(Control control)
        {
            return control is TextBoxBase ||
                   control is ComboBox ||
                   control is DateTimePicker ||
                   control is NumericUpDown ||
                   control is CheckBox ||
                   control is RadioButton;
        }

        private static int CountDescendants(Control control, Func<Control, bool> predicate)
        {
            if (control == null || predicate == null)
                return 0;

            int count = 0;
            foreach (Control child in control.Controls)
            {
                if (predicate(child))
                    count++;
                count += CountDescendants(child, predicate);
            }

            return count;
        }

        private static string Describe(Control control)
        {
            string labels = string.Join(" | ", control.Controls.OfType<Label>().Select(label => label.Text).Where(text => !string.IsNullOrWhiteSpace(text)).Take(3));
            return control.GetType().Name +
                   " " + (string.IsNullOrWhiteSpace(control.Name) ? "(unnamed)" : control.Name) +
                   " bounds=" + control.Bounds +
                   " tag=" + Convert.ToString(control.Tag) +
                   " labels=" + labels;
        }

        private static IEnumerable<Control> EnumerateControls(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (Control descendant in EnumerateControls(child))
                    yield return descendant;
            }
        }

        private static bool HasClickHandler(Button button)
        {
            // WinForms keeps event handlers in a private event list. Reflection is used only in this diagnostic test path.
            object eventClick = typeof(Control)
                .GetField("EventClick", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(null);
            object events = typeof(Component)
                .GetProperty("Events", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(button, null);
            if (eventClick == null || events == null)
                return true;

            Delegate handler = events.GetType()
                .GetProperty("Item", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(events, new[] { eventClick }) as Delegate;
            return handler != null;
        }

        private static bool IsContainerButton(Button button)
        {
            string text = (button.Text ?? string.Empty).Trim();
            if (text.Length <= 2)
                return true;
            if (text == "<" || text == ">" || text == "|<" || text == ">|")
                return true;
            return text.Length == 0 || text == "..." || text == "⋮";
        }
    }
}

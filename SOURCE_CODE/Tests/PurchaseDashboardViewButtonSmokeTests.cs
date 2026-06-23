using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.Services.Licensing;
using HVAC_Pro_Desktop.UI;

namespace HVAC_Pro_Desktop.Tests
{
    public static class PurchaseDashboardViewButtonSmokeTests
    {
        public static List<string> RunAll()
        {
            AppUserDto previousUser = SessionManager.CurrentUser;
            Guid? previousSessionId = SessionManager.CurrentSessionId;
            DateTime? previousExpiry = SessionManager.ExpiresAt;

            var results = new List<string>();

            try
            {
                EnsureQaLicense();
                SessionManager.SetSession(new AppUserDto
                {
                    UserId = 0,
                    Username = "qa-po-view-buttons",
                    DisplayName = "ServoERP QA",
                    RoleName = "Administrator",
                    IsActive = true
                }, Guid.NewGuid(), DateTime.Now.AddHours(1));

                using (var form = new PurchaseForm())
                {
                    form.Size = new Size(1600, 980);
                    form.CreateControl();
                    form.PerformLayout();
                    Application.DoEvents();

                    InvokeAsyncTask(form, "RefreshPurchaseDashboardFromHeaderAsync");
                    Application.DoEvents();

                    List<PurchaseOrder> rows = InvokeMethod(form, "GetFilteredPos") as List<PurchaseOrder>;
                    Assert(rows != null && rows.Count >= 3, "Not enough purchase orders are available to verify recent PO view buttons.");

                    var table = GetField<TableLayoutPanel>(form, "_poTable");
                    Assert(table != null, "Recent Purchase Orders table was not created.");

                    var viewButtons = table.Controls
                        .OfType<Button>()
                        .Where(button => button.Visible && button.Enabled && string.Equals((button.Text ?? string.Empty).Trim(), "View", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(button => table.GetRow(button))
                        .ToList();

                    Assert(viewButtons.Count >= 3, "Expected at least 3 visible View buttons in the recent purchase order table.");

                    var candidates = rows
                        .Select((po, index) => new { PurchaseOrder = po, Index = index })
                        .Where(item => string.IsNullOrWhiteSpace(item.PurchaseOrder.PdfPath))
                        .Take(3)
                        .ToList();

                    Assert(candidates.Count >= 3, "Expected at least 3 recent purchase orders without a stored PdfPath for View-button verification.");

                    foreach (var candidate in candidates)
                    {
                        PurchaseOrder expected = candidate.PurchaseOrder;
                        string poNumber = expected.PONumber ?? string.Empty;
                        string expectedTitle = "Purchase Order Preview - " + (string.IsNullOrWhiteSpace(poNumber) ? "(draft)" : poNumber);

                        viewButtons[candidate.Index].PerformClick();
                        Application.DoEvents();

                        Form preview = null;
                        WaitFor(() =>
                        {
                            preview = Application.OpenForms
                                .OfType<Form>()
                                .FirstOrDefault(open =>
                                    open != null &&
                                    !open.IsDisposed &&
                                    !ReferenceEquals(open, form) &&
                                    string.Equals(open.Text ?? string.Empty, expectedTitle, StringComparison.OrdinalIgnoreCase));
                            return preview != null;
                        },
                        8000,
                        "View button click did not open the purchase preview window for " + (string.IsNullOrWhiteSpace(poNumber) ? ("PO #" + expected.POID) : poNumber) + ".");

                        if (preview != null && !preview.IsDisposed)
                        {
                            preview.Close();
                            Application.DoEvents();
                        }

                        results.Add("PASS recent PO View button opened preview for " + (string.IsNullOrWhiteSpace(poNumber) ? ("PO #" + expected.POID) : poNumber));
                    }
                }
            }
            catch (Exception ex)
            {
                results.Add("FAIL purchase dashboard view button smoke | " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                SessionManager.SetSession(previousUser, previousSessionId, previousExpiry);
            }

            return results;
        }

        private static void EnsureQaLicense()
        {
            var licenseService = new LicenseService();
            LicenseValidationResult current = licenseService.ValidateCurrentLicense();
            if (current != null && current.Success && !current.IsFrozen)
                return;

            LicenseValidationResult trial = licenseService.ActivateTrial("ServoERP QA Purchase View Buttons");
            if (trial == null || !trial.Success || trial.IsFrozen)
                throw new InvalidOperationException("QA purchase-view-buttons license activation failed: " + (trial == null ? "no response" : trial.Message));
        }

        private static void InvokeAsyncTask(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(method != null, "Missing method: " + methodName);

            Task task = method.Invoke(target, null) as Task;
            Assert(task != null, methodName + " did not return a Task.");
            task.GetAwaiter().GetResult();
        }

        private static object InvokeMethod(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(method != null, "Missing method: " + methodName);
            return method.Invoke(target, null);
        }

        private static T GetField<T>(object target, string fieldName) where T : class
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(target) as T;
        }

        private static void WaitFor(Func<bool> condition, int timeoutMs, string failureMessage)
        {
            DateTime deadline = DateTime.Now.AddMilliseconds(timeoutMs);
            while (DateTime.Now < deadline)
            {
                if (condition())
                    return;

                Application.DoEvents();
                System.Threading.Thread.Sleep(100);
            }

            throw new InvalidOperationException(failureMessage);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}

using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.UI;

namespace HVAC_Pro_Desktop.Tests
{
    public static class ModuleDashboardNavigationSmokeTests
    {
        public static string RunAll()
        {
            using (var dashboard = new DashboardForm())
            {
                InvokePrivate(dashboard, "BuildShell");
                Button notifications = FindControl<Button>(dashboard, "btnDashboardNotifications");
                Panel alerts = FindControl<Panel>(dashboard, "pnlDashboardAlertsNotifications");
                if (notifications == null || notifications.AccessibleName != "Open alerts and notifications")
                    throw new InvalidOperationException("Dashboard notification icon should be globally accessible from the top bar.");
                if (alerts == null || alerts.Cursor != Cursors.Hand)
                    throw new InvalidOperationException("Dashboard alerts strip should open the notification center.");

                NotificationCenterService service = new NotificationCenterService();
                MethodInfo countMethod = service.GetType().GetMethod("GetActiveCount", BindingFlags.Instance | BindingFlags.Public);
                if (countMethod == null)
                    throw new InvalidOperationException("Notification service should expose a global active count for dashboard badges.");
            }

            using (var notificationDialog = new NotificationCenterDialog(pageKey => { }))
            {
                notificationDialog.Show();
                notificationDialog.PerformLayout();
                Application.DoEvents();
                DataGridView grid = FindControl<DataGridView>(notificationDialog, null);
                VScrollBar scrollBar = FindControl<VScrollBar>(notificationDialog, "vScrollNotificationRows");
                if (grid == null || grid.Columns.Count != 5)
                    throw new InvalidOperationException("Notification center should render as a five-column alert table.");
                if (scrollBar == null || !scrollBar.Visible || scrollBar.Dock != DockStyle.Right)
                    throw new InvalidOperationException("Notification center should show an internal right-side scrollbar.");
                if (grid.Columns[0].HeaderText != "Priority" || grid.Columns[1].HeaderText != "Category" || grid.Columns[2].HeaderText != "Reference / Description")
                    throw new InvalidOperationException("Notification center table headers should match the operational alert layout.");
                if (!HasButton(notificationDialog, "Refresh") || !HasButton(notificationDialog, "Dismiss") || !HasButton(notificationDialog, "Open"))
                    throw new InvalidOperationException("Notification center should expose Refresh, Dismiss, and Open actions.");
            }

            using (var supplierDialog = new SupplierPriceComparisonDialog("Copper Pipe", "Materials", 2m))
            {
                DataGridView supplierGrid = FindControl<DataGridView>(supplierDialog, null);
                if (supplierGrid == null || supplierGrid.Columns.Count != 7)
                    throw new InvalidOperationException("Supplier price comparison should render a ranked seven-column supplier grid.");
                if (!HasButton(supplierDialog, "Use Supplier"))
                    throw new InvalidOperationException("Supplier price comparison should expose a Use Supplier action.");
            }

            AssertPrivateMethod(typeof(PurchaseForm), "ShowSupplierComparisonForLineItem", "Purchase line items should expose supplier price comparison.");
            AssertPrivateMethod(typeof(PurchaseForm), "SelectPurchaseVendorById", "Purchase supplier comparison should apply the selected supplier.");

            using (var invoices = new InvoiceForm())
            {
                invoices.PerformLayout();
                AssertVisibility(invoices, "_invoiceDashboardPanel", true, "Invoice page should open on the invoice dashboard.");
                AssertVisibility(invoices, "_invoiceWorkspacePanel", false, "Invoice editor should be hidden until New Invoice is clicked.");
                AssertPrivateMethod(invoices, "BtnNew_Click", "Invoice New handler should exist for the dashboard action.");
            }

            using (var quotations = new TenderBidForm())
            {
                quotations.PerformLayout();
                AssertVisibility(quotations, "_quotationDashboardPanel", true, "Quotation page should open on the quotation dashboard.");
                AssertVisibility(quotations, "_quotationWorkspacePanel", false, "Quotation editor should be hidden until New Quote is clicked.");
                AssertVisibility(quotations, "_btnBackToQuoteDashboard", false, "Back to Dashboard should be hidden while the quotation dashboard is visible.");
                AssertPrivateMethod(quotations, "NewRecord", "Quotation New handler should exist for the dashboard action.");
                AssertQuotationEditorLayout(quotations);
                AssertPrivateMethod(quotations, "ShowQuotationDashboard", "Quotation dashboard return handler should exist.");
            }

            return "module dashboard navigation opens dashboards first and routes New actions to existing forms";
        }

        private static T FindControl<T>(Control root, string name) where T : Control
        {
            if (root == null)
                return null;

            foreach (Control child in root.Controls)
            {
                if (child is T && (name == null || child.Name == name))
                    return (T)child;

                T nested = FindControl<T>(child, name);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        private static bool HasButton(Control root, string text)
        {
            if (root == null)
                return false;

            foreach (Control child in root.Controls)
            {
                Button button = child as Button;
                if (button != null && button.Text.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                if (HasButton(child, text))
                    return true;
            }

            return false;
        }

        private static void AssertVisibility(Control owner, string fieldName, bool expected, string message)
        {
            Control control = GetField<Control>(owner, fieldName);
            if (control == null || control.Visible != expected)
                throw new InvalidOperationException(message);
        }

        private static void InvokePrivate(object owner, string methodName)
        {
            MethodInfo method = owner.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
                throw new InvalidOperationException("Missing method: " + methodName);
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 0)
            {
                method.Invoke(owner, null);
                return;
            }

            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(bool))
            {
                method.Invoke(owner, new object[] { true });
                return;
            }

            if (parameters.Length == 2)
            {
                method.Invoke(owner, new object[] { null, EventArgs.Empty });
                return;
            }

            throw new InvalidOperationException("Unsupported method signature: " + methodName);
        }

        private static void AssertPrivateMethod(object owner, string methodName, string message)
        {
            MethodInfo method = owner.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
                throw new InvalidOperationException(message);
        }

        private static void AssertPrivateMethod(Type ownerType, string methodName, string message)
        {
            MethodInfo method = ownerType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
                throw new InvalidOperationException(message);
        }

        private static T GetField<T>(object owner, string fieldName) where T : class
        {
            FieldInfo field = owner.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(owner) as T;
        }

        private static void AssertQuotationEditorLayout(TenderBidForm form)
        {
            Label emptyState = GetField<Label>(form, "_lblLineItemsEmptyState");
            DataGridView grid = GetField<DataGridView>(form, "_grid");
            NumericUpDown discount = GetField<NumericUpDown>(form, "_numDiscount");
            Label margin = GetField<Label>(form, "_lblMargin");
            if (emptyState == null || grid == null || grid.Parent == null || discount == null || margin == null)
                throw new InvalidOperationException("Quotation editor layout controls are missing.");

            if (emptyState.Bottom > grid.Parent.ClientSize.Height - 12)
                throw new InvalidOperationException("Quotation empty-item text should stay inside the line-item grid host.");

            if (discount.Parent == null || discount.Right > discount.Parent.ClientSize.Width - 16)
                throw new InvalidOperationException("Quotation discount input should fit inside the summary card.");

            if (margin.Parent == null || !margin.Parent.Controls.OfType<Label>().Any(label => label.Text == "Quote Summary"))
                throw new InvalidOperationException("Quotation summary heading should not be overwritten by metric updates.");
        }
    }
}

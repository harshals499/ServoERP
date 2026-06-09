using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using HVAC_Pro_Desktop.UI;

namespace HVAC_Pro_Desktop.Tests
{
    public static class ModuleDashboardNavigationSmokeTests
    {
        public static string RunAll()
        {
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

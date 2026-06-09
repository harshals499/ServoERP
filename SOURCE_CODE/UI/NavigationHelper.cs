using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    internal static class NavigationHelper
    {
        /// <summary>
        /// Opens a business record from any visible app surface.
        /// </summary>
        public static void OpenRecord(Control source, string recordType, int recordId)
        {
            OpenRecord(source, recordType, recordId, null);
        }

        /// <summary>
        /// Opens a business record and falls back to the supplied module page when exact detail routing is unavailable.
        /// </summary>
        public static void OpenRecord(Control source, string recordType, int recordId, string pageKey)
        {
            if (recordId <= 0 && !IsFileRecord(recordType))
                return;

            try
            {
                MainForm main = ResolveMainForm(source);
                string normalizedType = Normalize(recordType);
                string targetPage = string.IsNullOrWhiteSpace(pageKey) ? PageKeyFor(recordType) : pageKey;

                if (main != null)
                {
                    if (normalizedType == "CLIENT" || normalizedType == "B2BCLIENT" || normalizedType == "CUSTOMER")
                    {
                        main.NavigateToClientDetail(recordId);
                        return;
                    }

                    if (normalizedType == "SITE" || normalizedType == "CLIENTSITE")
                    {
                        main.NavigateToClientSite(recordId);
                        return;
                    }

                    if (normalizedType == "JOB" || normalizedType == "JOBS" || normalizedType == "WORKORDER" || normalizedType == "WORKORDERS")
                    {
                        main.NavigateToJobDetail(recordId);
                        return;
                    }

                    if (!string.IsNullOrWhiteSpace(targetPage))
                    {
                        main.NavigateTo(targetPage);
                        return;
                    }
                }

                if (!string.IsNullOrWhiteSpace(targetPage))
                    MessageBox.Show(source, "Open " + targetPage + " from the main menu to view this record.", BrandingService.WindowTitle("Navigation"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Navigation"), "Opening record", ex);
            }
        }

        /// <summary>
        /// Opens a global search result at the most specific app location available.
        /// </summary>
        public static void OpenSearchResult(Control source, GlobalSearchResult result, Action<string> fallbackNavigate)
        {
            if (result == null)
                return;

            OpenRecordOrFallback(source, result.Module, result.RecordId, result.PageKey, fallbackNavigate);
        }

        /// <summary>
        /// Opens a notification target at the most specific app location available.
        /// </summary>
        public static void OpenNotification(Control source, FoundationNotification notification, Action<string> fallbackNavigate)
        {
            if (notification == null)
                return;

            OpenRecordOrFallback(source, notification.Module, notification.RecordId.GetValueOrDefault(), notification.PageKey, fallbackNavigate);
        }

        /// <summary>
        /// Opens a site timeline item at the most specific app location available.
        /// </summary>
        public static void OpenTimelineItem(Control source, SiteTimelineItem item)
        {
            if (item == null || !item.RecordId.HasValue)
                return;

            OpenRecord(source, item.EventType, item.RecordId.Value, item.PageKey);
        }

        /// <summary>
        /// Opens a file path using the user's default Windows application.
        /// </summary>
        public static void OpenFile(Control source, string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                {
                    MessageBox.Show(source, "The linked file was not found. It may have been moved or deleted.", BrandingService.WindowTitle("Documents"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Documents"), "Opening file", ex);
            }
        }

        /// <summary>
        /// Applies app-standard clickable styling and a click handler to an existing control.
        /// </summary>
        public static void MakeLink(Control control, Action open)
        {
            if (control == null || open == null)
                return;

            control.Cursor = Cursors.Hand;
            Label label = control as Label;
            if (label != null)
            {
                label.ForeColor = DS.Primary600;
                Font normal = label.Font;
                Font underline = new Font(normal, normal.Style | FontStyle.Underline);
                label.MouseEnter += (s, e) => label.Font = underline;
                label.MouseLeave += (s, e) => label.Font = normal;
            }

            control.Click += (s, e) =>
            {
                try
                {
                    open();
                }
                catch (Exception ex)
                {
                    AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Navigation"), "Opening linked item", ex);
                }
            };
        }

        private static void OpenRecordOrFallback(Control source, string recordType, int recordId, string pageKey, Action<string> fallbackNavigate)
        {
            MainForm main = ResolveMainForm(source);
            if (main != null)
            {
                OpenRecord(source, recordType, recordId, pageKey);
                return;
            }

            if (!string.IsNullOrWhiteSpace(pageKey))
                fallbackNavigate?.Invoke(pageKey);
        }

        private static MainForm ResolveMainForm(Control source)
        {
            MainForm fromSource = source == null ? null : source.FindForm() as MainForm;
            return fromSource ?? Application.OpenForms.OfType<MainForm>().FirstOrDefault();
        }

        private static bool IsFileRecord(string recordType)
        {
            string normalized = Normalize(recordType);
            return normalized == "PDF" || normalized == "FILE" || normalized == "DOCUMENT";
        }

        private static string PageKeyFor(string recordType)
        {
            string normalized = Normalize(recordType);
            if (normalized == "INVOICE" || normalized == "INVOICES") return "Invoices";
            if (normalized == "PAYMENT" || normalized == "PAYMENTS" || normalized == "VENDORADVANCES") return "Payments";
            if (normalized == "PURCHASE" || normalized == "PURCHASES" || normalized == "PURCHASEORDER" || normalized == "PURCHASEORDERS") return "Purchases";
            if (normalized == "QUOTATION" || normalized == "QUOTATIONS" || normalized == "TENDERBID" || normalized == "APPROVALS") return "Quotations";
            if (normalized == "VENDOR" || normalized == "VENDORS") return "Vendors";
            if (normalized == "EMPLOYEE" || normalized == "EMPLOYEES" || normalized == "TECHNICIAN" || normalized == "TECHNICIANS") return "Employees";
            if (normalized == "INVENTORY" || normalized == "STOCK" || normalized == "STOCKITEM") return "Inventory";
            if (normalized == "CONTRACT" || normalized == "CONTRACTS" || normalized == "AMCCONTRACT") return "Contracts";
            if (normalized == "SERVICEDESK" || normalized == "SERVICEDESKINCIDENT" || normalized == "TICKET" || normalized == "TICKETS") return "ServiceDesk";
            if (normalized == "CLIENT" || normalized == "B2BCLIENT" || normalized == "CUSTOMER") return "Clients";
            if (normalized == "SITE" || normalized == "CLIENTSITE") return "Clients";
            if (normalized == "JOB" || normalized == "JOBS" || normalized == "WORKORDER" || normalized == "WORKORDERS" || normalized == "TECHNICIANDELAYS") return "Jobs";
            return recordType;
        }

        private static string Normalize(string value)
        {
            return new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        }
    }
}

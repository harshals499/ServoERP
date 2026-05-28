using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services.Integrations
{
    public sealed class TallyPrimeIntegrationService
    {
        private const string Provider = "TallyPrime";

        public bool IsEnabled => IntegrationConfig.GetBool("TallyPrime", "Enabled", false);

        public string EndpointUrl => IntegrationConfig.Get("TallyPrime", "EndpointUrl", "http://localhost:9000");

        public string ExportFolder => IntegrationConfig.Get(
            "TallyPrime",
            "ExportFolder",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ServoERP", "TallyExports"));

        public IntegrationOperationResult ExportInvoiceXml(Invoice invoice)
        {
            if (invoice == null)
                return IntegrationOperationResult.Fail(Provider, "ExportInvoiceXml", "Invoice is required.");

            try
            {
                Directory.CreateDirectory(ExportFolder);
                string path = Path.Combine(ExportFolder, SafeFileName("Invoice-" + invoice.InvoiceNumber) + ".xml");
                File.WriteAllText(path, BuildInvoiceVoucherXml(invoice), Encoding.UTF8);

                var result = IntegrationOperationResult.Ok(Provider, "ExportInvoiceXml", "Invoice voucher XML exported.");
                result.LocalPath = path;
                result.ReferenceId = invoice.InvoiceNumber;
                return result;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("TallyPrimeIntegrationService.ExportInvoiceXml", ex);
                return IntegrationOperationResult.Fail(Provider, "ExportInvoiceXml", ex.Message);
            }
        }

        public IntegrationOperationResult ExportPurchaseXml(PurchaseOrder purchaseOrder)
        {
            if (purchaseOrder == null)
                return IntegrationOperationResult.Fail(Provider, "ExportPurchaseXml", "Purchase order is required.");

            try
            {
                Directory.CreateDirectory(ExportFolder);
                string path = Path.Combine(ExportFolder, SafeFileName("Purchase-" + purchaseOrder.PONumber) + ".xml");
                File.WriteAllText(path, BuildPurchaseVoucherXml(purchaseOrder), Encoding.UTF8);

                var result = IntegrationOperationResult.Ok(Provider, "ExportPurchaseXml", "Purchase voucher XML exported.");
                result.LocalPath = path;
                result.ReferenceId = purchaseOrder.PONumber;
                return result;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("TallyPrimeIntegrationService.ExportPurchaseXml", ex);
                return IntegrationOperationResult.Fail(Provider, "ExportPurchaseXml", ex.Message);
            }
        }

        public Task<IntegrationOperationResult> PushInvoiceAsync(Invoice invoice, CancellationToken cancellationToken)
        {
            if (invoice == null)
                return Task.FromResult(IntegrationOperationResult.Fail(Provider, "PushInvoice", "Invoice is required."));

            return PostXmlAsync("PushInvoice", invoice.InvoiceNumber, BuildInvoiceVoucherXml(invoice), cancellationToken);
        }

        public Task<IntegrationOperationResult> PushPurchaseAsync(PurchaseOrder purchaseOrder, CancellationToken cancellationToken)
        {
            if (purchaseOrder == null)
                return Task.FromResult(IntegrationOperationResult.Fail(Provider, "PushPurchase", "Purchase order is required."));

            return PostXmlAsync("PushPurchase", purchaseOrder.PONumber, BuildPurchaseVoucherXml(purchaseOrder), cancellationToken);
        }

        private async Task<IntegrationOperationResult> PostXmlAsync(string operation, string referenceId, string xml, CancellationToken cancellationToken)
        {
            if (!IsEnabled)
                return IntegrationOperationResult.Fail(Provider, operation, "TallyPrime integration is disabled.");

            if (string.IsNullOrWhiteSpace(EndpointUrl))
                return IntegrationOperationResult.Fail(Provider, operation, "TallyPrime endpoint URL is not configured.");

            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
                using (var content = new StringContent(xml, Encoding.UTF8, "application/xml"))
                using (HttpResponseMessage response = await client.PostAsync(EndpointUrl, content, cancellationToken).ConfigureAwait(false))
                {
                    string raw = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var result = response.IsSuccessStatusCode
                        ? IntegrationOperationResult.Ok(Provider, operation, "TallyPrime accepted the voucher request.")
                        : IntegrationOperationResult.Fail(Provider, operation, "TallyPrime returned HTTP " + (int)response.StatusCode + ".");
                    result.ReferenceId = referenceId;
                    result.RawResponse = raw;
                    return result;
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("TallyPrimeIntegrationService." + operation, ex);
                return IntegrationOperationResult.Fail(Provider, operation, ex.Message);
            }
        }

        private static string BuildInvoiceVoucherXml(Invoice invoice)
        {
            string date = invoice.InvoiceDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            string number = Escape(invoice.InvoiceNumber);
            string client = Escape(string.IsNullOrWhiteSpace(invoice.ClientName) ? "Customer" : invoice.ClientName);
            string salesLedger = Escape(IntegrationConfig.Get("TallyPrime", "SalesLedgerName", "Sales"));
            string gstLedger = Escape(IntegrationConfig.Get("TallyPrime", "GstLedgerName", "GST Output"));

            var xml = new StringBuilder();
            xml.AppendLine("<ENVELOPE>");
            xml.AppendLine("  <HEADER><TALLYREQUEST>Import Data</TALLYREQUEST></HEADER>");
            xml.AppendLine("  <BODY><IMPORTDATA><REQUESTDESC><REPORTNAME>Vouchers</REPORTNAME></REQUESTDESC><REQUESTDATA>");
            xml.AppendLine("    <TALLYMESSAGE xmlns:UDF=\"TallyUDF\">");
            xml.AppendLine("      <VOUCHER VCHTYPE=\"Sales\" ACTION=\"Create\">");
            xml.AppendLine("        <DATE>" + date + "</DATE>");
            xml.AppendLine("        <VOUCHERTYPENAME>Sales</VOUCHERTYPENAME>");
            xml.AppendLine("        <VOUCHERNUMBER>" + number + "</VOUCHERNUMBER>");
            xml.AppendLine("        <PARTYLEDGERNAME>" + client + "</PARTYLEDGERNAME>");
            xml.AppendLine("        <PERSISTEDVIEW>Invoice Voucher View</PERSISTEDVIEW>");
            xml.AppendLine(BuildLedgerEntry(client, invoice.TotalAmount, true));
            xml.AppendLine(BuildLedgerEntry(salesLedger, -invoice.SubTotal, false));
            if (invoice.TaxAmount != 0)
                xml.AppendLine(BuildLedgerEntry(gstLedger, -invoice.TaxAmount, false));
            xml.AppendLine("      </VOUCHER>");
            xml.AppendLine("    </TALLYMESSAGE>");
            xml.AppendLine("  </REQUESTDATA></IMPORTDATA></BODY>");
            xml.AppendLine("</ENVELOPE>");
            return xml.ToString();
        }

        private static string BuildPurchaseVoucherXml(PurchaseOrder po)
        {
            string date = po.PODate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            string number = Escape(po.PONumber);
            string vendor = Escape(string.IsNullOrWhiteSpace(po.VendorName) ? "Vendor" : po.VendorName);
            string purchaseLedger = Escape(IntegrationConfig.Get("TallyPrime", "PurchaseLedgerName", "Purchase"));

            var xml = new StringBuilder();
            xml.AppendLine("<ENVELOPE>");
            xml.AppendLine("  <HEADER><TALLYREQUEST>Import Data</TALLYREQUEST></HEADER>");
            xml.AppendLine("  <BODY><IMPORTDATA><REQUESTDESC><REPORTNAME>Vouchers</REPORTNAME></REQUESTDESC><REQUESTDATA>");
            xml.AppendLine("    <TALLYMESSAGE xmlns:UDF=\"TallyUDF\">");
            xml.AppendLine("      <VOUCHER VCHTYPE=\"Purchase\" ACTION=\"Create\">");
            xml.AppendLine("        <DATE>" + date + "</DATE>");
            xml.AppendLine("        <VOUCHERTYPENAME>Purchase</VOUCHERTYPENAME>");
            xml.AppendLine("        <VOUCHERNUMBER>" + number + "</VOUCHERNUMBER>");
            xml.AppendLine("        <PARTYLEDGERNAME>" + vendor + "</PARTYLEDGERNAME>");
            xml.AppendLine(BuildLedgerEntry(vendor, -po.TotalAmount, false));
            xml.AppendLine(BuildLedgerEntry(purchaseLedger, po.TotalAmount, true));
            xml.AppendLine("      </VOUCHER>");
            xml.AppendLine("    </TALLYMESSAGE>");
            xml.AppendLine("  </REQUESTDATA></IMPORTDATA></BODY>");
            xml.AppendLine("</ENVELOPE>");
            return xml.ToString();
        }

        private static string BuildLedgerEntry(string ledgerName, decimal amount, bool isDebit)
        {
            string amt = amount.ToString("0.00", CultureInfo.InvariantCulture);
            return "        <ALLLEDGERENTRIES.LIST><LEDGERNAME>" + ledgerName + "</LEDGERNAME><ISDEEMEDPOSITIVE>" +
                   (isDebit ? "Yes" : "No") + "</ISDEEMEDPOSITIVE><AMOUNT>" + amt + "</AMOUNT></ALLLEDGERENTRIES.LIST>";
        }

        private static string Escape(string value)
        {
            return SecurityElement.Escape(value ?? string.Empty) ?? string.Empty;
        }

        private static string SafeFileName(string value)
        {
            string text = string.IsNullOrWhiteSpace(value) ? "export" : value.Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
                text = text.Replace(c, '-');
            return text;
        }
    }
}

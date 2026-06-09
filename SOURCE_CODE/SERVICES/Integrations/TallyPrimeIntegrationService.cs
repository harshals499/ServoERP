using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security;
using System.Xml;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services.Integrations
{
    public sealed class TallyPrimeIntegrationService
    {
        private const string Provider = "TallyPrime";
        private readonly TallyIntegrationRepository _repo = new TallyIntegrationRepository();
        private readonly InvoiceService _invoiceService = new InvoiceService();
        private readonly PaymentService _paymentService = new PaymentService();
        private readonly PurchaseService _purchaseService = new PurchaseService();
        private readonly InventoryService _inventoryService = new InventoryService();
        private readonly TallyXmlBuilder _builder = new TallyXmlBuilder();
        private readonly TallyXmlParser _parser = new TallyXmlParser();

        public bool IsEnabled => IntegrationConfig.GetBool("TallyPrime", "Enabled", false);

        public string EndpointUrl => IntegrationConfig.Get("TallyPrime", "EndpointUrl", "http://localhost:9000");

        public string ExportFolder => IntegrationConfig.Get(
            "TallyPrime",
            "ExportFolder",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ServoERP", "TallyExports"));

        public TallyConnectionSettings GetSettings()
        {
            return new TallyConnectionSettings
            {
                Enabled = IsEnabled,
                EndpointUrl = EndpointUrl,
                ExportFolder = ExportFolder,
                CompanyName = IntegrationConfig.Get("TallyPrime", "CompanyName", string.Empty),
                DefaultGodownName = IntegrationConfig.Get("TallyPrime", "DefaultGodownName", "Main Location")
            };
        }

        public void SaveSettings(TallyConnectionSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            IntegrationConfig.Set("TallyPrime", "Enabled", settings.Enabled ? "true" : "false");
            IntegrationConfig.Set("TallyPrime", "EndpointUrl", string.IsNullOrWhiteSpace(settings.EndpointUrl) ? "http://localhost:9000" : settings.EndpointUrl.Trim());
            IntegrationConfig.Set("TallyPrime", "ExportFolder", settings.ExportFolder ?? string.Empty);
            IntegrationConfig.Set("TallyPrime", "CompanyName", settings.CompanyName ?? string.Empty);
            IntegrationConfig.Set("TallyPrime", "DefaultGodownName", string.IsNullOrWhiteSpace(settings.DefaultGodownName) ? "Main Location" : settings.DefaultGodownName.Trim());
        }

        public IntegrationOperationResult TestConnection()
        {
            try
            {
                string response = PostXml(BuildCompanyExportRequest());
                IntegrationOperationResult result = IntegrationOperationResult.Ok(Provider, "TestConnection", "Tally responded successfully.");
                result.RawResponse = response;
                return result;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("TallyPrimeIntegrationService.TestConnection", ex);
                return IntegrationOperationResult.Fail(Provider, "TestConnection", ex.Message);
            }
        }

        public List<TallyLedgerMapping> GetLedgerMappings()
        {
            return _repo.GetLedgerMappings();
        }

        public void SaveLedgerMapping(TallyLedgerMapping mapping)
        {
            _repo.SaveLedgerMapping(mapping);
        }

        public List<TallyExportCandidate> GetInvoiceCandidates(DateTime fromDate, DateTime toDate, bool includeExported)
        {
            return _repo.GetInvoiceCandidates(fromDate, toDate, includeExported);
        }

        public List<TallyExportCandidate> GetPaymentCandidates(DateTime fromDate, DateTime toDate, bool includeExported)
        {
            return _repo.GetPaymentCandidates(fromDate, toDate, includeExported);
        }

        public List<TallyExportCandidate> GetPurchaseCandidates(DateTime fromDate, DateTime toDate, bool includeExported)
        {
            return _repo.GetPurchaseCandidates(fromDate, toDate, includeExported);
        }

        public DataTable GetStockItemsForSync()
        {
            return _repo.GetStockItemsForSync();
        }

        public List<TallySyncLogEntry> GetRecentLogs(int maxRows)
        {
            return _repo.GetRecentLogs(maxRows);
        }

        public TallyBatchResult ExportInvoices(IEnumerable<int> invoiceIds, bool directPush)
        {
            var result = new TallyBatchResult { OutputFolder = EnsureExportFolder("Invoices") };
            foreach (int id in invoiceIds ?? new int[0])
            {
                try
                {
                    Invoice invoice = _invoiceService.GetInvoiceById(id);
                    if (invoice == null)
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    string party = ResolvePartyLedger("Client", invoice.ClientName);
                    string xml = _builder.BuildSalesVoucher(
                        invoice,
                        party,
                        _repo.ResolveLedger("Sales", string.Empty, IntegrationConfig.Get("TallyPrime", "SalesLedgerName", "Sales")),
                        _repo.ResolveLedger("CGSTOutput", string.Empty, "Output CGST"),
                        _repo.ResolveLedger("SGSTOutput", string.Empty, "Output SGST"),
                        _repo.ResolveLedger("IGSTOutput", string.Empty, "Output IGST"));
                    ExportXml("Invoice", invoice.InvoiceID, invoice.InvoiceNumber, xml, directPush, result);
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    result.Messages.Add("Invoice #" + id + ": " + ex.Message);
                    _repo.MarkEntityExportResult("Invoice", id, false, ex.Message, null, null);
                }
            }
            return result;
        }

        public TallyBatchResult ExportPayments(IEnumerable<int> paymentIds, bool directPush)
        {
            var result = new TallyBatchResult { OutputFolder = EnsureExportFolder("Payments") };
            List<Payment> payments = _paymentService.GetAllPayments();
            foreach (int id in paymentIds ?? new int[0])
            {
                try
                {
                    Payment payment = payments.Find(p => p.PaymentID == id);
                    if (payment == null)
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    string bankLedger = _repo.ResolveLedger("PaymentMode", payment.PaymentMode, NormalizePaymentLedger(payment.PaymentMode));
                    string xml = _builder.BuildReceiptVoucher(payment, ResolvePartyLedger("Client", payment.ClientName), bankLedger);
                    ExportXml("Payment", payment.PaymentID, payment.PaymentNumber, xml, directPush, result);
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    result.Messages.Add("Payment #" + id + ": " + ex.Message);
                    _repo.MarkEntityExportResult("Payment", id, false, ex.Message, null, null);
                }
            }
            return result;
        }

        public TallyBatchResult ExportPurchases(IEnumerable<int> purchaseIds, bool directPush)
        {
            var result = new TallyBatchResult { OutputFolder = EnsureExportFolder("Purchases") };
            foreach (int id in purchaseIds ?? new int[0])
            {
                try
                {
                    PurchaseOrder purchase = _purchaseService.GetById(id);
                    if (purchase == null)
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    string xml = _builder.BuildPurchaseVoucher(
                        purchase,
                        ResolvePartyLedger("Vendor", purchase.VendorName),
                        _repo.ResolveLedger("Purchase", string.Empty, IntegrationConfig.Get("TallyPrime", "PurchaseLedgerName", "Purchase")),
                        _repo.ResolveLedger("CGSTInput", string.Empty, "Input CGST"),
                        _repo.ResolveLedger("SGSTInput", string.Empty, "Input SGST"),
                        _repo.ResolveLedger("IGSTInput", string.Empty, "Input IGST"));
                    ExportXml("PurchaseOrder", purchase.POID, purchase.PONumber, xml, directPush, result);
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    result.Messages.Add("Purchase #" + id + ": " + ex.Message);
                    _repo.MarkEntityExportResult("PurchaseOrder", id, false, ex.Message, null, null);
                }
            }
            return result;
        }

        public TallyBatchResult ExportSupplierPayments(IEnumerable<int> purchaseIds, bool directPush)
        {
            var result = new TallyBatchResult { OutputFolder = EnsureExportFolder("SupplierPayments") };
            foreach (int id in purchaseIds ?? new int[0])
            {
                try
                {
                    PurchaseOrder purchase = _purchaseService.GetById(id);
                    if (purchase == null || purchase.PaidAmount <= 0)
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    string bankLedger = _repo.ResolveLedger("PaymentMode", "Bank Transfer", "Bank");
                    string xml = _builder.BuildPaymentVoucher(purchase, ResolvePartyLedger("Vendor", purchase.VendorName), bankLedger);
                    ExportXml("PurchaseOrder", purchase.POID, "PAY-" + purchase.PONumber, xml, directPush, result);
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    result.Messages.Add("Supplier payment #" + id + ": " + ex.Message);
                    _repo.MarkEntityExportResult("PurchaseOrder", id, false, ex.Message, null, null);
                }
            }
            return result;
        }

        public TallyBatchResult SyncStockItems(IEnumerable<int> itemIds, bool directPush)
        {
            var result = new TallyBatchResult { OutputFolder = EnsureExportFolder("StockItems") };
            foreach (int id in itemIds ?? new int[0])
            {
                try
                {
                    DataRow row = _repo.GetStockItemRow(id);
                    if (row == null)
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    StockItem item = _inventoryService.GetById(id);
                    string xml = _builder.BuildStockItemMaster(
                        item,
                        Value(row, "TallyItemName", item.ItemName),
                        Value(row, "HSNCode", string.Empty),
                        Value(row, "TallyStockGroupName", item.Category),
                        Value(row, "TallyUnitName", item.Unit),
                        Value(row, "TallyGodownName", GetSettings().DefaultGodownName));
                    string path = SaveXml(result.OutputFolder, "StockItem-" + id.ToString(CultureInfo.InvariantCulture), xml);
                    if (directPush)
                    {
                        string response = PostXml(xml);
                        bool success = _parser.IsSuccess(response);
                        _repo.MarkStockSyncResult(id, success, _parser.ReadResponseSummary(response), response, path);
                        if (success) result.SuccessCount++; else result.FailedCount++;
                    }
                    else
                    {
                        _repo.MarkStockSyncResult(id, true, "Stock item XML exported for manual Tally import.", null, path);
                        result.SuccessCount++;
                    }
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    result.Messages.Add("Stock item #" + id + ": " + ex.Message);
                    _repo.MarkStockSyncResult(id, false, ex.Message, null, null);
                }
            }
            return result;
        }

        public TallyImportSummary ImportMastersFromXml(string xml, string sourceMode)
        {
            int batchId = _repo.StartImportBatch("Masters", sourceMode);
            var summary = new TallyImportSummary { ImportBatchID = batchId };
            try
            {
                foreach (TallyMasterRecord record in _parser.ParseMasters(xml))
                    _repo.ApplyMasterRecord(record, summary);
                _repo.CompleteImportBatch(batchId, summary);
                _repo.Log("Pull", "Master", null, "Import", "Success", "Imported Tally masters. Created " + summary.CreatedCount + ", updated " + summary.UpdatedCount + ".", null, null);
            }
            catch (Exception ex)
            {
                summary.ErrorCount++;
                summary.Messages.Add(ex.Message);
                _repo.CompleteImportBatch(batchId, summary);
                _repo.Log("Pull", "Master", null, "Import", "Failed", ex.Message, null, null);
            }
            return summary;
        }

        public TallyImportSummary PullMastersFromTally()
        {
            int batchId = _repo.StartImportBatch("Masters", "HTTP");
            var summary = new TallyImportSummary { ImportBatchID = batchId };
            try
            {
                string ledgerResponse = PostXml(_builder.BuildLedgerPullRequest());
                foreach (TallyMasterRecord record in _parser.ParseMasters(ledgerResponse))
                    _repo.ApplyMasterRecord(record, summary);

                string stockResponse = PostXml(_builder.BuildStockItemPullRequest());
                foreach (TallyMasterRecord record in _parser.ParseMasters(stockResponse))
                    _repo.ApplyMasterRecord(record, summary);

                _repo.CompleteImportBatch(batchId, summary);
                _repo.Log("Pull", "Master", null, "HTTPPull", "Success", "Pulled Tally masters. Created " + summary.CreatedCount + ", updated " + summary.UpdatedCount + ".", null, null);
            }
            catch (Exception ex)
            {
                summary.ErrorCount++;
                summary.Messages.Add(ex.Message);
                _repo.CompleteImportBatch(batchId, summary);
                _repo.Log("Pull", "Master", null, "HTTPPull", "Failed", ex.Message, null, null);
            }
            return summary;
        }

        public string AutoMapLocalRecords()
        {
            int clients = _repo.AutoMapClients();
            int vendors = _repo.AutoMapVendors();
            int stock = _repo.AutoMapStockItems();
            string message = "Auto-mapped " + clients + " clients, " + vendors + " vendors, and " + stock + " stock items.";
            _repo.Log("Sync", "Mapping", null, "AutoMap", "Success", message, null, null);
            return message;
        }

        public string BuildReconciliationSummary()
        {
            List<TallySyncLogEntry> logs = _repo.GetRecentLogs(200);
            int success = logs.FindAll(l => string.Equals(l.Status, "Success", StringComparison.OrdinalIgnoreCase)).Count;
            int failed = logs.FindAll(l => string.Equals(l.Status, "Failed", StringComparison.OrdinalIgnoreCase)).Count;
            return "Tally sync summary: " + success + " success, " + failed + " failed, " + logs.Count + " total log rows.";
        }

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

        private void ExportXml(string entityType, int entityId, string reference, string xml, bool directPush, TallyBatchResult result)
        {
            string path = SaveXml(result.OutputFolder, entityType + "-" + reference, xml);
            if (!directPush)
            {
                _repo.MarkEntityExportResult(entityType, entityId, true, "XML exported for manual Tally import.", null, path);
                result.SuccessCount++;
                return;
            }

            string response = PostXml(xml);
            bool success = _parser.IsSuccess(response);
            string summary = _parser.ReadResponseSummary(response);
            _repo.MarkEntityExportResult(entityType, entityId, success, summary, response, path);
            if (success)
                result.SuccessCount++;
            else
                result.FailedCount++;
            result.Messages.Add(reference + ": " + summary);
        }

        private string PostXml(string xml)
        {
            string endpoint = EndpointUrl;
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new InvalidOperationException("Tally endpoint URL is not configured.");

            byte[] bytes = Encoding.UTF8.GetBytes(xml ?? string.Empty);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(endpoint);
            request.Method = "POST";
            request.ContentType = "text/xml; charset=utf-8";
            request.Timeout = 30000;
            request.ContentLength = bytes.Length;
            using (Stream stream = request.GetRequestStream())
                stream.Write(bytes, 0, bytes.Length);
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (StreamReader reader = new StreamReader(response.GetResponseStream() ?? Stream.Null))
                return reader.ReadToEnd();
        }

        private string EnsureExportFolder(string childFolder)
        {
            string root = ExportFolder;
            if (string.IsNullOrWhiteSpace(root))
                root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ServoERP", "TallyExports");
            string folder = Path.Combine(root, childFolder ?? "Exports", DateTime.Today.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static string SaveXml(string folder, string reference, string xml)
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, SafeFileName(reference) + ".xml");
            File.WriteAllText(path, xml ?? string.Empty, Encoding.UTF8);
            return path;
        }

        private string ResolvePartyLedger(string partyType, string partyName)
        {
            if (string.Equals(partyType, "Vendor", StringComparison.OrdinalIgnoreCase))
                return _repo.ResolveLedger("Vendor", partyName, partyName);
            return _repo.ResolveLedger("Client", partyName, partyName);
        }

        private static string NormalizePaymentLedger(string paymentMode)
        {
            string mode = paymentMode ?? string.Empty;
            if (mode.IndexOf("Cash", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Cash";
            if (mode.IndexOf("UPI", StringComparison.OrdinalIgnoreCase) >= 0)
                return "UPI Collections";
            return "Bank";
        }

        private static string BuildCompanyExportRequest()
        {
            return "<ENVELOPE><HEADER><VERSION>1</VERSION><TALLYREQUEST>Export</TALLYREQUEST><TYPE>Data</TYPE><ID>List of Companies</ID></HEADER><BODY><DESC><STATICVARIABLES><SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT></STATICVARIABLES></DESC></BODY></ENVELOPE>";
        }

        private static string Value(DataRow row, string column, string fallback)
        {
            if (row == null || !row.Table.Columns.Contains(column) || row[column] == DBNull.Value)
                return fallback;
            string value = row[column].ToString();
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
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

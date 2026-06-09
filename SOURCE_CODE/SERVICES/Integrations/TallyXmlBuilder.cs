using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security;
using System.Text;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services.Integrations
{
    public sealed class TallyXmlBuilder
    {
        public string BuildSalesVoucher(Invoice invoice, string clientLedgerName, string salesLedgerName, string cgstLedgerName, string sgstLedgerName, string igstLedgerName)
        {
            if (invoice == null)
                throw new ArgumentNullException(nameof(invoice));

            string date = TallyDate(invoice.InvoiceDate);
            string party = Pick(clientLedgerName, invoice.ClientName, "Customer");
            string salesLedger = Pick(salesLedgerName, "Sales");
            StringBuilder voucher = StartVoucher("Sales", "Invoice Voucher View");
            AppendTag(voucher, 12, "DATE", date);
            AppendTag(voucher, 12, "VOUCHERTYPENAME", "Sales");
            AppendTag(voucher, 12, "VOUCHERNUMBER", invoice.InvoiceNumber);
            AppendTag(voucher, 12, "PARTYLEDGERNAME", party);
            AppendTag(voucher, 12, "PERSISTEDVIEW", "Invoice Voucher View");
            AppendTag(voucher, 12, "ISINVOICE", "Yes");
            AppendTag(voucher, 12, "NARRATION", "ServoERP invoice " + invoice.InvoiceNumber);
            AppendLedgerEntry(voucher, party, -Math.Abs(invoice.TotalAmount), true, true, invoice.InvoiceNumber, "New Ref");

            foreach (InvoiceLineItem line in invoice.LineItems ?? new List<InvoiceLineItem>())
                AppendInventoryEntry(voucher, line, salesLedger);

            if (invoice.CGSTAmount != 0)
                AppendLedgerEntry(voucher, Pick(cgstLedgerName, "Output CGST"), Math.Abs(invoice.CGSTAmount), false, false, null, null);
            if (invoice.SGSTAmount != 0)
                AppendLedgerEntry(voucher, Pick(sgstLedgerName, "Output SGST"), Math.Abs(invoice.SGSTAmount), false, false, null, null);
            if (invoice.IGSTAmount != 0)
                AppendLedgerEntry(voucher, Pick(igstLedgerName, "Output IGST"), Math.Abs(invoice.IGSTAmount), false, false, null, null);
            if (invoice.CGSTAmount == 0 && invoice.SGSTAmount == 0 && invoice.IGSTAmount == 0 && invoice.TaxAmount != 0)
                AppendLedgerEntry(voucher, Pick(igstLedgerName, "Output IGST"), Math.Abs(invoice.TaxAmount), false, false, null, null);

            EndVoucher(voucher);
            return Envelope("Vouchers", voucher.ToString());
        }

        public string BuildReceiptVoucher(Payment payment, string clientLedgerName, string bankLedgerName)
        {
            if (payment == null)
                throw new ArgumentNullException(nameof(payment));

            string party = Pick(clientLedgerName, payment.ClientName, "Customer");
            string bank = Pick(bankLedgerName, payment.PaymentMode, "Cash");
            StringBuilder voucher = StartVoucher("Receipt", "Accounting Voucher View");
            AppendTag(voucher, 12, "DATE", TallyDate(payment.PaymentDate));
            AppendTag(voucher, 12, "VOUCHERTYPENAME", "Receipt");
            AppendTag(voucher, 12, "VOUCHERNUMBER", payment.PaymentNumber);
            AppendTag(voucher, 12, "PARTYLEDGERNAME", party);
            AppendTag(voucher, 12, "PERSISTEDVIEW", "Accounting Voucher View");
            AppendTag(voucher, 12, "NARRATION", "ServoERP receipt " + payment.PaymentNumber + " " + payment.ReferenceNumber);
            AppendLedgerEntry(voucher, bank, -Math.Abs(payment.AmountPaid), true, false, null, null);
            AppendLedgerEntry(voucher, party, Math.Abs(payment.AmountPaid), false, true, payment.InvoiceNumber, "Agst Ref");
            EndVoucher(voucher);
            return Envelope("Vouchers", voucher.ToString());
        }

        public string BuildPurchaseVoucher(PurchaseOrder purchaseOrder, string vendorLedgerName, string purchaseLedgerName, string cgstLedgerName, string sgstLedgerName, string igstLedgerName)
        {
            if (purchaseOrder == null)
                throw new ArgumentNullException(nameof(purchaseOrder));

            string party = Pick(vendorLedgerName, purchaseOrder.VendorName, "Vendor");
            string purchaseLedger = Pick(purchaseLedgerName, "Purchase");
            string number = Pick(purchaseOrder.VendorInvoiceNumber, purchaseOrder.PONumber);
            StringBuilder voucher = StartVoucher("Purchase", "Invoice Voucher View");
            AppendTag(voucher, 12, "DATE", TallyDate(purchaseOrder.PODate));
            AppendTag(voucher, 12, "VOUCHERTYPENAME", "Purchase");
            AppendTag(voucher, 12, "VOUCHERNUMBER", number);
            AppendTag(voucher, 12, "PARTYLEDGERNAME", party);
            AppendTag(voucher, 12, "PERSISTEDVIEW", "Invoice Voucher View");
            AppendTag(voucher, 12, "ISINVOICE", "Yes");
            AppendTag(voucher, 12, "NARRATION", "ServoERP purchase " + purchaseOrder.PONumber);
            AppendLedgerEntry(voucher, party, Math.Abs(purchaseOrder.TotalAmount), false, true, number, "New Ref");

            decimal cgst = 0m;
            decimal sgst = 0m;
            decimal igst = 0m;
            foreach (PurchaseLineItem line in purchaseOrder.LineItems ?? new List<PurchaseLineItem>())
            {
                AppendPurchaseInventoryEntry(voucher, line, purchaseLedger);
                decimal taxable = line.Amount > 0 ? line.Amount : line.Quantity * line.Rate;
                cgst += Math.Round(taxable * line.CGSTRate / 100m, 2);
                sgst += Math.Round(taxable * line.SGSTRate / 100m, 2);
                igst += Math.Round(taxable * line.IGSTRate / 100m, 2);
            }

            if (cgst != 0)
                AppendLedgerEntry(voucher, Pick(cgstLedgerName, "Input CGST"), -Math.Abs(cgst), true, false, null, null);
            if (sgst != 0)
                AppendLedgerEntry(voucher, Pick(sgstLedgerName, "Input SGST"), -Math.Abs(sgst), true, false, null, null);
            if (igst != 0)
                AppendLedgerEntry(voucher, Pick(igstLedgerName, "Input IGST"), -Math.Abs(igst), true, false, null, null);

            EndVoucher(voucher);
            return Envelope("Vouchers", voucher.ToString());
        }

        public string BuildPaymentVoucher(PurchaseOrder purchaseOrder, string vendorLedgerName, string bankLedgerName)
        {
            if (purchaseOrder == null)
                throw new ArgumentNullException(nameof(purchaseOrder));

            decimal amount = purchaseOrder.PaidAmount > 0 ? purchaseOrder.PaidAmount : purchaseOrder.TotalAmount;
            string number = "VPAY-" + Pick(purchaseOrder.PONumber, purchaseOrder.VendorInvoiceNumber, purchaseOrder.POID.ToString(CultureInfo.InvariantCulture));
            string party = Pick(vendorLedgerName, purchaseOrder.VendorName, "Vendor");
            string bank = Pick(bankLedgerName, "Bank");
            StringBuilder voucher = StartVoucher("Payment", "Accounting Voucher View");
            AppendTag(voucher, 12, "DATE", TallyDate(DateTime.Today));
            AppendTag(voucher, 12, "VOUCHERTYPENAME", "Payment");
            AppendTag(voucher, 12, "VOUCHERNUMBER", number);
            AppendTag(voucher, 12, "PARTYLEDGERNAME", party);
            AppendTag(voucher, 12, "PERSISTEDVIEW", "Accounting Voucher View");
            AppendTag(voucher, 12, "NARRATION", "ServoERP supplier payment " + purchaseOrder.PaymentReference);
            AppendLedgerEntry(voucher, party, -Math.Abs(amount), true, true, Pick(purchaseOrder.VendorInvoiceNumber, purchaseOrder.PONumber), "Agst Ref");
            AppendLedgerEntry(voucher, bank, Math.Abs(amount), false, false, null, null);
            EndVoucher(voucher);
            return Envelope("Vouchers", voucher.ToString());
        }

        public string BuildStockItemMaster(StockItem item, string tallyItemName, string hsnCode, string stockGroupName, string tallyUnitName, string godownName)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            string name = Pick(tallyItemName, item.ItemName, "ServoERP Stock Item");
            string unit = Pick(tallyUnitName, item.Unit, "Nos");
            decimal value = Math.Round(item.CurrentStock * item.LastPurchaseRate, 2);
            var xml = new StringBuilder();
            xml.AppendLine("        <STOCKITEM ACTION=\"Create\">");
            AppendTag(xml, 10, "NAME", name);
            AppendTag(xml, 10, "PARENT", Pick(stockGroupName, item.Category, "Primary"));
            AppendTag(xml, 10, "BASEUNITS", unit);
            AppendTag(xml, 10, "GSTAPPLICABLE", "Applicable");
            AppendTag(xml, 10, "GSTTYPEOFSUPPLY", "Goods");
            if (!string.IsNullOrWhiteSpace(hsnCode))
                AppendTag(xml, 10, "GSTHSNNAME", hsnCode);
            AppendTag(xml, 10, "OPENINGBALANCE", FormatQty(item.CurrentStock, unit));
            AppendTag(xml, 10, "OPENINGRATE", Money(item.LastPurchaseRate) + "/" + unit);
            AppendTag(xml, 10, "OPENINGVALUE", Money(value));
            xml.AppendLine("          <NAME.LIST TYPE=\"String\">");
            AppendTag(xml, 12, "NAME", "SERVOERP-STOCK-" + item.ItemID.ToString(CultureInfo.InvariantCulture));
            xml.AppendLine("          </NAME.LIST>");
            xml.AppendLine("        </STOCKITEM>");
            return Envelope("All Masters", WrapTallyMessage(xml.ToString()));
        }

        public string BuildLedgerPullRequest()
        {
            return ExportEnvelope("List of Accounts");
        }

        public string BuildStockItemPullRequest()
        {
            return ExportEnvelope("Stock Item");
        }

        private static StringBuilder StartVoucher(string voucherType, string objectView)
        {
            var xml = new StringBuilder();
            xml.AppendLine("        <VOUCHER VCHTYPE=\"" + Escape(voucherType) + "\" ACTION=\"Create\" OBJVIEW=\"" + Escape(objectView) + "\">");
            return xml;
        }

        private static void EndVoucher(StringBuilder xml)
        {
            xml.AppendLine("        </VOUCHER>");
        }

        private static void AppendInventoryEntry(StringBuilder xml, InvoiceLineItem line, string salesLedger)
        {
            if (line == null || !line.IsBillable)
                return;

            string unit = Pick(line.Unit, "Nos");
            decimal taxable = line.Amount != 0 ? line.Amount : Math.Round(line.Quantity * line.Rate, 2);
            xml.AppendLine("          <ALLINVENTORYENTRIES.LIST>");
            AppendTag(xml, 12, "STOCKITEMNAME", Pick(line.Description, "ServoERP Service"));
            if (!string.IsNullOrWhiteSpace(line.HSNCode))
                AppendTag(xml, 12, "GSTHSNNAME", line.HSNCode);
            AppendTag(xml, 12, "ISDEEMEDPOSITIVE", "No");
            AppendTag(xml, 12, "ACTUALQTY", FormatQty(line.Quantity, unit));
            AppendTag(xml, 12, "BILLEDQTY", FormatQty(line.Quantity, unit));
            AppendTag(xml, 12, "RATE", Money(line.Rate) + "/" + unit);
            AppendTag(xml, 12, "AMOUNT", Money(Math.Abs(taxable)));
            xml.AppendLine("            <ACCOUNTINGALLOCATIONS.LIST>");
            AppendTag(xml, 14, "LEDGERNAME", salesLedger);
            AppendTag(xml, 14, "ISDEEMEDPOSITIVE", "No");
            AppendTag(xml, 14, "AMOUNT", Money(Math.Abs(taxable)));
            xml.AppendLine("            </ACCOUNTINGALLOCATIONS.LIST>");
            xml.AppendLine("          </ALLINVENTORYENTRIES.LIST>");
        }

        private static void AppendPurchaseInventoryEntry(StringBuilder xml, PurchaseLineItem line, string purchaseLedger)
        {
            if (line == null)
                return;

            string unit = Pick(line.UOM, "Nos");
            decimal taxable = line.Amount != 0 ? line.Amount : Math.Round(line.Quantity * line.Rate, 2);
            xml.AppendLine("          <ALLINVENTORYENTRIES.LIST>");
            AppendTag(xml, 12, "STOCKITEMNAME", Pick(line.Description, "ServoERP Purchase Item"));
            if (!string.IsNullOrWhiteSpace(line.HsnSacCode))
                AppendTag(xml, 12, "GSTHSNNAME", line.HsnSacCode);
            AppendTag(xml, 12, "ISDEEMEDPOSITIVE", "Yes");
            AppendTag(xml, 12, "ACTUALQTY", FormatQty(line.Quantity, unit));
            AppendTag(xml, 12, "BILLEDQTY", FormatQty(line.Quantity, unit));
            AppendTag(xml, 12, "RATE", Money(line.Rate) + "/" + unit);
            AppendTag(xml, 12, "AMOUNT", Money(-Math.Abs(taxable)));
            xml.AppendLine("            <ACCOUNTINGALLOCATIONS.LIST>");
            AppendTag(xml, 14, "LEDGERNAME", purchaseLedger);
            AppendTag(xml, 14, "ISDEEMEDPOSITIVE", "Yes");
            AppendTag(xml, 14, "AMOUNT", Money(-Math.Abs(taxable)));
            xml.AppendLine("            </ACCOUNTINGALLOCATIONS.LIST>");
            xml.AppendLine("          </ALLINVENTORYENTRIES.LIST>");
        }

        private static void AppendLedgerEntry(StringBuilder xml, string ledgerName, decimal amount, bool deemedPositive, bool partyLedger, string billName, string billType)
        {
            xml.AppendLine("          <LEDGERENTRIES.LIST>");
            AppendTag(xml, 12, "LEDGERNAME", ledgerName);
            AppendTag(xml, 12, "ISDEEMEDPOSITIVE", deemedPositive ? "Yes" : "No");
            if (partyLedger)
                AppendTag(xml, 12, "ISPARTYLEDGER", "Yes");
            AppendTag(xml, 12, "AMOUNT", Money(amount));
            if (!string.IsNullOrWhiteSpace(billName))
            {
                xml.AppendLine("            <BILLALLOCATIONS.LIST>");
                AppendTag(xml, 14, "NAME", billName);
                AppendTag(xml, 14, "BILLTYPE", Pick(billType, "New Ref"));
                AppendTag(xml, 14, "AMOUNT", Money(amount));
                xml.AppendLine("            </BILLALLOCATIONS.LIST>");
            }
            xml.AppendLine("          </LEDGERENTRIES.LIST>");
        }

        private static string Envelope(string reportName, string tallyMessageContent)
        {
            var xml = new StringBuilder();
            xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            xml.AppendLine("<ENVELOPE>");
            xml.AppendLine("  <HEADER>");
            xml.AppendLine("    <TALLYREQUEST>Import Data</TALLYREQUEST>");
            xml.AppendLine("  </HEADER>");
            xml.AppendLine("  <BODY>");
            xml.AppendLine("    <IMPORTDATA>");
            xml.AppendLine("      <REQUESTDESC>");
            AppendTag(xml, 8, "REPORTNAME", reportName);
            xml.AppendLine("      </REQUESTDESC>");
            xml.AppendLine("      <REQUESTDATA>");
            if (tallyMessageContent.IndexOf("<TALLYMESSAGE", StringComparison.OrdinalIgnoreCase) >= 0)
                xml.Append(tallyMessageContent);
            else
                xml.Append(WrapTallyMessage(tallyMessageContent));
            xml.AppendLine("      </REQUESTDATA>");
            xml.AppendLine("    </IMPORTDATA>");
            xml.AppendLine("  </BODY>");
            xml.AppendLine("</ENVELOPE>");
            return xml.ToString();
        }

        private static string ExportEnvelope(string reportName)
        {
            var xml = new StringBuilder();
            xml.AppendLine("<ENVELOPE>");
            xml.AppendLine("  <HEADER>");
            xml.AppendLine("    <VERSION>1</VERSION>");
            xml.AppendLine("    <TALLYREQUEST>Export</TALLYREQUEST>");
            xml.AppendLine("    <TYPE>Collection</TYPE>");
            AppendTag(xml, 4, "ID", reportName);
            xml.AppendLine("  </HEADER>");
            xml.AppendLine("  <BODY>");
            xml.AppendLine("    <DESC>");
            xml.AppendLine("      <STATICVARIABLES>");
            AppendTag(xml, 8, "SVEXPORTFORMAT", "$$SysName:XML");
            xml.AppendLine("      </STATICVARIABLES>");
            xml.AppendLine("    </DESC>");
            xml.AppendLine("  </BODY>");
            xml.AppendLine("</ENVELOPE>");
            return xml.ToString();
        }

        private static string WrapTallyMessage(string content)
        {
            return "        <TALLYMESSAGE xmlns:UDF=\"TallyUDF\">\r\n" + content + "        </TALLYMESSAGE>\r\n";
        }

        private static void AppendTag(StringBuilder xml, int spaces, string tagName, string value)
        {
            xml.Append(' ', spaces);
            xml.Append('<').Append(tagName).Append('>');
            xml.Append(Escape(value));
            xml.Append("</").Append(tagName).AppendLine(">");
        }

        private static string TallyDate(DateTime date)
        {
            if (date == default(DateTime))
                date = DateTime.Today;
            return date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }

        private static string Money(decimal value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static string FormatQty(decimal quantity, string unit)
        {
            return quantity.ToString("0.###", CultureInfo.InvariantCulture) + " " + Pick(unit, "Nos");
        }

        private static string Pick(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }
            return string.Empty;
        }

        private static string Escape(string value)
        {
            return SecurityElement.Escape(value ?? string.Empty) ?? string.Empty;
        }
    }
}

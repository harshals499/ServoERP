using System;
using System.Collections.Generic;
using System.Linq;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Models.Validation;

namespace HVAC_Pro_Desktop.Services.Validation
{
    public sealed class CalculationVerificationService
    {
        public ValidationResult VerifyInvoice(Invoice invoice)
        {
            var result = new ValidationResult();
            if (invoice == null)
                return result.Add(ValidationSeverity.Critical, "Invoices", "Invoice", "Invoice payload is missing.");

            decimal subtotal = 0m;
            decimal tax = 0m;
            foreach (InvoiceLineItem item in invoice.LineItems ?? new List<InvoiceLineItem>())
            {
                decimal discountPercent = Math.Min(Math.Max(item.DiscountPercent, 0m), 100m);
                decimal gross = Math.Round(item.Quantity * item.Rate, 2);
                decimal expectedAmount = item.IsBillable ? Math.Round(gross - (gross * discountPercent / 100m), 2) : 0m;
                decimal expectedTax = item.IsBillable ? Math.Round(expectedAmount * (item.GSTPercent / 100m), 2) : 0m;
                subtotal += expectedAmount;
                tax += expectedTax;
                if (Math.Abs(item.Amount - expectedAmount) > 0.05m)
                    result.Add(ValidationSeverity.Warning, "Invoices", "Line amount", "Invoice line amount does not match quantity x rate.", "Recalculate invoice before saving.", true);
                if (Math.Abs(item.TaxAmount - expectedTax) > 0.05m)
                    result.Add(ValidationSeverity.Warning, "Invoices", "Line GST", "Invoice line GST does not match taxable amount and GST rate.", "Recalculate GST.", true);
            }

            decimal expectedTotal = Math.Round(subtotal + tax + invoice.RoundOff, 2);
            if (Math.Abs(invoice.SubTotal - subtotal) > 0.05m)
                result.Add(ValidationSeverity.Warning, "Invoices", "SubTotal", "Invoice subtotal differs from line totals.", "Use automatic recalculation.", true);
            if (Math.Abs(invoice.TaxAmount - tax) > 0.05m)
                result.Add(ValidationSeverity.Warning, "Invoices", "TaxAmount", "Invoice tax differs from line GST totals.", "Use automatic recalculation.", true);
            if (Math.Abs(invoice.TotalAmount - expectedTotal) > 0.05m)
                result.Add(ValidationSeverity.Error, "Invoices", "TotalAmount", "Invoice grand total is wrong.", "Recalculate totals before saving.");
            if (invoice.PaidAmount < 0 || invoice.PaidAmount > invoice.TotalAmount + 0.05m)
                result.Add(ValidationSeverity.Error, "Invoices", "PaidAmount", "Paid amount cannot be negative or greater than invoice total.");
            return result;
        }

        public ValidationResult VerifyPurchaseOrder(PurchaseOrder po)
        {
            var result = new ValidationResult();
            if (po == null)
                return result.Add(ValidationSeverity.Critical, "Purchases", "PO", "Purchase order payload is missing.");

            decimal lineTotal = (po.LineItems ?? new List<PurchaseLineItem>()).Sum(l => Math.Round(l.Quantity * l.Rate, 2));
            if ((po.LineItems ?? new List<PurchaseLineItem>()).Any(l => l.Quantity <= 0 || l.Rate < 0))
                result.Add(ValidationSeverity.Error, "Purchases", "Line items", "PO line quantities must be positive and rates cannot be negative.");
            if (lineTotal > 0 && Math.Abs(po.TotalAmount - lineTotal) > Math.Max(1m, lineTotal * 0.05m))
                result.Add(ValidationSeverity.Warning, "Purchases", "TotalAmount", "PO total differs from line total by more than 5%.", "Confirm tax/charges or recalculate.", true);
            if (po.PaidAmount < 0 || po.PaidAmount > po.TotalAmount + 0.05m)
                result.Add(ValidationSeverity.Error, "Purchases", "PaidAmount", "PO paid amount cannot be negative or greater than PO total.");
            return result;
        }

        public ValidationResult VerifyQuotation(TenderBid bid)
        {
            var result = new ValidationResult();
            if (bid == null)
                return result.Add(ValidationSeverity.Critical, "Quotations", "Quotation", "Quotation payload is missing.");

            decimal taxable = (bid.LineItems ?? new List<TenderBidLineItem>()).Sum(l => Math.Round(l.Quantity * l.SellPricePerUnit, 2));
            decimal gst = (bid.LineItems ?? new List<TenderBidLineItem>()).Sum(l => Math.Round(Math.Round(l.Quantity * l.SellPricePerUnit, 2) * (l.GSTRatePct / 100m), 2));
            if (taxable > 0 && Math.Abs(bid.TotalTaxableValue - taxable) > 0.05m)
                result.Add(ValidationSeverity.Warning, "Quotations", "Taxable total", "Quotation taxable total differs from line totals.", "Recalculate quotation.", true);
            if (taxable > 0 && Math.Abs(bid.TotalWithGST - (taxable + gst)) > 0.05m)
                result.Add(ValidationSeverity.Error, "Quotations", "TotalWithGST", "Quotation total with GST is wrong.", "Recalculate quotation totals.");
            return result;
        }
    }
}

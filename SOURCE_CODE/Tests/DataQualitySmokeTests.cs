using System;
using System.Collections.Generic;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Models.Validation;
using HVAC_Pro_Desktop.Services.Validation;

namespace HVAC_Pro_Desktop.Tests
{
    public static class DataQualitySmokeTests
    {
        public static List<string> RunAll()
        {
            var passed = new List<string>();
            var rules = new BusinessRuleEngine();
            var duplicates = new DuplicateDetectionService();
            var calc = new CalculationVerificationService();
            var refs = new ReferenceIntegrityService();

            ExpectError(rules.ValidateClient(new B2BClient { CompanyName = "Bad Email", Email = "bad@", GSTNumber = "27ABCDE1234F1Z0" }), "invalid client email");
            passed.Add("invalid email rejected");

            ExpectWarning(rules.ValidateClient(new B2BClient { CompanyName = "Bad Phone", Phone = "abc" }), "invalid client phone warning");
            passed.Add("invalid phone warning produced");

            ExpectError(rules.ValidateVendor(new Vendor { VendorName = "Bad GST", GSTNumber = "BADGST" }), "invalid vendor GST");
            passed.Add("invalid GST rejected");

            ExpectError(duplicates.CheckClient(
                new B2BClient { ClientID = 2, CompanyName = "Acme New", GSTNumber = "27ABCDE1234F1Z0" },
                new[] { new B2BClient { ClientID = 1, CompanyName = "Acme Old", GSTNumber = "27ABCDE1234F1Z0" } }), "duplicate client GST");
            passed.Add("duplicate client GST rejected");

            ExpectError(duplicates.CheckVendor(
                new Vendor { VendorID = 2, VendorName = "Vendor New", GSTNumber = "27ABCDE1234F1Z0" },
                new[] { new Vendor { VendorID = 1, VendorName = "Vendor Old", GSTNumber = "27ABCDE1234F1Z0" } }), "duplicate vendor GST");
            passed.Add("duplicate vendor GST rejected");

            ExpectError(rules.ValidateInventoryItem(new StockItem { ItemName = "Copper Pipe", CurrentStock = -1 }), "negative stock");
            passed.Add("negative stock rejected");

            ExpectError(calc.VerifyInvoice(new Invoice
            {
                ClientID = 1,
                SiteID = 1,
                InvoiceDate = DateTime.Today,
                DueDate = DateTime.Today,
                SubTotal = 100m,
                TaxAmount = 18m,
                TotalAmount = 10m,
                LineItems = new List<InvoiceLineItem> { new InvoiceLineItem { Description = "Service", Quantity = 1m, Rate = 100m, GSTPercent = 18m, Amount = 100m, TaxAmount = 18m } }
            }), "invoice total mismatch");
            passed.Add("invoice total mismatch rejected");

            var discountedInvoice = new Invoice
            {
                ClientID = 1,
                SiteID = 1,
                InvoiceDate = DateTime.Today,
                DueDate = DateTime.Today,
                SubTotal = 90m,
                TaxAmount = 16.2m,
                TotalAmount = 106.2m,
                LineItems = new List<InvoiceLineItem> { new InvoiceLineItem { Description = "Service", Quantity = 1m, Rate = 100m, DiscountPercent = 10m, GSTPercent = 18m, Amount = 90m, TaxAmount = 16.2m } }
            };
            if (calc.VerifyInvoice(discountedInvoice).HasErrors || calc.VerifyInvoice(discountedInvoice).HasWarnings)
                throw new InvalidOperationException("Expected discounted invoice totals to verify cleanly.");
            passed.Add("invoice discount totals verified");

            ExpectError(calc.VerifyPurchaseOrder(new PurchaseOrder { VendorID = 1, PONumber = "PO-1", PODate = DateTime.Today, PayByDate = DateTime.Today, TotalAmount = 100m, PaidAmount = 101m }), "PO paid exceeds total");
            passed.Add("PO paid amount rejected");

            var supplier = new Vendor { VendorID = 10, VendorName = "ABC HVAC Parts", VendorType = "Supplier", IsSupplier = true, IsServiceVendor = false };
            var serviceVendor = new Vendor { VendorID = 20, VendorName = "City Duct Cleaning", VendorType = "Subcontractor", IsSupplier = false, IsServiceVendor = true };
            var partnerSet = new List<Vendor> { supplier, serviceVendor };
            if (partnerSet.FindAll(v => v.IsSupplier).Exists(v => v.IsServiceVendor && !v.IsSupplier))
                throw new InvalidOperationException("Supplier purchase list included a service-only vendor.");
            if (partnerSet.FindAll(v => v.IsServiceVendor).Exists(v => v.IsSupplier && !v.IsServiceVendor))
                throw new InvalidOperationException("Service vendor list included a supplier-only record.");
            passed.Add("supplier and service vendor role filters separated");

            var fullyReceivedPo = new PurchaseOrder { VendorID = 1, PONumber = "PO-2", PODate = DateTime.Today.AddDays(-10), PayByDate = DateTime.Today.AddDays(-5), TotalAmount = 100m, PaidAmount = 0m, Status = "Fully Received" };
            fullyReceivedPo.ApplyPaymentCompletionRule();
            if (!fullyReceivedPo.IsPaymentCompleted || fullyReceivedPo.PaidAmount != fullyReceivedPo.TotalAmount || fullyReceivedPo.BalanceDue != 0m || fullyReceivedPo.IsOverdue)
                throw new InvalidOperationException("Expected fully received purchase orders to be treated as fully paid and not overdue.");
            passed.Add("fully received PO treated as paid");

            ExpectError(calc.VerifyQuotation(new TenderBid
            {
                QuotationNumber = "QTN-1",
                ClientID = 1,
                SiteID = 1,
                TotalTaxableValue = 100m,
                TotalWithGST = 1m,
                LineItems = new List<TenderBidLineItem> { new TenderBidLineItem { ItemDescription = "Part", Quantity = 1m, SellPricePerUnit = 100m, GSTRatePct = 18m } }
            }), "quotation total mismatch");
            passed.Add("quotation total mismatch rejected");

            ExpectError(refs.CheckClientSite(1, 20, new[] { new ClientSite { ClientID = 2, SiteID = 20, SiteName = "Wrong Client Site" } }, "Invoices"), "broken client-site reference");
            passed.Add("broken client-site link rejected");

            ExpectError(rules.ValidateContract(new AMCContract { ClientID = 1, SiteID = 1, StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays(-1), MonthlyValue = 100m }), "impossible contract dates");
            passed.Add("impossible dates rejected");

            return passed;
        }

        private static void ExpectError(ValidationResult result, string name)
        {
            if (result == null || !result.HasErrors)
                throw new InvalidOperationException("Expected validation error for " + name + ".");
        }

        private static void ExpectWarning(ValidationResult result, string name)
        {
            if (result == null || !result.HasWarnings)
                throw new InvalidOperationException("Expected validation warning for " + name + ".");
        }
    }
}

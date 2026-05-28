using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Models.Validation;
using HVAC_Pro_Desktop.Services.Audit;
using HVAC_Pro_Desktop.Services.Validation;

namespace HVAC_Pro_Desktop.Services
{
    public class InvoiceService
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
        private readonly InvoiceRepository  _invoiceRepo  = new InvoiceRepository();
        private readonly ContractRepository _contractRepo = new ContractRepository();
        private readonly ClientRepository   _clientRepo   = new ClientRepository();
        private readonly SiteRepository     _siteRepo     = new SiteRepository();
        private readonly SettingsService    _settingsSvc  = new SettingsService();
        private readonly InvoiceTemplateService _templateSvc = new InvoiceTemplateService();
        private readonly InventoryService _inventorySvc = new InventoryService();
        private readonly InvoiceInventoryService _invoiceInventorySvc = new InvoiceInventoryService();
        private readonly DatabaseManager _db = new DatabaseManager();
        private readonly BusinessRuleEngine _businessRules = new BusinessRuleEngine();
        private readonly CalculationVerificationService _calculationVerifier = new CalculationVerificationService();
        private readonly GlobalValidationEngine _validation = new GlobalValidationEngine();
        private readonly AuditTrailService _audit = new AuditTrailService();
        private const decimal GstRate = 0.18m;

        // ── READ ─────────────────────────────────────────────
        public List<Invoice> GetAllInvoices()       => AppDataCache.GetOrCreate("invoices:all", CacheTtl, _invoiceRepo.GetAll);
        public List<Invoice> GetPendingInvoices()   => GetAllInvoices().Where(i => i.PaymentStatus == "Pending" || i.PaymentStatus == "Overdue" || i.PaymentStatus == "Partial" || i.PaymentStatus == "Draft").ToList();
        public List<Invoice> GetOverdueInvoices()   => GetAllInvoices().Where(i => (i.PaymentStatus == "Pending" || i.PaymentStatus == "Partial" || i.PaymentStatus == "Overdue") && i.DueDate < DateTime.Today).ToList();

        public Invoice GetInvoiceById(int id)
        {
            Invoice inv = _invoiceRepo.GetById(id);
            if (inv != null)
                inv.LineItems = _invoiceRepo.GetLineItems(id);
            return inv;
        }

        public List<Invoice> GetInvoicesForContract(int contractId)
        {
            return GetAllInvoices().Where(i => i.ContractID == contractId).ToList();
        }

        public List<Invoice> GetInvoicesForClient(int clientId)
        {
            return GetAllInvoices().Where(i => i.ClientID == clientId).ToList();
        }

        public List<InvoiceLineItem> GetLineItems(int invoiceId)
        {
            return _invoiceRepo.GetLineItems(invoiceId);
        }

        public List<InvoiceTemplate> GetActiveTemplates()
        {
            return _templateSvc.GetActiveTemplates();
        }

        public Invoice BuildInvoiceFromTemplate(string templateCode, int clientId, int siteId, int contractId)
        {
            InvoiceTemplate template = _templateSvc.GetByCode(templateCode);
            if (template == null)
                throw new Exception("Template not found: " + templateCode);

            var client = clientId > 0 ? _clientRepo.GetById(clientId) : null;
            var site = siteId > 0 ? _siteRepo.GetAll().FirstOrDefault(s => s.SiteID == siteId) : null;
            var contract = contractId > 0 ? _contractRepo.GetById(contractId) : null;
            string coverage = ResolveCoverageType(template, contract);

            var draft = new Invoice
            {
                ClientID = clientId,
                SiteID = siteId,
                ContractID = contractId,
                TemplateCode = template.TemplateCode,
                WorkflowType = template.WorkflowType,
                InvoiceTitle = "TAX INVOICE",
                InvoiceDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(ResolvePaymentTermDays(template.DefaultPaymentTerms)),
                GSTMode = string.IsNullOrWhiteSpace(template.DefaultGstMode) ? "IGST" : template.DefaultGstMode,
                GSTPercent = template.DefaultGstPercent <= 0 ? 18m : template.DefaultGstPercent,
                PaymentTerms = template.DefaultPaymentTerms,
                PlaceOfSupply = !string.IsNullOrWhiteSpace(site?.City) ? site.City : _settingsSvc.Get("DefaultPlaceOfSupply", "Maharashtra"),
                ContractCoverageType = coverage,
                Subject = FillTokens(template.DefaultSubject, client, site, contract),
                Notes = FillTokens(template.DefaultNotes, client, site, contract),
                ServiceChecklist = FillTokens(template.DefaultChecklist, client, site, contract),
                AssetDetails = ResolveAssetDetails(template, contract, site),
                WarrantyStatus = ResolveWarrantyStatus(contract),
                WarrantyExpiry = ResolveWarrantyExpiry(contract),
                PreventiveVisitDate = IsPreventiveFlow(template) ? DateTime.Today : (DateTime?)null,
                NextServiceDueDate = CalculateNextServiceDueDate(DateTime.Today, contract, template.WorkflowType),
                InventoryReservationStatus = "Pending",
                PaymentStatus = "Draft"
            };

            draft.LineItems = BuildTemplateLines(template.TemplateCode, draft, contract);
            PopulateInvoiceDefaults(draft);
            RecalculateTotals(draft);
            draft.BalanceDue = draft.TotalAmount;
            return draft;
        }

        // ── CREATE ───────────────────────────────────────────

        /// <summary>
        /// Creates a full ERP-style invoice with line items inside a transaction.
        /// Calculates SubTotal, TaxAmount, TotalAmount, BalanceDue from line items.
        /// </summary>
        public int CreateInvoiceWithLineItems(Invoice inv)
        {
            SessionManager.DemandPermission("Invoices", "Create");
            ValidateInvoice(inv);
            PopulateInvoiceDefaults(inv);
            RecalculateTotals(inv);
            ValidateInvoiceForSave(inv);
            if (SessionManager.IsLoggedIn)
            {
                inv.CreatedByUserId = SessionManager.CurrentUser.UserId;
                inv.CreatedByName = SessionManager.CurrentUser.DisplayName;
            }
            if (string.IsNullOrWhiteSpace(inv.InvoiceNumber))
                inv.InvoiceNumber = _invoiceRepo.GenerateInvoiceNumber();
            inv.BalanceDue    = inv.TotalAmount - inv.PaidAmount;
            if (string.IsNullOrEmpty(inv.PaymentStatus))
                inv.PaymentStatus = "Draft";
            int id = _invoiceRepo.Create(inv);
            if (string.Equals(inv.PaymentStatus, "Draft", StringComparison.OrdinalIgnoreCase))
            {
                _invoiceInventorySvc.SyncDraftReservations(id, inv.LineItems);
                MarkInventoryReservationStatus(id, "Reserved");
            }
            AppDataCache.RemovePrefix("invoices:");
            SessionManager.LogAction("CREATE", "Invoices", id, "Invoice saved");
            _audit.Record("CREATE", "Invoices", id, "Invoice saved with data-quality validation");
            return id;
        }

        /// <summary>
        /// Saves (replace-all) line items for an existing invoice and
        /// recalculates the invoice totals.
        /// </summary>
        public void UpdateInvoiceWithLineItems(Invoice inv)
        {
            SessionManager.DemandPermission("Invoices", "Edit");
            if (inv == null || inv.InvoiceID <= 0)
                throw new Exception("Invoice not found.");

            ValidateInvoice(inv);
            PopulateInvoiceDefaults(inv);
            RecalculateTotals(inv);
            ValidateInvoiceForSave(inv);
            if (SessionManager.IsLoggedIn)
            {
                inv.ModifiedByUserId = SessionManager.CurrentUser.UserId;
                inv.ModifiedByName = SessionManager.CurrentUser.DisplayName;
                inv.ModifiedDate = DateTime.Now;
            }
            inv.BalanceDue = inv.TotalAmount - inv.PaidAmount;
            _invoiceRepo.Update(inv);
            _invoiceRepo.SaveLineItems(inv.InvoiceID, inv.LineItems ?? new List<InvoiceLineItem>());
            if (string.Equals(inv.PaymentStatus, "Draft", StringComparison.OrdinalIgnoreCase))
            {
                _invoiceInventorySvc.SyncDraftReservations(inv.InvoiceID, inv.LineItems);
                MarkInventoryReservationStatus(inv.InvoiceID, "Reserved");
            }
            AppDataCache.RemovePrefix("invoices:");
            SessionManager.LogAction("EDIT", "Invoices", inv.InvoiceID, "Invoice saved");
            _audit.Record("EDIT", "Invoices", inv.InvoiceID, "Invoice saved with data-quality validation");
        }

        /// <summary>
        /// Quick invoice generation from an AMC contract (one line item = monthly AMC).
        /// </summary>
        public Invoice CreateInvoiceFromContract(int contractId, DateTime invoiceDate)
        {
            SessionManager.DemandPermission("Invoices", "Create");
            AMCContract contract = _contractRepo.GetById(contractId);
            if (contract == null)
                throw new Exception("Contract not found: " + contractId);

            decimal subTotal = contract.MonthlyValue;
            decimal tax      = Math.Round(subTotal * GstRate, 2);

            Invoice invoice = new Invoice
            {
                ContractID    = contractId,
                ClientID      = contract.ClientID,
                SiteID        = contract.SiteID,
                InvoiceNumber = _invoiceRepo.GenerateInvoiceNumber(),
                InvoiceDate   = invoiceDate,
                DueDate       = invoiceDate.AddDays(30),
                SubTotal      = subTotal,
                GSTPercent    = 18m,
                TaxAmount     = tax,
                TotalAmount   = subTotal + tax,
                PaidAmount    = 0,
                BalanceDue    = subTotal + tax,
                PaymentStatus = "Pending",
                InvoiceTitle  = "TAX INVOICE",
                LineItems     = new List<InvoiceLineItem>
                {
                    new InvoiceLineItem
                    {
                        Description = "AMC Monthly Service — " + invoiceDate.ToString("MMMM yyyy"),
                        Quantity    = 1,
                        Rate        = subTotal,
                        Amount      = subTotal
                    }
                }
            };

            invoice.InvoiceID = _invoiceRepo.Create(invoice);
            AppDataCache.RemovePrefix("invoices:");
            return invoice;
        }

        public void FinalizeInvoice(int invoiceId)
        {
            SessionManager.DemandPermission("Invoices", "Edit");
            Invoice inv = GetInvoiceById(invoiceId);
            if (inv == null)
                throw new Exception("Invoice not found.");

            inv.PaymentStatus = ResolveFinalStatus(inv);
            if (IsPreventiveInvoice(inv))
            {
                inv.PreventiveVisitDate = inv.InvoiceDate;
                inv.NextServiceDueDate = CalculateNextServiceDueDate(inv.InvoiceDate, inv.ContractID > 0 ? _contractRepo.GetById(inv.ContractID) : null, inv.WorkflowType);
            }

            _invoiceInventorySvc.FinalizeReservations(invoiceId);
            inv.InventoryReservationStatus = "Issued";
            _invoiceRepo.Update(inv);
            AppDataCache.RemovePrefix("invoices:");
        }

        public void CancelInvoice(int invoiceId)
        {
            SessionManager.DemandPermission("Invoices", "Delete");
            Invoice inv = GetInvoiceById(invoiceId);
            if (inv == null)
                throw new Exception("Invoice not found.");

            _invoiceInventorySvc.CancelReservations(invoiceId);
            inv.PaymentStatus = "Cancelled";
            inv.InventoryReservationStatus = "Restored";
            _invoiceRepo.Update(inv);
            AppDataCache.RemovePrefix("invoices:");
        }

        // ── AGING REPORT ─────────────────────────────────────
        public void DeleteInvoice(int invoiceId)
        {
            SessionManager.DemandPermission("Invoices", "Delete");
            Invoice inv = GetInvoiceById(invoiceId);
            if (inv == null)
                throw new Exception("Invoice not found.");

            _invoiceInventorySvc.CancelReservations(invoiceId);
            _invoiceRepo.Delete(invoiceId);
            AppDataCache.RemovePrefix("invoices:");
            AppDataCache.RemovePrefix("jobs:");
            SessionManager.LogAction("DELETE", "Invoices", invoiceId, "Invoice deleted");
            _audit.Record("DELETE", "Invoices", invoiceId, "Invoice and child records deleted");
        }

        public Invoice CreateCreditNoteForInvoice(int invoiceId, decimal creditAmount, string reason)
        {
            SessionManager.DemandPermission("Invoices", "Edit");
            if (creditAmount <= 0)
                throw new Exception("Credit amount must be greater than zero.");

            Invoice original = GetInvoiceById(invoiceId);
            if (original == null)
                throw new Exception("Invoice not found.");
            if (original.PaymentStatus == "Cancelled")
                throw new Exception("Cannot create a credit note for a cancelled invoice.");

            decimal openBalance = Math.Max(0m, original.BalanceDue);
            if (openBalance <= 0)
                throw new Exception("Invoice has no open balance to credit.");
            if (creditAmount > openBalance)
                throw new Exception("Credit amount cannot exceed the open balance of " + IndiaFormatHelper.FormatCurrency(openBalance) + ".");

            var creditNote = new Invoice
            {
                ClientID = original.ClientID,
                SiteID = original.SiteID,
                ContractID = original.ContractID,
                QuotationBidID = original.QuotationBidID,
                InvoiceDate = DateTime.Today,
                DueDate = DateTime.Today,
                InvoiceTitle = "CREDIT NOTE",
                Subject = "Credit note against " + original.InvoiceNumber,
                Notes = "Credit note against invoice " + original.InvoiceNumber + "." + Environment.NewLine + (reason ?? string.Empty),
                SendInvoiceTo = original.SendInvoiceTo,
                TemplateCode = original.TemplateCode,
                WorkflowType = "Credit Note",
                PaymentTerms = "Immediate",
                PlaceOfSupply = original.PlaceOfSupply,
                GSTMode = original.GSTMode,
                GSTPercent = original.GSTPercent > 0 ? original.GSTPercent : 18m,
                ContractCoverageType = original.ContractCoverageType,
                WarrantyStatus = original.WarrantyStatus,
                InventoryReservationStatus = "None",
                PaymentStatus = "Credit Note",
                PaidAmount = creditAmount
            };

            decimal gstPct = creditNote.GSTPercent > 0 ? creditNote.GSTPercent : 18m;
            decimal taxable = Math.Round(creditAmount / (1m + (gstPct / 100m)), 2);
            creditNote.LineItems.Add(new InvoiceLineItem
            {
                Description = "Credit adjustment for " + original.InvoiceNumber,
                HSNCode = FirstNonEmpty(original.LineItems.FirstOrDefault()?.HSNCode, "9987"),
                Unit = "Nos",
                Quantity = 1m,
                Rate = taxable,
                GSTPercent = gstPct,
                Amount = taxable,
                TaxAmount = Math.Round(creditAmount - taxable, 2),
                IsBillable = true,
                CoverageNote = reason
            });

            int creditNoteId = CreateInvoiceWithLineItems(creditNote);
            creditNote = GetInvoiceById(creditNoteId);
            creditNote.PaidAmount = creditNote.TotalAmount;
            creditNote.BalanceDue = 0m;
            creditNote.PaymentStatus = "Credit Note";
            creditNote.InventoryReservationStatus = "None";
            _invoiceRepo.Update(creditNote);

            decimal adjustedPaid = Math.Min(original.TotalAmount, original.PaidAmount + creditAmount);
            string adjustedStatus = adjustedPaid >= original.TotalAmount ? "Paid" : "Partial";
            _invoiceRepo.UpdatePaymentStatus(original.InvoiceID, adjustedPaid, adjustedStatus);
            _audit.Record("CREDIT_NOTE", "Invoices", original.InvoiceID, "Credit note " + creditNote.InvoiceNumber + " created for " + IndiaFormatHelper.FormatCurrency(creditAmount));
            AppDataCache.RemovePrefix("invoices:");
            return creditNote;
        }

        public DataTable GetAgingReport()
        {
            return _invoiceRepo.GetAgingReport();
        }

        // ── STATUS HELPERS ───────────────────────────────────
        public void MarkAsPaid(int invoiceId)
        {
            SessionManager.DemandPermission("Payments", "Create");
            Invoice inv = _invoiceRepo.GetById(invoiceId);
            if (inv == null) throw new Exception("Invoice not found");
            _invoiceRepo.UpdatePaymentStatus(invoiceId, inv.TotalAmount, "Paid");
            AppDataCache.RemovePrefix("invoices:");
        }

        public void MarkAsOverdue(int invoiceId)
        {
            SessionManager.DemandPermission("Invoices", "Edit");
            Invoice inv = _invoiceRepo.GetById(invoiceId);
            if (inv == null) throw new Exception("Invoice not found");
            _invoiceRepo.UpdatePaymentStatus(invoiceId, inv.PaidAmount, "Overdue");
            AppDataCache.RemovePrefix("invoices:");
        }

        public void AutoUpdateOverdueInvoices()
        {
            foreach (Invoice inv in _invoiceRepo.GetPendingInvoices())
                if (inv.DueDate < DateTime.Today && inv.PaymentStatus != "Paid")
                    _invoiceRepo.UpdatePaymentStatus(inv.InvoiceID, inv.PaidAmount, "Overdue");
            AppDataCache.RemovePrefix("invoices:");
        }

        // ── SUMMARY KPIs ─────────────────────────────────────
        public decimal GetTotalPendingAmount()
        {
            decimal total = 0;
            foreach (Invoice inv in GetPendingInvoices())
                total += inv.BalanceDue;
            return total;
        }

        // ── PRIVATE HELPERS ──────────────────────────────────
        private void RecalculateTotals(Invoice inv)
        {
            decimal sub = 0m;
            decimal tax = 0m;
            if (inv.LineItems != null)
            {
                foreach (var item in inv.LineItems)
                {
                    item.GSTPercent = item.GSTPercent <= 0 ? (inv.GSTPercent <= 0 ? 18m : inv.GSTPercent) : item.GSTPercent;
                    item.Quantity = item.Quantity <= 0 ? 1m : item.Quantity;
                    item.Category = string.IsNullOrWhiteSpace(item.Category) ? (item.IsStockItem ? "Material" : "Service") : item.Category.Trim();
                    item.TaxType = string.IsNullOrWhiteSpace(item.TaxType) ? "Taxable" : item.TaxType.Trim();
                    item.DiscountPercent = Math.Min(Math.Max(item.DiscountPercent, 0m), 100m);
                    decimal gross = Math.Round(item.Quantity * item.Rate, 2);
                    item.Amount = item.IsBillable ? Math.Round(gross - (gross * item.DiscountPercent / 100m), 2) : 0m;
                    bool taxable = item.IsBillable && !string.Equals(item.TaxType, "Exempt", StringComparison.OrdinalIgnoreCase) && !string.Equals(item.TaxType, "Nil Rated", StringComparison.OrdinalIgnoreCase);
                    item.TaxAmount = taxable ? Math.Round(item.Amount * (item.GSTPercent / 100m), 2) : 0m;
                    sub += item.Amount;
                    tax += item.TaxAmount;
                }
            }
            inv.SubTotal = sub;
            inv.GSTPercent = inv.GSTPercent <= 0 ? 18m : inv.GSTPercent;
            inv.TaxAmount = tax;
            inv.RoundOff = Math.Round(inv.RoundOff, 2);
            inv.GSTMode = string.IsNullOrWhiteSpace(inv.GSTMode) ? "IGST" : inv.GSTMode;
            if (string.Equals(inv.GSTMode, "CGST+SGST", StringComparison.OrdinalIgnoreCase))
            {
                inv.CGSTAmount = Math.Round(tax / 2m, 2);
                inv.SGSTAmount = tax - inv.CGSTAmount;
                inv.IGSTAmount = 0m;
            }
            else
            {
                inv.CGSTAmount = 0m;
                inv.SGSTAmount = 0m;
                inv.IGSTAmount = tax;
            }
            inv.TotalAmount = inv.SubTotal + inv.TaxAmount + inv.RoundOff;
            inv.BalanceDue = Math.Max(inv.TotalAmount - inv.PaidAmount, 0m);
        }

        private void ValidateInvoice(Invoice inv)
        {
            if (inv == null)
                throw new Exception("Invoice details are missing.");
            if (inv.ClientID <= 0)
                throw new Exception("Please select a client.");
            if (inv.SiteID <= 0)
                throw new Exception("Please select a site.");
            if (inv.InvoiceDate == default)
                throw new Exception("Invoice date is required.");
            if (inv.DueDate == default)
                throw new Exception("Due date is required.");
            if (inv.DueDate < inv.InvoiceDate)
                throw new Exception("Due date cannot be earlier than invoice date.");
            if (inv.LineItems == null || inv.LineItems.Count == 0)
                throw new Exception("At least one line item is required.");
            foreach (var item in inv.LineItems)
            {
                if (string.IsNullOrWhiteSpace(item.Description))
                    throw new Exception("All line items must have a description.");
                if (item.Quantity <= 0)
                    throw new Exception("Quantity must be greater than zero.");
                if (item.Rate < 0)
                    throw new Exception("Rate cannot be negative.");
            }
        }

        private void ValidateInvoiceForSave(Invoice inv)
        {
            ValidationResult result = _businessRules.ValidateInvoice(inv);
            result.Merge(_calculationVerifier.VerifyInvoice(inv));
            if (inv != null && !string.IsNullOrWhiteSpace(inv.InvoiceNumber))
            {
                bool duplicateNumber = GetAllInvoices().Any(existing =>
                    existing.InvoiceID != inv.InvoiceID &&
                    string.Equals((existing.InvoiceNumber ?? string.Empty).Trim(), inv.InvoiceNumber.Trim(), StringComparison.OrdinalIgnoreCase));
                if (duplicateNumber)
                    result.Add(ValidationSeverity.Error, "Invoices", "InvoiceNumber", "Another invoice already uses this invoice number.", "Open the existing invoice or generate a new invoice number.");
            }
            _validation.EnsureValid(result, "Invoice validation failed");
        }

        private void PopulateInvoiceDefaults(Invoice inv)
        {
            if (string.IsNullOrWhiteSpace(inv.InvoiceTitle))
                inv.InvoiceTitle = "TAX INVOICE";

            if (string.IsNullOrWhiteSpace(inv.CertificationNote))
                inv.CertificationNote = _settingsSvc.Get(
                    "DefaultCertificationNote",
                    "I/We hereby certify that this tax invoice is accounted for in the turnover of sales and the due tax, if any, has been paid or shall be paid.");

            if (string.IsNullOrWhiteSpace(inv.PaymentTerms))
                inv.PaymentTerms = _settingsSvc.Get("DefaultPaymentTerms", "30 Days");

            if (string.IsNullOrWhiteSpace(inv.PlaceOfSupply))
                inv.PlaceOfSupply = _settingsSvc.Get("DefaultPlaceOfSupply", "Maharashtra");

            if (string.IsNullOrWhiteSpace(inv.SendInvoiceTo) && inv.ClientID > 0)
            {
                var client = _clientRepo.GetById(inv.ClientID);
                if (client != null)
                {
                    inv.SendInvoiceTo = client.CompanyName;
                    if (!string.IsNullOrWhiteSpace(client.BillingAddress))
                        inv.SendInvoiceTo += Environment.NewLine + client.BillingAddress;
                }
            }
        }

        public string BuildInvoiceHtml(Invoice inv)
        {
            PopulateInvoiceDefaults(inv);
            RecalculateTotals(inv);
            var client = inv.ClientID > 0 ? _clientRepo.GetById(inv.ClientID) : null;
            var site = inv.SiteID > 0 ? _siteRepo.GetAll().FirstOrDefault(s => s.SiteID == inv.SiteID) : null;
            var settings = _settingsSvc.GetAll();

            string companyGst = GetSetting(settings, "CompanyGST", "");
            string companyPan = GetSetting(settings, "CompanyPAN", "");
            string shopLicense = GetSetting(settings, "CompanyShopLicense", "");
            string pfNumber = GetSetting(settings, "CompanyPFNumber", "");
            string esicNumber = GetSetting(settings, "CompanyESICNumber", "");
            string profTax = GetSetting(settings, "CompanyProfTax", GetSetting(settings, "CompanyProfessionalTax", ""));
            string msmeNumber = GetSetting(settings, "CompanyMSMENumber", "");

            string clientAddress = FirstNonEmpty(site?.Address, client?.BillingAddress, string.Empty);
            string clientGst = FirstNonEmpty(client?.GSTNumber, string.Empty);
            string subject = FirstNonEmpty(inv.Subject, "Supply / service invoice.");
            string invoiceNo = FirstNonEmpty(inv.InvoiceNumber, "DRAFT-PREVIEW");
            string words = ToWords((long)Math.Round(inv.TotalAmount)) + " Only.";

            var rows = new StringBuilder();
            int sr = 1;
            foreach (var item in inv.LineItems ?? new List<InvoiceLineItem>())
            {
                rows.Append("<tr>");
                rows.AppendFormat("<td class='center'>{0}</td>", sr++);
                rows.AppendFormat("<td class='desc'>{0}</td>", Html(item.Description));
                rows.AppendFormat("<td class='center'>{0}</td>", Html(item.HSNCode));
                rows.AppendFormat("<td class='center'>{0}</td>", Html(item.Unit));
                rows.AppendFormat("<td class='center'>{0}</td>", item.Quantity.ToString("0.###"));
                rows.AppendFormat("<td class='num'>{0}</td>", item.Rate.ToString("N2"));
                rows.AppendFormat("<td class='num'><strong>{0}</strong></td>", item.Amount.ToString("N2"));
                rows.Append("</tr>");
            }
            if (rows.Length == 0)
                rows.Append("<tr><td class='center'>1</td><td class='desc'>Service / material charges</td><td></td><td class='center'>Nos</td><td class='center'>1</td><td class='num'>0.00</td><td class='num'><strong>0.00</strong></td></tr>");

            string taxRows = string.Equals(inv.GSTMode, "CGST+SGST", StringComparison.OrdinalIgnoreCase)
                ? "<tr><td colspan='6'>Add: CGST " + (inv.GSTPercent / 2m).ToString("0.##") + "%</td><td class='total-value'>" + inv.CGSTAmount.ToString("N2") + "</td></tr>"
                  + "<tr><td colspan='6'>Add: SGST " + (inv.GSTPercent / 2m).ToString("0.##") + "%</td><td class='total-value'>" + inv.SGSTAmount.ToString("N2") + "</td></tr>"
                : "<tr><td colspan='6'>Add: IGST " + inv.GSTPercent.ToString("0.##") + "%</td><td class='total-value'>" + inv.IGSTAmount.ToString("N2") + "</td></tr>";
            string roundOffRow = inv.RoundOff != 0m
                ? "<tr><td colspan='6'>Round Off</td><td class='total-value'>" + inv.RoundOff.ToString("N2") + "</td></tr>"
                : string.Empty;

            return "<!DOCTYPE html><html><head><meta charset='utf-8'/><style>"
            + DocumentBranding.BuildOfficialHeaderCss()
            + DocumentBranding.BuildOfficialPrintCss()
            + "</style></head><body><div class='page'>"
            + DocumentBranding.BuildOfficialHeaderHtml()
            + new DocumentTemplateRenderer().BuildTemplateBannerHtml(CompanyDocumentTemplateType.Invoice)
            + "<div class='print-frame'><div class='doc-title'>" + Html(inv.InvoiceTitle) + "</div>"
            + "<table class='doc-grid'><tr><td class='client-cell'>To,<br/>" + Html(client?.CompanyName ?? inv.ClientName) + "<br/>" + Html(clientAddress).Replace("\n", "<br/>") + "<br/>GST No. " + Html(clientGst) + "</td>"
            + "<td class='meta-cell'>Date : " + inv.InvoiceDate.ToString("dd/MM/yyyy") + "</td></tr>"
            + "<tr><td></td><td class='meta-cell'>Invoice No. " + Html(invoiceNo) + "</td></tr>"
            + "<tr class='subject-row'><td colspan='2'>Sub : " + Html(subject) + "</td></tr>"
            + "<tr class='po-row'><td colspan='2'>PO No : " + Html(inv.PONumber) + (inv.PODate.HasValue ? ", Dtd: " + inv.PODate.Value.ToString("dd/MM/yyyy") + "." : "") + "</td></tr>"
            + "<tr class='blank-row'><td colspan='2'></td></tr></table>"
            + "<table class='doc-grid items'><thead><tr><th style='width:54px'>Sr No.</th><th>Description</th><th style='width:92px'>HSN Code</th><th style='width:58px'>Unit</th><th style='width:58px'>Qty</th><th style='width:118px'>Rate (Rs.)</th><th style='width:126px'>Amount (Rs.)</th></tr></thead><tbody>"
            + rows
            + "<tr><td></td><td></td><td></td><td></td><td></td><td></td><td class='total-value'>-</td></tr>"
            + "<tr><td colspan='6' class='total-label'>Total</td><td class='total-value'>" + inv.SubTotal.ToString("N2") + "</td></tr>"
            + taxRows
            + roundOffRow
            + "<tr><td colspan='6' class='total-label'>Grand Total Amount</td><td class='total-value'>" + inv.TotalAmount.ToString("N2") + "</td></tr>"
            + "<tr><td colspan='7' class='words'>Rupees - " + Html(words) + "</td></tr></tbody></table>"
            + "<table class='doc-grid'><tr><td class='footer-left compliance'>"
            + "Shop Lic.No. &nbsp;&nbsp; : &nbsp;" + Html(shopLicense) + "<br/>"
            + "P.F.No. &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; : &nbsp;" + Html(pfNumber) + "<br/>"
            + "ESIC Code No. : &nbsp;" + Html(esicNumber) + "<br/>"
            + "Prof. Tax No. &nbsp;&nbsp; : &nbsp;" + Html(profTax) + "<br/>"
            + "PAN CARD NO.: &nbsp;" + Html(companyPan) + "<br/>"
            + "GST No. &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; : &nbsp;" + Html(companyGst) + "<br/>"
            + "MSME NO &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; : &nbsp;" + Html(msmeNumber) + "</td>"
            + "<td class='signature'>" + DocumentBranding.BuildSignatureHtml(DocumentBranding.DefaultCompanyName) + "</td></tr>"
            + "<tr><td class='certification'>" + Html(inv.CertificationNote) + "</td>"
            + "<td class='footer-right'><span class='send-title'>Send Invoice To : </span><br/>" + Html(inv.SendInvoiceTo).Replace("\n", "<br/>") + "</td></tr>"
            + "</table></div></div></body></html>";
        }

        public string BuildTemplateComparison(Invoice inv)
        {
            var checks = new List<string>();
            checks.Add(Check("HVAC template selected", !string.IsNullOrWhiteSpace(inv.TemplateCode)));
            checks.Add(Check("Workflow tagged", !string.IsNullOrWhiteSpace(inv.WorkflowType)));
            checks.Add(Check("Client selected", inv.ClientID > 0));
            checks.Add(Check("Site selected", inv.SiteID > 0));
            checks.Add(Check("Subject line present", !string.IsNullOrWhiteSpace(inv.Subject)));
            checks.Add(Check("Checklist captured", !string.IsNullOrWhiteSpace(inv.ServiceChecklist)));
            checks.Add(Check("Asset / equipment info present", !string.IsNullOrWhiteSpace(inv.AssetDetails)));
            checks.Add(Check("Payment terms present", !string.IsNullOrWhiteSpace(inv.PaymentTerms)));
            checks.Add(Check("Place of supply present", !string.IsNullOrWhiteSpace(inv.PlaceOfSupply)));
            checks.Add(Check("Send invoice to block present", !string.IsNullOrWhiteSpace(inv.SendInvoiceTo)));
            checks.Add(Check("Certification note present", !string.IsNullOrWhiteSpace(inv.CertificationNote)));
            checks.Add(Check("At least one line item", inv.LineItems != null && inv.LineItems.Count > 0));
            checks.Add(Check("HSN/SAC on all lines", inv.LineItems != null && inv.LineItems.TrueForAll(li => !string.IsNullOrWhiteSpace(li.HSNCode))));
            checks.Add(Check("GST mode captured", !string.IsNullOrWhiteSpace(inv.GSTMode)));
            checks.Add(Check("Warranty / coverage reviewed", !string.IsNullOrWhiteSpace(inv.WarrantyStatus) || !string.IsNullOrWhiteSpace(inv.ContractCoverageType)));
            return string.Join(Environment.NewLine, checks);
        }

        public string GetBehavioralNudges(Invoice inv)
        {
            var nudges = new List<string>();
            if (inv == null) return string.Empty;

            if (string.IsNullOrWhiteSpace(inv.TemplateCode))
                nudges.Add("Select an HVAC template to autofill subject, checklist, and standard line items.");
            if (string.IsNullOrWhiteSpace(inv.Subject))
                nudges.Add("Add a subject line to improve approval confidence.");
            if (string.IsNullOrWhiteSpace(inv.PaymentTerms))
                nudges.Add("Payment terms are missing. Add them before sending.");
            if (string.Equals(inv.TemplateCode, "AMC_QUARTERLY_SPLIT_AC", StringComparison.OrdinalIgnoreCase) && inv.ContractID <= 0)
                nudges.Add("AMC visit selected without a linked contract. Link the AMC before finalisation.");
            if (string.Equals(inv.TemplateCode, "REPAIR_BREAKDOWN_AC", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(inv.WarrantyStatus))
                nudges.Add("Check warranty before billing repair spares.");
            if (inv.PaymentStatus == "Draft")
                nudges.Add("Draft invoice is ready. Preview and send it today to accelerate collections.");
            if (inv.PaymentStatus == "Pending" && inv.DueDate <= DateTime.Today.AddDays(3))
                nudges.Add("Payment is close to due. Send a reminder with the invoice preview.");
            if (inv.TotalAmount >= 100000)
                nudges.Add("High-value invoice. Consider WhatsApp plus email follow-up for faster acceptance.");
            if (inv.LineItems != null && inv.LineItems.Any(li => li.IsStockItem))
                nudges.Add(_invoiceInventorySvc.BuildAvailabilitySummary(inv.LineItems));
            if (inv.NextServiceDueDate.HasValue)
                nudges.Add("Next service due: " + inv.NextServiceDueDate.Value.ToString("dd MMM yyyy") + ".");

            return nudges.Count == 0 ? "Invoice looks ready to send." : string.Join(Environment.NewLine, nudges);
        }

        public void UpdateLineItems(int invoiceId, List<InvoiceLineItem> items)
        {
            Invoice invoice = GetInvoiceById(invoiceId);
            if (invoice == null)
                throw new Exception("Invoice not found.");

            invoice.LineItems = items ?? new List<InvoiceLineItem>();
            UpdateInvoiceWithLineItems(invoice);
        }

        public string GetInventorySummary(Invoice invoice)
        {
            if (invoice == null)
                return string.Empty;

            return _invoiceInventorySvc.BuildAvailabilitySummary(invoice.LineItems ?? new List<InvoiceLineItem>());
        }

        private string ResolveCoverageType(InvoiceTemplate template, AMCContract contract)
        {
            if (!string.IsNullOrWhiteSpace(contract?.ContractType))
                return contract.ContractType;

            if (!string.IsNullOrWhiteSpace(template?.ContractCoverageType))
                return template.ContractCoverageType;

            if (string.Equals(template?.TemplateCode, "AMC_QUARTERLY_SPLIT_AC", StringComparison.OrdinalIgnoreCase))
                return "Non-Comprehensive AMC";

            return "Billable Service";
        }

        private int ResolvePaymentTermDays(string paymentTerms)
        {
            if (string.IsNullOrWhiteSpace(paymentTerms))
                return 30;

            string digits = new string(paymentTerms.Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out int days) && days > 0)
                return days;

            return 30;
        }

        private string FillTokens(string templateText, B2BClient client, ClientSite site, AMCContract contract)
        {
            if (string.IsNullOrWhiteSpace(templateText))
                return string.Empty;

            string result = templateText;
            result = result.Replace("{CLIENT}", client?.CompanyName ?? string.Empty);
            result = result.Replace("{SITE}", site?.SiteName ?? string.Empty);
            result = result.Replace("{LOCATION}", site?.City ?? site?.Address ?? string.Empty);
            result = result.Replace("{CONTRACT}", contract != null ? ("AMC-" + contract.ContractID) : string.Empty);
            result = result.Replace("{DATE}", DateTime.Today.ToString("dd/MM/yyyy"));
            return result.Trim();
        }

        private string ResolveAssetDetails(InvoiceTemplate template, AMCContract contract, ClientSite site)
        {
            var lines = new List<string>();

            if (!string.IsNullOrWhiteSpace(template?.DefaultAssetInfo))
                lines.Add(template.DefaultAssetInfo);
            if (!string.IsNullOrWhiteSpace(contract?.ContractType))
                lines.Add("Contract Type: " + contract.ContractType);
            if (contract != null)
                lines.Add("Contract No: AMC-" + contract.ContractID);
            if (!string.IsNullOrWhiteSpace(site?.SiteName))
                lines.Add("Site: " + site.SiteName);
            if (!string.IsNullOrWhiteSpace(site?.Address))
                lines.Add("Address: " + site.Address);

            return string.Join(Environment.NewLine, lines.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct());
        }

        private string ResolveWarrantyStatus(AMCContract contract)
        {
            if (contract == null)
                return "Out of Warranty";

            if (contract.EndDate >= DateTime.Today)
                return "Under Contract";

            return "Out of Warranty";
        }

        private DateTime? ResolveWarrantyExpiry(AMCContract contract)
        {
            if (contract == null)
                return null;

            return contract.EndDate == default ? (DateTime?)null : contract.EndDate;
        }

        private bool IsPreventiveFlow(InvoiceTemplate template)
        {
            if (template == null)
                return false;

            return string.Equals(template.TemplateCode, "AMC_QUARTERLY_SPLIT_AC", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(template.WorkflowType) &&
                    template.WorkflowType.IndexOf("Preventive", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private DateTime? CalculateNextServiceDueDate(DateTime anchorDate, AMCContract contract, string workflowType)
        {
            if (!string.IsNullOrWhiteSpace(workflowType) &&
                workflowType.IndexOf("AMC", StringComparison.OrdinalIgnoreCase) >= 0)
                return anchorDate.AddMonths(3);

            if (!string.IsNullOrWhiteSpace(workflowType) &&
                workflowType.IndexOf("Preventive", StringComparison.OrdinalIgnoreCase) >= 0)
                return anchorDate.AddMonths(3);

            if (contract != null && contract.EndDate > anchorDate)
                return anchorDate.AddMonths(6);

            return null;
        }

        private List<InvoiceLineItem> BuildTemplateLines(string templateCode, Invoice invoice, AMCContract contract)
        {
            switch ((templateCode ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "SERVICE_SPLIT_AC":
                    return new List<InvoiceLineItem>
                    {
                        MakeServiceLine("Split AC service visit charge", "9987", "Visit", 1m, 1800m, 18m, true, null),
                        MakeServiceLine("Filter cleaning and performance checks", "9987", "Visit", 1m, 650m, 18m, true, "Checklist work included"),
                        MakeStockAwareLine("Consumables / insulation tape", "Insulation Tape", 1m, 180m, true, "Optional consumables as used")
                    };

                case "AMC_QUARTERLY_SPLIT_AC":
                    bool comprehensive = string.Equals(invoice.ContractCoverageType, "Comprehensive AMC", StringComparison.OrdinalIgnoreCase);
                    return new List<InvoiceLineItem>
                    {
                        MakeServiceLine("Quarterly preventive AMC visit", "9987", "Visit", 1m, contract?.MonthlyValue > 0 ? contract.MonthlyValue : 2500m, 18m, true, "Scheduled preventive maintenance"),
                        MakeServiceLine(
                            comprehensive ? "Routine consumables covered under comprehensive AMC" : "Routine consumables and spares",
                            "9987",
                            "Visit",
                            1m,
                            comprehensive ? 0m : 950m,
                            18m,
                            !comprehensive,
                            comprehensive ? "Included under comprehensive AMC" : "Billable for non-comprehensive AMC")
                    };

                case "REPAIR_BREAKDOWN_AC":
                    bool covered = string.Equals(invoice.WarrantyStatus, "Under Warranty", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(invoice.ContractCoverageType, "Comprehensive AMC", StringComparison.OrdinalIgnoreCase);
                    return new List<InvoiceLineItem>
                    {
                        MakeServiceLine("Breakdown diagnosis and repair labour", "9987", "Job", 1m, covered ? 0m : 3200m, 18m, !covered, covered ? "Covered under warranty / contract" : "Labour billed"),
                        MakeStockAwareLine("Capacitor replacement", "Capacitor", 1m, 850m, !covered, covered ? "Covered under warranty / contract" : "Suggested spare part"),
                        MakeStockAwareLine("Contactor relay", "Contactor", 1m, 1450m, !covered, covered ? "Covered under warranty / contract" : "Suggested spare part")
                    };

                case "INSTALL_SPLIT_AC":
                    return new List<InvoiceLineItem>
                    {
                        MakeServiceLine("Split AC installation labour", "9987", "Job", 1m, 4500m, 18m, true, "Installation crew charges"),
                        MakeStockAwareLine("Copper pipe set", "Copper Pipe", 1m, 2800m, true, "Installation material"),
                        MakeStockAwareLine("Drain pipe", "Drain Pipe", 1m, 650m, true, "Installation material"),
                        MakeStockAwareLine("Electrical wiring and lugs", "Wiring", 1m, 950m, true, "Installation material"),
                        MakeStockAwareLine("Outdoor unit bracket / stand", "Bracket", 1m, 1200m, true, "Mounting hardware"),
                        MakeServiceLine("Gas charging, testing and commissioning", "9987", "Job", 1m, 1800m, 18m, true, "Final testing and commissioning"),
                        MakeServiceLine("Transport and mobilization", "9966", "Job", 1m, 1200m, 18m, true, "Transport / logistics")
                    };
            }

            return new List<InvoiceLineItem>
            {
                MakeServiceLine("HVAC service", "9987", "Job", 1m, 1000m, 18m, true, null)
            };
        }

        private InvoiceLineItem MakeServiceLine(string description, string hsnSac, string unit, decimal quantity, decimal rate, decimal gstPercent, bool isBillable, string coverageNote)
        {
            return new InvoiceLineItem
            {
                Description = description,
                HSNCode = hsnSac,
                Category = InferInvoiceLineCategory(description, false),
                Unit = string.IsNullOrWhiteSpace(unit) ? "Nos" : unit,
                Quantity = quantity <= 0 ? 1m : quantity,
                Rate = rate,
                GSTPercent = gstPercent <= 0 ? 18m : gstPercent,
                TaxType = gstPercent <= 0 ? "Nil Rated" : "Taxable",
                IsBillable = isBillable,
                IsStockItem = false,
                CoverageNote = coverageNote,
                Amount = isBillable ? Math.Round(quantity * rate, 2) : 0m
            };
        }

        private InvoiceLineItem MakeStockAwareLine(string description, string stockLookupName, decimal quantity, decimal defaultRate, bool isBillable, string coverageNote)
        {
            StockItem stock = _inventorySvc.GetByName(stockLookupName);
            decimal rate = stock != null && stock.LastPurchaseRate > 0 ? stock.LastPurchaseRate : defaultRate;

            return new InvoiceLineItem
            {
                Description = stock?.ItemName ?? description,
                HSNCode = "9987",
                Category = InferInvoiceLineCategory(stock?.Category ?? description, true),
                Unit = stock?.Unit ?? "Nos",
                Quantity = quantity <= 0 ? 1m : quantity,
                Rate = rate,
                GSTPercent = 18m,
                TaxType = "Taxable",
                StockItemID = stock?.ItemID,
                IsStockItem = stock != null,
                IsBillable = isBillable,
                CoverageNote = coverageNote,
                Amount = isBillable ? Math.Round(quantity * rate, 2) : 0m
            };
        }

        private static string InferInvoiceLineCategory(string value, bool materialFallback)
        {
            string probe = (value ?? string.Empty).ToLowerInvariant();
            if (probe.Contains("labour") || probe.Contains("labor") || probe.Contains("technician")) return "Labour";
            if (probe.Contains("amc") || probe.Contains("contract")) return "AMC";
            if (probe.Contains("service") || probe.Contains("visit") || probe.Contains("charging") || probe.Contains("commission")) return "Service";
            if (probe.Contains("spare") || probe.Contains("capacitor") || probe.Contains("contactor")) return "Spare";
            return materialFallback ? "Material" : "Service";
        }

        private bool IsPreventiveInvoice(Invoice invoice)
        {
            if (invoice == null)
                return false;

            return string.Equals(invoice.TemplateCode, "AMC_QUARTERLY_SPLIT_AC", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(invoice.WorkflowType) &&
                    invoice.WorkflowType.IndexOf("Preventive", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private string ResolveFinalStatus(Invoice invoice)
        {
            if (invoice == null)
                return "Pending";
            if (invoice.TotalAmount <= 0)
                return "Sent";
            if (invoice.PaidAmount >= invoice.TotalAmount)
                return "Paid";
            if (invoice.PaidAmount > 0)
                return "Partial";
            if (invoice.DueDate < DateTime.Today)
                return "Overdue";
            return "Pending";
        }

        private void MarkInventoryReservationStatus(int invoiceId, string status)
        {
            Invoice current = GetInvoiceById(invoiceId);
            if (current == null)
                return;

            current.InventoryReservationStatus = status;
            _invoiceRepo.Update(current);
        }

        public void AddPendingCharge(int workOrderId, string description, decimal quantity, decimal unitRate, string hsnSac, decimal gstRate, int sourcePoId)
        {
            SessionManager.DemandPermission("Invoices", "Create");
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand exists = new SqlCommand(@"
                    SELECT TOP 1 1
                    FROM PendingCharges
                    WHERE WorkOrderId = @workOrderId
                      AND SourcePoId = @sourcePoId
                      AND Description = @description
                      AND IsBilled = 0", conn))
                {
                    exists.Parameters.AddWithValue("@workOrderId", workOrderId);
                    exists.Parameters.AddWithValue("@sourcePoId", sourcePoId);
                    exists.Parameters.AddWithValue("@description", description ?? string.Empty);
                    if (exists.ExecuteScalar() != null)
                        return;
                }

                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO PendingCharges
                        (WorkOrderId, Description, Qty, Rate, HsnSac, GSTRate, SourcePoId, CreatedDate, IsBilled)
                    VALUES
                        (@workOrderId, @description, @qty, @rate, @hsnSac, @gstRate, @sourcePoId, GETDATE(), 0)", conn))
                {
                    cmd.Parameters.AddWithValue("@workOrderId", workOrderId);
                    cmd.Parameters.AddWithValue("@description", description ?? string.Empty);
                    cmd.Parameters.AddWithValue("@qty", quantity);
                    cmd.Parameters.AddWithValue("@rate", unitRate);
                    cmd.Parameters.AddWithValue("@hsnSac", string.IsNullOrWhiteSpace(hsnSac) ? (object)DBNull.Value : hsnSac.Trim());
                    cmd.Parameters.AddWithValue("@gstRate", gstRate);
                    cmd.Parameters.AddWithValue("@sourcePoId", sourcePoId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public DateTime? GetPendingChargeCreatedDate(int sourcePoId)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT TOP 1 CreatedDate FROM PendingCharges WHERE SourcePoId = @sourcePoId ORDER BY CreatedDate DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@sourcePoId", sourcePoId);
                    object result = cmd.ExecuteScalar();
                    return result == null || result == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(result);
                }
            }
        }

        public DataTable GetPendingChargesReport(bool unbilledOnly)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT
                        j.JobNumber AS [Work Order],
                        c.CompanyName AS [Client],
                        pc.Description AS [Item],
                        pc.Qty AS [Qty],
                        pc.Rate AS [Rate],
                        (pc.Qty * pc.Rate) AS [Amount],
                        po.PONumber AS [Source PO],
                        pc.CreatedDate AS [Date Added],
                        CASE WHEN pc.IsBilled = 1 THEN 'Y' ELSE 'N' END AS [Billed]
                    FROM PendingCharges pc
                    INNER JOIN Jobs j ON pc.WorkOrderId = j.JobID
                    LEFT JOIN B2BClients c ON j.ClientID = c.ClientID
                    LEFT JOIN PurchaseOrders po ON pc.SourcePoId = po.POID
                    WHERE (@unbilledOnly = 0 OR pc.IsBilled = 0)
                    ORDER BY pc.CreatedDate DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@unbilledOnly", unbilledOnly);
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable table = new DataTable();
                        adapter.Fill(table);
                        return table;
                    }
                }
            }
        }

        private string Check(string label, bool passed)
        {
            return (passed ? "[OK] " : "[Missing] ") + label;
        }

        private static string GetSetting(Dictionary<string, string> settings, string key, string fallback)
        {
            return settings.ContainsKey(key) && !string.IsNullOrWhiteSpace(settings[key]) ? settings[key] : fallback;
        }

        private static string Html(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty).Replace(Environment.NewLine, "<br/>");
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values ?? new string[0])
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }
            return string.Empty;
        }

        private static string ToWords(long number)
        {
            if (number == 0) return "Zero";
            if (number < 0) return "Minus " + ToWords(Math.Abs(number));
            string[] units = { "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
            string[] tens = { "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

            Func<long, string> underHundred = null;
            underHundred = n =>
            {
                if (n < 20) return units[n];
                return tens[n / 10] + (n % 10 > 0 ? " " + units[n % 10] : "");
            };

            Func<long, string> convert = null;
            convert = n =>
            {
                if (n < 100) return underHundred(n);
                if (n < 1000) return units[n / 100] + " Hundred" + (n % 100 > 0 ? " " + convert(n % 100) : "");
                if (n < 100000) return convert(n / 1000) + " Thousand" + (n % 1000 > 0 ? " " + convert(n % 1000) : "");
                if (n < 10000000) return convert(n / 100000) + " Lakh" + (n % 100000 > 0 ? " " + convert(n % 100000) : "");
                return convert(n / 10000000) + " Crore" + (n % 10000000 > 0 ? " " + convert(n % 10000000) : "");
            };

            return convert(number);
        }
    }
}

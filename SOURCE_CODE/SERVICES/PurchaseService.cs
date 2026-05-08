using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Models.Validation;
using HVAC_Pro_Desktop.Services.Audit;
using HVAC_Pro_Desktop.Services.Validation;

namespace HVAC_Pro_Desktop.Services
{
    public class PurchaseService
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(45);
        private readonly PurchaseRepository _repo = new PurchaseRepository();
        private readonly VendorService _vendorService = new VendorService();
        private readonly InventoryService _inventoryService = new InventoryService();
        private readonly SettingsService _settingsService = new SettingsService();
        private readonly SiteService _siteService = new SiteService();
        private readonly EmployeeService _employeeService = new EmployeeService();
        private readonly InvoiceService _invoiceService = new InvoiceService();
        private readonly VendorAdvancePaymentService _vendorAdvanceService = new VendorAdvancePaymentService();
        private readonly JobService _jobService = new JobService();
        private readonly BusinessRuleEngine _businessRules = new BusinessRuleEngine();
        private readonly CalculationVerificationService _calculationVerifier = new CalculationVerificationService();
        private readonly GlobalValidationEngine _validation = new GlobalValidationEngine();
        private readonly AuditTrailService _audit = new AuditTrailService();

        public List<PurchaseOrder> GetAll() => AppDataCache.GetOrCreate("purchases:all", CacheTtl, _repo.GetAll);
        public List<PurchaseOrder> GetAllFresh() => _repo.GetAll();
        public PurchaseOrder GetById(int id) => _repo.GetById(id);
        public List<PurchaseOrder> GetByVendorId(int vendorId) => GetAll().FindAll(p => p.VendorID == vendorId);
        public int Create(PurchaseOrder po)
        {
            SessionManager.DemandPermission("Purchases", "Create");
            if (po == null)
                throw new Exception("Purchase order details are missing.");
            po.PayByDate = AutoSuggestPayByDate(po.PODate, po.VendorID, po.PayByDate);
            ValidatePurchaseOrderForSave(po);
            if (SessionManager.IsLoggedIn)
            {
                po.CreatedByUserId = SessionManager.CurrentUser.UserId;
                po.CreatedByName = SessionManager.CurrentUser.DisplayName;
                po.CreatedByDate = DateTime.Now;
            }
            int id = _repo.Create(po);
            _vendorService.RefreshVendorPurchaseTotals(po.VendorID);
            AppDataCache.RemovePrefix("purchases:");
            SessionManager.LogAction("CREATE", "Purchases", id, "Purchase order saved");
            _audit.Record("CREATE", "Purchases", id, "Purchase order saved with data-quality validation");
            return id;
        }
        public void Update(PurchaseOrder po)
        {
            SessionManager.DemandPermission("Purchases", "Edit");
            if (po == null)
                throw new Exception("Purchase order details are missing.");
            PurchaseOrder existing = po.POID > 0 ? _repo.GetById(po.POID) : null;
            po.PayByDate = AutoSuggestPayByDate(po.PODate, po.VendorID, po.PayByDate);
            ValidatePurchaseOrderForSave(po);
            if (SessionManager.IsLoggedIn)
            {
                po.ModifiedByUserId = SessionManager.CurrentUser.UserId;
                po.ModifiedByName = SessionManager.CurrentUser.DisplayName;
                po.ModifiedDate = DateTime.Now;
            }
            _repo.Update(po);
            _vendorService.RefreshVendorPurchaseTotals(po.VendorID, existing?.VendorID ?? 0);
            AppDataCache.RemovePrefix("purchases:");
            SessionManager.LogAction("EDIT", "Purchases", po.POID, "Purchase order saved");
            _audit.Record("EDIT", "Purchases", po.POID, "Purchase order saved with data-quality validation");
        }

        public PurchaseOrder CreatePO(int supplierId, IEnumerable<TenderBidLineItem> lineItems, TenderBid tenderBid)
        {
            SessionManager.DemandPermission("Purchases", "Create");
            if (supplierId <= 0)
                throw new Exception("Supplier is required to create a purchase order.");

            List<TenderBidLineItem> rows = (lineItems ?? new List<TenderBidLineItem>())
                .Where(li => li != null && li.Shortfall > 0m)
                .ToList();

            if (rows.Count == 0)
                throw new Exception("There are no shortfall items for this supplier.");

            string prefix = "PO-" + DateTime.Now.ToString("yyyyMMdd");
            int dailyCount = GetAll().Count(existing => existing.PONumber != null && existing.PONumber.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

            var po = new PurchaseOrder
            {
                VendorID = supplierId,
                ClientID = tenderBid?.ClientID ?? 0,
                SiteID = tenderBid?.SiteID ?? 0,
                RecommendedByBidID = tenderBid?.BidID > 0 ? (int?)tenderBid.BidID : null,
                PONumber = prefix + "-" + (dailyCount + 1).ToString("D3"),
                PODate = DateTime.Today,
                PayByDate = AutoSuggestPayByDate(DateTime.Today, supplierId),
                Status = "Pending",
                ComparisonNotes = tenderBid?.ComparisonSummary,
                Notes = "Auto-created from quotation " + (tenderBid?.QuotationNumber ?? "draft"),
                TotalAmount = 0m
            };

            foreach (TenderBidLineItem row in rows)
            {
                decimal amount = Math.Round(row.Shortfall * row.CostPerUnit, 2);
                po.LineItems.Add(new PurchaseLineItem
                {
                    InventoryItemId = row.InventoryItemId,
                    Description = row.ItemDescription,
                    Quantity = row.Shortfall,
                    Rate = row.CostPerUnit,
                    Amount = amount
                });
                po.TotalAmount += amount;
            }

            po.POID = Create(po);
            return GetById(po.POID) ?? po;
        }
        public decimal GetTotalSpendThisMonth()
        {
            decimal total = 0;
            foreach (var po in GetAll())
            {
                if (po.PODate.Month == DateTime.Today.Month && po.PODate.Year == DateTime.Today.Year)
                    total += po.TotalAmount;
            }
            return total;
        }
        public decimal GetTotalPurchaseSpendThisMonth() => _repo.GetTotalSpendThisMonth();
        public void MarkReceived(int poId)
        {
            SessionManager.DemandPermission("Purchases", "Edit");
            _repo.MarkReceived(poId);
            AppDataCache.RemovePrefix("purchases:");
            SessionManager.LogAction("EDIT", "Purchases", poId, "Purchase order marked received");
        }
        public void UpdateContractLink(int poId, int contractId)
        {
            SessionManager.DemandPermission("Purchases", "Edit");
            _repo.UpdateContractLink(poId, contractId);
            AppDataCache.RemovePrefix("purchases:");
        }

        public List<PurchaseOrder> GetPendingPayments()
        {
            return GetAll()
                .Where(p => p != null && p.BalanceDue > 0.01m)
                .OrderBy(p => p.PayByDate == default ? p.PODate : p.PayByDate)
                .ThenByDescending(p => p.IsOverdue)
                .ThenBy(p => p.PODate)
                .ToList();
        }

        public List<PurchaseOrder> GetPendingPaymentsFresh()
        {
            return GetAllFresh()
                .Where(p => p != null && p.BalanceDue > 0.01m)
                .OrderBy(p => p.PayByDate == default ? p.PODate : p.PayByDate)
                .ThenByDescending(p => p.IsOverdue)
                .ThenBy(p => p.PODate)
                .ToList();
        }

        public int GetOverduePaymentsCount()
        {
            return GetPendingPayments().Count(p => p.IsOverdue);
        }

        public int GetOverduePaymentsCountFresh()
        {
            return GetPendingPaymentsFresh().Count(p => p.IsOverdue);
        }

        public List<VendorPayableGroup> GetVendorPayables()
        {
            return GetPendingPayments()
                .GroupBy(p => new { p.VendorID, VendorName = string.IsNullOrWhiteSpace(p.VendorName) ? "Unknown Vendor" : p.VendorName })
                .Select(g => new VendorPayableGroup
                {
                    VendorID = g.Key.VendorID,
                    VendorName = g.Key.VendorName,
                    TotalOutstanding = g.Sum(x => x.BalanceDue),
                    OverdueCount = g.Count(x => x.IsOverdue),
                    Purchases = g.OrderBy(x => x.PayByDate).ThenBy(x => x.PODate).ToList()
                })
                .OrderByDescending(g => g.OverdueCount)
                .ThenBy(g => g.Purchases.FirstOrDefault()?.PayByDate ?? DateTime.MaxValue)
                .ThenBy(g => g.VendorName)
                .ToList();
        }

        public List<VendorPayableGroup> GetVendorPayablesFresh()
        {
            return GetPendingPaymentsFresh()
                .GroupBy(p => new { p.VendorID, VendorName = string.IsNullOrWhiteSpace(p.VendorName) ? "Unknown Vendor" : p.VendorName })
                .Select(g => new VendorPayableGroup
                {
                    VendorID = g.Key.VendorID,
                    VendorName = g.Key.VendorName,
                    TotalOutstanding = g.Sum(x => x.BalanceDue),
                    OverdueCount = g.Count(x => x.IsOverdue),
                    Purchases = g.OrderBy(x => x.PayByDate).ThenBy(x => x.PODate).ToList()
                })
                .OrderByDescending(g => g.OverdueCount)
                .ThenBy(g => g.Purchases.FirstOrDefault()?.PayByDate ?? DateTime.MaxValue)
                .ThenBy(g => g.VendorName)
                .ToList();
        }

        public void BatchMarkPaid(IEnumerable<int> poIds, string paymentReference)
        {
            SessionManager.DemandPermission("Payments", "Create");
            List<int> ids = (poIds ?? Enumerable.Empty<int>()).Where(id => id > 0).Distinct().ToList();
            decimal advanceApplied = 0m;
            foreach (int poId in ids)
                advanceApplied += _vendorAdvanceService.ApplyAvailableAdvanceToPurchaseOrder(GetById(poId));
            string finalReference = paymentReference;
            if (advanceApplied > 0m)
                finalReference = (string.IsNullOrWhiteSpace(finalReference) ? string.Empty : finalReference.Trim() + " | ") + "Advance adjusted " + advanceApplied.ToString("N2");
            _repo.BatchMarkPaid(ids, finalReference);
            AppDataCache.RemovePrefix("purchases:");
            SessionManager.LogAction("EDIT", "Purchases", null, "Purchase orders batch paid");
        }

        public DateTime AutoSuggestPayByDate(DateTime purchaseDate, int vendorId, DateTime? currentValue = null)
        {
            DateTime baseDate = (purchaseDate == default ? DateTime.Today : purchaseDate).Date;
            int creditDays = Math.Max(1, _vendorService.GetById(vendorId)?.DefaultCreditDays ?? 30);

            if (currentValue.HasValue && currentValue.Value.Year >= 2020 && currentValue.Value.Year <= baseDate.Year + 2)
                return currentValue.Value.Date;

            return baseDate.AddDays(creditDays);
        }

        public PurchaseOrder OnDeliveryModeChanged(int poId, string mode, int siteId)
        {
            if (poId > 0)
                SessionManager.DemandPermission("Purchases", "Edit");
            string deliveryMode = string.Equals(mode, "SiteDelivery", StringComparison.OrdinalIgnoreCase) ? "SiteDelivery" : "TechPickup";
            string address = null;
            if (deliveryMode == "SiteDelivery" && siteId > 0)
            {
                ClientSite site = _siteService.GetById(siteId);
                if (site != null)
                    address = BuildSiteAddress(site);
            }

            if (poId > 0)
                _repo.UpdateDeliveryDetails(poId, deliveryMode, address);

            AppDataCache.RemovePrefix("purchases:");
            PurchaseOrder po = poId > 0 ? _repo.GetById(poId) : new PurchaseOrder();
            po.DeliveryMode = deliveryMode;
            po.DeliveryAddress = address;
            return po;
        }

        public PurchaseOrder OnTechnicianAssigned(int poId, int employeeId)
        {
            if (poId > 0)
                SessionManager.DemandPermission("Purchases", "Edit");
            Employee employee = _employeeService.GetById(employeeId);
            if (poId > 0)
                _repo.UpdateAssignedTechnician(poId, employee?.EmployeeID, employee?.Name);

            AppDataCache.RemovePrefix("purchases:");
            PurchaseOrder po = poId > 0 ? _repo.GetById(poId) : new PurchaseOrder();
            po.AssignedTechnicianId = employee?.EmployeeID;
            po.AssignedTechnicianName = employee?.Name;
            return po;
        }

        public bool CheckLineItemPriceVariance(PurchaseLineItem lineItem)
        {
            if (lineItem == null || string.IsNullOrWhiteSpace(lineItem.Description))
                return false;

            decimal lastRate = _inventoryService.GetLastPurchaseRate(lineItem.Description);
            lineItem.HistoricalRate = lastRate;
            lineItem.PriceVariance = 0m;
            if (lastRate <= 0m || lineItem.Rate <= 0m)
                return false;

            decimal threshold = Math.Round(lastRate * 1.10m, 2);
            if (lineItem.Rate > threshold)
            {
                lineItem.PriceVariance = Math.Round(((lineItem.Rate - lastRate) / lastRate) * 100m, 2);
                return true;
            }

            return false;
        }

        public PendingChargeResult CreatePendingCharge(int poId)
        {
            SessionManager.DemandPermission("Invoices", "Create");
            PurchaseOrder po = GetById(poId);
            if (po == null)
                return new PendingChargeResult { Skipped = true, Message = "Purchase order not found." };
            if (!po.AddToClientInvoice)
                return new PendingChargeResult { Skipped = true, Message = "Client billing is disabled for this purchase order." };
            if (po.PendingChargeCreated)
            {
                DateTime? existingDate = _invoiceService.GetPendingChargeCreatedDate(poId);
                return new PendingChargeResult
                {
                    AlreadyExists = true,
                    Message = existingDate.HasValue ? "Charge already added on " + existingDate.Value.ToString("dd/MM/yyyy") : "Charge already added.",
                    WorkOrderName = ResolveWorkOrderName(po.LinkedToId),
                    CreatedDate = existingDate
                };
            }
            if (!string.Equals(po.LinkedToType, "WorkOrder", StringComparison.OrdinalIgnoreCase) || !po.LinkedToId.HasValue)
                return new PendingChargeResult { Skipped = true, Message = "Link to a Work Order first." };

            int createdCount = 0;
            foreach (PurchaseLineItem lineItem in po.LineItems.Where(li => string.Equals(li.JobLink, "Job", StringComparison.OrdinalIgnoreCase)))
            {
                _invoiceService.AddPendingCharge(
                    po.LinkedToId.Value,
                    lineItem.Description,
                    lineItem.Quantity,
                    lineItem.Rate,
                    lineItem.HsnSacCode,
                    lineItem.GSTRate,
                    poId);
                createdCount++;
            }

            if (createdCount == 0)
                return new PendingChargeResult { Skipped = true, Message = "No job-linked line items are available to bill." };

            _repo.UpdatePendingChargeStatus(poId, true);
            AppDataCache.RemovePrefix("purchases:");

            string workOrderName = ResolveWorkOrderName(po.LinkedToId);
            string message = "Pending charge added to Work Order " + workOrderName + ". It will appear on the next invoice.";
            LogPendingCharge("CREATE", poId, workOrderName, createdCount, message);
            return new PendingChargeResult
            {
                Created = true,
                Message = message,
                WorkOrderName = workOrderName,
                CreatedDate = DateTime.Now
            };
        }

        public PurchaseOrder SetCreatedBy(int poId)
        {
            if (poId > 0)
                SessionManager.DemandPermission("Purchases", "Edit");
            AppUserDto user = SessionManager.CurrentUser;
            DateTime stamp = DateTime.Now;
            if (poId > 0)
                _repo.UpdateCreatedBy(poId, user?.UserId, user?.DisplayName, stamp);

            AppDataCache.RemovePrefix("purchases:");
            PurchaseOrder po = poId > 0 ? _repo.GetById(poId) : new PurchaseOrder();
            po.CreatedByUserId = user?.UserId;
            po.CreatedByName = user?.DisplayName;
            po.CreatedByDate = stamp;
            return po;
        }

        public string BuildPurchaseOrderHtml(PurchaseOrder po)
        {
            if (po == null)
                throw new Exception("Purchase order details are missing.");

            Vendor vendor = po.VendorID > 0 ? _vendorService.GetById(po.VendorID) : null;
            IndiaCompanySettings settings = _settingsService.GetIndiaCompanySettings();
            DateTime payByDate = AutoSuggestPayByDate(po.PODate, po.VendorID, po.PayByDate);
            decimal taxableTotal = 0m;
            decimal cgstTotal = 0m;
            decimal sgstTotal = 0m;
            decimal igstTotal = 0m;
            decimal grandTotal = 0m;

            StringBuilder rows = new StringBuilder();
            int sr = 1;
            foreach (PurchaseLineItem line in po.LineItems ?? new List<PurchaseLineItem>())
            {
                decimal taxable = Math.Round(line.Quantity * line.Rate, 2);
                decimal cgst = Math.Round(taxable * (line.CGSTRate / 100m), 2);
                decimal sgst = Math.Round(taxable * (line.SGSTRate / 100m), 2);
                decimal igst = Math.Round(taxable * (line.IGSTRate / 100m), 2);
                decimal total = taxable + cgst + sgst + igst;
                string unit = string.IsNullOrWhiteSpace(line.UOM) ? ResolveUnit(line.Description) : line.UOM;

                taxableTotal += taxable;
                cgstTotal += cgst;
                sgstTotal += sgst;
                igstTotal += igst;
                grandTotal += total;

                rows.Append("<tr>");
                rows.AppendFormat("<td class='center'>{0}</td>", sr++);
                rows.AppendFormat("<td class='desc'>{0}</td>", Html(line.Description));
                rows.AppendFormat("<td class='center'>{0}</td>", Html(line.HsnSacCode));
                rows.AppendFormat("<td class='center'>{0}</td>", Html(unit));
                rows.AppendFormat("<td class='center'>{0}</td>", line.Quantity.ToString("0.###"));
                rows.AppendFormat("<td class='num'>{0}</td>", line.Rate.ToString("N2"));
                rows.AppendFormat("<td class='num'><strong>{0}</strong></td>", taxable.ToString("N2"));
                rows.Append("</tr>");
            }

            decimal tdsDeducted = TryParseDecimal(_settingsService.Get("PurchaseOrderTDS", "0"));
            decimal netPayable = grandTotal - tdsDeducted;
            string amountWords = "Rupees " + ToWords((long)Math.Round(netPayable)) + " Only";
            string shopLicense = _settingsService.Get("CompanyShopLicense", "");
            string pfNumber = _settingsService.Get("CompanyPFNumber", "");
            string esicNumber = _settingsService.Get("CompanyESICNumber", "");
            string profTax = _settingsService.Get("CompanyProfTax", _settingsService.Get("CompanyProfessionalTax", ""));
            string msmeNumber = _settingsService.Get("CompanyMSMENumber", "");
            string subject = "Purchase order for supply of materials / services.";
            string orderDate = (po.PODate == default ? DateTime.Today : po.PODate).ToString("dd/MM/yyyy");

            return "<!DOCTYPE html><html><head><meta charset='utf-8'/><style>"
            + DocumentBranding.BuildOfficialHeaderCss()
            + DocumentBranding.BuildOfficialPrintCss()
            + "</style></head><body><div class='page'>"
            + DocumentBranding.BuildOfficialHeaderHtml()
            + new DocumentTemplateRenderer().BuildTemplateBannerHtml(CompanyDocumentTemplateType.PurchaseOrder)
            + "<div class='print-frame'><div class='doc-title'>PURCHASE ORDER</div>"
            + "<table class='doc-grid'><tr><td class='client-cell'>To,<br/>" + Html(vendor?.VendorName ?? po.VendorName) + "<br/>" + Html(vendor?.Address).Replace("\n", "<br/>") + "<br/>GST No. " + Html(vendor?.GSTNumber ?? po.VendorGSTIN) + "</td>"
            + "<td class='meta-cell'>Date : " + orderDate + "</td></tr>"
            + "<tr><td></td><td class='meta-cell'>PO No. " + Html(po.PONumber) + "</td></tr>"
            + "<tr class='subject-row'><td colspan='2'>Sub : " + Html(subject) + "</td></tr>"
            + "<tr class='po-row'><td colspan='2'>Vendor Invoice No : " + Html(po.VendorInvoiceNumber) + " &nbsp;&nbsp; Pay By Date : " + payByDate.ToString("dd/MM/yyyy") + "</td></tr>"
            + "<tr class='blank-row'><td colspan='2'></td></tr></table>"
            + "<table class='doc-grid items'><thead><tr><th style='width:54px'>Sr No.</th><th>Description</th><th style='width:92px'>HSN Code</th><th style='width:58px'>Unit</th><th style='width:58px'>Qty</th><th style='width:118px'>Rate (Rs.)</th><th style='width:126px'>Amount (Rs.)</th></tr></thead><tbody>"
            + rows.ToString()
            + "<tr><td></td><td></td><td></td><td></td><td></td><td></td><td class='total-value'>-</td></tr>"
            + "<tr><td colspan='6' class='total-label'>Total</td><td class='total-value'>" + taxableTotal.ToString("N2") + "</td></tr>"
            + "<tr><td colspan='6'>Add: CGST</td><td class='total-value'>" + cgstTotal.ToString("N2") + "</td></tr>"
            + "<tr><td colspan='6'>Add: SGST</td><td class='total-value'>" + sgstTotal.ToString("N2") + "</td></tr>"
            + "<tr><td colspan='6'>Add: IGST</td><td class='total-value'>" + igstTotal.ToString("N2") + "</td></tr>"
            + "<tr><td colspan='6'>TDS Deducted</td><td class='total-value'>" + tdsDeducted.ToString("N2") + "</td></tr>"
            + "<tr><td colspan='6' class='total-label'>Grand Total Amount</td><td class='total-value'>" + netPayable.ToString("N2") + "</td></tr>"
            + "<tr><td colspan='7' class='words'>" + Html(amountWords) + ".</td></tr></tbody></table>"
            + "<table class='doc-grid'><tr><td class='footer-left compliance'>"
            + "Shop Lic.No. &nbsp;&nbsp; : &nbsp;" + Html(shopLicense) + "<br/>"
            + "P.F.No. &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; : &nbsp;" + Html(pfNumber) + "<br/>"
            + "ESIC Code No. : &nbsp;" + Html(esicNumber) + "<br/>"
            + "Prof. Tax No. &nbsp;&nbsp; : &nbsp;" + Html(profTax) + "<br/>"
            + "PAN CARD NO.: &nbsp;" + Html(settings.PAN) + "<br/>"
            + "GST No. &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; : &nbsp;" + Html(settings.GSTIN) + "<br/>"
            + "MSME NO &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; : &nbsp;" + Html(msmeNumber) + "</td>"
            + "<td class='signature'>For New Client<br/><br/><br/><span class='small'>Authorised Signatory</span></td></tr>"
            + "<tr><td class='certification'>Please supply goods / services as per the above purchase order and agreed terms.</td>"
            + "<td class='footer-right'><span class='send-title'>Bill / Dispatch To : </span><br/>" + Html(settings.CompanyName) + "<br/>" + Html(settings.Address).Replace("\n", "<br/>") + "</td></tr>"
            + "</table></div></div></body></html>";
        }

        private string BuildTermsHtml()
        {
            string configured = _settingsService.Get("PurchaseOrderTerms", string.Empty);
            List<string> terms = configured
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .ToList();

            if (terms.Count == 0)
            {
                terms.Add("Please supply goods as per specifications above.");
                terms.Add("Payment will be made as per agreed terms.");
            }

            return "<ul>" + string.Join(string.Empty, terms.Select(t => "<li>" + Html(t) + "</li>")) + "</ul>";
        }

        private string ResolveUnit(string description)
        {
            StockItem item = _inventoryService.GetByName(description);
            return string.IsNullOrWhiteSpace(item?.Unit) ? "Nos" : item.Unit;
        }

        private string BuildSiteAddress(ClientSite site)
        {
            if (site == null)
                return string.Empty;

            List<string> parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(site.SiteName))
                parts.Add(site.SiteName);
            if (!string.IsNullOrWhiteSpace(site.Address))
                parts.Add(site.Address);
            if (!string.IsNullOrWhiteSpace(site.City))
                parts.Add(site.City);
            return string.Join(", ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }

        private string ResolveWorkOrderName(int? jobId)
        {
            if (!jobId.HasValue)
                return string.Empty;

            Job job = _jobService.GetAll().FirstOrDefault(j => j.JobID == jobId.Value);
            if (job == null)
                return "Job #" + jobId.Value;
            return string.IsNullOrWhiteSpace(job.JobNumber) ? "Job #" + job.JobID : job.JobNumber;
        }

        private void LogPendingCharge(string action, int poId, string workOrderName, int createdCount, string message)
        {
            string path = Path.Combine(@"C:\HVAC_PRO_MSE\LOGS", "pending-charges.log");
            string line = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")
                + " | " + action
                + " | PO " + poId
                + " | WorkOrder " + workOrderName
                + " | Lines " + createdCount
                + " | " + message + Environment.NewLine;
            File.AppendAllText(path, line);
        }

        private static decimal TryParseDecimal(string value)
        {
            return decimal.TryParse(value, out decimal parsed) ? parsed : 0m;
        }

        private static string Html(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty).Replace(Environment.NewLine, "<br/>");
        }

        private static string ToWords(long number)
        {
            if (number == 0) return "Zero";
            if (number < 0) return "Minus " + ToWords(Math.Abs(number));
            string[] units = { "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
            string[] tens = { "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

            Func<long, string> underHundred = null;
            underHundred = n => n < 20 ? units[n] : tens[n / 10] + (n % 10 > 0 ? " " + units[n % 10] : "");

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

        private void ValidatePurchaseOrderForSave(PurchaseOrder po)
        {
            ValidationResult result = _businessRules.ValidatePurchaseOrder(po);
            result.Merge(_calculationVerifier.VerifyPurchaseOrder(po));
            if (po != null && !string.IsNullOrWhiteSpace(po.PONumber))
            {
                bool duplicateNumber = GetAllFresh().Any(existing =>
                    existing.POID != po.POID &&
                    string.Equals((existing.PONumber ?? string.Empty).Trim(), po.PONumber.Trim(), StringComparison.OrdinalIgnoreCase));
                if (duplicateNumber)
                    result.Add(ValidationSeverity.Error, "Purchases", "PONumber", "Another purchase order already uses this PO number.", "Open the existing PO or generate a new PO number.");
            }
            _validation.EnsureValid(result, "Purchase order validation failed");
        }
    }
}

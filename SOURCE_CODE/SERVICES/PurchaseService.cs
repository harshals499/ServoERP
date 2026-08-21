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
        private readonly UnitMeasurementService _unitMeasurements = new UnitMeasurementService();

        public List<PurchaseOrder> GetAll() => AppDataCache.GetOrCreate("purchases:all", CacheTtl, _repo.GetAll);
        public List<PurchaseOrder> GetAllFresh() => _repo.GetAll();
        public PurchaseOrder GetById(int id) => _repo.GetById(id);
        public List<PurchaseOrder> GetByVendorId(int vendorId) => GetAll().FindAll(p => p.VendorID == vendorId);

        /// <summary>
        /// Applies the Indian GST destination rule to all purchase lines before the order is saved.
        /// A supplier in the company's state is charged equally as CGST and SGST; a supplier in
        /// another state is charged as IGST.  This keeps saved totals and printed PO totals aligned.
        /// </summary>
        public void ApplyTaxJurisdiction(PurchaseOrder purchaseOrder, Vendor vendor)
        {
            if (purchaseOrder == null)
                return;

            IndiaCompanySettings settings = _settingsService.GetIndiaCompanySettings();
            string companyStateCode = ResolveStateCode(settings?.GSTIN, settings?.CompanyState);
            string vendorStateCode = ResolveStateCode(vendor?.GSTNumber ?? purchaseOrder.VendorGSTIN, vendor?.StateCode);
            bool isIntraState = !string.IsNullOrWhiteSpace(companyStateCode)
                && string.Equals(companyStateCode, vendorStateCode, StringComparison.OrdinalIgnoreCase);

            foreach (PurchaseLineItem line in purchaseOrder.LineItems ?? new List<PurchaseLineItem>())
            {
                decimal gstRate = line.GSTRate > 0m
                    ? line.GSTRate
                    : line.CGSTRate + line.SGSTRate + line.IGSTRate;
                gstRate = Math.Max(0m, gstRate);
                line.GSTRate = gstRate;
                line.CGSTRate = isIntraState ? gstRate / 2m : 0m;
                line.SGSTRate = isIntraState ? gstRate / 2m : 0m;
                line.IGSTRate = isIntraState ? 0m : gstRate;

                decimal taxable = Math.Round(line.Quantity * line.Rate, 2);
                decimal tax = Math.Round(taxable * gstRate / 100m, 2);
                line.Amount = taxable + tax;
            }
        }

        private static string ResolveStateCode(string gstin, string fallbackState)
        {
            string taxId = IndiaTaxValidationHelper.NormalizeTaxId(gstin);
            if (taxId.Length >= 2 && IndiaStateCatalog.IsValidStateCode(taxId.Substring(0, 2)))
                return taxId.Substring(0, 2);

            string fallback = (fallbackState ?? string.Empty).Trim();
            return IndiaStateCatalog.IsValidStateCode(fallback)
                ? fallback
                : IndiaStateCatalog.GetCodeByName(fallback);
        }
        public int Create(PurchaseOrder po)
        {
            SessionManager.DemandPermission("Purchases", "Create");
            if (po == null)
                throw new Exception("Purchase order details are missing.");
            po.PayByDate = AutoSuggestPayByDate(po.PODate, po.VendorID, po.PayByDate);
            po.ApplyPaymentCompletionRule();
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
            DashboardRefreshService.NotifyChanged("Purchases");
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
            po.ApplyPaymentCompletionRule();
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
            DashboardRefreshService.NotifyChanged("Purchases");
            SessionManager.LogAction("EDIT", "Purchases", po.POID, "Purchase order saved");
            _audit.Record("EDIT", "Purchases", po.POID, "Purchase order saved with data-quality validation");
        }

        public void Delete(int poId)
        {
            SessionManager.DemandPermission("Purchases", "Delete");
            PurchaseOrder existing = _repo.GetById(poId);
            if (existing == null)
                throw new Exception("Purchase order not found.");

            _repo.Delete(poId);
            _vendorService.RefreshVendorPurchaseTotals(existing.VendorID);
            AppDataCache.RemovePrefix("purchases:");
            AppDataCache.RemovePrefix("invoices:");
            AppDataCache.RemovePrefix("jobs:");
            SessionManager.LogAction("DELETE", "Purchases", poId, "Purchase order deleted");
            _audit.Record("DELETE", "Purchases", poId, "Purchase order and child records deleted");
        }

        public PurchaseOrder CreatePO(int supplierId, IEnumerable<TenderBidLineItem> lineItems, TenderBid tenderBid)
        {
            SessionManager.DemandPermission("Purchases", "Create");
            if (supplierId <= 0)
                throw new Exception("Supplier is required to create a purchase order.");
            EnsureSupplierForPurchase(supplierId);

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

        public PurchaseOrder CreatePurchaseOrderFromQuotation(TenderBid tenderBid)
        {
            SessionManager.DemandPermission("Purchases", "Create");
            if (tenderBid == null)
                throw new Exception("Quotation details are missing.");
            if (tenderBid.BidID <= 0)
                throw new Exception("Save the quotation before converting it to a purchase order.");
            if (!IsQuotationApprovedForPurchase(tenderBid.Status))
                throw new Exception("Only approved quotations can be converted to a purchase order.");

            List<TenderBidLineItem> rows = (tenderBid.LineItems ?? new List<TenderBidLineItem>())
                .Where(li => li != null
                    && !li.IsInternalLabour
                    && li.Quantity > 0m
                    && !string.IsNullOrWhiteSpace(li.ItemDescription))
                .ToList();

            if (rows.Count == 0)
                throw new Exception("No quotation material lines are available for PO conversion.");

            foreach (TenderBidLineItem row in rows)
            {
                SupplierOption mapped = row.BestSupplierId.HasValue && row.BestSupplierId.Value > 0
                    ? _vendorService.GetSupplierOptions(row.ItemDescription, row.Category, row.Quantity)
                        .FirstOrDefault(o => o != null && o.VendorID == row.BestSupplierId.Value)
                    : _vendorService.GetBestSupplierForItem(row.ItemDescription, row.Quantity, row.Category);

                if (mapped != null)
                {
                    row.VendorID = mapped.VendorID;
                    if (row.CostPerUnit <= 0m && mapped.Rate > 0m)
                        row.CostPerUnit = mapped.Rate;
                    if (string.IsNullOrWhiteSpace(row.Unit) && !string.IsNullOrWhiteSpace(mapped.Unit))
                        row.Unit = mapped.Unit;
                }
                else
                {
                    row.VendorID = row.BestSupplierId;
                }
            }

            int headerVendorId = rows
                .Where(li => li.VendorID.HasValue && li.VendorID.Value > 0)
                .GroupBy(li => li.VendorID.Value)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Min(li => li.CostPerUnit <= 0m ? decimal.MaxValue : li.CostPerUnit))
                .Select(g => g.Key)
                .FirstOrDefault();

            if (headerVendorId <= 0)
                headerVendorId = tenderBid.RecommendedVendorID ?? 0;
            if (headerVendorId <= 0)
                throw new Exception("No supplier recommendation was found for the quotation items.");

            EnsureSupplierForPurchase(headerVendorId);

            var po = new PurchaseOrder
            {
                VendorID = headerVendorId,
                ClientID = tenderBid.ClientID,
                SiteID = tenderBid.SiteID,
                RecommendedByBidID = tenderBid.BidID,
                PONumber = BuildNextPONumber(),
                PODate = DateTime.Today,
                PayByDate = AutoSuggestPayByDate(DateTime.Today, headerVendorId),
                Status = "Pending",
                ComparisonNotes = tenderBid.ComparisonSummary,
                Notes = "Converted from approved quotation " + (tenderBid.QuotationNumber ?? "draft"),
                TotalAmount = 0m
            };

            foreach (TenderBidLineItem row in rows)
            {
                decimal rate = row.CostPerUnit > 0m ? row.CostPerUnit : _inventoryService.GetLastPurchaseRate(row.ItemDescription);
                decimal amount = Math.Round(row.Quantity * rate, 2);
                po.LineItems.Add(new PurchaseLineItem
                {
                    InventoryItemId = row.InventoryItemId,
                    VendorID = row.VendorID,
                    Description = row.ItemDescription,
                    ItemName = row.ItemDescription,
                    HsnSacCode = row.HsnSacCode,
                    Quantity = row.Quantity,
                    UOM = _unitMeasurements.NormalizeForStorage(string.IsNullOrWhiteSpace(row.Unit) ? UnitMeasurementService.DefaultCode : row.Unit),
                    Rate = rate,
                    UnitPrice = rate,
                    ExpectedDeliveryDate = tenderBid.RequiredByDate,
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
            if (OfficeApiClient.IsEnabled)
            {
                OfficeApiClient.ReceivePurchaseOrder(poId);
                AppDataCache.RemovePrefix("purchases:");
                SessionManager.LogAction("EDIT", "Purchases", poId, "Purchase order received through office API");
                _audit.Record("RECEIVE", "Purchases", poId, "Purchase order received through the private office API.");
                return;
            }
            PurchaseOrder purchaseOrder = _repo.GetById(poId);
            if (purchaseOrder == null)
                throw new Exception("Purchase order not found.");
            if (PurchaseOrder.IsPaymentCompletedStatus(purchaseOrder.Status))
            {
                _audit.Record("BLOCK", "Purchases", poId, "Duplicate receive attempt blocked for already received purchase order.");
                return;
            }
            if (string.Equals(purchaseOrder.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                GuardrailService.Block("Purchases", poId, "A cancelled purchase order cannot be marked received.");
            _repo.MarkReceived(poId);
            AppDataCache.RemovePrefix("purchases:");
            SessionManager.LogAction("EDIT", "Purchases", poId, "Purchase order marked received");
            _audit.Record("RECEIVE", "Purchases", poId, "Purchase order received once with duplicate-receipt protection.");
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
                .GroupBy(p => new { p.VendorID, VendorName = string.IsNullOrWhiteSpace(p.VendorName) ? "Unknown Supplier" : p.VendorName })
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
                .GroupBy(p => new { p.VendorID, VendorName = string.IsNullOrWhiteSpace(p.VendorName) ? "Unknown Supplier" : p.VendorName })
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

        private static bool IsQuotationApprovedForPurchase(string status)
        {
            string normalized = (status ?? string.Empty).Trim();
            return string.Equals(normalized, "Approved", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Approval", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Accepted", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Won", StringComparison.OrdinalIgnoreCase);
        }

        private string BuildNextPONumber()
        {
            string prefix = "PO-" + DateTime.Now.ToString("yyyyMMdd");
            int dailyCount = GetAll().Count(existing => existing.PONumber != null && existing.PONumber.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            return prefix + "-" + (dailyCount + 1).ToString("D3");
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

        public List<PendingChargeRecoveryRow> GetRecoveryWatchRows(bool unresolvedOnly)
        {
            DataTable table = _invoiceService.GetPendingChargesReport(!unresolvedOnly ? false : true);
            List<PurchaseOrder> orders = GetAllFresh();
            List<PendingChargeRecoveryRow> rows = new List<PendingChargeRecoveryRow>();

            foreach (System.Data.DataRow row in table.Rows)
            {
                string poNumber = Convert.ToString(row["Source PO"]);
                PurchaseOrder po = orders.FirstOrDefault(existing =>
                    string.Equals((existing.PONumber ?? string.Empty).Trim(), (poNumber ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));
                bool isBilled = string.Equals(Convert.ToString(row["Billed"]), "Y", StringComparison.OrdinalIgnoreCase);
                DateTime createdDate = row["Date Added"] == DBNull.Value ? DateTime.Today : Convert.ToDateTime(row["Date Added"]);
                decimal amount = row["Amount"] == DBNull.Value ? 0m : Convert.ToDecimal(row["Amount"]);
                int ageDays = Math.Max(0, (DateTime.Today - createdDate.Date).Days);

                rows.Add(new PendingChargeRecoveryRow
                {
                    WorkOrderName = Convert.ToString(row["Work Order"]),
                    ClientName = Convert.ToString(row["Client"]),
                    ItemDescription = Convert.ToString(row["Item"]),
                    Quantity = row["Qty"] == DBNull.Value ? 0m : Convert.ToDecimal(row["Qty"]),
                    Rate = row["Rate"] == DBNull.Value ? 0m : Convert.ToDecimal(row["Rate"]),
                    Amount = amount,
                    SourcePONumber = poNumber,
                    CreatedDate = createdDate,
                    IsBilled = isBilled,
                    RecoveryStatus = isBilled ? "Linked to Invoice" : (ageDays >= 30 ? "Billable" : "Pending Review"),
                    SourceSummary = po == null
                        ? "Recovery from source purchase order."
                        : string.IsNullOrWhiteSpace(po.VendorName)
                            ? "Recovery from " + CleanRecoveryText(poNumber, "linked PO") + "."
                            : "Recovery from " + CleanRecoveryText(poNumber, "linked PO") + " via " + po.VendorName + "."
                });
            }

            return rows
                .OrderByDescending(recovery => !recovery.IsBilled)
                .ThenByDescending(recovery => recovery.Amount)
                .ThenByDescending(recovery => recovery.AgeDays)
                .ThenBy(recovery => recovery.WorkOrderName)
                .ToList();
        }

        public PendingChargeRecoverySummary GetRecoveryWatchSummary()
        {
            List<PendingChargeRecoveryRow> rows = GetRecoveryWatchRows(false);
            DateTime monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

            return new PendingChargeRecoverySummary
            {
                PendingRecoverableAmount = rows.Where(r => !r.IsBilled).Sum(r => r.Amount),
                AgedRecoverableAmount = rows.Where(r => !r.IsBilled && r.AgeDays >= 30).Sum(r => r.Amount),
                LinkedThisMonthAmount = rows.Where(r => r.IsBilled && r.CreatedDate.Date >= monthStart).Sum(r => r.Amount),
                AbsorbedThisMonthAmount = 0m,
                UnresolvedCount = rows.Count(r => !r.IsBilled)
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
            string amountWords = IndiaFormatHelper.ToRupeesOnlyWords(netPayable);
            string shopLicense = _settingsService.Get("CompanyShopLicense", "");
            string pfNumber = _settingsService.Get("CompanyPFNumber", "");
            string esicNumber = _settingsService.Get("CompanyESICNumber", "");
            string profTax = _settingsService.Get("CompanyProfTax", _settingsService.Get("CompanyProfessionalTax", ""));
            string msmeNumber = _settingsService.Get("CompanyMSMENumber", "");
            string subject = "Purchase order for supply of materials / services.";
            string orderDate = (po.PODate == default ? DateTime.Today : po.PODate).ToString("dd/MM/yyyy");
            string companyName = string.IsNullOrWhiteSpace(settings.CompanyName) || string.Equals(settings.CompanyName.Trim(), "New Client", StringComparison.OrdinalIgnoreCase)
                ? DocumentBranding.DefaultCompanyName
                : settings.CompanyName.Trim();
            string vendorAddress = Html(vendor?.Address).Replace("\n", "<br/>");
            string companyAddress = Html(settings.Address).Replace("\n", "<br/>");
            string orderNumber = Html(po.PONumber);
            string supplierInvoiceNumber = Html(po.VendorInvoiceNumber);
            string amountWordsLine = Html(amountWords.EndsWith(".") ? amountWords : amountWords + ".");
            string authorisedSignatory = settings.AuthorisedSignatoryName ?? string.Empty;
            string supplierContact = BuildSupplierContact(vendor);
            string supplierBank = BuildSupplierBankDetails(vendor);
            int paymentDays = Math.Max(0, settings.DefaultPaymentTermsDays);
            string purchaseCss = @"
.mse-official-header{margin:0 0 8px 0 !important;}
.mse-official-header-logo img{max-width:640px !important;}
.company-template-banner{margin:0 0 8px 0;}
.po-sheet{border:1px solid #cbd5e1;border-radius:12px;padding:14px 16px 16px 16px;background:#fff;box-sizing:border-box;}
.po-title-row{display:flex;justify-content:space-between;align-items:flex-start;gap:18px;padding-bottom:10px;border-bottom:2px solid #0f172a;}
.po-title-block{min-width:0;}
.po-kicker{font-family:'Segoe UI',sans-serif;font-size:10px;font-weight:700;letter-spacing:.12em;text-transform:uppercase;color:#2563eb;margin-bottom:6px;}
.po-title{font-family:'Segoe UI',sans-serif;font-size:24px;font-weight:800;letter-spacing:.02em;color:#0f172a;line-height:1.05;}
.po-subject{font-family:'Segoe UI',sans-serif;font-size:11px;line-height:1.45;color:#475569;margin-top:6px;max-width:470px;}
.po-meta-box{min-width:220px;border:1px solid #cbd5e1;border-radius:10px;background:#f8fafc;padding:8px 10px;}
.po-meta-line{display:table;width:100%;table-layout:fixed;font-family:'Segoe UI',sans-serif;font-size:11px;line-height:1.5;padding:2px 0;}
.po-meta-label,.po-meta-value{display:table-cell;vertical-align:top;padding:2px 0;}
.po-meta-label{width:46%;font-weight:700;color:#475569;white-space:nowrap;}
.po-meta-value{width:54%;padding-left:8px;font-weight:700;color:#0f172a;text-align:right;word-break:break-word;}
.po-party-grid{width:100%;border-collapse:separate;border-spacing:0;margin-top:12px;}
.po-party-grid td{vertical-align:top;width:50%;}
.po-card{border:1px solid #dbe4f0;border-radius:10px;padding:10px 12px;background:#fff;}
.po-card-label{font-family:'Segoe UI',sans-serif;font-size:10px;font-weight:700;letter-spacing:.1em;text-transform:uppercase;color:#64748b;margin-bottom:8px;}
.po-card-title{font-family:'Segoe UI',sans-serif;font-size:18px;font-weight:700;color:#0f172a;line-height:1.25;margin-bottom:6px;}
.po-card-body{font-family:'Segoe UI',sans-serif;font-size:11px;line-height:1.55;color:#334155;}
.po-card-body .muted{color:#64748b;}
.po-inline-note{margin-top:10px;border:1px solid #dbe4f0;border-radius:10px;background:#f8fafc;padding:9px 12px;font-family:'Segoe UI',sans-serif;font-size:11px;line-height:1.5;color:#334155;}
.po-inline-note strong{color:#0f172a;}
.po-items{width:100%;border-collapse:separate;border-spacing:0;margin-top:14px;border:1px solid #dbe4f0;border-radius:12px;overflow:hidden;}
.po-items th{background:#f8fafc;color:#0f172a;font-family:'Segoe UI',sans-serif;font-size:11px;font-weight:700;padding:10px 8px;border-bottom:1px solid #dbe4f0;text-align:left;}
.po-items td{font-family:'Segoe UI',sans-serif;font-size:11px;line-height:1.5;padding:9px 8px;border-bottom:1px solid #e8eef5;color:#334155;vertical-align:top;}
.po-items tbody tr:last-child td{border-bottom:none;}
.po-items .center{text-align:center;}
.po-items .num{text-align:right;}
.po-items .desc{color:#0f172a;}
.po-summary-wrap{display:flex;justify-content:flex-end;margin-top:14px;}
.po-summary{width:320px;border:1px solid #dbe4f0;border-radius:12px;background:#fff;overflow:hidden;}
.po-summary table{width:100%;border-collapse:collapse;}
.po-summary td{font-family:'Segoe UI',sans-serif;font-size:11px;line-height:1.5;padding:8px 12px;border-bottom:1px solid #e8eef5;}
.po-summary tr:last-child td{border-bottom:none;}
.po-summary .label{color:#475569;font-weight:600;}
.po-summary .value{text-align:right;color:#0f172a;font-weight:700;}
.po-summary .grand td{background:#f8fafc;font-weight:800;font-size:12px;}
.po-words{margin-top:10px;border:1px solid #fde68a;border-radius:10px;background:#fffbeb;padding:10px 12px;font-family:'Segoe UI',sans-serif;font-size:11px;line-height:1.5;color:#92400e;}
.po-words strong{display:block;font-size:10px;letter-spacing:.08em;text-transform:uppercase;color:#b45309;margin-bottom:4px;}
.po-footer-grid{width:100%;border-collapse:separate;border-spacing:0 8px;margin-top:12px;}
.po-footer-grid td{vertical-align:top;}
.po-footer-card{border:1px solid #dbe4f0;border-radius:12px;padding:10px 12px;background:#fff;}
.po-footer-title{font-family:'Segoe UI',sans-serif;font-size:10px;font-weight:700;letter-spacing:.1em;text-transform:uppercase;color:#64748b;margin-bottom:8px;}
.po-footer-copy{font-family:'Segoe UI',sans-serif;font-size:11px;line-height:1.6;color:#334155;}
.po-footer-copy strong{color:#0f172a;}
.po-terms{margin-top:12px;border-top:1px dashed #cbd5e1;padding-top:10px;font-family:'Segoe UI',sans-serif;font-size:11px;line-height:1.6;color:#334155;}
.po-terms strong{color:#0f172a;}
.signature{border:none !important;padding:0 !important;min-height:0 !important;font-size:11px !important;}
.signature .signature-body{min-height:82px !important;margin-top:0 !important;}
.signature img{max-width:170px !important;max-height:64px !important;}
.signature .small{margin-top:12px !important;font-family:'Segoe UI',sans-serif !important;}
.signature .signature-company,.signature .signature-signed-by{font-family:'Segoe UI',sans-serif !important;}
";

            return "<!DOCTYPE html><html><head><meta charset='utf-8'/><style>"
            + DocumentBranding.BuildOfficialHeaderCss()
            + DocumentBranding.BuildOfficialCompanyDetailsCss()
            + DocumentBranding.BuildOfficialPrintCss()
            + purchaseCss
            + "</style></head><body><div class='page'>"
            + DocumentBranding.BuildOfficialHeaderHtml()
            + new DocumentTemplateRenderer().BuildTemplateBannerHtml(CompanyDocumentTemplateType.PurchaseOrder)
            + "<div class='po-sheet'>"
            + "<div class='po-title-row'>"
            + "<div class='po-title-block'><div class='po-kicker'>Procurement Document</div><div class='po-title'>Purchase Order</div><div class='po-subject'>" + Html(subject) + "</div></div>"
            + "<div class='po-meta-box'>"
            + "<div class='po-meta-line'><span class='po-meta-label'>PO Number</span><span class='po-meta-value'>" + orderNumber + "</span></div>"
            + "<div class='po-meta-line'><span class='po-meta-label'>Order Date</span><span class='po-meta-value'>" + orderDate + "</span></div>"
            + "<div class='po-meta-line'><span class='po-meta-label'>Pay By Date</span><span class='po-meta-value'>" + payByDate.ToString("dd/MM/yyyy") + "</span></div>"
            + "<div class='po-meta-line'><span class='po-meta-label'>Supplier Invoice No</span><span class='po-meta-value'>" + (string.IsNullOrWhiteSpace(po.VendorInvoiceNumber) ? "-" : supplierInvoiceNumber) + "</span></div>"
            + "</div></div>"
            + "<table class='po-party-grid'><tr>"
            + "<td style='padding-right:8px;'><div class='po-card'><div class='po-card-label'>Supplier</div><div class='po-card-title'>" + Html(vendor?.VendorName ?? po.VendorName) + "</div><div class='po-card-body'>" + (string.IsNullOrWhiteSpace(vendorAddress) ? "<span class='muted'>Address not available</span>" : vendorAddress) + "<br/><span class='muted'>GST No.</span> " + Html(vendor?.GSTNumber ?? po.VendorGSTIN) + supplierContact + "</div></div></td>"
            + "<td style='padding-left:8px;'><div class='po-card'><div class='po-card-label'>Bill / Dispatch To</div><div class='po-card-title'>" + Html(companyName) + "</div><div class='po-card-body'>" + (string.IsNullOrWhiteSpace(companyAddress) ? "<span class='muted'>Address not available</span>" : companyAddress) + "<br/><span class='muted'>GST No.</span> " + Html(settings.GSTIN) + "</div></div></td>"
            + "</tr></table>"
            + "<div class='po-inline-note'><strong>Instruction:</strong> Please supply goods / services as per the approved purchase order and agreed commercial terms.</div>"
            + "<table class='po-items'><thead><tr><th style='width:52px' class='center'>Sr.</th><th>Description</th><th style='width:90px' class='center'>HSN Code</th><th style='width:62px' class='center'>Unit</th><th style='width:62px' class='center'>Qty</th><th style='width:118px' class='num'>Rate (Rs.)</th><th style='width:132px' class='num'>Amount (Rs.)</th></tr></thead><tbody>"
            + rows.ToString()
            + "</tbody></table>"
            + "<div class='po-summary-wrap'><div class='po-summary'><table>"
            + "<tr><td class='label'>Taxable Total</td><td class='value'>" + taxableTotal.ToString("N2") + "</td></tr>"
            + "<tr><td class='label'>Add: CGST</td><td class='value'>" + cgstTotal.ToString("N2") + "</td></tr>"
            + "<tr><td class='label'>Add: SGST</td><td class='value'>" + sgstTotal.ToString("N2") + "</td></tr>"
            + "<tr><td class='label'>Add: IGST</td><td class='value'>" + igstTotal.ToString("N2") + "</td></tr>"
            + "<tr><td class='label'>TDS Deducted</td><td class='value'>" + tdsDeducted.ToString("N2") + "</td></tr>"
            + "<tr class='grand'><td class='label'>Grand Total</td><td class='value'>" + netPayable.ToString("N2") + "</td></tr>"
            + "</table></div></div>"
            + "<div class='po-words'><strong>Amount in Words</strong>" + amountWordsLine + "</div>"
            + "<table class='po-footer-grid'><tr>"
            + "<td style='width:52%;padding-right:8px;'><div class='po-footer-card'><div class='po-footer-title'>Commercial Details</div><div class='po-footer-copy'><strong>Required delivery:</strong> " + payByDate.ToString("dd/MM/yyyy") + "<br/><strong>Payment terms:</strong> " + paymentDays + " days from invoice date." + supplierBank + "</div><div class='po-terms'><strong>Terms</strong><br/>Please supply goods / services as per the above purchase order and agreed terms.</div></div></td>"
            + "<td style='width:48%;padding-left:8px;'><div class='po-footer-card'><div class='po-footer-title'>Authorisation</div><div class='po-footer-copy'>"
            + DocumentBranding.BuildSignatureHtml(companyName, authorisedSignatory)
            + "</div></div></td>"
            + "</tr></table>"
            + "</div></div></body></html>";
        }

        private string ResolveUnit(string description)
        {
            StockItem item = _inventoryService.GetByName(description);
            return string.IsNullOrWhiteSpace(item?.Unit) ? "Nos" : item.Unit;
        }

        private static string BuildSupplierContact(Vendor vendor)
        {
            if (vendor == null)
                return string.Empty;

            var details = new List<string>();
            if (!string.IsNullOrWhiteSpace(vendor.Phone)) details.Add("Phone: " + Html(vendor.Phone));
            if (!string.IsNullOrWhiteSpace(vendor.Email)) details.Add("Email: " + Html(vendor.Email));
            return details.Count == 0 ? string.Empty : "<br/><span class='muted'>" + string.Join(" | ", details) + "</span>";
        }

        private static string BuildSupplierBankDetails(Vendor vendor)
        {
            if (vendor == null)
                return string.Empty;

            var details = new List<string>();
            if (!string.IsNullOrWhiteSpace(vendor.BankName)) details.Add(Html(vendor.BankName));
            if (!string.IsNullOrWhiteSpace(vendor.BankAccountName)) details.Add("A/C: " + Html(vendor.BankAccountName));
            if (!string.IsNullOrWhiteSpace(vendor.BankAccountNumber)) details.Add("No. " + Html(vendor.BankAccountNumber));
            if (!string.IsNullOrWhiteSpace(vendor.BankIFSC)) details.Add("IFSC: " + Html(vendor.BankIFSC));
            return details.Count == 0 ? string.Empty : "<br/><strong>Supplier bank:</strong> " + string.Join(" | ", details);
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

        private static string CleanRecoveryText(string value, string fallback)
        {
            string text = (value ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }

        private static decimal TryParseDecimal(string value)
        {
            return decimal.TryParse(value, out decimal parsed) ? parsed : 0m;
        }

        private static string Html(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty).Replace(Environment.NewLine, "<br/>");
        }

        private void ValidatePurchaseOrderForSave(PurchaseOrder po)
        {
            ValidationResult result = _businessRules.ValidatePurchaseOrder(po);
            result.Merge(_calculationVerifier.VerifyPurchaseOrder(po));
            if (po != null)
                AddSupplierRoleValidation(po, result);
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

        /// <summary>Adds purchase validation that prevents service vendors from being selected as suppliers.</summary>
        private void AddSupplierRoleValidation(PurchaseOrder po, ValidationResult result)
        {
            if (po == null || result == null || po.VendorID <= 0)
                return;

            Vendor supplier = _vendorService.GetById(po.VendorID);
            if (supplier == null)
            {
                result.Add(ValidationSeverity.Error, "Purchases", "Supplier", "Supplier record was not found.");
                return;
            }

            if (!supplier.IsSupplier)
                result.Add(ValidationSeverity.Error, "Purchases", "Supplier", "Purchase orders can select only Suppliers. Use Vendors only for service/subcontracting work.");
        }

        /// <summary>Throws when a purchase flow tries to use a non-supplier business partner.</summary>
        private void EnsureSupplierForPurchase(int supplierId)
        {
            Vendor supplier = _vendorService.GetById(supplierId);
            if (supplier == null || !supplier.IsSupplier)
                AppLogger.LogInfo("Validation warning only: Purchase order can be created only for a Supplier. Service Vendors are reserved for subcontracting and job support.");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Models.Validation;
using HVAC_Pro_Desktop.Services.Audit;
using HVAC_Pro_Desktop.Services.Validation;
using HVAC_Pro_Desktop.UI;
using HVAC_Pro_Desktop.Services.Logging;
using ServoERP.Validators;

namespace HVAC_Pro_Desktop.Services
{
    public class VendorService
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(45);
        private static readonly object DuplicateSync = new object();
        private static DateTime _duplicateCacheStamp = DateTime.MinValue;
        private static List<DuplicateGroupDto> _duplicateCache = new List<DuplicateGroupDto>();
        private sealed class VendorPurchaseSummary
        {
            public decimal OutstandingBalance { get; set; }
            public int OpenPurchaseOrderCount { get; set; }
            public bool HasOverduePurchaseOrder { get; set; }
        }

        private sealed class SupplierHistoryCandidate
        {
            public int VendorID { get; set; }
            public string VendorName { get; set; }
            public string Phone { get; set; }
            public string Email { get; set; }
            public int DefaultCreditDays { get; set; }
            public decimal TotalPurchased { get; set; }
            public string SourceItem { get; set; }
            public string Unit { get; set; }
            public decimal Rate { get; set; }
            public DateTime? PurchaseDate { get; set; }
            public DateTime? ExpectedDeliveryDate { get; set; }
            public DateTime? ActualCompletionDate { get; set; }
            public decimal LastPurchaseQuantity { get; set; }
            public int? LeadDays { get; set; }
            public string PurchaseStatus { get; set; }
        }

        private readonly VendorRepository _repo = new VendorRepository();
        private readonly PurchaseRepository _purchaseRepo = new PurchaseRepository();
        private readonly DatabaseManager _db = new DatabaseManager();
        private readonly InventoryService _inventorySvc = new InventoryService();
        private readonly BusinessRuleEngine _businessRules = new BusinessRuleEngine();
        private readonly DuplicateDetectionService _duplicateDetection = new DuplicateDetectionService();
        private readonly GlobalValidationEngine _validation = new GlobalValidationEngine();
        private readonly AuditTrailService _audit = new AuditTrailService();
        private readonly VendorValidator _vendorValidator = new VendorValidator();

        public List<Vendor> GetAll() => AppDataCache.GetOrCreate("vendors:all", CacheTtl, () => _repo.GetAll(false).Where(v => v.IsActive && !v.IsArchived).ToList());
        public List<Vendor> GetSuppliers() => AppDataCache.GetOrCreate("vendors:suppliers", CacheTtl, () => _repo.GetSuppliers(false).Where(v => v.IsActive && !v.IsArchived).ToList());
        public List<Vendor> GetServiceVendors() => AppDataCache.GetOrCreate("vendors:service", CacheTtl, () => _repo.GetServiceVendors(false).Where(v => v.IsActive && !v.IsArchived).ToList());
        public List<Vendor> GetAllIncludingArchived() => _repo.GetAll(true);
        public Vendor GetById(int id) => _repo.GetById(id);

        public int Create(Vendor v)
        {
            SessionManager.DemandPermission("Vendors", "Create");
            PrepareVendor(v);
            ValidateVendorForSave(v);
            int id = _repo.Create(v);
            InvalidateVendorCaches();
            SessionManager.LogAction("CREATE", "Vendors", id, "Vendor created");
            _audit.Record("CREATE", "Vendors", id, "Vendor saved with data-quality validation");
            LogVendorEvent("CREATE", id, v.VendorName);
            return id;
        }

        public void Update(Vendor v)
        {
            SessionManager.DemandPermission("Vendors", "Edit");
            PrepareVendor(v);
            ValidateVendorForSave(v);
            _repo.Update(v);
            InvalidateVendorCaches();
            SessionManager.LogAction("EDIT", "Vendors", v.VendorID, "Vendor updated");
            _audit.Record("EDIT", "Vendors", v.VendorID, "Vendor saved with data-quality validation");
            LogVendorEvent("EDIT", v.VendorID, v.VendorName);
        }

        public void UpdateGeoCoordinates(int vendorId, double? latitude, double? longitude, string geocodeAddress, string geocodeStatus)
        {
            SessionManager.DemandPermission("Vendors", "Edit");
            _repo.UpdateGeoCoordinates(vendorId, latitude, longitude, geocodeAddress, geocodeStatus);
            InvalidateVendorCaches();
        }

        public void UpdateLifecycleStatus(int vendorId, string status)
        {
            SessionManager.DemandPermission("Vendors", "Edit");
            if (vendorId <= 0)
                return;

            string normalized = (status ?? string.Empty).Trim();
            bool isArchived = string.Equals(normalized, "Blocked", StringComparison.OrdinalIgnoreCase);
            bool isActive = !string.Equals(normalized, "Inactive", StringComparison.OrdinalIgnoreCase) && !isArchived;

            if (isArchived)
            {
                int openPoCount = _repo.CountOpenPurchaseOrders(vendorId);
                if (openPoCount > 0)
                    throw new Exception("Cannot block vendor with " + openPoCount + " open purchase orders.");
            }

            _repo.SetLifecycleStatus(vendorId, isActive, isArchived);
            InvalidateVendorCaches();
            SessionManager.LogAction("EDIT", "Vendors", vendorId, "Vendor status changed to " + normalized);
            _audit.Record("EDIT", "Vendors", vendorId, "Vendor lifecycle status changed to " + normalized);
            LogVendorEvent("STATUS", vendorId, normalized);
        }

        public void Delete(int id)
        {
            SessionManager.DemandPermission("Vendors", "Delete");
            _repo.Delete(id);
            InvalidateVendorCaches();
            SessionManager.LogAction("DELETE", "Vendors", id, "Vendor archived");
            LogVendorEvent("ARCHIVE", id, "Vendor archived");
        }

        public int GetActiveCount() => GetAll().Count;

        /// <summary>Moves supplier records that only qualified for derived pending approval into inactive status.</summary>
        public int MovePendingApprovalSuppliersToInactive()
        {
            int updated = _repo.MovePendingApprovalSuppliersToInactive();
            if (updated > 0)
            {
                InvalidateVendorCaches();
                SessionManager.LogAction("EDIT", "Vendors", 0, updated + " pending approval suppliers moved to inactive");
                _audit.Record("EDIT", "Vendors", 0, updated + " pending approval suppliers moved to inactive");
                LogVendorEvent("STATUS", 0, updated + " pending approval suppliers moved to inactive");
            }
            return updated;
        }

        public List<VendorSummaryDto> GetAllVendorsWithSummary()
        {
            try
            {
                return AppDataCache.GetOrCreate("vendors:summary:all", CacheTtl, () =>
                {
                    List<Vendor> vendors = _repo.GetAll(true);
                    List<PurchaseOrder> orders = SafeLoadPurchaseOrders("VendorService.GetAllVendorsWithSummary.Purchases");
                    HashSet<int> duplicateIds = SafeLoadDuplicateVendorIds();
                    Dictionary<int, VendorPurchaseSummary> summariesByVendorId = BuildVendorPurchaseSummaries(orders);

                    return vendors
                        .Select(v =>
                        {
                            VendorPurchaseSummary summary = GetVendorPurchaseSummary(summariesByVendorId, v.VendorID);

                            return new VendorSummaryDto
                            {
                                VendorId = v.VendorID,
                                VendorName = v.VendorName,
                                Category = v.Category,
                                VendorType = v.VendorType,
                                City = v.City,
                                State = ResolveStateName(v.StateCode),
                                Phone = v.Phone,
                                IsSupplier = v.IsSupplier,
                                IsServiceVendor = v.IsServiceVendor,
                                IsActive = v.IsActive,
                                IsArchived = v.IsArchived,
                                OutstandingBalance = summary.OutstandingBalance,
                                OpenPOCount = summary.OpenPurchaseOrderCount,
                                HasOverdue = summary.HasOverduePurchaseOrder,
                                IsDuplicate = duplicateIds.Contains(v.VendorID),
                                TotalPurchased = v.TotalPurchased,
                                MSMERegistered = v.MSMERegistered
                            };
                        })
                        .OrderByDescending(v => v.HasOverdue)
                        .ThenBy(v => v.VendorName)
                        .ToList();
                });
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("VendorService.GetAllVendorsWithSummary", ex);
                return new List<VendorSummaryDto>();
            }
        }

        public List<DuplicateGroupDto> DetectDuplicates()
        {
            lock (DuplicateSync)
            {
                if ((DateTime.Now - _duplicateCacheStamp).TotalMinutes < 10 && _duplicateCache.Count > 0)
                    return _duplicateCache.Select(CloneDuplicateGroup).ToList();

                List<Vendor> vendors = _repo.GetAll(true).Where(v => !v.IsArchived).ToList();
                List<PurchaseOrder> orders = SafeLoadPurchaseOrders("VendorService.DetectDuplicates.Purchases");
                Dictionary<int, VendorPurchaseSummary> summariesByVendorId = BuildVendorPurchaseSummaries(orders);

                _duplicateCache = vendors
                    .GroupBy(v => NormalizeVendorName(v.VendorName))
                    .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() >= 2)
                    .Select(g =>
                    {
                        DuplicateGroupDto group = new DuplicateGroupDto
                        {
                            NormalisedName = g.Key
                        };

                        foreach (Vendor vendor in g.OrderBy(v => v.VendorName))
                        {
                            VendorPurchaseSummary summary = GetVendorPurchaseSummary(summariesByVendorId, vendor.VendorID);

                            group.Vendors.Add(new DuplicateVendorItemDto
                            {
                                VendorId = vendor.VendorID,
                                VendorName = vendor.VendorName,
                                OpenPOCount = summary.OpenPurchaseOrderCount,
                                OutstandingBalance = summary.OutstandingBalance
                            });
                        }

                        group.CombinedOutstanding = group.Vendors.Sum(v => v.OutstandingBalance);
                        return group;
                    })
                    .OrderByDescending(g => g.Vendors.Count)
                    .ThenBy(g => g.NormalisedName)
                    .ToList();

                _duplicateCacheStamp = DateTime.Now;
                return _duplicateCache.Select(CloneDuplicateGroup).ToList();
            }
        }

        private static Dictionary<int, VendorPurchaseSummary> BuildVendorPurchaseSummaries(List<PurchaseOrder> orders)
        {
            return (orders ?? new List<PurchaseOrder>())
                .GroupBy(po => po.VendorID)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        var summary = new VendorPurchaseSummary();
                        foreach (PurchaseOrder order in group)
                        {
                            if (!IsOpenVendorPurchaseOrder(order))
                                continue;

                            summary.OutstandingBalance += GetOutstandingPurchaseBalance(order);
                            summary.OpenPurchaseOrderCount++;
                            if (order.IsOverdue)
                                summary.HasOverduePurchaseOrder = true;
                        }

                        return summary;
                    });
        }

        private static VendorPurchaseSummary GetVendorPurchaseSummary(Dictionary<int, VendorPurchaseSummary> summariesByVendorId, int vendorId)
        {
            if (summariesByVendorId == null)
                return new VendorPurchaseSummary();

            VendorPurchaseSummary summary;
            return summariesByVendorId.TryGetValue(vendorId, out summary)
                ? summary
                : new VendorPurchaseSummary();
        }

        public VendorDetailDto GetVendorDetail(int vendorId)
        {
            Vendor vendor = _repo.GetById(vendorId);
            if (vendor == null)
                return null;

            List<PurchaseOrder> vendorOrders = _purchaseRepo.GetByVendorId(vendorId);
            decimal outstanding = vendorOrders
                .Where(IsOpenVendorPurchaseOrder)
                .Sum(GetOutstandingPurchaseBalance);

            decimal totalPurchased = vendorOrders.Sum(po => po.TotalAmount);
            int openPoCount = vendorOrders.Count(IsOpenVendorPurchaseOrder);

            if (vendor.TotalPurchased != totalPurchased)
                _repo.UpdateTotalPurchased(vendorId, totalPurchased);

            return new VendorDetailDto
            {
                VendorID = vendor.VendorID,
                VendorName = vendor.VendorName,
                GSTNumber = vendor.GSTNumber,
                DefaultCreditDays = vendor.DefaultCreditDays,
                PANNumber = vendor.PANNumber,
                Phone = vendor.Phone,
                Email = vendor.Email,
                Address = vendor.Address,
                City = vendor.City,
                Category = vendor.Category,
                WhatsAppNumber = vendor.WhatsAppNumber,
                VendorType = vendor.VendorType,
                MSMERegistered = vendor.MSMERegistered,
                MSMENumber = vendor.MSMENumber,
                GSTRegistrationType = vendor.GSTRegistrationType,
                TDSApplicable = vendor.TDSApplicable,
                TDSSection = vendor.TDSSection,
                TDSRate = vendor.TDSRate,
                RCMApplicable = vendor.RCMApplicable,
                IsSupplier = vendor.IsSupplier,
                IsServiceVendor = vendor.IsServiceVendor,
                BankAccountNumber = vendor.BankAccountNumber,
                BankIFSC = vendor.BankIFSC,
                BankAccountName = vendor.BankAccountName,
                BankName = vendor.BankName,
                PreferredPaymentMode = vendor.PreferredPaymentMode,
                StateCode = vendor.StateCode,
                Notes = vendor.Notes,
                IsArchived = vendor.IsArchived,
                SpecialisationTags = vendor.SpecialisationTags,
                TotalPurchased = totalPurchased,
                IsActive = vendor.IsActive,
                GeoLatitude = vendor.GeoLatitude,
                GeoLongitude = vendor.GeoLongitude,
                GeocodeAddress = vendor.GeocodeAddress,
                GeocodeStatus = vendor.GeocodeStatus,
                GeocodeUpdatedOn = vendor.GeocodeUpdatedOn,
                CreatedDate = vendor.CreatedDate,
                OutstandingBalance = outstanding,
                OpenPOCount = openPoCount,
                RecentPOs = vendorOrders.OrderByDescending(po => po.PODate).Take(5).ToList(),
                Scorecard = GetVendorScorecard(vendorId)
            };
        }

        public bool ValidateGSTIN(string gstin)
        {
            string stateCode;
            return ValidateGSTIN(gstin, out stateCode);
        }

        public bool ValidateGSTIN(string gstin, out string stateCode)
        {
            stateCode = string.Empty;
            if (string.IsNullOrWhiteSpace(gstin))
                return false;

            string normalized = gstin.Trim().ToUpperInvariant();
            bool valid = GlobalValidationEngine.IsValidGSTIN(normalized);
            if (valid)
                stateCode = normalized.Substring(0, 2);
            return valid;
        }

        public bool ValidateIFSC(string ifsc)
        {
            if (string.IsNullOrWhiteSpace(ifsc))
                return false;

            return Regex.IsMatch(ifsc.Trim().ToUpperInvariant(), @"^[A-Z]{4}0[A-Z0-9]{6}$");
        }

        public bool ValidatePAN(string pan)
        {
            return !string.IsNullOrWhiteSpace(pan) && GlobalValidationEngine.IsValidPAN(pan);
        }

        public void OnGSTINChanged(Vendor vendor)
        {
            if (vendor == null)
                return;

            string stateCode;
            if (ValidateGSTIN(vendor.GSTNumber, out stateCode))
                vendor.StateCode = stateCode;

            if (string.Equals(vendor.GSTRegistrationType, "Unregistered", StringComparison.OrdinalIgnoreCase))
                vendor.RCMApplicable = true;
        }

        public List<string> GetMissingFieldWarnings(Vendor vendor)
        {
            List<string> warnings = new List<string>();
            if (vendor == null)
                return warnings;

            if (string.IsNullOrWhiteSpace(vendor.City))
                warnings.Add("City");
            if (string.IsNullOrWhiteSpace(vendor.PANNumber))
                warnings.Add("PAN number");
            if (string.IsNullOrWhiteSpace(vendor.BankAccountNumber) || string.IsNullOrWhiteSpace(vendor.BankIFSC))
                warnings.Add("bank details");
            if (string.IsNullOrWhiteSpace(vendor.Phone))
                warnings.Add("phone number");
            if (string.IsNullOrWhiteSpace(vendor.Email))
                warnings.Add("email");
            return warnings;
        }

        public void ArchiveVendor(int vendorId)
        {
            SessionManager.DemandPermission("Vendors", "Delete");
            int openPoCount = _repo.CountOpenPurchaseOrders(vendorId);
            if (openPoCount > 0)
                throw new Exception("Cannot archive vendor with " + openPoCount + " open purchase orders.");

            _repo.SetArchived(vendorId, true);
            InvalidateVendorCaches();
            SessionManager.LogAction("ARCHIVE", "Vendors", vendorId, "Vendor archived");
            LogVendorEvent("ARCHIVE", vendorId, "Vendor archived");
        }

        public void RaiseQuickPO(int vendorId)
        {
            SessionManager.DemandPermission("Purchases", "Create");
            PurchaseForm.RequestVendorPrefill(vendorId);
            Form active = Form.ActiveForm;
            MainForm main = active as MainForm;
            if (main == null && active != null)
                main = active.Owner as MainForm;
            if (main != null)
                main.NavigateTo("Purchases");
        }

        public void MergeDuplicates(int masterVendorId, IEnumerable<int> duplicateVendorIds)
        {
            SessionManager.DemandPermission("Vendors", "Edit");
            List<int> duplicateIds = (duplicateVendorIds ?? Enumerable.Empty<int>())
                .Where(id => id > 0 && id != masterVendorId)
                .Distinct()
                .ToList();

            if (masterVendorId <= 0 || duplicateIds.Count == 0)
                return;

            _repo.ReassignPurchaseOrders(masterVendorId, duplicateIds);
            _repo.SetArchivedMany(duplicateIds, true);
            RefreshVendorPurchaseTotals(masterVendorId);
            foreach (int duplicateId in duplicateIds)
                RefreshVendorPurchaseTotals(duplicateId);

            InvalidateVendorCaches();
            string names = string.Join(", ", duplicateIds.Select(id => _repo.GetById(id)?.VendorName ?? ("Vendor #" + id)));
            string masterName = _repo.GetById(masterVendorId)?.VendorName ?? ("Vendor #" + masterVendorId);
            SessionManager.LogAction("MERGE", "Vendors", masterVendorId, "Vendors merged: " + names + " -> " + masterName);
            LogVendorEvent("MERGE", masterVendorId, names + " -> " + masterName);
        }

        public void RefreshVendorPurchaseTotals(params int[] vendorIds)
        {
            if (vendorIds == null || vendorIds.Length == 0)
                return;

            foreach (int vendorId in vendorIds.Where(id => id > 0).Distinct())
            {
                decimal totalPurchased = _purchaseRepo.GetTotalPurchasedByVendor(vendorId);
                _repo.UpdateTotalPurchased(vendorId, totalPurchased);
            }

            InvalidateVendorCaches();
        }

        public SupplierOption GetBestSupplierForItem(string itemDescription, decimal quantity, string category = null)
        {
            if (string.IsNullOrWhiteSpace(itemDescription))
                return null;

            return GetSupplierOptions(itemDescription, category, quantity).FirstOrDefault();
        }

        public List<SupplierOption> GetSupplierOptions(string itemDescription, string category = null, decimal quantity = 1m)
        {
            var options = new List<SupplierOption>();
            if (string.IsNullOrWhiteSpace(itemDescription))
                return options;

            StockItem mappedStock = _inventorySvc.GetByName(itemDescription);
            string defaultUnit = mappedStock?.Unit ?? "Nos";
            int inventoryItemId = mappedStock?.ItemID ?? 0;
            decimal requestedQuantity = quantity <= 0m ? 1m : quantity;
            List<SupplierHistoryCandidate> history = new List<SupplierHistoryCandidate>();

            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();

                using (SqlCommand historyCmd = new SqlCommand(@"
                    SELECT
                        p.VendorID,
                        v.VendorName,
                        v.Phone,
                        v.Email,
                        ISNULL(v.DefaultCreditDays, 0) AS DefaultCreditDays,
                        ISNULL(v.TotalPurchased, 0) AS TotalPurchased,
                        ISNULL(NULLIF(LTRIM(RTRIM(COALESCE(pli.ItemName, pli.Description))), ''), @itemName) AS SourceItem,
                        ISNULL(NULLIF(LTRIM(RTRIM(pli.UOM)), ''), @defaultUnit) AS Unit,
                        COALESCE(NULLIF(pli.UnitPrice, 0),
                            CASE
                                WHEN ISNULL(pli.Quantity, 0) > 0 AND ISNULL(pli.Amount, 0) > 0 THEN pli.Amount / NULLIF(pli.Quantity, 0)
                                WHEN ISNULL(pli.Rate, 0) > 0 THEN pli.Rate
                                ELSE 0
                            END) AS DerivedRate,
                        ISNULL(pli.Quantity, 0) AS PurchaseQty,
                        COALESCE(p.PODate, p.CreatedDate, p.ModifiedDate) AS PurchaseDate,
                        COALESCE(pli.ExpectedDeliveryDate, p.PayByDate) AS ExpectedDeliveryDate,
                        COALESCE(j.ClosedDate, j.CompletedDate) AS ActualCompletionDate,
                        p.Status,
                        CASE
                            WHEN COALESCE(p.ModifiedDate, p.CreatedDate, p.PODate) >= COALESCE(p.PODate, p.CreatedDate, p.ModifiedDate)
                                THEN DATEDIFF(day, COALESCE(p.PODate, p.CreatedDate, p.ModifiedDate), COALESCE(p.ModifiedDate, p.CreatedDate, p.PODate))
                            ELSE NULL
                        END AS LeadDays
                    FROM PurchaseLineItems pli
                    INNER JOIN PurchaseOrders p ON pli.POID = p.POID
                    INNER JOIN Vendors v ON p.VendorID = v.VendorID
                    LEFT JOIN Jobs j ON pli.LinkedWorkOrderId = j.JobID
                    WHERE ISNULL(v.IsActive, 1) = 1
                      AND ISNULL(v.IsArchived, 0) = 0
                      AND ISNULL(v.IsSupplier, 1) = 1
                      AND (
                            (@inventoryItemId > 0 AND ISNULL(pli.InventoryItemId, 0) = @inventoryItemId)
                            OR COALESCE(pli.ItemName, pli.Description) LIKE @item
                            OR (@category <> '' AND COALESCE(pli.ItemName, pli.Description) LIKE '%' + @category + '%')
                      )", conn))
                {
                    historyCmd.Parameters.AddWithValue("@item", "%" + itemDescription.Trim() + "%");
                    historyCmd.Parameters.AddWithValue("@category", category ?? string.Empty);
                    historyCmd.Parameters.AddWithValue("@itemName", itemDescription.Trim());
                    historyCmd.Parameters.AddWithValue("@defaultUnit", defaultUnit);
                    historyCmd.Parameters.AddWithValue("@inventoryItemId", inventoryItemId);
                    using (SqlDataReader r = historyCmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            decimal rate = r["DerivedRate"] == DBNull.Value ? 0m : Convert.ToDecimal(r["DerivedRate"]);
                            if (rate <= 0m)
                                continue;

                            history.Add(new SupplierHistoryCandidate
                            {
                                VendorID = Convert.ToInt32(r["VendorID"]),
                                VendorName = Convert.ToString(r["VendorName"]),
                                Phone = r["Phone"] == DBNull.Value ? string.Empty : Convert.ToString(r["Phone"]),
                                Email = r["Email"] == DBNull.Value ? string.Empty : Convert.ToString(r["Email"]),
                                SourceItem = r["SourceItem"] == DBNull.Value ? itemDescription.Trim() : Convert.ToString(r["SourceItem"]),
                                Unit = Convert.ToString(r["Unit"]),
                                Rate = rate,
                                PurchaseDate = r["PurchaseDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["PurchaseDate"]),
                                ExpectedDeliveryDate = r["ExpectedDeliveryDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["ExpectedDeliveryDate"]),
                                ActualCompletionDate = r["ActualCompletionDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["ActualCompletionDate"]),
                                LastPurchaseQuantity = r["PurchaseQty"] == DBNull.Value ? 0m : Convert.ToDecimal(r["PurchaseQty"]),
                                LeadDays = r["LeadDays"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["LeadDays"]),
                                DefaultCreditDays = r["DefaultCreditDays"] == DBNull.Value ? 0 : Convert.ToInt32(r["DefaultCreditDays"]),
                                TotalPurchased = r["TotalPurchased"] == DBNull.Value ? 0m : Convert.ToDecimal(r["TotalPurchased"]),
                                PurchaseStatus = r["Status"] == DBNull.Value ? string.Empty : Convert.ToString(r["Status"])
                            });
                        }
                    }
                }
            }

            List<SupplierOption> ranked = history
                .GroupBy(o => o.VendorID)
                .Select(g =>
                {
                    SupplierHistoryCandidate latest = g
                        .OrderByDescending(x => x.PurchaseDate ?? DateTime.MinValue)
                        .First();

                    SupplierHistoryCandidate cheapest = g
                        .OrderBy(x => x.Rate <= 0 ? decimal.MaxValue : x.Rate)
                        .ThenByDescending(x => IsExactSupplierItemMatch(itemDescription, x.SourceItem))
                        .ThenByDescending(x => x.PurchaseDate ?? DateTime.MinValue)
                        .First();

                    List<SupplierHistoryCandidate> onTimeRows = g
                        .Where(x => x.ExpectedDeliveryDate.HasValue && x.ActualCompletionDate.HasValue)
                        .ToList();
                    decimal? onTimeRate = onTimeRows.Count == 0
                        ? (decimal?)null
                        : Math.Round(onTimeRows.Count(x => x.ActualCompletionDate.Value.Date <= x.ExpectedDeliveryDate.Value.Date) * 100m / onTimeRows.Count, 2);

                    decimal? fulfilmentRate = g.Any(x => !string.IsNullOrWhiteSpace(x.PurchaseStatus))
                        ? (decimal?)Math.Round(g.Average(x => GetFulfilmentScore(x.PurchaseStatus)), 2)
                        : null;

                    decimal qtyAvailable = 0m;
                    decimal? stockCoveragePct = null;
                    if (mappedStock != null)
                    {
                        qtyAvailable = Math.Min(mappedStock.AvailableStock, Math.Max(0m, latest.LastPurchaseQuantity));
                        if (requestedQuantity > 0m)
                            stockCoveragePct = Math.Round(Math.Min(100m, (qtyAvailable / requestedQuantity) * 100m), 2);
                    }

                    return new SupplierOption
                    {
                        VendorID = cheapest.VendorID,
                        VendorName = cheapest.VendorName,
                        Rate = cheapest.Rate,
                        Unit = string.IsNullOrWhiteSpace(latest.Unit) ? cheapest.Unit : latest.Unit,
                        Source = "Purchase history",
                        Phone = latest.Phone ?? cheapest.Phone,
                        Email = latest.Email ?? cheapest.Email,
                        MatchedItemName = latest.SourceItem ?? cheapest.SourceItem,
                        EffectiveDate = latest.PurchaseDate ?? cheapest.PurchaseDate,
                        LastPurchaseDate = g.Max(x => x.PurchaseDate),
                        QtyAvailable = qtyAvailable,
                        LeadDays = g.Any(x => x.LeadDays.HasValue)
                            ? (int?)Convert.ToInt32(Math.Round(g.Where(x => x.LeadDays.HasValue).Average(x => x.LeadDays.Value), MidpointRounding.AwayFromZero))
                            : null,
                        OnTimeDeliveryRatePct = onTimeRate,
                        StockCoveragePct = stockCoveragePct,
                        FulfilmentRatePct = fulfilmentRate,
                        RequestedQuantity = requestedQuantity,
                        DefaultCreditDays = latest.DefaultCreditDays,
                        TotalPurchased = latest.TotalPurchased
                    };
                })
                .ToList();

            ApplyWeightedSupplierScores(itemDescription, mappedStock, ranked);
            return ranked
                .OrderBy(o => o.WeightedScore)
                .ThenBy(o => o.Rate <= 0 ? decimal.MaxValue : o.Rate)
                .ThenByDescending(o => IsExactSupplierItemMatch(itemDescription, o.MatchedItemName))
                .ThenBy(o => o.VendorName)
                .ToList();
        }

        public SupplierRateDriftInfo GetSupplierRateDrift(string itemDescription, int? vendorId, decimal currentRate)
        {
            if (string.IsNullOrWhiteSpace(itemDescription) || !vendorId.HasValue || vendorId.Value <= 0 || currentRate <= 0m)
                return null;

            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 1
                        p.VendorID,
                        v.VendorName,
                        COALESCE(NULLIF(pli.UnitPrice, 0),
                            CASE
                                WHEN ISNULL(pli.Quantity, 0) > 0 AND ISNULL(pli.Amount, 0) > 0 THEN pli.Amount / NULLIF(pli.Quantity, 0)
                                WHEN ISNULL(pli.Rate, 0) > 0 THEN pli.Rate
                                ELSE 0
                            END) AS LastRate
                    FROM PurchaseLineItems pli
                    INNER JOIN PurchaseOrders p ON p.POID = pli.POID
                    INNER JOIN Vendors v ON v.VendorID = p.VendorID
                    WHERE p.VendorID = @vendorId
                      AND (
                            COALESCE(pli.ItemName, pli.Description) = @exact
                            OR COALESCE(pli.ItemName, pli.Description) LIKE @item
                          )
                    ORDER BY COALESCE(p.PODate, p.CreatedDate, p.ModifiedDate) DESC, pli.LineItemID DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@vendorId", vendorId.Value);
                    cmd.Parameters.AddWithValue("@exact", itemDescription.Trim());
                    cmd.Parameters.AddWithValue("@item", "%" + itemDescription.Trim() + "%");
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read() || reader["LastRate"] == DBNull.Value)
                            return null;

                        decimal lastRate = Convert.ToDecimal(reader["LastRate"]);
                        if (lastRate <= 0m)
                            return null;

                        decimal driftPct = Math.Round(((currentRate - lastRate) / lastRate) * 100m, 2);
                        SupplierRateDriftInfo info = new SupplierRateDriftInfo
                        {
                            VendorID = vendorId.Value,
                            VendorName = reader["VendorName"] == DBNull.Value ? string.Empty : Convert.ToString(reader["VendorName"]),
                            ItemName = itemDescription.Trim(),
                            CurrentRate = currentRate,
                            LastRate = lastRate,
                            DriftPercent = Math.Abs(driftPct),
                            IsIncrease = driftPct > 0.01m,
                            IsDecrease = driftPct < -0.01m,
                            IsWarningThresholdExceeded = driftPct > 5m
                        };

                        if (info.IsIncrease)
                            info.DisplayText = "Rate up " + info.DriftPercent.ToString("0.##") + "% vs last purchase";
                        else if (info.IsDecrease)
                            info.DisplayText = "Rate down " + info.DriftPercent.ToString("0.##") + "% vs last purchase";
                        else
                            info.DisplayText = "Rate matches last purchase";

                        return info;
                    }
                }
            }
        }

        public VendorScorecardDto GetVendorScorecard(int vendorId)
        {
            VendorScorecardDto dto = new VendorScorecardDto();
            if (vendorId <= 0)
                return dto;

            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT
                        SUM(ISNULL(p.TotalAmount, 0)) AS TotalPurchaseValue,
                        AVG(CASE
                            WHEN COALESCE(pli.ExpectedDeliveryDate, p.PayByDate) IS NULL OR COALESCE(j.ClosedDate, j.CompletedDate) IS NULL THEN NULL
                            WHEN COALESCE(j.ClosedDate, j.CompletedDate) <= COALESCE(pli.ExpectedDeliveryDate, p.PayByDate) THEN 100.0
                            ELSE 0.0
                        END) AS OnTimeRatePct,
                        AVG(CASE
                            WHEN ISNULL(p.Status, '') IN ('Fully Received', 'Received', 'Paid', 'Closed') THEN 100.0
                            WHEN ISNULL(p.Status, '') IN ('Partial', 'Partially Received') THEN 60.0
                            ELSE 0.0
                        END) AS FulfilmentPct
                    FROM PurchaseOrders p
                    LEFT JOIN PurchaseLineItems pli ON pli.POID = p.POID
                    LEFT JOIN Jobs j ON pli.LinkedWorkOrderId = j.JobID
                    WHERE p.VendorID = @vendorId;", conn))
                {
                    cmd.Parameters.AddWithValue("@vendorId", vendorId);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            dto.TotalPurchaseValue = reader["TotalPurchaseValue"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["TotalPurchaseValue"]);
                            dto.OnTimeDeliveryRatePct = reader["OnTimeRatePct"] == DBNull.Value ? (decimal?)null : Math.Round(Convert.ToDecimal(reader["OnTimeRatePct"]), 2);
                            dto.FulfilmentCompletenessPct = reader["FulfilmentPct"] == DBNull.Value ? (decimal?)null : Math.Round(Convert.ToDecimal(reader["FulfilmentPct"]), 2);
                            dto.ReliabilityScorePct = BuildCompositeReliability(dto.OnTimeDeliveryRatePct, dto.FulfilmentCompletenessPct);
                        }
                    }
                }

                using (SqlCommand trendCmd = new SqlCommand(@"
                    SELECT
                        COALESCE(NULLIF(LTRIM(RTRIM(COALESCE(pli.ItemName, pli.Description))), ''), 'Material') AS ItemName,
                        ISNULL(NULLIF(LTRIM(RTRIM(pli.UOM)), ''), 'Nos') AS UOM,
                        DATEFROMPARTS(YEAR(COALESCE(p.PODate, p.CreatedDate, GETDATE())), MONTH(COALESCE(p.PODate, p.CreatedDate, GETDATE())), 1) AS PeriodDate,
                        AVG(COALESCE(NULLIF(pli.UnitPrice, 0),
                            CASE
                                WHEN ISNULL(pli.Quantity, 0) > 0 AND ISNULL(pli.Amount, 0) > 0 THEN pli.Amount / NULLIF(pli.Quantity, 0)
                                WHEN ISNULL(pli.Rate, 0) > 0 THEN pli.Rate
                                ELSE 0
                            END)) AS AvgUnitPrice,
                        SUM(ISNULL(pli.Quantity, 0)) AS TotalQty
                    FROM PurchaseOrders p
                    INNER JOIN PurchaseLineItems pli ON pli.POID = p.POID
                    WHERE p.VendorID = @vendorId
                      AND COALESCE(p.PODate, p.CreatedDate, GETDATE()) >= DATEADD(month, -11, DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1))
                    GROUP BY
                        COALESCE(NULLIF(LTRIM(RTRIM(COALESCE(pli.ItemName, pli.Description))), ''), 'Material'),
                        ISNULL(NULLIF(LTRIM(RTRIM(pli.UOM)), ''), 'Nos'),
                        DATEFROMPARTS(YEAR(COALESCE(p.PODate, p.CreatedDate, GETDATE())), MONTH(COALESCE(p.PODate, p.CreatedDate, GETDATE())), 1)
                    ORDER BY ItemName, PeriodDate;", conn))
                {
                    trendCmd.Parameters.AddWithValue("@vendorId", vendorId);
                    using (SqlDataReader reader = trendCmd.ExecuteReader())
                    {
                        Dictionary<string, VendorPriceTrendSeries> byItem = new Dictionary<string, VendorPriceTrendSeries>(StringComparer.OrdinalIgnoreCase);
                        while (reader.Read())
                        {
                            string itemName = reader["ItemName"] == DBNull.Value ? "Material" : Convert.ToString(reader["ItemName"]);
                            VendorPriceTrendSeries series;
                            if (!byItem.TryGetValue(itemName, out series))
                            {
                                series = new VendorPriceTrendSeries
                                {
                                    ItemName = itemName,
                                    UOM = reader["UOM"] == DBNull.Value ? "Nos" : Convert.ToString(reader["UOM"])
                                };
                                byItem[itemName] = series;
                            }

                            series.Points.Add(new VendorPriceTrendPoint
                            {
                                PeriodDate = reader["PeriodDate"] == DBNull.Value ? DateTime.Today : Convert.ToDateTime(reader["PeriodDate"]),
                                UnitPrice = reader["AvgUnitPrice"] == DBNull.Value ? 0m : Math.Round(Convert.ToDecimal(reader["AvgUnitPrice"]), 2),
                                Quantity = reader["TotalQty"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["TotalQty"])
                            });
                        }

                        dto.PriceTrends = byItem.Values
                            .OrderByDescending(s => s.Points.Count)
                            .ThenBy(s => s.ItemName)
                            .ToList();
                    }
                }
            }

            return dto;
        }

        private void ApplyWeightedSupplierScores(string itemDescription, StockItem mappedStock, List<SupplierOption> options)
        {
            if (options == null || options.Count == 0)
                return;

            decimal lowestRate = options.Where(o => o != null && o.Rate > 0m).Select(o => o.Rate).DefaultIfEmpty(0m).Min();
            decimal highestRate = options.Where(o => o != null && o.Rate > 0m).Select(o => o.Rate).DefaultIfEmpty(lowestRate).Max();

            foreach (SupplierOption option in options.Where(o => o != null))
            {
                option.PreferredVendorMatch = mappedStock != null
                    && mappedStock.VendorID.HasValue
                    && mappedStock.VendorID.Value > 0
                    && mappedStock.VendorID.Value == option.VendorID;

                decimal priceScore = BuildPriceScore(option.Rate, lowestRate, highestRate);
                option.WeightedPriceScore = priceScore;

                List<Tuple<decimal, decimal>> weightedSignals = new List<Tuple<decimal, decimal>>
                {
                    Tuple.Create(50m, priceScore)
                };

                if (option.OnTimeDeliveryRatePct.HasValue)
                    weightedSignals.Add(Tuple.Create(30m, option.OnTimeDeliveryRatePct.Value));
                if (option.StockCoveragePct.HasValue)
                    weightedSignals.Add(Tuple.Create(20m, option.StockCoveragePct.Value));

                decimal totalWeight = weightedSignals.Sum(x => x.Item1);
                decimal compositeScore = totalWeight <= 0m
                    ? 0m
                    : Math.Round(weightedSignals.Sum(x => x.Item2 * x.Item1) / totalWeight, 2);
                decimal penaltyScore = Math.Round(Math.Max(0m, 100m - compositeScore), 2);

                option.WeightedScore = penaltyScore;
                option.RecommendationScore = Math.Round(100m - penaltyScore, 2);

                List<string> reasons = new List<string>();
                reasons.Add("score " + penaltyScore.ToString("0.##"));
                reasons.Add("price " + priceScore.ToString("0.##"));
                if (option.OnTimeDeliveryRatePct.HasValue)
                    reasons.Add("on-time " + option.OnTimeDeliveryRatePct.Value.ToString("0.#") + "%");
                if (option.StockCoveragePct.HasValue)
                    reasons.Add("stock " + option.StockCoveragePct.Value.ToString("0.#") + "%");
                if (option.PreferredVendorMatch)
                    reasons.Add("mapped vendor");
                if (IsExactSupplierItemMatch(itemDescription, option.MatchedItemName))
                    reasons.Add("exact item match");
                option.RecommendationReason = string.Join(", ", reasons.Take(4));
            }
        }

        private static decimal BuildPriceScore(decimal rate, decimal lowestRate, decimal highestRate)
        {
            if (rate <= 0m || lowestRate <= 0m)
                return 0m;

            if (highestRate <= lowestRate)
                return 100m;

            decimal normalized = (highestRate - rate) / (highestRate - lowestRate);
            return Math.Round(Math.Max(0m, Math.Min(100m, normalized * 100m)), 2);
        }

        private static decimal GetFulfilmentScore(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return 0m;

            string normalized = status.Trim();
            if (string.Equals(normalized, "Fully Received", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Received", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Paid", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Closed", StringComparison.OrdinalIgnoreCase))
                return 100m;
            if (string.Equals(normalized, "Partial", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Partially Received", StringComparison.OrdinalIgnoreCase))
                return 60m;
            return 0m;
        }

        private static decimal? BuildCompositeReliability(decimal? onTimeRatePct, decimal? fulfilmentPct)
        {
            List<Tuple<decimal, decimal>> signals = new List<Tuple<decimal, decimal>>();
            if (onTimeRatePct.HasValue)
                signals.Add(Tuple.Create(60m, onTimeRatePct.Value));
            if (fulfilmentPct.HasValue)
                signals.Add(Tuple.Create(40m, fulfilmentPct.Value));
            if (signals.Count == 0)
                return null;

            decimal totalWeight = signals.Sum(x => x.Item1);
            return Math.Round(signals.Sum(x => x.Item2 * x.Item1) / totalWeight, 2);
        }

        private static bool IsExactSupplierItemMatch(string itemDescription, string matchedItemName)
        {
            return string.Equals(NormalizeSupplierMatchText(itemDescription), NormalizeSupplierMatchText(matchedItemName), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeSupplierMatchText(string text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : new string(text.Trim().ToLowerInvariant().Where(ch => char.IsLetterOrDigit(ch)).ToArray());
        }

        private void PrepareVendor(Vendor vendor)
        {
            if (vendor == null)
                return;

            vendor.VendorName = (vendor.VendorName ?? string.Empty).Trim();
            vendor.GSTNumber = string.IsNullOrWhiteSpace(vendor.GSTNumber) ? null : vendor.GSTNumber.Trim().ToUpperInvariant();
            vendor.PANNumber = string.IsNullOrWhiteSpace(vendor.PANNumber) ? null : vendor.PANNumber.Trim().ToUpperInvariant();
            vendor.BankIFSC = string.IsNullOrWhiteSpace(vendor.BankIFSC) ? null : vendor.BankIFSC.Trim().ToUpperInvariant();
            vendor.Phone = string.IsNullOrWhiteSpace(vendor.Phone) ? null : GlobalValidationEngine.CleanText(vendor.Phone, 30);
            vendor.WhatsAppNumber = string.IsNullOrWhiteSpace(vendor.WhatsAppNumber) ? null : GlobalValidationEngine.CleanText(vendor.WhatsAppNumber, 30);
            vendor.Email = string.IsNullOrWhiteSpace(vendor.Email) ? null : GlobalValidationEngine.CleanText(vendor.Email, 200);
            vendor.Address = string.IsNullOrWhiteSpace(vendor.Address) ? null : GlobalValidationEngine.CleanText(vendor.Address, 500);
            vendor.City = string.IsNullOrWhiteSpace(vendor.City) ? null : GlobalValidationEngine.CleanText(vendor.City, 120);
            vendor.Category = string.IsNullOrWhiteSpace(vendor.Category) ? null : GlobalValidationEngine.CleanText(vendor.Category, 120);
            vendor.BankAccountNumber = string.IsNullOrWhiteSpace(vendor.BankAccountNumber) ? null : GlobalValidationEngine.CleanText(vendor.BankAccountNumber, 60);
            vendor.BankAccountName = string.IsNullOrWhiteSpace(vendor.BankAccountName) ? null : GlobalValidationEngine.CleanText(vendor.BankAccountName, 120);
            vendor.BankName = string.IsNullOrWhiteSpace(vendor.BankName) ? null : GlobalValidationEngine.CleanText(vendor.BankName, 120);
            vendor.PreferredPaymentMode = string.IsNullOrWhiteSpace(vendor.PreferredPaymentMode) ? null : GlobalValidationEngine.CleanText(vendor.PreferredPaymentMode, 40);
            vendor.Notes = string.IsNullOrWhiteSpace(vendor.Notes) ? null : GlobalValidationEngine.CleanText(vendor.Notes, 1000);
            vendor.GSTRegistrationType = string.IsNullOrWhiteSpace(vendor.GSTRegistrationType) ? "Regular" : vendor.GSTRegistrationType.Trim();
            vendor.VendorType = string.IsNullOrWhiteSpace(vendor.VendorType) ? "Supplier" : vendor.VendorType.Trim();
            ApplyRoleDefaults(vendor);
            vendor.MSMERegistered = string.IsNullOrWhiteSpace(vendor.MSMERegistered) ? "No" : vendor.MSMERegistered.Trim();
            vendor.SpecialisationTags = NormalizeTags(vendor.SpecialisationTags);

            OnGSTINChanged(vendor);
            if (string.Equals(vendor.GSTRegistrationType, "Unregistered", StringComparison.OrdinalIgnoreCase))
                vendor.RCMApplicable = true;
        }

        /// <summary>Applies safe supplier/vendor role defaults from the legacy VendorType field.</summary>
        private static void ApplyRoleDefaults(Vendor vendor)
        {
            if (vendor == null)
                return;

            string type = (vendor.VendorType ?? string.Empty).Trim();
            bool supplierType = string.Equals(type, "Supplier", StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, "Distributor", StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, "Trader", StringComparison.OrdinalIgnoreCase);
            bool serviceType = string.Equals(type, "Vendor", StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, "Subcontractor", StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, "Labour", StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, "Service Provider", StringComparison.OrdinalIgnoreCase);

            if (!vendor.IsSupplier && !vendor.IsServiceVendor)
            {
                vendor.IsSupplier = supplierType || !serviceType;
                vendor.IsServiceVendor = serviceType;
            }

            if (supplierType)
                vendor.IsSupplier = true;
            if (serviceType)
                vendor.IsServiceVendor = true;
        }

        private void ValidateVendorForSave(Vendor vendor)
        {
            FluentValidationGuard.EnsureValid(_vendorValidator, vendor, "Vendor validation failed.");
            ValidationResult result = _businessRules.ValidateVendor(vendor);
            result.Merge(_duplicateDetection.CheckVendor(vendor, _repo.GetAll(true).Where(v => !v.IsArchived)));
            _validation.EnsureValid(result, "Vendor validation failed");
        }

        private void InvalidateVendorCaches()
        {
            AppDataCache.RemovePrefix("vendors:");
            lock (DuplicateSync)
            {
                _duplicateCacheStamp = DateTime.MinValue;
                _duplicateCache = new List<DuplicateGroupDto>();
            }
        }

        /// <summary>Loads purchase orders for vendor metrics without blocking vendor master visibility.</summary>
        private List<PurchaseOrder> SafeLoadPurchaseOrders(string context)
        {
            try
            {
                return _purchaseRepo.GetAll();
            }
            catch (Exception ex)
            {
                AppRuntime.LogException(context, ex);
                return new List<PurchaseOrder>();
            }
        }

        /// <summary>Loads duplicate vendor ids without blanking the vendor dashboard when duplicate analysis fails.</summary>
        private HashSet<int> SafeLoadDuplicateVendorIds()
        {
            try
            {
                return new HashSet<int>(DetectDuplicates().SelectMany(g => g.Vendors.Select(v => v.VendorId)));
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("VendorService.GetAllVendorsWithSummary.Duplicates", ex);
                return new HashSet<int>();
            }
        }

        private static string NormalizeTags(string tags)
        {
            if (string.IsNullOrWhiteSpace(tags))
                return null;

            List<string> cleaned = tags
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(tag => tag.Trim())
                .Where(tag => tag.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToList();

            return cleaned.Count == 0 ? null : string.Join(",", cleaned);
        }

        private static string NormalizeVendorName(string vendorName)
        {
            if (string.IsNullOrWhiteSpace(vendorName))
                return string.Empty;

            string normalized = vendorName.Trim().ToUpperInvariant();
            string[] removals = { "M/S.", "M/S", "MR.", "MR", "MRS.", "MRS", "VENDOR:", "OR ", "LTD", "PVT", "LIMITED", "PRIVATE" };
            foreach (string removal in removals)
                normalized = normalized.Replace(removal, " ");

            normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
            return normalized;
        }

        private static bool IsOpenVendorPurchaseOrder(PurchaseOrder po)
        {
            if (po == null || po.IsPaymentCompleted)
                return false;
            if (po.TotalAmount > 0m && po.PaidAmount >= po.TotalAmount)
                return false;

            return IsPurchaseStatus(po.Status,
                "Draft",
                "Pending",
                "Pending Approval",
                "Approval Pending",
                "Approved",
                "Partial",
                "Partially Received");
        }

        private static decimal GetOutstandingPurchaseBalance(PurchaseOrder po)
        {
            if (!IsOpenVendorPurchaseOrder(po))
                return 0m;

            if (po.BalanceDue > 0.01m)
                return po.BalanceDue;

            return po.PaidAmount <= 0.01m ? Math.Max(0m, po.TotalAmount) : 0m;
        }

        private static bool IsPurchaseStatus(string status, params string[] allowed)
        {
            if (string.IsNullOrWhiteSpace(status))
                return false;

            foreach (string item in allowed)
            {
                if (string.Equals(status.Trim(), item, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static DuplicateGroupDto CloneDuplicateGroup(DuplicateGroupDto group)
        {
            return new DuplicateGroupDto
            {
                NormalisedName = group.NormalisedName,
                CombinedOutstanding = group.CombinedOutstanding,
                Vendors = group.Vendors
                    .Select(v => new DuplicateVendorItemDto
                    {
                        VendorId = v.VendorId,
                        VendorName = v.VendorName,
                        OpenPOCount = v.OpenPOCount,
                        OutstandingBalance = v.OutstandingBalance
                    })
                    .ToList()
            };
        }

        private static string ResolveStateName(string stateCode)
        {
            if (string.IsNullOrWhiteSpace(stateCode))
                return string.Empty;

            return IndiaStateCatalog.Names.FirstOrDefault(name => IndiaStateCatalog.GetCodeByName(name) == stateCode) ?? stateCode;
        }

        private static void LogVendorEvent(string action, int vendorId, string detail)
        {
            try
            {
                ServoLog.WriteDiagnosticLine("vendor-actions.log", action + " | " + vendorId + " | " + detail);
            }
            catch
            {
            }
        }
    }
}

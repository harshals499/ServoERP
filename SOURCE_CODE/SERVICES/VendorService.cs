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

namespace HVAC_Pro_Desktop.Services
{
    public class VendorService
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(45);
        private static readonly object DuplicateSync = new object();
        private static DateTime _duplicateCacheStamp = DateTime.MinValue;
        private static List<DuplicateGroupDto> _duplicateCache = new List<DuplicateGroupDto>();

        private readonly VendorRepository _repo = new VendorRepository();
        private readonly PurchaseRepository _purchaseRepo = new PurchaseRepository();
        private readonly DatabaseManager _db = new DatabaseManager();
        private readonly InventoryService _inventorySvc = new InventoryService();
        private readonly BusinessRuleEngine _businessRules = new BusinessRuleEngine();
        private readonly DuplicateDetectionService _duplicateDetection = new DuplicateDetectionService();
        private readonly GlobalValidationEngine _validation = new GlobalValidationEngine();
        private readonly AuditTrailService _audit = new AuditTrailService();

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
                List<Vendor> vendors = _repo.GetAll(true);
                List<PurchaseOrder> orders = SafeLoadPurchaseOrders("VendorService.GetAllVendorsWithSummary.Purchases");
                HashSet<int> duplicateIds = SafeLoadDuplicateVendorIds();

                return vendors
                    .Select(v =>
                    {
                        List<PurchaseOrder> vendorOrders = orders.Where(po => po.VendorID == v.VendorID).ToList();
                        decimal outstanding = vendorOrders
                            .Where(IsOpenVendorPurchaseOrder)
                            .Sum(GetOutstandingPurchaseBalance);

                        int openCount = vendorOrders.Count(IsOpenVendorPurchaseOrder);
                        bool hasOverdue = vendorOrders.Any(po => po.IsOverdue);

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
                            OutstandingBalance = outstanding,
                            OpenPOCount = openCount,
                            HasOverdue = hasOverdue,
                            IsDuplicate = duplicateIds.Contains(v.VendorID),
                            TotalPurchased = v.TotalPurchased,
                            MSMERegistered = v.MSMERegistered
                        };
                    })
                    .OrderByDescending(v => v.HasOverdue)
                    .ThenBy(v => v.VendorName)
                    .ToList();
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
                            List<PurchaseOrder> vendorOrders = orders.Where(po => po.VendorID == vendor.VendorID).ToList();
                            decimal outstanding = vendorOrders
                                .Where(IsOpenVendorPurchaseOrder)
                                .Sum(GetOutstandingPurchaseBalance);

                            group.Vendors.Add(new DuplicateVendorItemDto
                            {
                                VendorId = vendor.VendorID,
                                VendorName = vendor.VendorName,
                                OpenPOCount = vendorOrders.Count(IsOpenVendorPurchaseOrder),
                                OutstandingBalance = outstanding
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
                RecentPOs = vendorOrders.OrderByDescending(po => po.PODate).Take(5).ToList()
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
            bool valid = Regex.IsMatch(normalized, @"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$");
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
            if (string.IsNullOrWhiteSpace(pan))
                return false;

            return Regex.IsMatch(pan.Trim().ToUpperInvariant(), @"^[A-Z]{5}[0-9]{4}[A-Z]{1}$");
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

            List<SupplierOption> options = GetSupplierOptions(itemDescription, category);
            return options
                .OrderBy(o => o.Rate <= 0 ? decimal.MaxValue : o.Rate)
                .ThenBy(o => o.VendorName)
                .FirstOrDefault();
        }

        public List<SupplierOption> GetSupplierOptions(string itemDescription, string category = null)
        {
            var options = new List<SupplierOption>();
            if (string.IsNullOrWhiteSpace(itemDescription))
                return options;

            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();

                using (SqlCommand supplierCmd = new SqlCommand(@"
                    SELECT sip.VendorID, v.VendorName, sip.Rate, ISNULL(NULLIF(sip.Unit, ''), @defaultUnit) AS Unit, sip.Source, v.Phone, v.Email
                    FROM SupplierItemPrices sip
                    INNER JOIN Vendors v ON sip.VendorID = v.VendorID
                    WHERE (sip.ItemName LIKE @item OR (@category <> '' AND sip.Category = @category))
                      AND ISNULL(v.IsActive, 1) = 1
                      AND ISNULL(v.IsArchived, 0) = 0
                      AND ISNULL(v.IsSupplier, 1) = 1
                    ORDER BY sip.Rate ASC, v.VendorName ASC", conn))
                {
                    supplierCmd.Parameters.AddWithValue("@item", "%" + itemDescription.Trim() + "%");
                    supplierCmd.Parameters.AddWithValue("@category", category ?? string.Empty);
                    supplierCmd.Parameters.AddWithValue("@defaultUnit", _inventorySvc.GetByName(itemDescription)?.Unit ?? "Nos");
                    using (SqlDataReader r = supplierCmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            options.Add(new SupplierOption
                            {
                                VendorID = Convert.ToInt32(r["VendorID"]),
                                VendorName = Convert.ToString(r["VendorName"]),
                                Rate = r["Rate"] == DBNull.Value ? 0m : Convert.ToDecimal(r["Rate"]),
                                Unit = Convert.ToString(r["Unit"]),
                                Source = r["Source"] == DBNull.Value ? "Supplier master" : Convert.ToString(r["Source"]),
                                Phone = r["Phone"] == DBNull.Value ? string.Empty : Convert.ToString(r["Phone"]),
                                Email = r["Email"] == DBNull.Value ? string.Empty : Convert.ToString(r["Email"])
                            });
                        }
                    }
                }

                if (options.Count == 0)
                {
                    using (SqlCommand historyCmd = new SqlCommand(@"
                        SELECT TOP 10
                            p.VendorID,
                            v.VendorName,
                            v.Phone,
                            v.Email,
                            MAX(CASE WHEN pli.Quantity > 0 THEN pli.Amount / NULLIF(pli.Quantity, 0) ELSE pli.Rate END) AS DerivedRate
                        FROM PurchaseLineItems pli
                        INNER JOIN PurchaseOrders p ON pli.POID = p.POID
                        INNER JOIN Vendors v ON p.VendorID = v.VendorID
                        WHERE (pli.Description LIKE @item OR (@category <> '' AND pli.Description LIKE '%' + @category + '%'))
                          AND ISNULL(v.IsActive, 1) = 1
                          AND ISNULL(v.IsArchived, 0) = 0
                          AND ISNULL(v.IsSupplier, 1) = 1
                        GROUP BY p.VendorID, v.VendorName, v.Phone, v.Email
                        HAVING MAX(CASE WHEN pli.Quantity > 0 THEN pli.Amount / NULLIF(pli.Quantity, 0) ELSE pli.Rate END) IS NOT NULL
                        ORDER BY MAX(CASE WHEN pli.Quantity > 0 THEN pli.Amount / NULLIF(pli.Quantity, 0) ELSE pli.Rate END) ASC", conn))
                    {
                        historyCmd.Parameters.AddWithValue("@item", "%" + itemDescription.Trim() + "%");
                        historyCmd.Parameters.AddWithValue("@category", category ?? string.Empty);
                        using (SqlDataReader r = historyCmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                decimal rate = r["DerivedRate"] == DBNull.Value ? 0m : Convert.ToDecimal(r["DerivedRate"]);
                                if (rate <= 0)
                                    continue;

                                options.Add(new SupplierOption
                                {
                                    VendorID = Convert.ToInt32(r["VendorID"]),
                                    VendorName = Convert.ToString(r["VendorName"]),
                                    Rate = rate,
                                    Unit = _inventorySvc.GetByName(itemDescription)?.Unit ?? "Nos",
                                    Source = "Purchase history",
                                    Phone = r["Phone"] == DBNull.Value ? string.Empty : Convert.ToString(r["Phone"]),
                                    Email = r["Email"] == DBNull.Value ? string.Empty : Convert.ToString(r["Email"])
                                });
                            }
                        }
                    }
                }
            }

            return options
                .GroupBy(o => o.VendorID)
                .Select(g => g.OrderBy(x => x.Rate <= 0 ? decimal.MaxValue : x.Rate).First())
                .OrderBy(o => o.Rate <= 0 ? decimal.MaxValue : o.Rate)
                .ThenBy(o => o.VendorName)
                .ToList();
        }

        private void PrepareVendor(Vendor vendor)
        {
            if (vendor == null)
                return;

            vendor.VendorName = (vendor.VendorName ?? string.Empty).Trim();
            vendor.GSTNumber = string.IsNullOrWhiteSpace(vendor.GSTNumber) ? null : vendor.GSTNumber.Trim().ToUpperInvariant();
            vendor.PANNumber = string.IsNullOrWhiteSpace(vendor.PANNumber) ? null : vendor.PANNumber.Trim().ToUpperInvariant();
            vendor.BankIFSC = string.IsNullOrWhiteSpace(vendor.BankIFSC) ? null : vendor.BankIFSC.Trim().ToUpperInvariant();
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
                string dir = @"C:\HVAC_PRO_MSE\LOGS";
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "vendor-actions.log");
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + action + " | " + vendorId + " | " + detail + Environment.NewLine;
                File.AppendAllText(path, line);
            }
            catch
            {
            }
        }
    }
}

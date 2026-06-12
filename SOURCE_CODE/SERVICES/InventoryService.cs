using System;
using System.Collections.Generic;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Models.Validation;
using HVAC_Pro_Desktop.Services.Audit;
using HVAC_Pro_Desktop.Services.Validation;

namespace HVAC_Pro_Desktop.Services
{
    public class InventoryService
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(45);
        private readonly InventoryRepository _repo = new InventoryRepository();
        private readonly BusinessRuleEngine _businessRules = new BusinessRuleEngine();
        private readonly GlobalValidationEngine _validation = new GlobalValidationEngine();
        private readonly AuditTrailService _audit = new AuditTrailService();

        public List<StockItem> GetAll()                    => AppDataCache.GetOrCreate("inventory:all", CacheTtl, _repo.GetAll);
        public StockItem       GetById(int id)             => _repo.GetById(id);
        public List<StockItem> GetLowStock()               => _repo.GetLowStock();
        public List<InventoryDuplicateGroup> FindDuplicateItems() => _repo.FindDuplicateItems();
        public InventoryDuplicateCleanupResult MergeDuplicateItems()
        {
            SessionManager.DemandPermission("Inventory", "Delete");
            InventoryDuplicateCleanupResult result = _repo.MergeDuplicateItems();
            AppDataCache.RemovePrefix("inventory:");
            SessionManager.LogAction("MERGE", "Inventory", null, "Duplicate material cleanup");
            _audit.Record("MERGE", "Inventory", null, "Duplicate material cleanup archived " + result.ItemsArchived + " duplicate rows.");
            return result;
        }
        public int Create(StockItem item)
        {
            SessionManager.DemandPermission("Inventory", "Create");
            ValidateInventoryItem(item);
            int id = _repo.Create(item);
            AppDataCache.RemovePrefix("inventory:");
            SessionManager.LogAction("CREATE", "Inventory", id, "Material item saved");
            _audit.Record("CREATE", "Inventory", id, "Material item saved with data-quality validation");
            return id;
        }
        public void Update(StockItem item)
        {
            SessionManager.DemandPermission("Inventory", "Edit");
            ValidateInventoryItem(item);
            _repo.Update(item);
            AppDataCache.RemovePrefix("inventory:");
            SessionManager.LogAction("EDIT", "Inventory", item.ItemID, "Material item saved");
            _audit.Record("EDIT", "Inventory", item.ItemID, "Material item saved with data-quality validation");
        }
        public void UpdateMaterialRate(int itemId, decimal rate, string source = null)
        {
            SessionManager.DemandPermission("Inventory", "Edit");
            if (itemId <= 0)
                throw new Exception("Material item is required.");
            if (rate < 0)
                throw new Exception("Material rate cannot be negative.");

            _repo.UpdateLastPurchaseRate(itemId, rate);
            AppDataCache.RemovePrefix("inventory:");
            SessionManager.LogAction("EDIT", "Inventory", itemId, "Material rate updated");
            _audit.Record("EDIT", "Inventory", itemId, "Material rate updated" + (string.IsNullOrWhiteSpace(source) ? "." : " from " + source.Trim() + "."));
        }
        public void Delete(int itemId)
        {
            SessionManager.DemandPermission("Inventory", "Delete");
            StockItem existing = _repo.GetById(itemId);
            if (existing == null)
                throw new Exception("Material item not found.");

            _repo.Delete(itemId);
            AppDataCache.RemovePrefix("inventory:");
            SessionManager.LogAction("DELETE", "Inventory", itemId, "Material item archived");
            _audit.Record("DELETE", "Inventory", itemId, "Material item archived from active inventory: " + existing.ItemName);
        }
        public decimal GetTotalStockValue()
        {
            return _repo.GetTotalStockValue();
        }
        public int GetLowStockCount()          => _repo.GetLowStockCount();
        public void AddStock(int itemId, decimal qty)
        {
            SessionManager.DemandPermission("Inventory", "Edit");
            StockItem item = _repo.GetById(itemId);
            if (item == null)
                throw new Exception("Material item not found.");
            decimal newStock = item.CurrentStock + qty;
            if (newStock < 0)
                throw new InvalidOperationException("Material quantity cannot go below zero for " + item.ItemName + ". Current quantity: " + item.CurrentStock.ToString("0.###") + ", requested movement: " + qty.ToString("0.###") + ".");
            _repo.AddStock(itemId, qty);
            AppDataCache.RemovePrefix("inventory:");
            _audit.Record("STOCK", "Inventory", itemId, "Stock movement " + qty.ToString("0.###") + " validated");
        }

        public int TransferStock(int itemId, decimal qty, string fromLocation, string toLocation, string referenceNo, string notes)
        {
            SessionManager.DemandPermission("Inventory", "Edit");
            if (itemId <= 0)
                throw new InvalidOperationException("Material item is required.");
            if (qty <= 0)
                throw new InvalidOperationException("Transfer quantity must be greater than zero.");
            if (string.IsNullOrWhiteSpace(fromLocation))
                throw new InvalidOperationException("Source location is required.");
            if (string.IsNullOrWhiteSpace(toLocation))
                throw new InvalidOperationException("Destination location is required.");
            if (string.Equals(fromLocation.Trim(), toLocation.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Source and destination locations must be different.");

            StockMovement movement = new StockMovement
            {
                ItemID = itemId,
                MovementType = "TransferOut",
                Quantity = qty,
                FromLocation = GlobalValidationEngine.CleanText(fromLocation, 120),
                ToLocation = GlobalValidationEngine.CleanText(toLocation, 120),
                ReferenceNo = GlobalValidationEngine.CleanText(referenceNo, 80),
                Notes = GlobalValidationEngine.CleanText(notes, 1000),
                CreatedByUserId = SessionManager.CurrentUser?.UserId,
                CreatedByName = ResolveActorName()
            };

            int id = _repo.RecordMovement(movement);
            AppDataCache.RemovePrefix("inventory:");
            SessionManager.LogAction("TRANSFER", "Inventory", itemId, "Stock transferred");
            _audit.Record("TRANSFER", "Inventory", itemId, "Stock transferred from " + movement.FromLocation + " to " + movement.ToLocation + ": " + qty.ToString("0.###"));
            return id;
        }

        public int AdjustStock(int itemId, decimal signedQty, string referenceNo, string notes)
        {
            SessionManager.DemandPermission("Inventory", "Edit");
            if (itemId <= 0)
                throw new InvalidOperationException("Material item is required.");
            if (signedQty == 0)
                throw new InvalidOperationException("Adjustment quantity cannot be zero.");

            StockMovement movement = new StockMovement
            {
                ItemID = itemId,
                MovementType = signedQty < 0 ? "Decrease" : "Increase",
                Quantity = Math.Abs(signedQty),
                ReferenceNo = GlobalValidationEngine.CleanText(referenceNo, 80),
                Notes = GlobalValidationEngine.CleanText(notes, 1000),
                CreatedByUserId = SessionManager.CurrentUser?.UserId,
                CreatedByName = ResolveActorName()
            };

            int id = _repo.RecordMovement(movement);
            AppDataCache.RemovePrefix("inventory:");
            SessionManager.LogAction("ADJUST", "Inventory", itemId, "Stock adjusted");
            _audit.Record("ADJUST", "Inventory", itemId, "Stock adjusted by " + signedQty.ToString("0.###"));
            return id;
        }

        public List<StockMovement> GetMovements(int itemId, int maxRows = 100)
        {
            return _repo.GetMovements(itemId, maxRows);
        }

        private static string ResolveActorName()
        {
            AppUserDto user = SessionManager.CurrentUser;
            if (!string.IsNullOrWhiteSpace(user?.DisplayName))
                return user.DisplayName.Trim();
            if (!string.IsNullOrWhiteSpace(user?.Username))
                return user.Username.Trim();
            return Environment.UserName;
        }
        public StockItem GetByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            return _repo.GetByName(name.Trim());
        }

        public StockItem GetByExactName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            string needle = name.Trim();
            return GetAll().Find(i => string.Equals(i.ItemName, needle, StringComparison.OrdinalIgnoreCase));
        }

        public decimal GetStockByDescription(string itemDescription)
        {
            StockItem item = GetByName(itemDescription);
            return item?.AvailableStock ?? 0m;
        }

        public decimal GetLastPurchaseRate(string itemDescription)
        {
            StockItem item = GetByName(itemDescription);
            return item?.LastPurchaseRate ?? 0m;
        }

        private void ValidateInventoryItem(StockItem item)
        {
            ValidationResult result = _businessRules.ValidateInventoryItem(item);
            _validation.EnsureValid(result, "Inventory validation failed");
        }
    }
}

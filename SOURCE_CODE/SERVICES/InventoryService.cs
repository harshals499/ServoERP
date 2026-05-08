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
        public List<StockItem> GetLowStock()               => GetAll().FindAll(i => i.CurrentStock <= i.ReorderLevel);
        public int Create(StockItem item)
        {
            SessionManager.DemandPermission("Inventory", "Create");
            ValidateInventoryItem(item);
            int id = _repo.Create(item);
            AppDataCache.RemovePrefix("inventory:");
            SessionManager.LogAction("CREATE", "Inventory", id, "Inventory item saved");
            _audit.Record("CREATE", "Inventory", id, "Inventory item saved with data-quality validation");
            return id;
        }
        public void Update(StockItem item)
        {
            SessionManager.DemandPermission("Inventory", "Edit");
            ValidateInventoryItem(item);
            _repo.Update(item);
            AppDataCache.RemovePrefix("inventory:");
            SessionManager.LogAction("EDIT", "Inventory", item.ItemID, "Inventory item saved");
            _audit.Record("EDIT", "Inventory", item.ItemID, "Inventory item saved with data-quality validation");
        }
        public decimal GetTotalStockValue()
        {
            decimal total = 0;
            foreach (var item in GetAll())
                total += item.CurrentStock * item.LastPurchaseRate;
            return total;
        }
        public int GetLowStockCount()          => GetLowStock().Count;
        public void AddStock(int itemId, decimal qty)
        {
            SessionManager.DemandPermission("Inventory", "Edit");
            StockItem item = _repo.GetById(itemId);
            if (item == null)
                throw new Exception("Inventory item not found.");
            decimal newStock = item.CurrentStock + qty;
            if (newStock < 0)
                throw new InvalidOperationException("Stock cannot go negative for " + item.ItemName + ". Available stock: " + item.CurrentStock.ToString("0.###") + ", requested movement: " + qty.ToString("0.###") + ".");
            _repo.AddStock(itemId, qty);
            AppDataCache.RemovePrefix("inventory:");
            _audit.Record("STOCK", "Inventory", itemId, "Stock movement " + qty.ToString("0.###") + " validated");
        }
        public StockItem GetByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            string needle = name.Trim();
            return GetAll().Find(i =>
                string.Equals(i.ItemName, needle, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(i.ItemName) && i.ItemName.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0));
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

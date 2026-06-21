using System;
using System.Collections.Generic;
using System.Linq;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public class SupplierItemPriceService
    {
        private readonly SupplierItemPriceRepository _repo = new SupplierItemPriceRepository();

        public List<SupplierItemPrice> GetByItemId(int itemId)
        {
            return _repo.GetByItemId(itemId);
        }

        public List<SupplierItemPrice> GetMatchingForItem(string itemName, string category, int? itemId)
        {
            return _repo.GetMatchingForItem(itemName, category, itemId);
        }

        public void SaveForItem(int itemId, string itemName, string category, IEnumerable<SupplierItemPrice> prices)
        {
            SessionManager.DemandPermission("Inventory", "Edit");
            if (itemId <= 0)
                throw new InvalidOperationException("Material item is required before supplier prices can be saved.");

            List<SupplierItemPrice> cleaned = (prices ?? Enumerable.Empty<SupplierItemPrice>())
                .Where(p => p != null && p.VendorID > 0)
                .GroupBy(p => p.VendorID)
                .Select(g =>
                {
                    SupplierItemPrice preferred = g.FirstOrDefault(x => x.IsPreferred);
                    SupplierItemPrice picked = preferred ?? g.OrderBy(x => x.Rate).First();
                    picked.ItemID = itemId;
                    picked.ItemName = itemName;
                    picked.Category = category;
                    picked.Source = string.IsNullOrWhiteSpace(picked.Source) ? "Item details" : picked.Source;
                    picked.EffectiveDate = picked.EffectiveDate == default(DateTime) ? DateTime.Now : picked.EffectiveDate;
                    return picked;
                })
                .ToList();

            if (cleaned.Count > 1 && cleaned.All(p => !p.IsPreferred))
                cleaned[0].IsPreferred = true;

            _repo.ReplaceForItem(itemId, itemName, category, cleaned);
            AppDataCache.RemovePrefix("inventory:");
        }
    }
}

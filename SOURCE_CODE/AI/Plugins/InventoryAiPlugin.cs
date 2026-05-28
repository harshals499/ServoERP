using System.Linq;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.AI.Plugins
{
    /// <summary>
    /// Read-only inventory helper for shortage and reorder reasoning.
    /// </summary>
    public class InventoryAiPlugin
    {
        public AiPluginResult Build(string prompt)
        {
            var low = new InventoryService().GetLowStock().OrderBy(i => i.AvailableStock).Take(10).ToList();
            var result = new AiPluginResult { Intent = "Inventory Assistant" };
            result.Context = low.Count == 0
                ? "No low-stock inventory items found."
                : "Low stock items: " + string.Join("; ", low.Select(i => i.ItemName + " | " + i.Category + " | Available " + i.AvailableStock.ToString("0.##") + " " + i.Unit + " | Reorder " + i.ReorderLevel.ToString("0.##") + " | Vendor " + (i.VendorName ?? "-")));
            return result;
        }
    }
}

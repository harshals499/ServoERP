using System.Linq;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.AI.Plugins
{
    /// <summary>
    /// Read-only materials helper for vendor ordering and procurement planning.
    /// </summary>
    public class InventoryAiPlugin
    {
        public AiPluginResult Build(string prompt)
        {
            var low = new InventoryService().GetLowStock().OrderBy(i => i.AvailableStock).Take(10).ToList();
            var result = new AiPluginResult { Intent = "Materials Assistant" };
            result.Context = low.Count == 0
                ? "No material ordering items found."
                : "Materials to order: " + string.Join("; ", low.Select(i => i.ItemName + " | " + i.Category + " | Plan " + i.ReorderLevel.ToString("0.##") + " " + i.Unit + " | Supplier " + (i.VendorName ?? "-")));
            return result;
        }
    }
}

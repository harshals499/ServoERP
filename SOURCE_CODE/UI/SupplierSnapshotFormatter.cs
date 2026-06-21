using System;
using System.Collections.Generic;
using System.Linq;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    internal sealed class SupplierSnapshotSummary
    {
        public string EyebrowText { get; set; }
        public string ItemText { get; set; }
        public string SummaryText { get; set; }
        public string DetailText { get; set; }
        public string TooltipText { get; set; }
        public string RecommendationText { get; set; }
        public SupplierOption BestOption { get; set; }
        public int OptionCount { get; set; }
        public bool HasOptions => BestOption != null && OptionCount > 0;
        public bool HasMultipleOptions => OptionCount > 1;
    }

    internal static class SupplierSnapshotFormatter
    {
        public static SupplierSnapshotSummary CreatePrompt(string itemText, string summaryText, string detailText)
        {
            return new SupplierSnapshotSummary
            {
                EyebrowText = "SUPPLIER SNAPSHOT",
                ItemText = itemText,
                SummaryText = summaryText,
                DetailText = detailText,
                TooltipText = summaryText,
                RecommendationText = string.Empty
            };
        }

        public static SupplierSnapshotSummary CreateSummary(string itemDescription, decimal quantity, IEnumerable<SupplierOption> options, Func<string, string> normalizeUnit = null)
        {
            List<SupplierOption> ranked = (options ?? Enumerable.Empty<SupplierOption>())
                .Where(option => option != null && option.VendorID > 0)
                .OrderBy(option => option.Rate <= 0m ? decimal.MaxValue : option.Rate)
                .ThenBy(option => option.VendorName)
                .ToList();

            if (string.IsNullOrWhiteSpace(itemDescription))
            {
                return CreatePrompt(
                    "Select a material to compare offers",
                    "Best supplier, live offer count, and price guidance appear here.",
                    "Choose a material to see recent supplier history.");
            }

            if (ranked.Count == 0)
            {
                return new SupplierSnapshotSummary
                {
                    EyebrowText = "SUPPLIER SNAPSHOT",
                    ItemText = itemDescription,
                    SummaryText = "No saved supplier offer found yet for this material.",
                    DetailText = "No vendor history found in purchase orders for this material yet.",
                    TooltipText = "No saved supplier price found yet.",
                    RecommendationText = "Save a supplier quote to unlock recommendations.",
                    OptionCount = 0
                };
            }

            SupplierOption best = ranked[0];
            string topThree = string.Join("  |  ", ranked.Take(3).Select(option =>
            {
                string unitText = string.IsNullOrWhiteSpace(option.Unit) || normalizeUnit == null
                    ? string.Empty
                    : " / " + normalizeUnit(option.Unit);
                return option.VendorName + " " + IndiaFormatHelper.FormatCurrency(option.Rate) + unitText;
            }));
            if (ranked.Count > 3)
                topThree += "  |  +" + (ranked.Count - 3).ToString() + " more";

            string recommendationReason = !string.IsNullOrWhiteSpace(best.RecommendationReason)
                ? best.RecommendationReason
                : ranked.Count > 1
                    ? "Lowest saved rate across " + ranked.Count.ToString() + " supplier options."
                    : "Best available saved supplier rate for this material.";

            string normalizedUnit = string.IsNullOrWhiteSpace(best.Unit) || normalizeUnit == null
                ? string.Empty
                : " / " + normalizeUnit(best.Unit);

            return new SupplierSnapshotSummary
            {
                EyebrowText = ranked.Count > 1 ? "BEST OF " + ranked.Count.ToString() + " SUPPLIERS" : "BEST SUPPLIER",
                ItemText = itemDescription,
                SummaryText = best.VendorName + " is best at " + IndiaFormatHelper.FormatCurrency(best.Rate) + normalizedUnit + " for " + quantity.ToString("0.##") + " qty.",
                DetailText = topThree,
                TooltipText = "Best supplier: " + best.VendorName + " at " + IndiaFormatHelper.FormatCurrency(best.Rate) + ".",
                RecommendationText = recommendationReason,
                BestOption = best,
                OptionCount = ranked.Count
            };
        }
    }
}

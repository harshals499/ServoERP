using System;
using System.Collections.Generic;
using System.Linq;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.Tests
{
    public static class QuotationDeliveryServiceSmokeTests
    {
        public static IEnumerable<string> RunAll()
        {
            TenderBid quotation = AcceptedQuotation();
            QuotationDeliveryPlan plan = QuotationDeliveryService.BuildDraftPlan(quotation, new[]
            {
                new StockItem { ItemID = 10, ItemName = "Copper pipe", CurrentStock = 8m, ReservedStock = 2m, Unit = "Mtr" }
            });

            Assert(plan.Materials.Count == 2, "material lines should exclude internal labour");
            Assert(plan.Materials[0].QuantityReserved == 6m && plan.Materials[0].ShortfallQuantity == 4m, "available stock should be reserved before creating a shortfall");
            Assert(plan.PurchaseRequirements.Count == 2, "each stock shortfall should become a draft purchase requirement");
            yield return "Accepted Quote Delivery builds reservations and draft purchase requirements without consuming stock.";

            Assert(plan.BillingSchedule.Count == 1 && plan.BillingSchedule[0].Amount == 118000m, "billing schedule must copy the accepted GST-inclusive total");
            Assert(plan.ChecklistItems.Count >= 4, "delivery checklist should be operationally complete");
            yield return "Accepted Quote Delivery prepares one reviewed billing milestone and a field checklist.";

            Assert(QuotationDeliveryService.IsAccepted(quotation), "accepted quotation should be eligible");
            quotation.Status = "Draft";
            quotation.CustomerDocumentStatus = "Quote Draft";
            Assert(!QuotationDeliveryService.IsAccepted(quotation), "draft quotation should be blocked");
            yield return "Accepted Quote Delivery blocks quotations without customer acceptance.";

            Assert(plan.PurchaseRequirements.All(item => item.Status == "Draft"), "purchase requirements must remain drafts");
            yield return "Accepted Quote Delivery never auto-sends supplier purchase orders.";
        }

        public static QuotationDeliveryPlan BuildPreviewPlan()
        {
            return QuotationDeliveryService.BuildDraftPlan(AcceptedQuotation(), new[]
            {
                new StockItem { ItemID = 10, ItemName = "Copper pipe", CurrentStock = 8m, ReservedStock = 2m, Unit = "Mtr" }
            });
        }

        private static TenderBid AcceptedQuotation()
        {
            return new TenderBid
            {
                BidID = 45801,
                QuotationNumber = "QT-2026-0458",
                TenderName = "VRF installation and commissioning",
                ClientID = 17,
                SiteID = 23,
                ClientName = "Madhusuman Industries",
                SiteName = "Pune Plant",
                Status = "Accepted",
                CustomerDocumentStatus = "Quote Accepted",
                DueDate = new DateTime(2026, 10, 12),
                RequiredByDate = new DateTime(2026, 10, 10),
                TotalWithGST = 118000m,
                LineItems = new List<TenderBidLineItem>
                {
                    new TenderBidLineItem { LineItemId = 1, InventoryItemId = 10, ItemDescription = "Copper pipe", Quantity = 10m, Unit = "Mtr", StockAvailable = 6m, BestSupplierId = 4, BestSupplierName = "Cool Air Traders" },
                    new TenderBidLineItem { LineItemId = 2, ItemDescription = "Outdoor unit mounting kit", Quantity = 2m, Unit = "Nos", StockAvailable = 0m },
                    new TenderBidLineItem { LineItemId = 3, ItemDescription = "Installation labour", Quantity = 1m, Unit = "Job", IsInternalLabour = true }
                }
            };
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Accepted Quote Delivery smoke test failed: " + message);
        }
    }
}

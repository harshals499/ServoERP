using System;
using System.Collections.Generic;

namespace HVAC_Pro_Desktop.Models
{
    public sealed class QuotationDeliveryPlan
    {
        public int QuotationId { get; set; }
        public string QuotationNumber { get; set; }
        public string QuotationStatus { get; set; }
        public string ClientName { get; set; }
        public string SiteName { get; set; }
        public string JobTitle { get; set; }
        public DateTime ScheduledDate { get; set; }
        public decimal AcceptedValue { get; set; }
        public int? ExistingJobId { get; set; }
        public string ExistingJobNumber { get; set; }
        public List<string> ChecklistItems { get; set; } = new List<string>();
        public List<QuotationDeliveryMaterialLine> Materials { get; set; } = new List<QuotationDeliveryMaterialLine>();
        public List<QuotationPurchaseRequirement> PurchaseRequirements { get; set; } = new List<QuotationPurchaseRequirement>();
        public List<QuotationBillingMilestone> BillingSchedule { get; set; } = new List<QuotationBillingMilestone>();
        public List<string> Warnings { get; set; } = new List<string>();

        public decimal TotalReservedQuantity
        {
            get
            {
                decimal total = 0m;
                foreach (QuotationDeliveryMaterialLine line in Materials)
                    total += line.QuantityReserved;
                return total;
            }
        }

        public decimal TotalShortfallQuantity
        {
            get
            {
                decimal total = 0m;
                foreach (QuotationDeliveryMaterialLine line in Materials)
                    total += line.ShortfallQuantity;
                return total;
            }
        }
    }

    public sealed class QuotationDeliveryMaterialLine
    {
        public int QuotationLineItemId { get; set; }
        public int? InventoryItemId { get; set; }
        public string ItemDescription { get; set; }
        public decimal QuantityRequired { get; set; }
        public decimal QuantityAvailable { get; set; }
        public decimal QuantityReserved { get; set; }
        public decimal ShortfallQuantity { get; set; }
        public string Unit { get; set; }
        public int? PreferredVendorId { get; set; }
        public string PreferredVendorName { get; set; }
        public string ReservationStatus { get; set; }
    }

    public sealed class QuotationPurchaseRequirement
    {
        public int QuotationLineItemId { get; set; }
        public int? InventoryItemId { get; set; }
        public string ItemDescription { get; set; }
        public decimal QuantityRequired { get; set; }
        public string Unit { get; set; }
        public int? PreferredVendorId { get; set; }
        public string PreferredVendorName { get; set; }
        public DateTime RequiredByDate { get; set; }
        public string Status { get; set; } = "Draft";
    }

    public sealed class QuotationBillingMilestone
    {
        public string MilestoneName { get; set; }
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = "Planned";
    }

    public sealed class QuotationDeliveryResult
    {
        public int DeliveryBatchId { get; set; }
        public int JobId { get; set; }
        public string JobNumber { get; set; }
        public bool WasAlreadyCreated { get; set; }
        public int ChecklistItemCount { get; set; }
        public int ReservationCount { get; set; }
        public int PurchaseRequirementCount { get; set; }
        public int BillingMilestoneCount { get; set; }
    }
}

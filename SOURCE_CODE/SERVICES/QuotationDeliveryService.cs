using System;
using System.Collections.Generic;
using System.Linq;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    /// <summary>
    /// Converts an accepted quotation into reviewed delivery records. It never creates a supplier
    /// purchase order or customer invoice; those remain explicit follow-up actions.
    /// </summary>
    public sealed class QuotationDeliveryService
    {
        private readonly TenderService _tenderService = new TenderService();
        private readonly InventoryService _inventoryService = new InventoryService();
        private readonly JobService _jobService = new JobService();
        private readonly QuotationDeliveryRepository _repository = new QuotationDeliveryRepository();

        public QuotationDeliveryPlan BuildPlan(int quotationId)
        {
            TenderBid quotation = _tenderService.GetByIdDetailed(quotationId);
            if (quotation == null)
                throw new InvalidOperationException("The selected quotation could not be found.");
            if (quotation.ClientID <= 0)
                throw new InvalidOperationException("Link the quotation to a client before starting delivery.");

            QuotationDeliveryResult existing = _repository.FindCompleted(quotationId);
            if (existing == null && !IsAccepted(quotation))
                throw new InvalidOperationException("Only an accepted quotation can start the delivery wizard. Mark the quotation Accepted after customer confirmation.");
            List<StockItem> stock = _inventoryService.GetAll() ?? new List<StockItem>();
            QuotationDeliveryPlan plan = BuildDraftPlan(quotation, stock);
            if (existing != null)
            {
                plan.ExistingJobId = existing.JobId;
                plan.ExistingJobNumber = existing.JobNumber;
                plan.Warnings.Add("This quotation was already converted to " + First(existing.JobNumber, "job #" + existing.JobId) + ". Reopening the wizard will not create duplicates.");
            }
            return plan;
        }

        public QuotationDeliveryResult Confirm(QuotationDeliveryPlan plan)
        {
            if (plan == null || plan.QuotationId <= 0)
                throw new InvalidOperationException("The delivery plan is missing.");

            QuotationDeliveryResult existing = _repository.FindCompleted(plan.QuotationId);
            if (existing != null)
                return existing;

            TenderBid quotation = _tenderService.GetByIdDetailed(plan.QuotationId);
            if (quotation == null || !IsAccepted(quotation))
                throw new InvalidOperationException("The quotation is no longer accepted. Refresh it before continuing.");

            int deliveryBatchId = _repository.GetOrCreatePendingBatch(plan.QuotationId);
            int? jobId = _repository.RecoverJobId(deliveryBatchId);
            Job job;
            if (jobId.HasValue)
            {
                job = _jobService.GetById(jobId.Value);
                if (job == null)
                    throw new InvalidOperationException("A pending delivery handoff references a job that is no longer available. Contact support before retrying.");
            }
            else
            {
                string marker = "[DeliveryBatch:" + deliveryBatchId + "]";
                job = _tenderService.CreateDispatchJobFromQuotation(plan.QuotationId, marker);
            }

            QuotationDeliveryResult result = _repository.Complete(deliveryBatchId, job.JobID, plan);
            _jobService.LogActivity(job.JobID,
                "Accepted quotation " + First(plan.QuotationNumber, "#" + plan.QuotationId) +
                " converted through Delivery Wizard: " + result.ReservationCount + " material plan(s), " +
                result.PurchaseRequirementCount + " draft purchase requirement(s), and " +
                result.BillingMilestoneCount + " billing milestone(s).", "Success");
            AppDataCache.RemovePrefix("jobs:");
            AppDataCache.RemovePrefix("tenders:");
            AppDataCache.RemovePrefix("quotations:");
            DashboardRefreshService.NotifyChanged("Jobs");
            return result;
        }

        public static QuotationDeliveryPlan BuildDraftPlan(TenderBid quotation, IEnumerable<StockItem> inventory)
        {
            if (quotation == null) throw new ArgumentNullException(nameof(quotation));

            DateTime scheduledDate = (quotation.RequiredByDate ?? quotation.DueDate).Date;
            decimal acceptedValue = quotation.TotalWithGST > 0m ? quotation.TotalWithGST : quotation.BidValue;
            var plan = new QuotationDeliveryPlan
            {
                QuotationId = quotation.BidID,
                QuotationNumber = quotation.QuotationNumber,
                QuotationStatus = quotation.Status,
                ClientName = quotation.ClientName,
                SiteName = quotation.SiteName,
                JobTitle = First(quotation.TenderName, "Quotation delivery"),
                ScheduledDate = scheduledDate,
                AcceptedValue = acceptedValue
            };

            plan.ChecklistItems.Add("Review accepted quotation scope with the assigned technician");
            plan.ChecklistItems.Add("Confirm site access, safety requirements, and customer contact");
            plan.ChecklistItems.Add("Verify required materials before technician dispatch");
            plan.ChecklistItems.Add("Complete work, capture customer approval, and prepare billing handoff");

            Dictionary<int, StockItem> inventoryById = (inventory ?? Enumerable.Empty<StockItem>())
                .Where(item => item != null && item.ItemID > 0)
                .GroupBy(item => item.ItemID)
                .ToDictionary(group => group.Key, group => group.First());

            foreach (TenderBidLineItem line in (quotation.LineItems ?? new List<TenderBidLineItem>())
                .Where(value => value != null && !value.IsInternalLabour && value.Quantity > 0m && !string.IsNullOrWhiteSpace(value.ItemDescription)))
            {
                StockItem item = null;
                if (line.InventoryItemId.HasValue)
                    inventoryById.TryGetValue(line.InventoryItemId.Value, out item);

                decimal available = Math.Max(0m, item == null ? line.StockAvailable : item.AvailableStock);
                decimal reserved = Math.Min(line.Quantity, available);
                decimal shortfall = Math.Max(0m, line.Quantity - reserved);
                var material = new QuotationDeliveryMaterialLine
                {
                    QuotationLineItemId = line.LineItemId,
                    InventoryItemId = line.InventoryItemId,
                    ItemDescription = line.ItemDescription.Trim(),
                    QuantityRequired = line.Quantity,
                    QuantityAvailable = available,
                    QuantityReserved = reserved,
                    ShortfallQuantity = shortfall,
                    Unit = First(line.Unit, "Nos"),
                    PreferredVendorId = line.BestSupplierId ?? line.VendorID,
                    PreferredVendorName = line.BestSupplierName,
                    ReservationStatus = shortfall <= 0m ? "Reserved" : reserved > 0m ? "Partially Reserved" : "Purchase Required"
                };
                plan.Materials.Add(material);

                if (shortfall > 0m)
                {
                    plan.PurchaseRequirements.Add(new QuotationPurchaseRequirement
                    {
                        QuotationLineItemId = line.LineItemId,
                        InventoryItemId = line.InventoryItemId,
                        ItemDescription = material.ItemDescription,
                        QuantityRequired = shortfall,
                        Unit = material.Unit,
                        PreferredVendorId = material.PreferredVendorId,
                        PreferredVendorName = material.PreferredVendorName,
                        RequiredByDate = scheduledDate,
                        Status = "Draft"
                    });
                }
            }

            plan.BillingSchedule.Add(new QuotationBillingMilestone
            {
                MilestoneName = "On work completion",
                Amount = acceptedValue,
                DueDate = scheduledDate,
                Status = "Planned"
            });

            if (quotation.SiteID <= 0)
                plan.Warnings.Add("No site is linked. Confirm the delivery location before dispatch.");
            if (plan.Materials.Count == 0)
                plan.Warnings.Add("No material lines were found. The job and checklist can still be created.");
            if (plan.PurchaseRequirements.Any(requirement => !requirement.PreferredVendorId.HasValue))
                plan.Warnings.Add("One or more material shortfalls have no preferred supplier. Procurement must review them before creating a PO.");
            if (acceptedValue <= 0m)
                plan.Warnings.Add("The accepted value is zero. Accounts must review the planned billing milestone.");

            return plan;
        }

        public static bool IsAccepted(TenderBid quotation)
        {
            if (quotation == null) return false;
            string status = (quotation.Status ?? string.Empty).Trim();
            string customerStatus = (quotation.CustomerDocumentStatus ?? string.Empty).Trim();
            return status.Equals("Accepted", StringComparison.OrdinalIgnoreCase)
                || status.Equals("Won", StringComparison.OrdinalIgnoreCase)
                || customerStatus.Equals("Quote Accepted", StringComparison.OrdinalIgnoreCase)
                || customerStatus.Equals("PO Received", StringComparison.OrdinalIgnoreCase);
        }

        private static string First(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}

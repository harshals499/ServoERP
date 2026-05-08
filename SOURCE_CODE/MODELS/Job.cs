using System;
using System.Collections.Generic;

namespace HVAC_Pro_Desktop.Models
{
    public class Job
    {
        public int JobID { get; set; }
        public string JobNumber { get; set; }
        public int ClientID { get; set; }
        public int SiteID { get; set; }
        public string Title { get; set; }
        public string JobTitle { get; set; }
        public string Description { get; set; }
        public int? AssignedEmployeeID { get; set; }
        public string AssignedEmployeeName { get; set; }
        public DateTime ScheduledDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public DateTime? ClosedDate { get; set; }
        public string Priority { get; set; }
        public string Status { get; set; }
        public string PipelineStatus { get; set; }
        public string JobType { get; set; }
        public int? LinkedContractId { get; set; }
        public decimal EstimatedCost { get; set; }
        public decimal Revenue { get; set; }
        public decimal QuotedRevenue { get; set; }
        public decimal ActualRevenue { get; set; }
        public bool IsOverdue { get; set; }
        public int? InvoiceId { get; set; }
        public string Notes { get; set; }
        public string ClientName { get; set; }
        public string SiteName { get; set; }
        public int? CreatedByUserId { get; set; }
        public string CreatedByName { get; set; }
        public int? ModifiedByUserId { get; set; }
        public string ModifiedByName { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class JobChecklistItem
    {
        public int ChecklistItemId { get; set; }
        public int JobId { get; set; }
        public string ItemText { get; set; }
        public bool IsCompleted { get; set; }
        public string CompletedBy { get; set; }
        public DateTime? CompletedDate { get; set; }
        public int SortOrder { get; set; }
    }

    public class JobPartUsed
    {
        public int PartUsedId { get; set; }
        public int JobId { get; set; }
        public int? InventoryItemId { get; set; }
        public string ItemDescription { get; set; }
        public decimal QuantityUsed { get; set; }
        public string Unit { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }
        public bool IsFromInventory { get; set; }
        public string StockStatus { get; set; }
        public decimal AvailableStock { get; set; }
    }

    public class JobActivityEntry
    {
        public int ActivityId { get; set; }
        public int JobId { get; set; }
        public string ActivityText { get; set; }
        public string PerformedBy { get; set; }
        public DateTime ActivityDate { get; set; }
        public string ActivityType { get; set; }
    }

    public class JobChecklistTemplate
    {
        public int TemplateId { get; set; }
        public string JobType { get; set; }
        public string ItemText { get; set; }
        public int SortOrder { get; set; }
    }

    public class JobSummaryDto
    {
        public int JobId { get; set; }
        public string JobNumber { get; set; }
        public string JobTitle { get; set; }
        public string JobType { get; set; }
        public string PipelineStatus { get; set; }
        public string Priority { get; set; }
        public string ClientName { get; set; }
        public string SiteName { get; set; }
        public string TechnicianName { get; set; }
        public int? TechnicianId { get; set; }
        public DateTime ScheduledDate { get; set; }
        public bool IsOverdue { get; set; }
        public decimal QuotedRevenue { get; set; }
        public decimal EstimatedMarginPct { get; set; }
        public int ChecklistCompletedCount { get; set; }
        public int ChecklistTotalCount { get; set; }
        public string Notes { get; set; }
        public decimal PartsCost { get; set; }
    }

    public class JobDetailDto
    {
        public Job Job { get; set; }
        public B2BClient Client { get; set; }
        public ClientSite Site { get; set; }
        public Employee Technician { get; set; }
        public AMCContract Contract { get; set; }
        public List<JobChecklistItem> ChecklistItems { get; set; } = new List<JobChecklistItem>();
        public List<JobPartUsed> PartsUsed { get; set; } = new List<JobPartUsed>();
        public List<JobActivityEntry> ActivityLog { get; set; } = new List<JobActivityEntry>();
        public decimal PartsCost { get; set; }
        public decimal TravelCost { get; set; }
        public decimal LabourCost { get; set; }
        public decimal EstimatedProfit { get; set; }
        public decimal EstimatedMarginPct { get; set; }
        public int ChecklistCompletedCount { get; set; }
        public int ChecklistTotalCount { get; set; }
        public bool IsChecklistComplete => ChecklistTotalCount > 0 && ChecklistCompletedCount >= ChecklistTotalCount;
    }

    public class NudgeDto
    {
        public string NudgeType { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
    }

    public class WorkloadDto
    {
        public int EmployeeId { get; set; }
        public int JobCount { get; set; }
        public decimal LoadPercent { get; set; }
        public string LoadColour { get; set; }
    }
}

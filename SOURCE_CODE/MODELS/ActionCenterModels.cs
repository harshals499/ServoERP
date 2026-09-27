using System;
using System.Collections.Generic;

namespace HVAC_Pro_Desktop.Models
{
    public enum ActionCenterPriority
    {
        Normal = 1,
        Today = 2,
        Urgent = 3,
        Critical = 4
    }

    public enum ActionCenterBucket
    {
        NeedsAttention,
        Today,
        MyActions,
        Waiting,
        Upcoming
    }

    public sealed class ActionCenterItem
    {
        public string ActionId { get; set; }
        public string ActionType { get; set; }
        public string SourceModule { get; set; }
        public int SourceRecordId { get; set; }
        public int? SourceChildRecordId { get; set; }
        public int? ClientId { get; set; }
        public int? SiteId { get; set; }
        public string Reference { get; set; }
        public string ClientName { get; set; }
        public string SiteName { get; set; }
        public string Title { get; set; }
        public string Explanation { get; set; }
        public string AssignedUser { get; set; }
        public int? AssignedEmployeeId { get; set; }
        public string AssignedRole { get; set; }
        public string CurrentStatus { get; set; }
        public string SuggestedAction { get; set; }
        public string PriorityReason { get; set; }
        public string DeepLinkIntent { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DueAt { get; set; }
        public DateTime? SlaDeadline { get; set; }
        public decimal FinancialValue { get; set; }
        public ActionCenterPriority Priority { get; set; }
        public ActionCenterBucket Bucket { get; set; }
        public int PriorityScore { get; set; }
        public bool IsOverdue { get; set; }
        public bool IsWaiting { get; set; }
        public bool IsMine { get; set; }
    }

    public sealed class ActionCenterInput
    {
        public List<Job> Jobs { get; set; } = new List<Job>();
        public List<ServiceDeskIncident> ServiceIncidents { get; set; } = new List<ServiceDeskIncident>();
        public List<AMCContract> Contracts { get; set; } = new List<AMCContract>();
        public List<TenderBid> Quotations { get; set; } = new List<TenderBid>();
        public List<Invoice> Invoices { get; set; } = new List<Invoice>();
        public List<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
        public List<StockItem> InventoryItems { get; set; } = new List<StockItem>();
        public List<Payment> Payments { get; set; } = new List<Payment>();
        public List<ActionCenterAmcVisit> AmcVisits { get; set; } = new List<ActionCenterAmcVisit>();
        public List<ActionCenterPartShortage> PartShortages { get; set; } = new List<ActionCenterPartShortage>();
        public AppUserDto User { get; set; }
        public DateTime Now { get; set; } = DateTime.Now;
    }

    public sealed class ActionCenterAmcVisit
    {
        public int VisitId { get; set; }
        public int ContractId { get; set; }
        public int? JobId { get; set; }
        public int VisitNumber { get; set; }
        public DateTime ScheduledDate { get; set; }
        public string Status { get; set; }
        public string TechnicianName { get; set; }
        public int? AssignedEmployeeId { get; set; }
        public int? ClientId { get; set; }
        public int? SiteId { get; set; }
        public string ClientName { get; set; }
        public string SiteName { get; set; }
    }

    public sealed class ActionCenterPartShortage
    {
        public int PartUsedId { get; set; }
        public int JobId { get; set; }
        public string JobNumber { get; set; }
        public string ItemDescription { get; set; }
        public decimal RequiredQuantity { get; set; }
        public decimal AvailableQuantity { get; set; }
        public string Unit { get; set; }
        public string StockStatus { get; set; }
        public int? AssignedEmployeeId { get; set; }
        public string AssignedEmployeeName { get; set; }
        public int? ClientId { get; set; }
        public int? SiteId { get; set; }
        public string ClientName { get; set; }
        public string SiteName { get; set; }
        public DateTime ScheduledDate { get; set; }
    }

    public sealed class ActionCenterSnapshot
    {
        public DateTime GeneratedAt { get; set; }
        public string WorkspaceName { get; set; }
        public List<ActionCenterItem> Items { get; set; } = new List<ActionCenterItem>();
        public ActionCenterItem RecommendedNextAction { get; set; }
        public int CriticalCount { get; set; }
        public int OverdueCount { get; set; }
        public int TodayCount { get; set; }
        public int WaitingCount { get; set; }
        public int UpcomingCount { get; set; }
    }
}

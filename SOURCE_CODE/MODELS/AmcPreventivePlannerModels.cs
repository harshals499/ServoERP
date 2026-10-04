using System;
using System.Collections.Generic;

namespace HVAC_Pro_Desktop.Models
{
    public sealed class AmcPreventivePlan
    {
        public DateTime GeneratedAt { get; set; }
        public int DailyCapacity { get; set; }
        public List<AmcPreventiveObligation> Obligations { get; set; } = new List<AmcPreventiveObligation>();
        public List<AmcReminderDraft> ReminderDrafts { get; set; } = new List<AmcReminderDraft>();
        public int ActiveContractCount { get; set; }
        public int ProposedVisitCount { get; set; }
        public int MissedObligationCount { get; set; }
        public int CapacityConflictCount { get; set; }
    }

    public sealed class AmcPreventiveObligation
    {
        public int ContractId { get; set; }
        public string AmcNumber { get; set; }
        public string ClientName { get; set; }
        public string SiteName { get; set; }
        public int VisitNumber { get; set; }
        public DateTime TargetDate { get; set; }
        public DateTime PlannedDate { get; set; }
        public int PlannedLoad { get; set; }
        public string Status { get; set; }
        public string Action { get; set; }
        public string CapacityNote { get; set; }
        public bool IsProposed { get; set; }
        public bool IsMissed { get; set; }
        public int? ExistingVisitId { get; set; }
    }

    public sealed class AmcReminderDraft
    {
        public int ContractId { get; set; }
        public int VisitNumber { get; set; }
        public int? VisitId { get; set; }
        public string AmcNumber { get; set; }
        public string ClientName { get; set; }
        public string SiteName { get; set; }
        public DateTime ScheduledDate { get; set; }
        public string Channel { get; set; }
        public string Recipient { get; set; }
        public string DraftText { get; set; }
        public string Status { get; set; } = "Draft";
    }

    public sealed class AmcPlannerContractSnapshot
    {
        public int ContractId { get; set; }
        public string AmcNumber { get; set; }
        public string ClientName { get; set; }
        public string SiteName { get; set; }
        public string ContactName { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int VisitsPerYear { get; set; }
        public string Status { get; set; }
        public List<AmcPlannerVisitSnapshot> Visits { get; set; } = new List<AmcPlannerVisitSnapshot>();
    }

    public sealed class AmcPlannerVisitSnapshot
    {
        public int VisitId { get; set; }
        public int ContractId { get; set; }
        public int VisitNumber { get; set; }
        public DateTime ScheduledDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string Status { get; set; }
    }

    public sealed class AmcPlannerApplyResult
    {
        public int VisitsCreated { get; set; }
        public int ReminderDraftsCreated { get; set; }
        public int VisitsAlreadyPresent { get; set; }
    }
}

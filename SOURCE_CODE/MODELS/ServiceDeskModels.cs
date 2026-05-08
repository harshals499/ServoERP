using System;
using System.Collections.Generic;

namespace HVAC_Pro_Desktop.Models
{
    public class ServiceDeskIncident
    {
        public int IncidentId { get; set; }
        public string IncidentNumber { get; set; }
        public int? ClientId { get; set; }
        public int? SiteId { get; set; }
        public int? AssignedEmployeeId { get; set; }
        public int? LinkedJobId { get; set; }
        public string ClientName { get; set; }
        public string SiteName { get; set; }
        public string AssignedEmployeeName { get; set; }
        public string CallerName { get; set; }
        public string CallerPhone { get; set; }
        public string Category { get; set; }
        public string EquipmentType { get; set; }
        public string AssetSerialNumber { get; set; }
        public string Priority { get; set; }
        public string Status { get; set; }
        public string ShortDescription { get; set; }
        public string Description { get; set; }
        public string RootCause { get; set; }
        public string ResolutionCode { get; set; }
        public DateTime OpenedAt { get; set; }
        public DateTime? AssignedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime SlaDueAt { get; set; }
        public bool SlaBreached { get; set; }
        public string CreatedByName { get; set; }
        public string ModifiedByName { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }

    public class ServiceDeskNote
    {
        public int NoteId { get; set; }
        public int IncidentId { get; set; }
        public string NoteType { get; set; }
        public string NoteText { get; set; }
        public string CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ServiceDeskSnapshot
    {
        public int OpenCount { get; set; }
        public int CriticalCount { get; set; }
        public int BreachedCount { get; set; }
        public int ResolvedTodayCount { get; set; }
    }

    public class ServiceDeskDetail
    {
        public ServiceDeskIncident Incident { get; set; }
        public List<ServiceDeskNote> Notes { get; set; } = new List<ServiceDeskNote>();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services.Audit;

namespace HVAC_Pro_Desktop.Services
{
    public class ServiceDeskService
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(15);
        private readonly ServiceDeskRepository _repo = new ServiceDeskRepository();
        private readonly JobService _jobService = new JobService();
        private readonly AuditTrailService _audit = new AuditTrailService();

        public List<ServiceDeskIncident> GetAll()
        {
            List<ServiceDeskIncident> list = AppDataCache.GetOrCreate("servicedesk:all", CacheTtl, _repo.GetAll);
            foreach (ServiceDeskIncident incident in list)
                incident.SlaBreached = IsBreached(incident);
            return list;
        }

        public ServiceDeskDetail GetDetail(int incidentId)
        {
            ServiceDeskIncident incident = _repo.GetById(incidentId);
            if (incident == null)
                return null;
            incident.SlaBreached = IsBreached(incident);
            return new ServiceDeskDetail
            {
                Incident = incident,
                Notes = _repo.GetNotes(incidentId)
            };
        }

        public ServiceDeskSnapshot GetSnapshot()
        {
            List<ServiceDeskIncident> all = GetAll();
            return new ServiceDeskSnapshot
            {
                OpenCount = all.Count(i => !IsClosed(i.Status)),
                CriticalCount = all.Count(i => !IsClosed(i.Status) && string.Equals(i.Priority, "Critical", StringComparison.OrdinalIgnoreCase)),
                BreachedCount = all.Count(i => !IsClosed(i.Status) && IsBreached(i)),
                ResolvedTodayCount = all.Count(i => i.ResolvedAt.HasValue && i.ResolvedAt.Value.Date == DateTime.Today)
            };
        }

        public string GenerateIncidentNumber()
        {
            return _repo.GenerateIncidentNumber();
        }

        public int Save(ServiceDeskIncident incident)
        {
            if (incident == null)
                throw new Exception("Incident is missing.");
            SessionManager.DemandPermission("ServiceDesk", incident.IncidentId <= 0 ? "Create" : "Edit");
            if (string.IsNullOrWhiteSpace(incident.ShortDescription))
                throw new Exception("Short description is required.");

            Normalize(incident);

            int id;
            if (incident.IncidentId <= 0)
            {
                if (SessionManager.IsLoggedIn)
                    incident.CreatedByName = SessionManager.CurrentUser.DisplayName ?? SessionManager.CurrentUser.Username;
                id = _repo.Create(incident);
                AddNote(new ServiceDeskNote { IncidentId = id, NoteType = "System", NoteText = "Incident opened." });
                SessionManager.LogAction("CREATE", "ServiceDesk", id, "Service desk incident created");
                _audit.Record("CREATE", "ServiceDesk", id, "Incident " + incident.IncidentNumber + " created: " + incident.ShortDescription);
            }
            else
            {
                if (SessionManager.IsLoggedIn)
                    incident.ModifiedByName = SessionManager.CurrentUser.DisplayName ?? SessionManager.CurrentUser.Username;
                incident.ModifiedDate = DateTime.Now;
                _repo.Update(incident);
                id = incident.IncidentId;
                SessionManager.LogAction("EDIT", "ServiceDesk", id, "Service desk incident updated");
                _audit.Record("EDIT", "ServiceDesk", id, "Incident " + incident.IncidentNumber + " updated. Status: " + incident.Status);
            }

            AppDataCache.RemovePrefix("servicedesk:");
            return id;
        }

        public void AddNote(ServiceDeskNote note)
        {
            if (note == null || note.IncidentId <= 0 || string.IsNullOrWhiteSpace(note.NoteText))
                return;

            if (string.IsNullOrWhiteSpace(note.NoteType))
                note.NoteType = "Work note";
            if (!string.Equals(note.NoteType, "System", StringComparison.OrdinalIgnoreCase))
                SessionManager.DemandPermission("ServiceDesk", "Edit");
            if (string.IsNullOrWhiteSpace(note.CreatedByName) && SessionManager.IsLoggedIn)
                note.CreatedByName = SessionManager.CurrentUser.DisplayName ?? SessionManager.CurrentUser.Username;
            if (note.CreatedAt == default(DateTime))
                note.CreatedAt = DateTime.Now;

            _repo.AddNote(note);
            AppDataCache.RemovePrefix("servicedesk:");
            SessionManager.LogAction("NOTE", "ServiceDesk", note.IncidentId, "Service desk note added");
            _audit.Record("NOTE", "ServiceDesk", note.IncidentId, "Service desk note added: " + note.NoteType);
        }

        public int CreateJobFromIncident(ServiceDeskIncident incident)
        {
            SessionManager.DemandPermission("WorkOrders", "Create");
            if (incident == null || incident.IncidentId <= 0)
                throw new Exception("Save the incident first.");
            if (!incident.ClientId.HasValue || !incident.SiteId.HasValue)
                throw new Exception("Client and site are required before creating a job.");

            Job job = new Job
            {
                JobNumber = _jobService.GenerateJobNumber(),
                ClientID = incident.ClientId.Value,
                SiteID = incident.SiteId.Value,
                AssignedEmployeeID = incident.AssignedEmployeeId,
                Title = incident.ShortDescription,
                JobTitle = incident.ShortDescription,
                Description = incident.Description,
                JobType = MapCategoryToJobType(incident.Category),
                Priority = incident.Priority,
                Status = "Pending",
                PipelineStatus = incident.AssignedEmployeeId.HasValue ? "Assigned" : "Created",
                ScheduledDate = DateTime.Today,
                Notes = "Created from Service Desk " + incident.IncidentNumber
            };

            int jobId = _jobService.Create(job);
            _repo.LinkJob(incident.IncidentId, jobId);
            AddNote(new ServiceDeskNote { IncidentId = incident.IncidentId, NoteType = "System", NoteText = "Work order created: JOB-" + jobId });
            AppDataCache.RemovePrefix("servicedesk:");
            _audit.Record("CREATE", "WorkOrders", jobId, "Work order created from service desk incident " + incident.IncidentNumber);
            _audit.Record("LINK", "ServiceDesk", incident.IncidentId, "Incident linked to work order #" + jobId);
            return jobId;
        }

        public static DateTime ComputeSlaDue(string priority, DateTime openedAt)
        {
            switch ((priority ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "CRITICAL": return openedAt.AddHours(2);
                case "HIGH": return openedAt.AddHours(4);
                case "LOW": return openedAt.AddDays(2);
                default: return openedAt.AddDays(1);
            }
        }

        private static void Normalize(ServiceDeskIncident incident)
        {
            if (string.IsNullOrWhiteSpace(incident.IncidentNumber))
                incident.IncidentNumber = "INC" + DateTime.Now.ToString("yyyyMMddHHmmss");
            if (incident.OpenedAt == default(DateTime))
                incident.OpenedAt = DateTime.Now;
            if (string.IsNullOrWhiteSpace(incident.Priority))
                incident.Priority = "Medium";
            if (string.IsNullOrWhiteSpace(incident.Status))
                incident.Status = "New";
            if (string.IsNullOrWhiteSpace(incident.Category))
                incident.Category = "General";
            if (incident.SlaDueAt == default(DateTime))
                incident.SlaDueAt = ComputeSlaDue(incident.Priority, incident.OpenedAt);
            if (incident.AssignedEmployeeId.HasValue && !incident.AssignedAt.HasValue)
                incident.AssignedAt = DateTime.Now;
            if (string.Equals(incident.Status, "Resolved", StringComparison.OrdinalIgnoreCase) && !incident.ResolvedAt.HasValue)
                incident.ResolvedAt = DateTime.Now;
            if (string.Equals(incident.Status, "Closed", StringComparison.OrdinalIgnoreCase) && !incident.ClosedAt.HasValue)
                incident.ClosedAt = DateTime.Now;
            incident.SlaBreached = IsBreached(incident);
        }

        private static bool IsBreached(ServiceDeskIncident incident)
        {
            if (incident == null || IsClosed(incident.Status))
                return false;
            return DateTime.Now > incident.SlaDueAt;
        }

        private static bool IsClosed(string status)
        {
            return string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "Resolved", StringComparison.OrdinalIgnoreCase);
        }

        private static string MapCategoryToJobType(string category)
        {
            string value = category ?? string.Empty;
            if (value.IndexOf("AMC", StringComparison.OrdinalIgnoreCase) >= 0)
                return "AMC Visit";
            if (value.IndexOf("Install", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Installation";
            if (value.IndexOf("Gas", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Gas Charging";
            return "Breakdown";
        }
    }
}

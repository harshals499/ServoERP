using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Models.Validation;
using HVAC_Pro_Desktop.Services.Audit;
using HVAC_Pro_Desktop.Services.Validation;
using ServoERP.Validators;

namespace HVAC_Pro_Desktop.Services
{
    public class ClientService
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(45);
        private readonly ClientRepository   _clientRepo   = new ClientRepository();
        private readonly ContractRepository _contractRepo = new ContractRepository();
        private readonly BusinessRuleEngine _businessRules = new BusinessRuleEngine();
        private readonly DuplicateDetectionService _duplicateDetection = new DuplicateDetectionService();
        private readonly GlobalValidationEngine _validation = new GlobalValidationEngine();
        private readonly ReferenceIntegrityService _referenceIntegrity = new ReferenceIntegrityService();
        private readonly AuditTrailService _audit = new AuditTrailService();
        private readonly B2BClientValidator _clientValidator = new B2BClientValidator();
        private readonly SyncMetadataService _syncMetadata = new SyncMetadataService();
        private readonly SyncOutboxService _syncOutbox = new SyncOutboxService();

        // ── READ ─────────────────────────────────────────────
        public List<B2BClient> GetAllClients()
        {
            return AppDataCache.GetOrCreate("clients:all", CacheTtl, _clientRepo.GetAll);
        }

        public List<B2BClient> GetAllClientsIncludingInactive()
        {
            return AppDataCache.GetOrCreate("clients:all-with-inactive", CacheTtl, _clientRepo.GetAllIncludingInactive);
        }

        public B2BClient GetClientById(int id)
        {
            return _clientRepo.GetById(id);
        }

        public List<ClientSite> GetClientSites(int clientId)
        {
            return _clientRepo.GetClientSites(clientId);
        }

        public List<ClientContact> GetClientContacts(int clientId)
        {
            return _clientRepo.GetClientContacts(clientId);
        }

        public List<ClientTeamMember> GetTeamMembers(int clientId)
        {
            return _clientRepo.GetTeamMembers(clientId);
        }

        public void SaveTeamMember(ClientTeamMember member)
        {
            SessionManager.DemandPermission("Clients", member != null && member.Id > 0 ? "Edit" : "Create");
            if (member == null || member.ClientId <= 0)
                throw new Exception("Client team member is missing.");
            _clientRepo.SaveTeamMember(member);
            AppDataCache.RemovePrefix("clients:");
            _audit.Record(member.Id > 0 ? "EDIT" : "CREATE", "Clients", member.ClientId, "Client team member saved.");
        }

        public void DeleteTeamMember(int memberId)
        {
            SessionManager.DemandPermission("Clients", "Delete");
            _clientRepo.DeleteTeamMember(memberId);
            AppDataCache.RemovePrefix("clients:");
            _audit.Record("DELETE", "Clients", memberId, "Client team member deleted.");
        }

        public List<ClientActivity> GetActivities(int clientId, string filterType = "All")
        {
            return _clientRepo.GetActivities(clientId, filterType);
        }

        public void LogActivity(ClientActivity activity)
        {
            SessionManager.DemandPermission("Clients", "Edit");
            if (activity == null || activity.ClientId <= 0)
                throw new Exception("Client activity is missing.");
            if (string.IsNullOrWhiteSpace(activity.ActivityType))
                activity.ActivityType = "Note";
            if (activity.CreatedAt == default(DateTime))
                activity.CreatedAt = DateTime.Now;
            if (string.IsNullOrWhiteSpace(activity.CreatedBy) && SessionManager.IsLoggedIn)
                activity.CreatedBy = SessionManager.CurrentUser.DisplayName ?? SessionManager.CurrentUser.Username;
            _clientRepo.LogActivity(activity);
        }

        public int ComputeHealthScore(int clientId)
        {
            try
            {
                decimal payment = 0m;
                try
                {
                    List<Invoice> invoices = new InvoiceService().GetInvoicesForClient(clientId);
                    decimal total = invoices.Sum(i => i.BalanceDue);
                    decimal overdue = invoices.Where(i => i.DueDate < DateTime.Today && !string.Equals(i.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase)).Sum(i => i.BalanceDue);
                    payment = invoices.Count == 0 || total <= 0 ? 100m : Math.Max(0m, 100m - Math.Min(100m, (overdue / total) * 100m));
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("ClientService.ComputeHealthScore.Payment", ex);
                }

                decimal jobs = 0m;
                try
                {
                    List<Job> clientJobs = new JobService().GetAll().Where(j => j.ClientID == clientId).ToList();
                    int overdue = clientJobs.Count(j => j.IsOverdue);
                    jobs = clientJobs.Count == 0 ? 100m : Math.Max(0m, 100m - ((overdue * 100m) / clientJobs.Count));
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("ClientService.ComputeHealthScore.Jobs", ex);
                }

                decimal contracts = 0m;
                try
                {
                    List<AMCContract> clientContracts = new ContractService().GetContractsByClient(clientId);
                    int active = clientContracts.Count(c => string.Equals(c.ContractStatus, "Active", StringComparison.OrdinalIgnoreCase) && c.EndDate >= DateTime.Today);
                    contracts = clientContracts.Count == 0 ? 0m : Math.Min(100m, (active * 100m) / clientContracts.Count);
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("ClientService.ComputeHealthScore.Contracts", ex);
                }

                decimal activity = 0m;
                try
                {
                    ClientActivity last = _clientRepo.GetActivities(clientId, "All").FirstOrDefault();
                    activity = last != null && last.CreatedAt >= DateTime.Now.AddDays(-30) ? 100m : 30m;
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("ClientService.ComputeHealthScore.Activity", ex);
                }

                int score = (int)Math.Round((payment * 0.30m) + (jobs * 0.25m) + (contracts * 0.25m) + (activity * 0.20m), 0);
                score = Math.Max(0, Math.Min(100, score));
                try { _clientRepo.UpdateHealthScore(clientId, score); } catch (Exception ex) { AppLogger.LogError("ClientService.ComputeHealthScore.Cache", ex); }
                return score;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("ClientService.ComputeHealthScore", ex);
                return 0;
            }
        }

        public List<string> GetTagsForClient(int clientId)
        {
            B2BClient client = _clientRepo.GetById(clientId);
            return SplitTags(client == null ? null : client.Tags);
        }

        public void SaveTagsForClient(int clientId, List<string> tags)
        {
            SessionManager.DemandPermission("Clients", "Edit");
            string value = string.Join(",", (tags ?? new List<string>()).Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).Distinct(StringComparer.OrdinalIgnoreCase));
            _clientRepo.UpdateTags(clientId, value);
            AppDataCache.RemovePrefix("clients:");
            _audit.Record("EDIT", "Clients", clientId, "Client tags updated.");
        }

        // ── CREATE ───────────────────────────────────────────
        public int CreateClient(B2BClient client)
        {
            SessionManager.DemandPermission("Clients", "Create");
            _syncMetadata.EnsureClientIdentity(client);
            ValidateClientForSave(client);
            client.CustomerSince = client.CustomerSince == default
                ? DateTime.Today
                : client.CustomerSince;
            try
            {
                B2BClient existing = client != null && client.SyncPublicId.HasValue ? _clientRepo.GetBySyncPublicId(client.SyncPublicId.Value) : null;
                if (existing != null)
                {
                    client.ClientID = existing.ClientID;
                    UpdateClient(client);
                    return existing.ClientID;
                }

                int id = _clientRepo.Create(client);
                client.ClientID = id;
                _clientRepo.ReplaceClientContacts(id, client.Contacts ?? new List<ClientContact>());
                _syncMetadata.TouchClient(id, client.SyncPublicId ?? Guid.NewGuid());
                NodeIdentityService.EnsureRegistered();
                _syncOutbox.Emit("Clients", client.SyncPublicId ?? Guid.Empty, "Create", client);
                AppDataCache.RemovePrefix("clients:");
                _audit.Record("CREATE", "Clients", id, "Client saved with data-quality validation");
                return id;
            }
            catch (Exception ex) when (OfflineSyncService.ShouldQueue(ex))
            {
                OfflineQueueResult queued = OfflineSyncService.Queue("Clients", "Create", client, null, false, ex.Message, client == null ? (Guid?)null : client.SyncPublicId);
                AppDataCache.RemovePrefix("clients:");
                return queued.LocalId;
            }
        }

        // ── UPDATE ───────────────────────────────────────────
        public void UpdateClient(B2BClient client)
        {
            SessionManager.DemandPermission("Clients", "Edit");
            _syncMetadata.EnsureClientIdentity(client);
            ValidateClientForSave(client);
            try
            {
                _clientRepo.Update(client);
                _clientRepo.ReplaceClientContacts(client.ClientID, client.Contacts ?? new List<ClientContact>());
                _syncMetadata.TouchClient(client.ClientID, client.SyncPublicId ?? Guid.NewGuid());
                NodeIdentityService.EnsureRegistered();
                _syncOutbox.Emit("Clients", client.SyncPublicId ?? Guid.Empty, "Update", client);
                AppDataCache.RemovePrefix("clients:");
                _audit.Record("EDIT", "Clients", client.ClientID, "Client saved with data-quality validation");
            }
            catch (Exception ex) when (OfflineSyncService.ShouldQueue(ex))
            {
                OfflineSyncService.Queue("Clients", "Update", client, client.ClientID, false, ex.Message, client == null ? (Guid?)null : client.SyncPublicId);
                AppDataCache.RemovePrefix("clients:");
            }
        }

        /// <summary>Updates a client's lifecycle status without rewriting the full client profile.</summary>
        public void UpdateLifecycleStatus(int clientId, string status)
        {
            SessionManager.DemandPermission("Clients", "Edit");
            if (clientId <= 0)
                return;

            string normalized = NormalizeLifecycleStatus(status);
            bool active = string.Equals(normalized, "Active", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(normalized, "Prospect", StringComparison.OrdinalIgnoreCase);

            _clientRepo.SetLifecycleStatus(clientId, normalized, active);
            AppDataCache.RemovePrefix("clients:");
            _audit.Record("EDIT", "Clients", clientId, "Client lifecycle status changed to " + normalized);
        }

        // ── DELETE (soft) ────────────────────────────────────
        public void DeleteClient(int id)
        {
            SessionManager.DemandPermission("Clients", "Delete");
            _clientRepo.Delete(id);
            AppDataCache.RemovePrefix("clients:");
            _audit.Record("DELETE", "Clients", id, "Client deleted or archived.");
        }

        // ── SITE ─────────────────────────────────────────────
        public int CreateSite(ClientSite site)
        {
            SessionManager.DemandPermission("Clients", "Create");
            _syncMetadata.EnsureSiteIdentity(site);
            ValidationResult result = _businessRules.ValidateSite(site);
            if (site != null)
                result.Merge(_referenceIntegrity.CheckClientSite(site.ClientID, site.SiteID, GetClientSites(site.ClientID), "Sites"));
            _validation.EnsureValid(result, "Site validation failed");
            try
            {
                ClientSite existing = site != null && site.SyncPublicId.HasValue ? new SiteRepository().GetBySyncPublicId(site.SyncPublicId.Value) : null;
                if (existing != null)
                {
                    site.SiteID = existing.SiteID;
                    new SiteService().Update(site);
                    return existing.SiteID;
                }

                int id = _clientRepo.CreateSite(site);
                site.SiteID = id;
                _syncMetadata.TouchSite(id, site.SyncPublicId ?? Guid.NewGuid());
                NodeIdentityService.EnsureRegistered();
                _syncOutbox.Emit("Sites", site.SyncPublicId ?? Guid.Empty, "Create", site);
                AppDataCache.RemovePrefix("sites:");
                _audit.Record("CREATE", "Sites", id, "Site saved with data-quality validation");
                return id;
            }
            catch (Exception ex) when (OfflineSyncService.ShouldQueue(ex))
            {
                OfflineQueueResult queued = OfflineSyncService.Queue("Sites", "Create", site, null, false, ex.Message, site == null ? (Guid?)null : site.SyncPublicId);
                AppDataCache.RemovePrefix("sites:");
                return queued.LocalId;
            }
        }

        // ── BUSINESS LOGIC ───────────────────────────────────

        /// <summary>
        /// Refresh TotalAnnualValue on the client record from all active contracts.
        /// </summary>
        public void RefreshAnnualValue(int clientId)
        {
            SessionManager.DemandPermission("Clients", "Edit");
            var contracts = _contractRepo.GetByClientId(clientId);
            decimal total = 0;
            foreach (var c in contracts)
                if (c.ContractStatus == "Active")
                    total += c.AnnualValue;

            var client = _clientRepo.GetById(clientId);
            if (client != null)
            {
                client.TotalAnnualValue = total;
                _clientRepo.Update(client);
            }
        }

        // ── VALIDATION ───────────────────────────────────────
        private static void NormalizeClient(B2BClient client)
        {
            if (client == null)
                return;

            client.CompanyName = GlobalValidationEngine.CleanText(client.CompanyName, 200);
            client.PrimaryContact = GlobalValidationEngine.CleanText(client.PrimaryContact, 120);
            client.SecondaryContact = GlobalValidationEngine.CleanText(client.SecondaryContact, 120);
            client.Phone = GlobalValidationEngine.CleanText(client.Phone, 30);
            client.Email = GlobalValidationEngine.CleanText(client.Email, 200);
            client.GSTNumber = string.IsNullOrWhiteSpace(client.GSTNumber) ? null : client.GSTNumber.Trim().ToUpperInvariant();
            client.PANNumber = string.IsNullOrWhiteSpace(client.PANNumber) ? null : client.PANNumber.Trim().ToUpperInvariant();
            client.BillingAddress = GlobalValidationEngine.CleanText(client.BillingAddress, 500);
            client.City = GlobalValidationEngine.CleanText(client.City, 120);
            client.IndustryType = GlobalValidationEngine.CleanText(client.IndustryType, 120);
            client.RelationshipStage = GlobalValidationEngine.CleanText(client.RelationshipStage, 60);
            client.Tags = GlobalValidationEngine.CleanText(client.Tags, 250);
            client.Notes = GlobalValidationEngine.CleanText(client.Notes, 1000);
            client.AssignedTo = GlobalValidationEngine.CleanText(client.AssignedTo, 120);
            client.LeadSource = GlobalValidationEngine.CleanText(client.LeadSource, 120);
        }

        private static void ValidateContacts(B2BClient client)
        {
            if (client?.Contacts == null)
                return;

            foreach (ClientContact contact in client.Contacts)
            {
                if (contact == null)
                    continue;

                contact.ContactName = GlobalValidationEngine.CleanText(contact.ContactName, 150);
                contact.Role = GlobalValidationEngine.CleanText(contact.Role, 100);
                contact.Phone = GlobalValidationEngine.CleanText(contact.Phone, 30);
                contact.Email = GlobalValidationEngine.CleanText(contact.Email, 200);

                if (string.IsNullOrWhiteSpace(contact.ContactName) &&
                    string.IsNullOrWhiteSpace(contact.Role) &&
                    string.IsNullOrWhiteSpace(contact.Phone) &&
                    string.IsNullOrWhiteSpace(contact.Email))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(contact.ContactName))
                    throw new ValidationException("Each client employee row needs a name.");
            }
        }

        private void ValidateClientForSave(B2BClient client)
        {
            NormalizeClient(client);
            FluentValidationGuard.EnsureValid(_clientValidator, client, "Client validation failed.");
            ValidateContacts(client);
            ValidationResult result = _businessRules.ValidateClient(client);
            result.Merge(_duplicateDetection.CheckClient(client, GetAllClientsIncludingInactive()));
            _validation.EnsureValid(result, "Client validation failed");
        }

        private static List<string> SplitTags(string tags)
        {
            return (tags ?? string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Normalizes free-form client lifecycle text to supported application statuses.</summary>
        private static string NormalizeLifecycleStatus(string status)
        {
            string value = (status ?? string.Empty).Trim();
            if (value.Length == 0)
                return "Active";
            if (value.IndexOf("black", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Blacklisted";
            if (value.IndexOf("hold", StringComparison.OrdinalIgnoreCase) >= 0)
                return "On Hold";
            if (value.IndexOf("prospect", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("lead", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Prospect";
            if (value.IndexOf("inactive", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Inactive";
            return "Active";
        }
    }
}

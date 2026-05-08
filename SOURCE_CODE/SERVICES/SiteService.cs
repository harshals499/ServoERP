using System;
using System.Collections.Generic;
using System.Linq;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Models.Validation;
using HVAC_Pro_Desktop.Services.Audit;
using HVAC_Pro_Desktop.Services.Validation;

namespace HVAC_Pro_Desktop.Services
{
    public class SiteService
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(45);
        private readonly SiteRepository _repo = new SiteRepository();
        private readonly BusinessRuleEngine _businessRules = new BusinessRuleEngine();
        private readonly GlobalValidationEngine _validation = new GlobalValidationEngine();
        private readonly AuditTrailService _audit = new AuditTrailService();

        public List<ClientSite> GetByClientId(int clientId)
        {
            return ApplyDisplayNames(GetAll().FindAll(site => site.ClientID == clientId));
        }

        public List<ClientSite> GetAll()
        {
            return ApplyDisplayNames(AppDataCache.GetOrCreate("sites:all", CacheTtl, _repo.GetAll));
        }

        public ClientSite GetById(int siteId)
        {
            foreach (ClientSite site in GetAll())
                if (site.SiteID == siteId)
                    return site;
            return null;
        }

        public int Create(ClientSite s)
        {
            SessionManager.DemandPermission("Clients", "Create");
            ValidateSiteForSave(s);
            int id = _repo.Create(s);
            AppDataCache.RemovePrefix("sites:");
            _audit.Record("CREATE", "Sites", id, "Site saved with data-quality validation");
            return id;
        }

        public void Update(ClientSite s)
        {
            SessionManager.DemandPermission("Clients", "Edit");
            ValidateSiteForSave(s);
            _repo.Update(s);
            AppDataCache.RemovePrefix("sites:");
            _audit.Record("EDIT", "Sites", s.SiteID, "Site saved with data-quality validation");
        }

        public void UpdateGeoCoordinates(int siteId, double? latitude, double? longitude, string geocodeAddress, string geocodeStatus)
        {
            SessionManager.DemandPermission("Clients", "Edit");
            _repo.UpdateGeoCoordinates(siteId, latitude, longitude, geocodeAddress, geocodeStatus);
            AppDataCache.RemovePrefix("sites:");
            _audit.Record("EDIT", "Sites", siteId, "Site geocode updated. Status: " + (geocodeStatus ?? string.Empty));
        }

        public void Delete(int siteId)
        {
            SessionManager.DemandPermission("Clients", "Delete");
            _repo.Delete(siteId);
            AppDataCache.RemovePrefix("sites:");
            _audit.Record("DELETE", "Sites", siteId, "Site deleted");
        }

        public static List<ClientSite> ApplyDisplayNames(IEnumerable<ClientSite> sites)
        {
            List<ClientSite> list = (sites ?? Enumerable.Empty<ClientSite>())
                .Where(site => site != null)
                .GroupBy(site => BuildDuplicateKey(site), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderBy(site => site.SiteID).First())
                .OrderBy(site => site.SiteName)
                .ThenBy(site => site.City)
                .ThenBy(site => site.SiteID)
                .ToList();

            foreach (IGrouping<string, ClientSite> group in list.GroupBy(site => Normalize(site.SiteName), StringComparer.OrdinalIgnoreCase))
            {
                bool duplicateName = group.Count() > 1;
                foreach (ClientSite site in group)
                {
                    string name = string.IsNullOrWhiteSpace(site.SiteName) ? "Site " + site.SiteID : site.SiteName.Trim();
                    if (!duplicateName)
                    {
                        site.DisplayName = name;
                        continue;
                    }

                    string qualifier = FirstNonEmpty(site.City, site.Address, site.GeocodeAddress, "Site " + site.SiteID);
                    site.DisplayName = name + " - " + qualifier;
                }
            }

            return list;
        }

        public static string GetDisplayName(ClientSite site)
        {
            if (site == null)
                return string.Empty;
            return string.IsNullOrWhiteSpace(site.DisplayName) ? (site.SiteName ?? string.Empty) : site.DisplayName;
        }

        private static string BuildDuplicateKey(ClientSite site)
        {
            return site.ClientID + "|" + Normalize(site.SiteName) + "|" + Normalize(site.City) + "|" + Normalize(site.Address);
        }

        private static string Normalize(string value)
        {
            return string.Join(" ", (value ?? string.Empty).Trim().Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            return string.Empty;
        }

        private void ValidateSiteForSave(ClientSite site)
        {
            ValidationResult result = _businessRules.ValidateSite(site);
            _validation.EnsureValid(result, "Site validation failed");
        }
    }
}

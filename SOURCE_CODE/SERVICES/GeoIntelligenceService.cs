using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public class GeoIntelligenceService
    {
        private readonly SiteService _siteSvc = new SiteService();
        private readonly ClientService _clientSvc = new ClientService();
        private readonly ContractService _contractSvc = new ContractService();
        private readonly InvoiceService _invoiceSvc = new InvoiceService();
        private readonly JobService _jobSvc = new JobService();
        private readonly SLAService _slaSvc = new SLAService();
        private readonly SettingsService _settingsSvc = new SettingsService();
        private readonly EmployeeService _employeeSvc = new EmployeeService();
        private readonly InventoryService _inventorySvc = new InventoryService();

        public List<SiteMapDto> GetSiteMapData(DateTime dateFrom, DateTime dateTo, string contractType)
        {
            return GetSiteMapData(new GeoFilterOptions
            {
                DateFrom = dateFrom,
                DateTo = dateTo,
                ContractType = contractType
            });
        }

        public List<SiteMapDto> GetSiteMapData(GeoFilterOptions filter)
        {
            try
            {
                GeoFilterOptions options = NormalizeFilter(filter);
                DateTime from = options.DateFrom.Date;
                DateTime to = options.DateTo.Date.AddDays(1).AddTicks(-1);

                List<ClientSite> sites = _siteSvc.GetAll() ?? new List<ClientSite>();
                List<B2BClient> clients = _clientSvc.GetAllClients() ?? new List<B2BClient>();
                List<AMCContract> contracts = _contractSvc.GetAllContracts() ?? new List<AMCContract>();
                List<Invoice> invoices = _invoiceSvc.GetAllInvoices() ?? new List<Invoice>();
                List<Job> jobs = _jobSvc.GetAll() ?? new List<Job>();
                List<SLALog> slaLogs = _slaSvc.GetAll() ?? new List<SLALog>();

                HashSet<int> technicianSiteIds = ResolveTechnicianSiteIds(options.TechnicianId, jobs);
                Dictionary<int, List<ClientSite>> allSitesByClient = sites
                    .GroupBy(s => s.ClientID)
                    .ToDictionary(g => g.Key, g => g.ToList());
                Dictionary<int, List<ClientSite>> mappedSitesByClient = sites
                    .Where(s => s.GeoLatitude.HasValue && s.GeoLongitude.HasValue)
                    .GroupBy(s => s.ClientID)
                    .ToDictionary(g => g.Key, g => g.ToList());

                List<SiteMapDto> result = new List<SiteMapDto>();
                foreach (B2BClient client in clients.Where(c => c.IsActive))
                {
                    List<ClientSite> clientSites = allSitesByClient.ContainsKey(client.ClientID)
                        ? allSitesByClient[client.ClientID]
                        : new List<ClientSite>();
                    List<ClientSite> mappedClientSites = mappedSitesByClient.ContainsKey(client.ClientID)
                        ? mappedSitesByClient[client.ClientID]
                        : new List<ClientSite>();
                    ClientSite representativeSite = ResolveRepresentativeSite(mappedClientSites, jobs, from, to);

                    double? latitude = client.GeoLatitude ?? (representativeSite == null ? (double?)null : representativeSite.GeoLatitude);
                    double? longitude = client.GeoLongitude ?? (representativeSite == null ? (double?)null : representativeSite.GeoLongitude);
                    if (!latitude.HasValue || !longitude.HasValue)
                        continue;

                    HashSet<int> clientSiteIds = new HashSet<int>(clientSites.Select(s => s.SiteID));
                    if (options.TechnicianId.HasValue && options.TechnicianId.Value > 0 && !clientSiteIds.Any(technicianSiteIds.Contains))
                        continue;

                    List<AMCContract> clientContracts = contracts.Where(c => c.ClientID == client.ClientID).OrderByDescending(c => c.EndDate).ToList();
                    AMCContract activeContract = ResolveActiveContract(clientContracts);
                    if (!MatchesContractFilter(options.ContractType, activeContract, clientContracts))
                        continue;

                    List<Job> clientJobs = jobs.Where(j => j.ClientID == client.ClientID || clientSiteIds.Contains(j.SiteID)).ToList();
                    List<Job> filteredJobs = clientJobs.Where(j => j.ScheduledDate >= from && j.ScheduledDate <= to).ToList();
                    List<Invoice> clientInvoices = invoices.Where(i => i.ClientID == client.ClientID || clientSiteIds.Contains(i.SiteID)).ToList();
                    List<int> contractIds = clientContracts.Select(c => c.ContractID).ToList();
                    bool slaBreached = slaLogs.Any(log => contractIds.Contains(log.ContractID) && !log.Compliant && log.LogDate >= DateTime.Today.AddDays(-90));

                    int openCalls = filteredJobs.Count(j => IsOpenCall(j.Status));
                    int overdueCount = clientInvoices.Count(IsOverdueInvoice);
                    DateTime? lastPmDate = ResolveLastPmDate(clientJobs, clientInvoices);
                    DateTime? nextPmDueDate = ResolveNextPmDueDate(lastPmDate, activeContract, clientInvoices);
                    string pinColour = ResolvePinColour(activeContract, overdueCount, slaBreached);

                    if (options.UrgentOnly && !string.Equals(pinColour, "red", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (options.OpenCallsOnly && openCalls <= 0)
                        continue;
                    if (options.RenewalRiskOnly && (activeContract == null || activeContract.EndDate.Date > DateTime.Today.AddDays(60)))
                        continue;
                    if (options.NoContractOnly && activeContract != null)
                        continue;

                    result.Add(new SiteMapDto
                    {
                        SiteId = representativeSite == null ? 0 : representativeSite.SiteID,
                        SiteName = string.IsNullOrWhiteSpace(client.CompanyName) ? "Client #" + client.ClientID : client.CompanyName.Trim(),
                        ClientName = BuildClientMapSubtitle(client, clientSites.Count),
                        Lat = latitude.Value,
                        Lon = longitude.Value,
                        ContractType = activeContract != null ? NormalizeContractLabel(activeContract.ContractType) : "No Contract",
                        ContractExpiry = activeContract != null ? (DateTime?)activeContract.EndDate.Date : null,
                        OpenCallsCount = openCalls,
                        OverdueInvoiceCount = overdueCount,
                        LastPMDate = lastPmDate,
                        NextPMDueDate = nextPmDueDate,
                        PinColour = pinColour,
                        PopupHtml = BuildClientPopupHtml(client, representativeSite, activeContract, openCalls, nextPmDueDate, pinColour, clientSites.Count)
                    });
                }

                return result.OrderByDescending(d => d.OpenCallsCount).ThenBy(d => d.SiteName).ToList();
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("GeoIntelligenceService.GetSiteMapData", ex);
                throw;
            }
        }

        public ZoneInsightDto GetZoneInsights(int siteId)
        {
            try
            {
                ClientSite site = _siteSvc.GetById(siteId);
                if (site == null)
                    return null;

                string clientName = _clientSvc.GetClientById(site.ClientID)?.CompanyName ?? ("Client #" + site.ClientID);
                List<AMCContract> contracts = (_contractSvc.GetAllContracts() ?? new List<AMCContract>()).Where(c => c.SiteID == siteId).OrderByDescending(c => c.EndDate).ToList();
                AMCContract activeContract = ResolveActiveContract(contracts);
                List<Invoice> invoices = (_invoiceSvc.GetAllInvoices() ?? new List<Invoice>()).Where(i => i.SiteID == siteId).ToList();
                List<Job> jobs = (_jobSvc.GetAll() ?? new List<Job>()).Where(j => j.SiteID == siteId).ToList();

                int openCalls = jobs.Count(j => IsOpenCall(j.Status));
                decimal overdueAmount = invoices.Where(IsOverdueInvoice).Sum(i => i.BalanceDue);
                DateTime? lastPmDate = ResolveLastPmDate(jobs, invoices);
                DateTime? nextPmDueDate = ResolveNextPmDueDate(lastPmDate, activeContract, invoices);
                string topIssue = ResolveTopIssue(jobs.Where(j => j.ScheduledDate >= DateTime.Today.AddDays(-90)).ToList());
                IndiaCompanySettings settings = _settingsSvc.GetIndiaCompanySettings();
                int breakdownRate = ResolveBreakdownRatePercent(jobs);
                int travelMinutes = ResolveTravelMinutes(settings, site);
                int extraTechs = ResolveAdditionalTechnicians(openCalls, breakdownRate);
                string healthColour = ResolvePinColour(activeContract, overdueAmount > 0m ? 1 : 0, false);

                return new ZoneInsightDto
                {
                    SiteId = site.SiteID,
                    SiteName = string.IsNullOrWhiteSpace(site.SiteName) ? "Site #" + site.SiteID : site.SiteName.Trim(),
                    ClientName = clientName,
                    ContractType = activeContract != null ? NormalizeContractLabel(activeContract.ContractType) : "No Contract",
                    ContractExpiry = activeContract != null ? (DateTime?)activeContract.EndDate.Date : null,
                    ContractValue = activeContract != null ? ResolveContractValue(activeContract) : 0m,
                    OpenCallsCount = openCalls,
                    LastPMDate = lastPmDate,
                    NextPMDueDate = nextPmDueDate,
                    OverdueInvoicesAmount = overdueAmount,
                    TopIssue = topIssue,
                    RecommendedAction = ResolveRecommendedAction(activeContract, overdueAmount, nextPmDueDate, openCalls),
                    DemandScoreLabel = ResolveDemandScoreLabel(openCalls, overdueAmount, activeContract),
                    BreakdownRatePercent = breakdownRate,
                    AvgTravelTimeMinutes = travelMinutes,
                    AdditionalTechniciansRecommended = extraTechs,
                    HealthColour = healthColour
                };
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("GeoIntelligenceService.GetZoneInsights", ex);
                throw;
            }
        }

        public SummaryCardsDto GetSummaryCards()
        {
            return GetSummaryCards(null);
        }

        public SummaryCardsDto GetSummaryCards(GeoFilterOptions filter)
        {
            try
            {
                GeoFilterOptions options = NormalizeFilter(filter);
                List<SiteMapDto> visibleSites = GetSiteMapData(options);
                List<Job> jobs = _jobSvc.GetAll() ?? new List<Job>();
                IndiaCompanySettings settings = _settingsSvc.GetIndiaCompanySettings();
                SummaryCardsDto summary = new SummaryCardsDto { MappedSitesCount = visibleSites.Count };

                foreach (SiteMapDto site in visibleSites
                    .OrderByDescending(s => s.OpenCallsCount)
                    .ThenBy(s => s.SiteName)
                    .Take(3))
                {
                    summary.TopDemandAreas.Add(new GeoSummaryItemDto
                    {
                        SiteId = site.SiteId,
                        Title = site.SiteName,
                        Detail = site.OpenCallsCount.ToString(CultureInfo.InvariantCulture) + " open calls"
                    });
                }

                int windowDays = Math.Max(1, (options.DateTo.Date - options.DateFrom.Date).Days + 1);
                DateTime currentFrom = options.DateFrom.Date;
                DateTime currentTo = options.DateTo.Date.AddDays(1).AddTicks(-1);
                DateTime previousFrom = options.DateFrom.Date.AddDays(-windowDays);
                DateTime previousTo = options.DateFrom.Date.AddTicks(-1);

                foreach (SiteMapDto site in visibleSites)
                {
                    int currentCount = jobs.Count(j => j.SiteID == site.SiteId && j.ScheduledDate >= currentFrom && j.ScheduledDate <= currentTo);
                    int previousCount = jobs.Count(j => j.SiteID == site.SiteId && j.ScheduledDate >= previousFrom && j.ScheduledDate <= previousTo);
                    if (previousCount <= 0 || currentCount <= previousCount)
                        continue;

                    double growth = ((double)(currentCount - previousCount) / previousCount) * 100d;
                    if (growth > 20d)
                    {
                        summary.RisingBreakdowns.Add(new GeoSummaryItemDto
                        {
                            SiteId = site.SiteId,
                            Title = site.SiteName,
                            Detail = "+" + growth.ToString("0", CultureInfo.InvariantCulture) + "% cases"
                        });
                    }
                }

                if (settings.OfficeLatitude.HasValue && settings.OfficeLongitude.HasValue)
                {
                    SiteMapDto farthest = null;
                    double farthestKm = 0d;
                    foreach (SiteMapDto site in visibleSites)
                    {
                        double km = CalculateDistanceKm(settings.OfficeLatitude.Value, settings.OfficeLongitude.Value, site.Lat, site.Lon);
                        if (km > 30d && km > farthestKm)
                        {
                            farthest = site;
                            farthestKm = km;
                        }
                    }

                    if (farthest != null)
                    {
                        summary.TravelAlertTitle = farthest.SiteName;
                        summary.TravelAlertDetail = farthestKm.ToString("0.0", CultureInfo.InvariantCulture) + " km from office";
                    }
                    else
                    {
                        summary.TravelAlertTitle = "Live";
                        summary.TravelAlertDetail = "Routes are within the 30 km travel threshold.";
                    }
                }
                else
                {
                    summary.TravelAlertTitle = "Set office coordinates";
                    summary.TravelAlertDetail = "Set office coordinates in Settings to enable distance alerts.";
                    summary.TravelNeedsOfficeCoordinates = true;
                }

                summary.UpsellOpportunity = visibleSites.Count(site => string.Equals(site.ContractType, "No Contract", StringComparison.OrdinalIgnoreCase));
                return summary;
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("GeoIntelligenceService.GetSummaryCards", ex);
                throw;
            }
        }

        public List<GeoNotificationDto> GetNotificationItems()
        {
            try
            {
                List<GeoNotificationDto> items = new List<GeoNotificationDto>();

                foreach (Invoice invoice in (_invoiceSvc.GetAllInvoices() ?? new List<Invoice>())
                    .Where(IsOverdueInvoice)
                    .OrderBy(i => i.DueDate)
                    .Take(3))
                {
                    items.Add(new GeoNotificationDto
                    {
                        Type = "Overdue Invoice",
                        Message = (invoice.InvoiceNumber ?? ("Invoice #" + invoice.InvoiceID)) + " overdue for " + FormatCurrency(invoice.BalanceDue)
                    });
                }

                foreach (AMCContract contract in (_contractSvc.GetExpiringContractsInNextDays(30) ?? new List<AMCContract>())
                    .OrderBy(c => c.EndDate)
                    .Take(2))
                {
                    items.Add(new GeoNotificationDto
                    {
                        Type = "Renewal Risk",
                        Message = NormalizeContractLabel(contract.ContractType) + " expires on " + contract.EndDate.ToString("dd MMM yyyy")
                    });
                }

                if (items.Count < 5)
                {
                    int lowStock = _inventorySvc.GetLowStockCount();
                    if (lowStock > 0)
                    {
                        items.Add(new GeoNotificationDto
                        {
                            Type = "Inventory",
                            Message = lowStock.ToString(CultureInfo.InvariantCulture) + " stock item(s) below reorder level"
                        });
                    }
                }

                return items.Take(5).ToList();
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("GeoIntelligenceService.GetNotificationItems", ex);
                return new List<GeoNotificationDto>();
            }
        }

        public List<Employee> GetActiveTechnicians()
        {
            try
            {
                return _employeeSvc.GetActiveTechnicians() ?? new List<Employee>();
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("GeoIntelligenceService.GetActiveTechnicians", ex);
                return new List<Employee>();
            }
        }

        public int GetMissingCoordinatesCount()
        {
            try
            {
                return (_siteSvc.GetAll() ?? new List<ClientSite>())
                    .Count(site => !site.GeoLatitude.HasValue || !site.GeoLongitude.HasValue);
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("GeoIntelligenceService.GetMissingCoordinatesCount", ex);
                return 0;
            }
        }

        private static GeoFilterOptions NormalizeFilter(GeoFilterOptions filter)
        {
            GeoFilterOptions normalized = filter ?? new GeoFilterOptions();
            if (normalized.DateFrom == default(DateTime))
                normalized.DateFrom = DateTime.Today.AddDays(-29);
            if (normalized.DateTo == default(DateTime))
                normalized.DateTo = DateTime.Today;
            if (normalized.DateTo < normalized.DateFrom)
                normalized.DateTo = normalized.DateFrom;
            normalized.RangeKey = string.IsNullOrWhiteSpace(normalized.RangeKey) ? "last30" : normalized.RangeKey.Trim();
            normalized.ContractType = NormalizeContractFilter(normalized.ContractType);
            return normalized;
        }

        private static string NormalizeContractFilter(string contractType)
        {
            string value = (contractType ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
                return "All";
            if (string.Equals(value, "OMC", StringComparison.OrdinalIgnoreCase))
                return "O&M";
            return value;
        }

        private static string NormalizeContractLabel(string contractType)
        {
            string value = (contractType ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
                return "AMC";
            if (value.IndexOf("O&M", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("OMC", StringComparison.OrdinalIgnoreCase) >= 0)
                return "OMC";
            if (value.IndexOf("AMC", StringComparison.OrdinalIgnoreCase) >= 0)
                return "AMC";
            if (value.IndexOf("Warranty", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Warranty";
            return value;
        }

        private static HashSet<int> ResolveTechnicianSiteIds(int? technicianId, List<Job> jobs)
        {
            if (!technicianId.HasValue || technicianId.Value <= 0)
                return new HashSet<int>();

            return new HashSet<int>((jobs ?? new List<Job>())
                .Where(j => j.AssignedEmployeeID.HasValue && j.AssignedEmployeeID.Value == technicianId.Value)
                .Select(j => j.SiteID));
        }

        private static bool MatchesContractFilter(string filter, AMCContract activeContract, List<AMCContract> siteContracts)
        {
            if (string.Equals(filter, "All", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(filter, "No Contract", StringComparison.OrdinalIgnoreCase))
                return activeContract == null;

            if (activeContract != null && MatchesContractType(activeContract.ContractType, filter))
                return true;

            return activeContract == null && siteContracts.Any(c => MatchesContractType(c.ContractType, filter));
        }

        private static bool MatchesContractType(string contractType, string filter)
        {
            string normalizedContract = NormalizeContractFilter(contractType);
            string normalizedFilter = NormalizeContractFilter(filter);

            if (string.Equals(normalizedFilter, "AMC", StringComparison.OrdinalIgnoreCase))
                return normalizedContract.IndexOf("AMC", StringComparison.OrdinalIgnoreCase) >= 0;

            return string.Equals(normalizedContract, normalizedFilter, StringComparison.OrdinalIgnoreCase);
        }

        private static AMCContract ResolveActiveContract(List<AMCContract> contracts)
        {
            DateTime today = DateTime.Today;
            return (contracts ?? new List<AMCContract>())
                .Where(c => string.Equals(c.ContractStatus, "Active", StringComparison.OrdinalIgnoreCase) && c.EndDate.Date >= today)
                .OrderBy(c => c.EndDate)
                .FirstOrDefault();
        }

        private static bool IsOpenCall(string status)
        {
            string value = (status ?? string.Empty).Trim();
            return !string.Equals(value, "Completed", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(value, "Closed", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(value, "Cancelled", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(value, "Resolved", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsOverdueInvoice(Invoice invoice)
        {
            if (invoice == null)
                return false;

            string status = (invoice.PaymentStatus ?? string.Empty).Trim();
            bool unpaid = string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "Partial", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "Overdue", StringComparison.OrdinalIgnoreCase);
            return unpaid && invoice.DueDate.Date < DateTime.Today;
        }

        private static string ResolvePinColour(AMCContract activeContract, int overdueInvoiceCount, bool slaBreached)
        {
            if (overdueInvoiceCount > 0 || slaBreached)
                return "red";

            if (activeContract == null)
                return "grey";

            if (activeContract.EndDate.Date <= DateTime.Today.AddDays(60))
                return "amber";

            return "green";
        }

        private static DateTime? ResolveLastPmDate(List<Job> jobs, List<Invoice> invoices)
        {
            List<DateTime> dates = new List<DateTime>();
            dates.AddRange((jobs ?? new List<Job>())
                .Where(j => IsPmJob(j.Title, j.Description) && j.CompletedDate.HasValue)
                .Select(j => j.CompletedDate.Value.Date));
            dates.AddRange((invoices ?? new List<Invoice>())
                .Where(i => i.PreventiveVisitDate.HasValue)
                .Select(i => i.PreventiveVisitDate.Value.Date));

            return dates.Count == 0 ? (DateTime?)null : dates.Max();
        }

        private static DateTime? ResolveNextPmDueDate(DateTime? lastPmDate, AMCContract activeContract, List<Invoice> invoices)
        {
            DateTime? invoiceDate = (invoices ?? new List<Invoice>())
                .Where(i => i.NextServiceDueDate.HasValue)
                .OrderBy(i => i.NextServiceDueDate.Value)
                .Select(i => (DateTime?)i.NextServiceDueDate.Value.Date)
                .FirstOrDefault();
            if (invoiceDate.HasValue)
                return invoiceDate;

            if (!lastPmDate.HasValue || activeContract == null)
                return null;

            int intervalDays = 90;
            string frequency = activeContract.MaintenanceFrequency ?? string.Empty;
            if (frequency.IndexOf("Month", StringComparison.OrdinalIgnoreCase) >= 0)
                intervalDays = 30;
            else if (frequency.IndexOf("Quarter", StringComparison.OrdinalIgnoreCase) >= 0)
                intervalDays = 90;
            else if (frequency.IndexOf("Half", StringComparison.OrdinalIgnoreCase) >= 0)
                intervalDays = 180;
            else if (frequency.IndexOf("Year", StringComparison.OrdinalIgnoreCase) >= 0)
                intervalDays = 365;

            return lastPmDate.Value.AddDays(intervalDays).Date;
        }

        private static bool IsPmJob(string title, string description)
        {
            string text = ((title ?? string.Empty) + " " + (description ?? string.Empty)).ToLowerInvariant();
            return text.Contains("preventive") || text.Contains("pm ") || text.Contains(" pm") || text.Contains("maintenance");
        }

        private static string ResolveTopIssue(List<Job> jobs)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (Job job in jobs ?? new List<Job>())
            {
                string key = InferBreakdownType(job.Title, job.Description);
                counts[key] = counts.ContainsKey(key) ? counts[key] + 1 : 1;
            }

            return counts.Count == 0
                ? "No repeated issue trend"
                : counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key;
        }

        private static string InferBreakdownType(string title, string description)
        {
            string text = ((title ?? string.Empty) + " " + (description ?? string.Empty)).ToLowerInvariant();
            if (text.Contains("chiller")) return "Chiller breakdown";
            if (text.Contains("ahu")) return "AHU fault";
            if (text.Contains("compressor")) return "Compressor issue";
            if (text.Contains("cooling tower")) return "Cooling tower issue";
            if (text.Contains("electrical")) return "Electrical fault";
            if (text.Contains("refrigerant") || text.Contains("gas leak")) return "Refrigerant leak";
            if (text.Contains("sensor") || text.Contains("control")) return "Controls / sensor issue";
            return "General HVAC breakdown";
        }

        private static string ResolveRecommendedAction(AMCContract activeContract, decimal overdueAmount, DateTime? nextPmDueDate, int openCalls)
        {
            if (overdueAmount > 0m)
                return "Follow up on " + FormatCurrency(overdueAmount) + " overdue";

            if (activeContract != null && activeContract.EndDate.Date <= DateTime.Today.AddDays(30))
                return "Initiate AMC renewal";

            if (nextPmDueDate.HasValue && nextPmDueDate.Value.Date <= DateTime.Today.AddDays(7))
                return "Schedule PM visit";

            if (openCalls > 2)
                return "Assign additional technician";

            return "Site is healthy";
        }

        private static string ResolveDemandScoreLabel(int openCalls, decimal overdueAmount, AMCContract activeContract)
        {
            if (overdueAmount > 0m || openCalls >= 4)
                return "High";

            if (activeContract != null && activeContract.EndDate.Date <= DateTime.Today.AddDays(45))
                return "Medium";

            if (openCalls >= 2)
                return "Medium";

            return "Stable";
        }

        private static int ResolveBreakdownRatePercent(List<Job> jobs)
        {
            List<Job> recent = (jobs ?? new List<Job>())
                .Where(j => j.ScheduledDate >= DateTime.Today.AddDays(-90))
                .ToList();
            if (recent.Count == 0)
                return 8;

            int breakdowns = recent.Count(j => !IsPmJob(j.Title, j.Description));
            return Math.Max(8, Math.Min(100, (int)Math.Round((double)breakdowns / recent.Count * 100d)));
        }

        private static int ResolveTravelMinutes(IndiaCompanySettings settings, ClientSite site)
        {
            if (settings == null ||
                !settings.OfficeLatitude.HasValue ||
                !settings.OfficeLongitude.HasValue ||
                site == null ||
                !site.GeoLatitude.HasValue ||
                !site.GeoLongitude.HasValue)
            {
                return 35;
            }

            double km = CalculateDistanceKm(
                settings.OfficeLatitude.Value,
                settings.OfficeLongitude.Value,
                site.GeoLatitude.Value,
                site.GeoLongitude.Value);

            return Math.Max(12, (int)Math.Round(km * 2.2d));
        }

        private static int ResolveAdditionalTechnicians(int openCalls, int breakdownRatePercent)
        {
            if (openCalls >= 5 || breakdownRatePercent >= 45)
                return 3;
            if (openCalls >= 3 || breakdownRatePercent >= 28)
                return 2;
            if (openCalls >= 2)
                return 1;
            return 0;
        }

        private static decimal ResolveContractValue(AMCContract contract)
        {
            if (contract == null)
                return 0m;

            return contract.AnnualValue > 0m ? contract.AnnualValue : contract.MonthlyValue * 12m;
        }

        private static ClientSite ResolveRepresentativeSite(List<ClientSite> mappedSites, List<Job> jobs, DateTime from, DateTime to)
        {
            if (mappedSites == null || mappedSites.Count == 0)
                return null;

            Dictionary<int, int> openCallCounts = (jobs ?? new List<Job>())
                .Where(j => j.ScheduledDate >= from && j.ScheduledDate <= to && IsOpenCall(j.Status))
                .GroupBy(j => j.SiteID)
                .ToDictionary(g => g.Key, g => g.Count());

            return mappedSites
                .OrderByDescending(s => openCallCounts.ContainsKey(s.SiteID) ? openCallCounts[s.SiteID] : 0)
                .ThenBy(s => s.SiteName ?? string.Empty)
                .FirstOrDefault();
        }

        private static string BuildClientMapSubtitle(B2BClient client, int siteCount)
        {
            List<string> parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(client.IndustryType))
                parts.Add(client.IndustryType.Trim());
            if (!string.IsNullOrWhiteSpace(client.City))
                parts.Add(client.City.Trim());
            parts.Add(siteCount == 1 ? "1 site" : siteCount.ToString(CultureInfo.InvariantCulture) + " sites");
            return string.Join(" | ", parts);
        }

        private static string BuildClientPopupHtml(B2BClient client, ClientSite representativeSite, AMCContract activeContract, int openCalls, DateTime? nextPmDueDate, string pinColour, int siteCount)
        {
            string badgeText = activeContract == null ? "No Contract" : NormalizeContractLabel(activeContract.ContractType ?? "Contract");
            string badgeColor = ResolveBadgeHex(pinColour);
            string nextPmText = nextPmDueDate.HasValue ? nextPmDueDate.Value.ToString("dd MMM yyyy") : "Not scheduled";
            string zoneText = openCalls >= 3 ? "High Demand Client" : (openCalls >= 1 ? "Active Client" : "Mapped Client");
            string siteText = representativeSite == null || string.IsNullOrWhiteSpace(representativeSite.SiteName)
                ? (siteCount == 1 ? "1 mapped site" : siteCount.ToString(CultureInfo.InvariantCulture) + " mapped sites")
                : representativeSite.SiteName.Trim();
            string viewButton = representativeSite == null
                ? string.Empty
                : "<button style='margin-top:14px;width:100%;background:#2f74e5;color:#fff;border:none;border-radius:10px;padding:10px 12px;font-size:12px;font-weight:700;cursor:pointer;' onclick='viewSite(" + representativeSite.SiteID.ToString(CultureInfo.InvariantCulture) + ");return false;'>View Client Site</button>";

            return
                "<div style='font-family:Segoe UI,sans-serif;min-width:260px;padding:18px 18px 16px;border-radius:18px;background:#fff;color:#17315f;border:1px solid #d9e2ef;box-shadow:0 14px 32px rgba(23,49,95,.16);'>" +
                "<div style='font-size:15px;font-weight:700;line-height:1.2;'>" + Html(client.CompanyName) + "</div>" +
                "<div style='font-size:12px;color:#53719b;margin-top:3px;'>" + Html(BuildClientMapSubtitle(client, siteCount)) + "</div>" +
                "<div style='font-size:11px;color:#17315f;margin-top:10px;font-weight:700;letter-spacing:.2px;'>" + Html(zoneText) + "</div>" +
                "<div style='display:inline-block;background:" + badgeColor + ";color:#fff;border-radius:999px;padding:4px 10px;font-size:11px;font-weight:700;margin-top:10px;'>" + Html(badgeText) + "</div>" +
                "<div style='font-size:12px;color:#17315f;margin-top:14px;'><span style='color:#53719b;'>Representative Site</span><span style='float:right;font-weight:700;'>" + Html(siteText) + "</span></div>" +
                "<div style='font-size:12px;color:#17315f;margin-top:6px;'><span style='color:#53719b;'>Open Calls</span><span style='float:right;font-weight:700;'>" + openCalls.ToString(CultureInfo.InvariantCulture) + "</span></div>" +
                "<div style='font-size:12px;color:#17315f;margin-top:6px;'><span style='color:#53719b;'>Next PM</span><span style='float:right;font-weight:700;'>" + Html(nextPmText) + "</span></div>" +
                viewButton +
                "</div>";
        }

        private static string ResolveBadgeHex(string pinColour)
        {
            switch ((pinColour ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "red":
                    return "#dc2626";
                case "amber":
                    return "#d97706";
                case "green":
                    return "#16a34a";
                default:
                    return "#6b7280";
            }
        }

        private static string Html(string value)
        {
            return (value ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }

        private static string FormatCurrency(decimal value)
        {
            return "\u20B9" + value.ToString("N0", CultureInfo.InvariantCulture);
        }

        private static double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double earthRadiusKm = 6371.0d;
            double dLat = DegreesToRadians(lat2 - lat1);
            double dLon = DegreesToRadians(lon2 - lon1);
            double a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return earthRadiusKm * c;
        }

        private static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180d;
        }
    }
}

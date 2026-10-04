using System;
using System.Collections.Generic;
using System.Linq;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    /// <summary>Builds a deterministic AMC calendar for review; it never sends reminders automatically.</summary>
    public sealed class AmcPreventivePlannerService
    {
        private readonly AmcPreventivePlannerRepository _repository = new AmcPreventivePlannerRepository();

        public AmcPreventivePlan BuildPlan(int dailyCapacity = 3)
        {
            List<AmcPlannerContractSnapshot> contracts = _repository.LoadContracts();
            DateTime min = contracts.Count == 0 ? DateTime.Today : contracts.Min(value => value.StartDate).Date;
            DateTime max = contracts.Count == 0 ? DateTime.Today.AddYears(1) : contracts.Max(value => value.EndDate).Date;
            Dictionary<DateTime, int> capacity = _repository.LoadCapacity(min, max);
            return BuildPlan(contracts, capacity, DateTime.Today, dailyCapacity);
        }

        public AmcPlannerApplyResult Confirm(AmcPreventivePlan plan)
        {
            if (plan == null || (plan.ProposedVisitCount == 0 && plan.ReminderDrafts.Count == 0))
                throw new InvalidOperationException("There are no proposed AMC visits or reminder drafts to save.");
            AmcPlannerApplyResult result = _repository.Apply(plan);
            AppDataCache.RemovePrefix("amc:");
            DashboardRefreshService.NotifyChanged("AMC");
            return result;
        }

        public static AmcPreventivePlan BuildPlan(
            IEnumerable<AmcPlannerContractSnapshot> contractSource,
            IDictionary<DateTime, int> existingCapacity,
            DateTime today,
            int dailyCapacity)
        {
            int safeCapacity = Math.Max(1, dailyCapacity);
            var loads = (existingCapacity ?? new Dictionary<DateTime, int>())
                .GroupBy(value => value.Key.Date)
                .ToDictionary(group => group.Key, group => group.Sum(value => value.Value));
            List<AmcPlannerContractSnapshot> contracts = (contractSource ?? Enumerable.Empty<AmcPlannerContractSnapshot>())
                .Where(value => value != null && value.ContractId > 0 && value.EndDate.Date >= today.Date)
                .OrderBy(value => value.EndDate)
                .ThenBy(value => value.ContractId)
                .ToList();
            var plan = new AmcPreventivePlan
            {
                GeneratedAt = DateTime.Now,
                DailyCapacity = safeCapacity,
                ActiveContractCount = contracts.Count
            };

            foreach (AmcPlannerContractSnapshot contract in contracts)
            {
                DateTime start = contract.StartDate.Date;
                DateTime end = contract.EndDate.Date;
                if (end <= start) continue;
                int years = Math.Max(1, (int)Math.Ceiling((end - start).TotalDays / 365.25d));
                int required = Math.Min(60, Math.Max(1, contract.VisitsPerYear) * years);
                Dictionary<int, AmcPlannerVisitSnapshot> existingByNumber = (contract.Visits ?? new List<AmcPlannerVisitSnapshot>())
                    .Where(value => value.VisitNumber > 0)
                    .GroupBy(value => value.VisitNumber)
                    .ToDictionary(group => group.Key, group => group.OrderBy(value => value.VisitId).First());

                for (int visitNumber = 1; visitNumber <= required; visitNumber++)
                {
                    DateTime target = start.AddDays(Math.Round((end - start).TotalDays * ((visitNumber - 0.5d) / required))).Date;
                    AmcPlannerVisitSnapshot existing;
                    if (existingByNumber.TryGetValue(visitNumber, out existing))
                    {
                        bool missed = IsOpen(existing.Status) && !existing.CompletedDate.HasValue && existing.ScheduledDate.Date < today.Date;
                        plan.Obligations.Add(new AmcPreventiveObligation
                        {
                            ContractId = contract.ContractId,
                            AmcNumber = First(contract.AmcNumber, "AMC #" + contract.ContractId),
                            ClientName = contract.ClientName,
                            SiteName = contract.SiteName,
                            VisitNumber = visitNumber,
                            TargetDate = target,
                            PlannedDate = existing.ScheduledDate.Date,
                            PlannedLoad = Load(loads, existing.ScheduledDate),
                            Status = missed ? "Missed obligation" : First(existing.Status, "Scheduled"),
                            Action = missed ? "Dispatcher follow-up required" : "Existing visit",
                            CapacityNote = "Already scheduled",
                            IsMissed = missed,
                            ExistingVisitId = existing.VisitId
                        });
                        if (missed) plan.MissedObligationCount++;
                        AddReminderIfRelevant(plan, contract, visitNumber, existing.VisitId, existing.ScheduledDate, today);
                        continue;
                    }

                    bool pastDueTarget = target < today.Date;
                    DateTime earliest = pastDueTarget ? today.Date : target.AddDays(-14);
                    DateTime preferredLatest = pastDueTarget ? today.Date.AddDays(14) : target.AddDays(14);
                    DateTime latest = preferredLatest > end ? end : preferredLatest;
                    if (latest < earliest) latest = earliest <= end ? earliest : end;
                    DateTime planned = FindBestDate(earliest, latest, target, loads, safeCapacity);
                    int before = Load(loads, planned);
                    loads[planned] = before + 1;
                    bool conflict = before >= safeCapacity;
                    if (conflict) plan.CapacityConflictCount++;
                    var obligation = new AmcPreventiveObligation
                    {
                        ContractId = contract.ContractId,
                        AmcNumber = First(contract.AmcNumber, "AMC #" + contract.ContractId),
                        ClientName = contract.ClientName,
                        SiteName = contract.SiteName,
                        VisitNumber = visitNumber,
                        TargetDate = target,
                        PlannedDate = planned,
                        PlannedLoad = before + 1,
                        Status = "Proposed",
                        Action = "Create after confirmation",
                        CapacityNote = conflict ? "Capacity exceeded — review" : "Balanced within capacity",
                        IsProposed = true,
                        IsMissed = target < today.Date
                    };
                    plan.Obligations.Add(obligation);
                    plan.ProposedVisitCount++;
                    if (obligation.IsMissed) plan.MissedObligationCount++;
                    AddReminderIfRelevant(plan, contract, visitNumber, null, planned, today);
                }
            }
            return plan;
        }

        private static void AddReminderIfRelevant(AmcPreventivePlan plan, AmcPlannerContractSnapshot contract, int visitNumber, int? visitId, DateTime date, DateTime today)
        {
            if (date.Date < today.Date || date.Date > today.Date.AddDays(45)) return;
            string channel = !string.IsNullOrWhiteSpace(contract.Phone) ? "WhatsApp" : "Email";
            string recipient = channel == "WhatsApp" ? contract.Phone : contract.Email;
            plan.ReminderDrafts.Add(new AmcReminderDraft
            {
                ContractId = contract.ContractId,
                VisitId = visitId,
                VisitNumber = visitNumber,
                AmcNumber = First(contract.AmcNumber, "AMC #" + contract.ContractId),
                ClientName = contract.ClientName,
                SiteName = contract.SiteName,
                ScheduledDate = date.Date,
                Channel = channel,
                Recipient = recipient,
                DraftText = "Dear " + First(contract.ContactName, "Customer") + ", your preventive maintenance visit for " +
                            First(contract.SiteName, contract.ClientName) + " is planned for " + date.ToString("dd/MM/yyyy") +
                            ". Please reply with a convenient access window. Regards, ServoERP Service Team."
            });
        }

        private static DateTime FindBestDate(DateTime from, DateTime to, DateTime target, IDictionary<DateTime, int> loads, int capacity)
        {
            List<DateTime> candidates = new List<DateTime>();
            for (DateTime date = from.Date; date <= to.Date; date = date.AddDays(1))
                if (date.DayOfWeek != DayOfWeek.Sunday) candidates.Add(date);
            if (candidates.Count == 0) candidates.Add(from.Date);
            return candidates.OrderBy(date => Load(loads, date) >= capacity ? 1 : 0)
                .ThenBy(date => Load(loads, date))
                .ThenBy(date => Math.Abs((date - target.Date).TotalDays))
                .ThenBy(date => date)
                .First();
        }

        private static int Load(IDictionary<DateTime, int> loads, DateTime date)
        {
            int value;
            return loads != null && loads.TryGetValue(date.Date, out value) ? value : 0;
        }

        private static bool IsOpen(string status)
        {
            string value = (status ?? string.Empty).Trim();
            return !value.Equals("Completed", StringComparison.OrdinalIgnoreCase) &&
                   !value.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) &&
                   !value.Equals("Closed", StringComparison.OrdinalIgnoreCase);
        }

        private static string First(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.Tests
{
    public static class AmcPreventivePlannerSmokeTests
    {
        public static IEnumerable<string> RunAll()
        {
            DateTime today = new DateTime(2026, 10, 3);
            AmcPlannerContractSnapshot contract = BuildContract(today);
            var capacity = new Dictionary<DateTime, int>
            {
                [new DateTime(2026, 11, 16)] = 3,
                [new DateTime(2026, 11, 17)] = 1
            };
            AmcPreventivePlan plan = AmcPreventivePlannerService.BuildPlan(new[] { contract }, capacity, today, 3);
            Assert(plan.ProposedVisitCount == 3, "three missing visits should be proposed");
            Assert(plan.Obligations.Where(value => value.IsProposed).All(value => value.PlannedDate.DayOfWeek != DayOfWeek.Sunday), "Sunday should be avoided");
            Assert(plan.Obligations.Where(value => value.IsProposed).All(value => value.PlannedLoad <= 3), "available capacity should be respected");
            yield return "AMC planner creates a capacity-balanced preventive calendar.";

            AmcPreventiveObligation missed = plan.Obligations.Single(value => value.ExistingVisitId == 44);
            Assert(missed.IsMissed && missed.Status == "Missed obligation", "overdue open visit must be flagged");
            Assert(plan.MissedObligationCount >= 1, "missed KPI should be populated");
            yield return "AMC planner explicitly flags missed contractual obligations.";

            Assert(plan.ReminderDrafts.Count > 0, "upcoming visits should have reminder drafts");
            Assert(plan.ReminderDrafts.All(value => value.Status == "Draft"), "reminders must remain drafts");
            Assert(plan.ReminderDrafts.All(value => value.DraftText.Contains("planned for")), "draft text should include the planned visit");
            yield return "AMC planner prepares WhatsApp/email drafts without sending them.";
        }

        public static AmcPreventivePlan BuildPreviewPlan()
        {
            DateTime today = DateTime.Today;
            var contracts = new List<AmcPlannerContractSnapshot>
            {
                new AmcPlannerContractSnapshot
                {
                    ContractId = 101, AmcNumber = "AMC-2026-0101", ClientName = "Sahyadri Foods Pvt Ltd",
                    SiteName = "Chakan Cold Store", ContactName = "Ms Patil", Phone = "+91 98765 43210",
                    StartDate = today.AddMonths(-4), EndDate = today.AddMonths(8), VisitsPerYear = 4,
                    Visits = new List<AmcPlannerVisitSnapshot>
                    {
                        new AmcPlannerVisitSnapshot { VisitId = 501, ContractId = 101, VisitNumber = 1, ScheduledDate = today.AddDays(-18), Status = "Scheduled" }
                    }
                },
                new AmcPlannerContractSnapshot
                {
                    ContractId = 102, AmcNumber = "AMC-2026-0102", ClientName = "Pragati Hospital",
                    SiteName = "Main Building", ContactName = "Facilities Desk", Email = "facilities@example.in",
                    StartDate = today.AddMonths(-2), EndDate = today.AddMonths(10), VisitsPerYear = 3
                }
            };
            var capacity = new Dictionary<DateTime, int>
            {
                [today.AddDays(12)] = 2,
                [today.AddDays(13)] = 1
            };
            return AmcPreventivePlannerService.BuildPlan(contracts, capacity, today, 3);
        }

        private static AmcPlannerContractSnapshot BuildContract(DateTime today)
        {
            return new AmcPlannerContractSnapshot
            {
                ContractId = 7,
                AmcNumber = "AMC-TEST-007",
                ClientName = "Test Client",
                SiteName = "Plant 1",
                ContactName = "Service Coordinator",
                Phone = "+91 90000 00000",
                StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2026, 12, 31),
                VisitsPerYear = 4,
                Visits = new List<AmcPlannerVisitSnapshot>
                {
                    new AmcPlannerVisitSnapshot { VisitId = 44, ContractId = 7, VisitNumber = 1, ScheduledDate = today.AddDays(-30), Status = "Scheduled" }
                }
            };
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("AMC Preventive Planner smoke test failed: " + message);
        }
    }
}

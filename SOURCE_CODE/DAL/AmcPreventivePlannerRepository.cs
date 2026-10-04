using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.DAL
{
    /// <summary>Reads AMC planning inputs and persists only dispatcher-confirmed visits and reminder drafts.</summary>
    public sealed class AmcPreventivePlannerRepository
    {
        public List<AmcPlannerContractSnapshot> LoadContracts()
        {
            DbHelper.EnsureAMCSchema();
            using (SqlConnection connection = DatabaseConnectionFactory.CreateConnection())
            {
                DatabaseConnectionFactory.Open(connection, "AmcPreventivePlanner.LoadContracts");
                List<AmcPlannerContractSnapshot> contracts = connection.Query<AmcPlannerContractSnapshot>(@"
SELECT c.ContractID AS ContractId,
       c.AMCNumber AS AmcNumber,
       b.CompanyName AS ClientName,
       s.SiteName,
       b.PrimaryContact AS ContactName,
       b.Phone,
       b.Email,
       CAST(c.StartDate AS date) AS StartDate,
       CAST(c.EndDate AS date) AS EndDate,
       CASE WHEN ISNULL(c.VisitsPerYear,0) < 1 THEN 1 ELSE c.VisitsPerYear END AS VisitsPerYear,
       ISNULL(c.Status,c.ContractStatus) AS Status
FROM AMCContracts c
INNER JOIN B2BClients b ON b.ClientID=c.ClientID
LEFT JOIN ClientSites s ON s.SiteID=c.SiteID
WHERE c.StartDate IS NOT NULL AND c.EndDate IS NOT NULL
  AND c.EndDate >= CAST(GETDATE() AS date)
  AND ISNULL(c.Status,c.ContractStatus) NOT IN ('Cancelled','Expired','Closed');").ToList();

                Dictionary<int, AmcPlannerContractSnapshot> byId = contracts.ToDictionary(value => value.ContractId);
                foreach (AmcPlannerVisitSnapshot visit in connection.Query<AmcPlannerVisitSnapshot>(@"
SELECT VisitID AS VisitId, AMCID AS ContractId, VisitNumber, CAST(ScheduledDate AS date) AS ScheduledDate,
       CAST(CompletedDate AS date) AS CompletedDate, Status
FROM AMCVisits
WHERE AMCID IN (SELECT ContractID FROM AMCContracts WHERE EndDate >= CAST(GETDATE() AS date));"))
                {
                    AmcPlannerContractSnapshot contract;
                    if (byId.TryGetValue(visit.ContractId, out contract))
                        contract.Visits.Add(visit);
                }
                return contracts;
            }
        }

        public Dictionary<DateTime, int> LoadCapacity(DateTime fromDate, DateTime toDate)
        {
            DbHelper.EnsureAMCSchema();
            using (SqlConnection connection = DatabaseConnectionFactory.CreateConnection())
            {
                DatabaseConnectionFactory.Open(connection, "AmcPreventivePlanner.LoadCapacity");
                return connection.Query<CapacityRow>(@"
SELECT WorkDate, SUM(ItemCount) AS ItemCount
FROM (
    SELECT CAST(ScheduledDate AS date) AS WorkDate, COUNT(1) AS ItemCount
    FROM AMCVisits
    WHERE ScheduledDate BETWEEN @fromDate AND @toDate AND Status NOT IN ('Cancelled','Closed')
    GROUP BY CAST(ScheduledDate AS date)
    UNION ALL
    SELECT CAST(ScheduledDate AS date) AS WorkDate, COUNT(1) AS ItemCount
    FROM Jobs
    WHERE ScheduledDate BETWEEN @fromDate AND @toDate
      AND ISNULL(NULLIF(PipelineStatus,''),Status) NOT IN ('Cancelled','Closed','Completed','Invoiced')
    GROUP BY CAST(ScheduledDate AS date)
) load
GROUP BY WorkDate;", new { fromDate = fromDate.Date, toDate = toDate.Date })
                    .ToDictionary(value => value.WorkDate.Date, value => value.ItemCount);
            }
        }

        public AmcPlannerApplyResult Apply(AmcPreventivePlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            DbHelper.EnsureAMCSchema();
            using (SqlConnection connection = DatabaseConnectionFactory.CreateConnection())
            {
                DatabaseConnectionFactory.Open(connection, "AmcPreventivePlanner.Apply");
                // A serializable transaction is intentional: visit existence checks, visit creation,
                // and their unsent reminder drafts are one idempotent dispatcher-confirmed operation.
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    var result = new AmcPlannerApplyResult();
                    foreach (AmcPreventiveObligation item in plan.Obligations.Where(value => value.IsProposed))
                    {
                        int? visitId = connection.QuerySingleOrDefault<int?>(@"
SELECT TOP 1 VisitID FROM AMCVisits WITH (UPDLOCK,HOLDLOCK)
WHERE AMCID=@ContractId AND VisitNumber=@VisitNumber;", item, transaction);
                        if (!visitId.HasValue)
                        {
                            visitId = connection.QuerySingle<int>(@"
INSERT INTO AMCVisits (AMCID,VisitNumber,ScheduledDate,Status,CreatedAt,UpdatedAt)
OUTPUT INSERTED.VisitID
VALUES (@ContractId,@VisitNumber,@PlannedDate,'Scheduled',GETDATE(),GETDATE());", item, transaction);
                            result.VisitsCreated++;
                        }
                        else
                        {
                            result.VisitsAlreadyPresent++;
                        }

                    }

                    foreach (AmcReminderDraft draft in plan.ReminderDrafts)
                    {
                        int? visitId = connection.QuerySingleOrDefault<int?>(@"
SELECT TOP 1 VisitID FROM AMCVisits
WHERE AMCID=@ContractId AND VisitNumber=@VisitNumber
ORDER BY VisitID;", draft, transaction);
                        int created = connection.Execute(@"
IF NOT EXISTS (
    SELECT 1 FROM AMCVisitReminderDrafts WITH (UPDLOCK,HOLDLOCK)
    WHERE AMCID=@ContractId AND VisitNumber=@VisitNumber AND ScheduledDate=@ScheduledDate AND Status='Draft'
)
INSERT INTO AMCVisitReminderDrafts
    (AMCID,VisitID,VisitNumber,ScheduledDate,Channel,Recipient,DraftText,Status,CreatedByName)
VALUES
    (@ContractId,@visitId,@VisitNumber,@ScheduledDate,@Channel,@Recipient,@DraftText,'Draft',@createdByName);",
                                new
                                {
                                    draft.ContractId,
                                    visitId,
                                    draft.VisitNumber,
                                    draft.ScheduledDate,
                                    draft.Channel,
                                    draft.Recipient,
                                    draft.DraftText,
                                    createdByName = CurrentUserName()
                                }, transaction);
                        result.ReminderDraftsCreated += created;
                    }
                    transaction.Commit();
                    return result;
                }
            }
        }

        private static string CurrentUserName()
        {
            return Services.SessionManager.IsLoggedIn && Services.SessionManager.CurrentUser != null
                ? Services.SessionManager.CurrentUser.DisplayName
                : Environment.UserName;
        }

        private sealed class CapacityRow
        {
            public DateTime WorkDate { get; set; }
            public int ItemCount { get; set; }
        }
    }
}

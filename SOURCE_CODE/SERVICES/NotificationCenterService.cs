using System;
using System.Collections.Generic;
using System.Linq;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class NotificationCenterService
    {
        private readonly DbExecutor _db = new DbExecutor();

        public List<FoundationNotification> GetActiveNotifications(int max = 50)
        {
            var list = new List<FoundationNotification>();
            AddOverdueInvoices(list, max);
            AddLowInventory(list, max);
            AddPendingApprovals(list, max);
            AddTechnicianDelays(list, max);
            AddOverdueJobs(list, max);
            AddExpiringContracts(list, max);
            AddServiceDeskSla(list, max);

            RemoveDismissed(list);
            return list
                .OrderBy(n => SeverityRank(n.Severity))
                .ThenBy(n => n.CreatedAt)
                .Take(Math.Max(1, max))
                .ToList();
        }

        public void Dismiss(FoundationNotification notification)
        {
            if (notification == null || string.IsNullOrWhiteSpace(notification.NotificationKey))
                return;

            int? userId = SessionManager.CurrentUser?.UserId;
            try
            {
                _db.Execute(@"
IF NOT EXISTS (
    SELECT 1 FROM NotificationDismissals
    WHERE NotificationKey=@key AND ((UserId IS NULL AND @userId IS NULL) OR UserId=@userId)
)
INSERT INTO NotificationDismissals (NotificationKey, UserId) VALUES (@key, @userId);",
                    DbExecutor.Param("@key", notification.NotificationKey),
                    DbExecutor.Param("@userId", userId.HasValue ? (object)userId.Value : DBNull.Value));
                SessionManager.LogAction("DISMISS", "Notifications", notification.RecordId, notification.Module + ": " + notification.Title);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("NotificationCenterService.Dismiss", ex);
                throw;
            }
        }

        private void AddOverdueInvoices(List<FoundationNotification> list, int max)
        {
            AddRows(@"SELECT TOP (@max) InvoiceID AS Id, InvoiceNumber AS Title, 'Overdue invoice: ' + CONVERT(NVARCHAR(40), BalanceDue) AS Detail, DueDate AS CreatedAt FROM Invoices WHERE BalanceDue > 0 AND DueDate < CAST(GETDATE() AS DATE) ORDER BY DueDate", max, "High", "Invoices", "Invoices", list);
        }

        private void AddOverdueJobs(List<FoundationNotification> list, int max)
        {
            AddRows(@"SELECT TOP (@max) JobID AS Id, JobNumber AS Title, 'Scheduled date passed: ' + CONVERT(NVARCHAR(20), ScheduledDate, 106) AS Detail, ScheduledDate AS CreatedAt FROM Jobs WHERE ScheduledDate < CAST(GETDATE() AS DATE) AND ISNULL(NULLIF(PipelineStatus,''), Status) NOT IN ('Closed','Invoiced','Completed') ORDER BY ScheduledDate", max, "High", "Jobs", "Jobs", list);
        }

        private void AddLowInventory(List<FoundationNotification> list, int max)
        {
            AddRows(@"SELECT TOP (@max) ItemID AS Id, ItemName AS Title, 'Current stock below reorder level: ' + CONVERT(NVARCHAR(40), CurrentStock) + ' / ' + CONVERT(NVARCHAR(40), ReorderLevel) AS Detail, LastUpdated AS CreatedAt FROM StockItems WHERE CurrentStock <= ReorderLevel ORDER BY CurrentStock", max, "Medium", "Inventory", "Inventory", list);
        }

        private void AddPendingApprovals(List<FoundationNotification> list, int max)
        {
            AddRows(@"SELECT TOP (@max) BidID AS Id, ISNULL(NULLIF(QuotationNumber,''), 'Quotation #' + CONVERT(NVARCHAR(20), BidID)) AS Title, 'Pending approval: ' + ISNULL(NULLIF(Status,''), 'Draft') + CASE WHEN RequiredByDate IS NOT NULL THEN ' | required by ' + CONVERT(NVARCHAR(20), RequiredByDate, 106) ELSE '' END AS Detail, COALESCE(RequiredByDate, DueDate, SubmittedDate, GETDATE()) AS CreatedAt FROM Quotations WHERE ISNULL(Status,'') IN ('Draft','Pending','Submitted','Approval Pending','Pending Approval','Analysed','Analyzed') OR ISNULL(AnalysisStatus,'') IN ('Pending','Manual','Needs Review','Shortfall') ORDER BY COALESCE(RequiredByDate, DueDate, SubmittedDate, GETDATE())", max, "Medium", "Approvals", "Quotations", list);
            AddRows(@"SELECT TOP (@max) POID AS Id, ISNULL(NULLIF(PONumber,''), 'PO #' + CONVERT(NVARCHAR(20), POID)) AS Title, CASE WHEN AddToClientInvoice = 1 AND PendingChargeCreated = 0 THEN 'Pending approval: client charge not created' ELSE 'Pending approval: ' + ISNULL(NULLIF(Status,''), 'Pending') END AS Detail, PODate AS CreatedAt FROM PurchaseOrders WHERE ISNULL(Status,'') IN ('Draft','Pending','Approval Pending','Pending Approval') OR (AddToClientInvoice = 1 AND PendingChargeCreated = 0) ORDER BY PODate", max, "Medium", "Approvals", "Purchases", list);
            AddRows(@"SELECT TOP (@max) InvoiceID AS Id, ISNULL(NULLIF(InvoiceNumber,''), 'Invoice #' + CONVERT(NVARCHAR(20), InvoiceID)) AS Title, 'Pending approval: invoice is still ' + ISNULL(NULLIF(PaymentStatus,''), 'Draft') AS Detail, InvoiceDate AS CreatedAt FROM Invoices WHERE ISNULL(PaymentStatus,'') IN ('Draft','Approval Pending','Pending Approval') ORDER BY InvoiceDate", max, "Medium", "Approvals", "Invoices", list);
        }

        private void AddTechnicianDelays(List<FoundationNotification> list, int max)
        {
            AddRows(@"SELECT TOP (@max) j.JobID AS Id, ISNULL(NULLIF(j.JobNumber,''), 'Job #' + CONVERT(NVARCHAR(20), j.JobID)) AS Title, 'Technician delay: ' + ISNULL(e.Name, 'Assigned technician') + ' | ' + CONVERT(NVARCHAR(20), DATEDIFF(day, j.ScheduledDate, GETDATE())) + ' day(s) past schedule' AS Detail, j.ScheduledDate AS CreatedAt FROM Jobs j LEFT JOIN Employees e ON j.AssignedEmployeeID = e.EmployeeID WHERE j.AssignedEmployeeID IS NOT NULL AND j.ScheduledDate < CAST(GETDATE() AS DATE) AND ISNULL(NULLIF(j.PipelineStatus,''), j.Status) NOT IN ('Closed','Invoiced','Completed','Cancelled') ORDER BY j.ScheduledDate", max, "High", "Technician Delays", "Jobs", list);
            AddRows(@"IF OBJECT_ID('ServiceDeskIncidents', 'U') IS NOT NULL SELECT TOP (@max) IncidentId AS Id, ISNULL(NULLIF(IncidentNumber,''), 'Incident #' + CONVERT(NVARCHAR(20), IncidentId)) AS Title, 'Technician delay: SLA breached by ' + CONVERT(NVARCHAR(20), DATEDIFF(hour, SlaDueAt, GETDATE())) + ' hour(s)' AS Detail, SlaDueAt AS CreatedAt FROM ServiceDeskIncidents WHERE AssignedEmployeeId IS NOT NULL AND ISNULL(Status,'') NOT IN ('Resolved','Closed','Cancelled') AND SlaDueAt < GETDATE() ORDER BY SlaDueAt", max, "High", "Technician Delays", "ServiceDesk", list);
        }

        private void AddExpiringContracts(List<FoundationNotification> list, int max)
        {
            AddRows(@"SELECT TOP (@max) ContractID AS Id, 'Contract #' + CONVERT(NVARCHAR(20), ContractID) AS Title, 'Expires on ' + CONVERT(NVARCHAR(20), EndDate, 106) AS Detail, EndDate AS CreatedAt FROM AMCContracts WHERE ContractStatus = 'Active' AND EndDate BETWEEN CAST(GETDATE() AS DATE) AND DATEADD(day, 30, CAST(GETDATE() AS DATE)) ORDER BY EndDate", max, "Medium", "Contracts", "Contracts", list);
        }

        private void AddServiceDeskSla(List<FoundationNotification> list, int max)
        {
            AddRows(@"IF OBJECT_ID('ServiceDeskIncidents', 'U') IS NOT NULL SELECT TOP (@max) IncidentId AS Id, IncidentNumber AS Title, 'SLA due: ' + CONVERT(NVARCHAR(20), SlaDueAt, 106) AS Detail, SlaDueAt AS CreatedAt FROM ServiceDeskIncidents WHERE Status NOT IN ('Resolved','Closed') AND SlaDueAt <= DATEADD(hour, 4, GETDATE()) ORDER BY SlaDueAt", max, "High", "Service Desk", "ServiceDesk", list);
        }

        private void AddRows(string sql, int max, string severity, string module, string pageKey, List<FoundationNotification> list)
        {
            try
            {
                list.AddRange(_db.Query(sql, reader =>
                    {
                        return new FoundationNotification
                        {
                            NotificationKey = BuildKey(module, pageKey, reader["Id"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["Id"]), Convert.ToString(reader["Title"])),
                            Severity = severity,
                            Module = module,
                            PageKey = pageKey,
                            RecordId = reader["Id"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["Id"]),
                            Title = Convert.ToString(reader["Title"]),
                            Detail = Convert.ToString(reader["Detail"]),
                            CreatedAt = reader["CreatedAt"] == DBNull.Value ? DateTime.Now : Convert.ToDateTime(reader["CreatedAt"])
                        };
                    },
                    DbExecutor.Param("@max", Math.Max(1, max))));
            }
            catch (Exception ex)
            {
                AppLogger.LogError("NotificationCenterService." + module, ex);
            }
        }

        private void RemoveDismissed(List<FoundationNotification> list)
        {
            if (list.Count == 0)
                return;

            int? userId = SessionManager.CurrentUser?.UserId;
            try
            {
                HashSet<string> dismissed = new HashSet<string>(_db.Query(@"
SELECT NotificationKey FROM NotificationDismissals
WHERE UserId IS NULL OR UserId=@userId", reader => Convert.ToString(reader["NotificationKey"]),
                    DbExecutor.Param("@userId", userId.HasValue ? (object)userId.Value : DBNull.Value)),
                    StringComparer.OrdinalIgnoreCase);
                list.RemoveAll(n => dismissed.Contains(n.NotificationKey));
            }
            catch (Exception ex)
            {
                AppLogger.LogError("NotificationCenterService.RemoveDismissed", ex);
            }
        }

        private static string BuildKey(string module, string pageKey, int? recordId, string title)
        {
            return string.Join("|", new[]
            {
                module ?? string.Empty,
                pageKey ?? string.Empty,
                recordId.HasValue ? recordId.Value.ToString() : "0",
                (title ?? string.Empty).Trim()
            });
        }

        private static int SeverityRank(string severity)
        {
            string value = (severity ?? string.Empty).Trim().ToUpperInvariant();
            if (value == "CRITICAL") return 0;
            if (value == "HIGH") return 1;
            if (value == "MEDIUM") return 2;
            if (value == "LOW") return 3;
            return 4;
        }
    }
}

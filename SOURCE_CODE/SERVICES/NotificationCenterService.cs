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
            AddOverduePurchasePayments(list, max);
            AddLowInventory(list, max);
            AddPendingApprovals(list, max);
            AddTechnicianDelays(list, max);
            AddOverdueJobs(list, max);
            AddExpiringContracts(list, max);
            AddServiceDeskSla(list, max);
            AddClientDataQuality(list, max);
            AddVendorDataQuality(list, max);
            AddEmployeeCompliance(list, max);
            AddPaymentReconciliation(list, max);
            AddBackupHealth(list, max);

            RemoveDismissed(list);
            return list
                .OrderBy(n => SeverityRank(n.Severity))
                .ThenBy(n => n.CreatedAt)
                .Take(Math.Max(1, max))
                .ToList();
        }

        public int GetActiveCount(int max = 99)
        {
            return GetActiveNotifications(max).Count;
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

        private void AddOverduePurchasePayments(List<FoundationNotification> list, int max)
        {
            AddRows(@"SELECT TOP (@max) POID AS Id, ISNULL(NULLIF(PONumber,''), 'PO #' + CONVERT(NVARCHAR(20), POID)) AS Title, 'Supplier payable overdue: ' + CONVERT(NVARCHAR(40), TotalAmount - PaidAmount) + CASE WHEN PayByDate IS NOT NULL THEN ' | due ' + CONVERT(NVARCHAR(20), PayByDate, 106) ELSE '' END AS Detail, COALESCE(PayByDate, PODate) AS CreatedAt FROM PurchaseOrders WHERE (TotalAmount - PaidAmount) > 0 AND PayByDate IS NOT NULL AND PayByDate < CAST(GETDATE() AS DATE) AND ISNULL(Status,'') NOT IN ('Cancelled','Closed','Paid','Fully Paid','Fully Received') ORDER BY PayByDate", max, "High", "Payments", "Payments", list);
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

        private void AddClientDataQuality(List<FoundationNotification> list, int max)
        {
            AddRows(@"IF OBJECT_ID('B2BClients', 'U') IS NOT NULL SELECT TOP (@max) ClientID AS Id, ISNULL(NULLIF(CompanyName,''), 'Client #' + CONVERT(NVARCHAR(20), ClientID)) AS Title, 'Client record needs contact details' AS Detail, COALESCE(CustomerSince, GETDATE()) AS CreatedAt FROM B2BClients WHERE NULLIF(LTRIM(RTRIM(ISNULL(Phone,''))), '') IS NULL OR NULLIF(LTRIM(RTRIM(ISNULL(PrimaryContact,''))), '') IS NULL ORDER BY COALESCE(CustomerSince, GETDATE())", max, "Low", "Clients", "Clients", list);
        }

        private void AddVendorDataQuality(List<FoundationNotification> list, int max)
        {
            AddRows(@"IF OBJECT_ID('Vendors', 'U') IS NOT NULL SELECT TOP (@max) VendorID AS Id, ISNULL(NULLIF(VendorName,''), 'Vendor #' + CONVERT(NVARCHAR(20), VendorID)) AS Title, 'Vendor master missing GST/PAN/contact details' AS Detail, COALESCE(CreatedDate, GETDATE()) AS CreatedAt FROM Vendors WHERE ISNULL(IsActive, 1) = 1 AND (NULLIF(LTRIM(RTRIM(ISNULL(GSTNumber,''))), '') IS NULL OR NULLIF(LTRIM(RTRIM(ISNULL(PANNumber,''))), '') IS NULL OR NULLIF(LTRIM(RTRIM(ISNULL(Phone,''))), '') IS NULL) ORDER BY COALESCE(CreatedDate, GETDATE())", max, "Low", "Vendors", "Vendors", list);
        }

        private void AddEmployeeCompliance(List<FoundationNotification> list, int max)
        {
            AddRows(@"IF OBJECT_ID('EmployeeDocuments', 'U') IS NOT NULL SELECT TOP (@max) d.DocumentID AS Id, ISNULL(e.Name, 'Employee document') AS Title, 'Employee document expires on ' + CONVERT(NVARCHAR(20), d.ExpiryDate, 106) AS Detail, d.ExpiryDate AS CreatedAt FROM EmployeeDocuments d LEFT JOIN Employees e ON d.EmployeeID = e.EmployeeID WHERE d.ExpiryDate BETWEEN CAST(GETDATE() AS DATE) AND DATEADD(day, 30, CAST(GETDATE() AS DATE)) ORDER BY d.ExpiryDate", max, "Medium", "Employees", "Employees", list);
            AddRows(@"IF OBJECT_ID('EmployeeSkills', 'U') IS NOT NULL SELECT TOP (@max) s.SkillID AS Id, ISNULL(e.Name, 'Employee skill') AS Title, 'Certification expires on ' + CONVERT(NVARCHAR(20), s.ExpiryDate, 106) AS Detail, s.ExpiryDate AS CreatedAt FROM EmployeeSkills s LEFT JOIN Employees e ON s.EmployeeID = e.EmployeeID WHERE s.ExpiryDate BETWEEN CAST(GETDATE() AS DATE) AND DATEADD(day, 30, CAST(GETDATE() AS DATE)) ORDER BY s.ExpiryDate", max, "Medium", "Employees", "Employees", list);
        }

        private void AddPaymentReconciliation(List<FoundationNotification> list, int max)
        {
            AddRows(@"IF OBJECT_ID('Payments', 'U') IS NOT NULL SELECT TOP (@max) PaymentID AS Id, ISNULL(NULLIF(PaymentNumber,''), 'Payment #' + CONVERT(NVARCHAR(20), PaymentID)) AS Title, 'Payment reference pending for reconciliation' AS Detail, PaymentDate AS CreatedAt FROM Payments WHERE PaymentDate < DATEADD(day, -3, GETDATE()) AND ISNULL(PaymentMode,'') NOT IN ('Cash') AND NULLIF(LTRIM(RTRIM(ISNULL(ReferenceNumber,''))), '') IS NULL ORDER BY PaymentDate", max, "Medium", "Payments", "Payments", list);
        }

        private void AddBackupHealth(List<FoundationNotification> list, int max)
        {
            AddRows(@"IF OBJECT_ID('BackupLog', 'U') IS NOT NULL AND COL_LENGTH('BackupLog', 'BackupTime') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM BackupLog WHERE BackupTime >= DATEADD(day, -7, GETDATE()) AND ISNULL(Success, 0) = 1) SELECT TOP (@max) 0 AS Id, 'Backup attention required' AS Title, 'No successful backup recorded in the last 7 days' AS Detail, DATEADD(day, -7, GETDATE()) AS CreatedAt", max, "Medium", "Settings", "Settings", list);
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

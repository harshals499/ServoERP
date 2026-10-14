using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using System.Globalization;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class GlobalSearchService
    {
        internal static bool CanView(string pageKey)
        {
            string module = pageKey == "Jobs" || pageKey == "ServiceDesk" ? "WorkOrders" : pageKey == "Vendors" ? "Suppliers" : pageKey == "Contracts" ? "Contracts" : pageKey;
            var user = SessionManager.CurrentUser;
            RolePermissionDto permission;
            if (user == null) return false;
            if (user.Permissions != null && user.Permissions.Count > 0 &&
                (!user.Permissions.TryGetValue(module, out permission) || permission == null || !permission.CanView)) return false;
            return SessionManager.HasPermission(module, "View");
        }
        private sealed class SearchRow
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string Detail { get; set; }
            public DateTime? RecordDate { get; set; }
            public string ClientName { get; set; }
            public string SiteName { get; set; }
            public string Technician { get; set; }
            public string Status { get; set; }
            public string JobType { get; set; }
            public decimal? Amount { get; set; }
            public decimal? BalanceDue { get; set; }
            public DateTime? DueDate { get; set; }
        }
        public List<GlobalSearchResult> Search(string query, int maxResults = 50)
        {
            var results = new List<GlobalSearchResult>();
            query = (query ?? string.Empty).Trim();
            if (query.Length < 2)
                return results;

            using (SqlConnection conn = new DatabaseManager().GetConnection())
            {
                conn.Open();
                string like = "%" + query + "%";
                SearchClients(conn, like, results, maxResults);
                SearchSites(conn, like, results, maxResults);
                SearchJobs(conn, like, results, maxResults);
                SearchInvoices(conn, like, results, maxResults);
                SearchPayments(conn, like, results, maxResults);
                SearchVendorAdvances(conn, like, results, maxResults);
                SearchPurchases(conn, like, results, maxResults);
                SearchInventory(conn, like, results, maxResults);
                SearchVendors(conn, like, results, maxResults);
                SearchEmployees(conn, like, results, maxResults);
                SearchContracts(conn, like, results, maxResults);
                SearchServiceDesk(conn, like, results, maxResults);
            }

            // Round-robin modules so a large client list cannot starve jobs or invoices.
            return results.GroupBy(r => r.Module).SelectMany(g => g.Select((r, index) => new { Record = r, Index = index }))
                .OrderBy(r => r.Index).Take(Math.Max(1, maxResults)).Select(r => r.Record).ToList();
        }

        private static void SearchClients(SqlConnection conn, string like, List<GlobalSearchResult> results, int max)
        {
            AddRows(conn, @"SELECT TOP (@max) ClientID AS Id, CompanyName AS Title, ISNULL(Phone,'') + ' ' + ISNULL(Email,'') AS Detail, CustomerSince AS RecordDate FROM B2BClients WHERE CompanyName LIKE @q OR ISNULL(Phone,'') LIKE @q OR ISNULL(Email,'') LIKE @q ORDER BY CompanyName", like, max, "Clients", "Clients", results);
        }

        private static void SearchSites(SqlConnection conn, string like, List<GlobalSearchResult> results, int max)
        {
            AddRows(conn, @"SELECT TOP (@max) SiteID AS Id, SiteName AS Title, ISNULL(Address,'') + ' ' + ISNULL(City,'') AS Detail, NULL AS RecordDate FROM ClientSites WHERE SiteName LIKE @q OR ISNULL(Address,'') LIKE @q OR ISNULL(City,'') LIKE @q ORDER BY SiteName", like, max, "Sites", "Clients", results);
        }

        private static void SearchJobs(SqlConnection conn, string like, List<GlobalSearchResult> results, int max)
        {
            AddRows(conn, @"SELECT TOP (@max) j.JobID AS Id, ISNULL(j.JobNumber,'') + ' - ' + ISNULL(j.JobTitle, j.Title) AS Title,
                ISNULL(j.Description,'') AS Detail, j.ScheduledDate AS RecordDate, c.CompanyName AS ClientName, s.SiteName,
                ISNULL(e.Name,'Unassigned') AS Technician, ISNULL(j.PipelineStatus,j.Status) AS Status, j.JobType
                FROM dbo.ActiveJobs j LEFT JOIN B2BClients c ON c.ClientID=j.ClientID LEFT JOIN ClientSites s ON s.SiteID=j.SiteID
                LEFT JOIN Employees e ON e.EmployeeID=j.AssignedEmployeeID
                WHERE (@technician=0 OR j.AssignedEmployeeID=@employee OR e.Name=@displayName OR e.Name=@username)
                AND (ISNULL(j.JobNumber,'') LIKE @q OR ISNULL(j.JobTitle,j.Title) LIKE @q OR ISNULL(j.Description,'') LIKE @q OR c.CompanyName LIKE @q OR s.SiteName LIKE @q)
                ORDER BY j.ScheduledDate DESC", like, max, "Jobs", "Jobs", results);
        }

        private static void SearchInvoices(SqlConnection conn, string like, List<GlobalSearchResult> results, int max)
        {
            AddRows(conn, @"SELECT TOP (@max) i.InvoiceID AS Id, i.InvoiceNumber AS Title, i.PaymentStatus AS Detail,
                i.PaymentStatus AS Status, i.TotalAmount AS Amount, i.BalanceDue, i.DueDate, i.InvoiceDate AS RecordDate,
                c.CompanyName AS ClientName, s.SiteName FROM dbo.ActiveInvoices i LEFT JOIN B2BClients c ON c.ClientID=i.ClientID
                LEFT JOIN ClientSites s ON s.SiteID=i.SiteID WHERE ISNULL(i.InvoiceNumber,'') LIKE @q OR ISNULL(i.PaymentStatus,'') LIKE @q
                OR c.CompanyName LIKE @q OR s.SiteName LIKE @q ORDER BY i.InvoiceDate DESC", like, max, "Invoices", "Invoices", results);
        }

        private static void SearchPurchases(SqlConnection conn, string like, List<GlobalSearchResult> results, int max)
        {
            AddRows(conn, @"SELECT TOP (@max) POID AS Id, PONumber AS Title, ISNULL(Status,'') + ' | Total ' + CONVERT(NVARCHAR(40), TotalAmount) AS Detail, PODate AS RecordDate FROM PurchaseOrders WHERE ISNULL(PONumber,'') LIKE @q OR ISNULL(VendorInvoiceNumber,'') LIKE @q OR ISNULL(Status,'') LIKE @q ORDER BY PODate DESC", like, max, "Purchases", "Purchases", results);
        }

        private static void SearchPayments(SqlConnection conn, string like, List<GlobalSearchResult> results, int max)
        {
            AddRows(conn, @"SELECT TOP (@max) PaymentID AS Id, ISNULL(ReferenceNumber, 'Payment #' + CONVERT(NVARCHAR(20), PaymentID)) AS Title, ISNULL(PaymentMode,'') + ' | Amount ' + CONVERT(NVARCHAR(40), AmountPaid) AS Detail, PaymentDate AS RecordDate FROM Payments WHERE ISNULL(ReferenceNumber,'') LIKE @q OR ISNULL(PaymentMode,'') LIKE @q OR ISNULL(Notes,'') LIKE @q ORDER BY PaymentDate DESC", like, max, "Payments", "Payments", results);
        }

        private static void SearchVendorAdvances(SqlConnection conn, string like, List<GlobalSearchResult> results, int max)
        {
            AddRows(conn, @"IF OBJECT_ID('VendorAdvancePayments', 'U') IS NOT NULL SELECT TOP (@max) vap.AdvancePaymentId AS Id, ISNULL(v.VendorName, 'Vendor #' + CONVERT(NVARCHAR(20), vap.VendorId)) + ' ' + vap.TransactionType AS Title, ISNULL(vap.ReferenceNumber,'') + ' | Amount ' + CONVERT(NVARCHAR(40), vap.Amount) AS Detail, vap.TransactionDate AS RecordDate FROM VendorAdvancePayments vap LEFT JOIN Vendors v ON vap.VendorId = v.VendorID WHERE ISNULL(v.VendorName,'') LIKE @q OR ISNULL(vap.ReferenceNumber,'') LIKE @q OR ISNULL(vap.Notes,'') LIKE @q ORDER BY vap.TransactionDate DESC", like, max, "Vendor Advances", "Payments", results);
        }

        private static void SearchInventory(SqlConnection conn, string like, List<GlobalSearchResult> results, int max)
        {
            AddRows(conn, @"SELECT TOP (@max) ItemID AS Id, ItemName AS Title, ISNULL(Category,'') + ' | Stock ' + CONVERT(NVARCHAR(40), CurrentStock) + ' ' + ISNULL(Unit,'') AS Detail, LastUpdated AS RecordDate FROM StockItems WHERE ItemName LIKE @q OR ISNULL(Category,'') LIKE @q ORDER BY ItemName", like, max, "Inventory", "Inventory", results);
        }

        private static void SearchVendors(SqlConnection conn, string like, List<GlobalSearchResult> results, int max)
        {
            AddRows(conn, @"SELECT TOP (@max) VendorID AS Id, VendorName AS Title, ISNULL(Phone,'') + ' ' + ISNULL(Email,'') AS Detail, CreatedDate AS RecordDate FROM Vendors WHERE VendorName LIKE @q OR ISNULL(Phone,'') LIKE @q OR ISNULL(Email,'') LIKE @q ORDER BY VendorName", like, max, "Vendors", "Vendors", results);
        }

        private static void SearchEmployees(SqlConnection conn, string like, List<GlobalSearchResult> results, int max)
        {
            AddRows(conn, @"SELECT TOP (@max) EmployeeID AS Id, Name AS Title, ISNULL(Designation,'') + ' ' + ISNULL(Department,'') + ' ' + ISNULL(Phone,'') AS Detail, CreatedDate AS RecordDate FROM Employees WHERE Name LIKE @q OR ISNULL(Designation,'') LIKE @q OR ISNULL(Department,'') LIKE @q OR ISNULL(Phone,'') LIKE @q ORDER BY Name", like, max, "Employees", "Employees", results);
        }

        private static void SearchContracts(SqlConnection conn, string like, List<GlobalSearchResult> results, int max)
        {
            AddRows(conn, @"SELECT TOP (@max) ContractID AS Id, 'Contract #' + CONVERT(NVARCHAR(20), ContractID) AS Title, ISNULL(ContractStatus,'') + ' | Frequency ' + ISNULL(MaintenanceFrequency,'') AS Detail, StartDate AS RecordDate FROM AMCContracts WHERE CONVERT(NVARCHAR(20), ContractID) LIKE @q OR ISNULL(ContractStatus,'') LIKE @q OR ISNULL(MaintenanceFrequency,'') LIKE @q ORDER BY StartDate DESC", like, max, "Contracts", "Contracts", results);
        }

        private static void SearchServiceDesk(SqlConnection conn, string like, List<GlobalSearchResult> results, int max)
        {
            AddRows(conn, @"IF OBJECT_ID('ServiceDeskIncidents', 'U') IS NOT NULL SELECT TOP (@max) IncidentId AS Id, IncidentNumber + ' - ' + ShortDescription AS Title, Status + ' | ' + Priority AS Detail, OpenedAt AS RecordDate FROM ServiceDeskIncidents WHERE IncidentNumber LIKE @q OR ShortDescription LIKE @q OR ISNULL(Description,'') LIKE @q ORDER BY OpenedAt DESC", like, max, "Service Desk", "ServiceDesk", results);
        }

        private static void AddRows(SqlConnection conn, string sql, string like, int max, string module, string pageKey, List<GlobalSearchResult> results)
        {
            if (!CanView(pageKey)) return;
            try
            {
                var user = SessionManager.CurrentUser;
                foreach (var row in conn.Query<SearchRow>(sql, new { q = like, max = Math.Max(1, Math.Min(120, max)),
                    technician = (user?.RoleName ?? "").IndexOf("technician", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 0,
                    employee = user?.EmployeeId, displayName = user?.DisplayName ?? "", username = user?.Username ?? "" }, commandTimeout: 15))
                {
                    results.Add(new GlobalSearchResult { Module = module, PageKey = pageKey, RecordId = row.Id,
                        Title = row.Title, Detail = row.Detail + (row.Amount.HasValue ? " · Total ₹" + row.Amount.Value.ToString("N2",CultureInfo.GetCultureInfo("en-IN")) : ""), RecordDate = row.RecordDate, ClientName = row.ClientName,
                        SiteName = row.SiteName, Technician = row.Technician, Status = row.Status, JobType = row.JobType,
                        Amount=row.Amount, BalanceDue=row.BalanceDue, DueDate=row.DueDate });
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("GlobalSearchService." + module, ex);
                throw;
            }
        }
    }
}

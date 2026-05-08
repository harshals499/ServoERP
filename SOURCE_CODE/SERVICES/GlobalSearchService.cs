using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class GlobalSearchService
    {
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

            return results.Count > maxResults ? results.GetRange(0, maxResults) : results;
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
            AddRows(conn, @"SELECT TOP (@max) JobID AS Id, ISNULL(JobNumber,'') + ' - ' + ISNULL(JobTitle, Title) AS Title, ISNULL(PipelineStatus, Status) + ' | ' + ISNULL(Description,'') AS Detail, ScheduledDate AS RecordDate FROM Jobs WHERE ISNULL(JobNumber,'') LIKE @q OR ISNULL(JobTitle, Title) LIKE @q OR ISNULL(Description,'') LIKE @q ORDER BY ScheduledDate DESC", like, max, "Jobs", "Jobs", results);
        }

        private static void SearchInvoices(SqlConnection conn, string like, List<GlobalSearchResult> results, int max)
        {
            AddRows(conn, @"SELECT TOP (@max) InvoiceID AS Id, InvoiceNumber AS Title, PaymentStatus + ' | Total ' + CONVERT(NVARCHAR(40), TotalAmount) AS Detail, InvoiceDate AS RecordDate FROM Invoices WHERE ISNULL(InvoiceNumber,'') LIKE @q OR ISNULL(PaymentStatus,'') LIKE @q ORDER BY InvoiceDate DESC", like, max, "Invoices", "Invoices", results);
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
            try
            {
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@q", like);
                    cmd.Parameters.AddWithValue("@max", Math.Max(1, max));
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new GlobalSearchResult
                            {
                                Module = module,
                                PageKey = pageKey,
                                RecordId = reader["Id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Id"]),
                                Title = Convert.ToString(reader["Title"]),
                                Detail = Convert.ToString(reader["Detail"]),
                                RecordDate = reader["RecordDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["RecordDate"])
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("GlobalSearchService." + module, ex);
            }
        }
    }
}

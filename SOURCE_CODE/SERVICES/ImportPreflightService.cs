using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class ImportReferenceSnapshot
    {
        public IEnumerable<string> ClientNames { get; set; } = Enumerable.Empty<string>();
        public IEnumerable<string> VendorNames { get; set; } = Enumerable.Empty<string>();
        public IEnumerable<string> EmployeeNames { get; set; } = Enumerable.Empty<string>();
        public IEnumerable<string> InvoiceNumbers { get; set; } = Enumerable.Empty<string>();
        public IEnumerable<string> ClientSiteKeys { get; set; } = Enumerable.Empty<string>();
    }

    public sealed class ImportPreflightResult
    {
        public List<string> Errors { get; } = new List<string>();
        public string ImportOrderAdvice { get; set; } = string.Empty;
        public bool CanImport => Errors.Count == 0;

        public string ToUserMessage()
        {
            var builder = new StringBuilder();
            builder.AppendLine("Preflight validation failed. No rows were imported.");
            if (!string.IsNullOrWhiteSpace(ImportOrderAdvice))
            {
                builder.AppendLine();
                builder.AppendLine(ImportOrderAdvice);
            }

            builder.AppendLine();
            builder.AppendLine("Fix these first:");
            foreach (string error in Errors.Take(30))
                builder.AppendLine(error);
            if (Errors.Count > 30)
                builder.AppendLine("...and " + (Errors.Count - 30) + " more issue(s).");
            return builder.ToString();
        }
    }

    public static class ImportPreflightService
    {
        public static ImportReferenceSnapshot LoadReferenceSnapshot(SqlConnection conn)
        {
            return new ImportReferenceSnapshot
            {
                ClientNames = QueryStrings(conn, "IF OBJECT_ID('dbo.B2BClients','U') IS NOT NULL SELECT CompanyName FROM B2BClients ELSE SELECT CAST(NULL AS NVARCHAR(255)) WHERE 1=0"),
                VendorNames = QueryStrings(conn, "IF OBJECT_ID('dbo.Vendors','U') IS NOT NULL SELECT VendorName FROM Vendors ELSE SELECT CAST(NULL AS NVARCHAR(255)) WHERE 1=0"),
                EmployeeNames = QueryStrings(conn, "IF OBJECT_ID('dbo.Employees','U') IS NOT NULL SELECT Name FROM Employees ELSE SELECT CAST(NULL AS NVARCHAR(255)) WHERE 1=0"),
                InvoiceNumbers = QueryStrings(conn, "IF OBJECT_ID('dbo.Invoices','U') IS NOT NULL SELECT InvoiceNumber FROM Invoices ELSE SELECT CAST(NULL AS NVARCHAR(255)) WHERE 1=0"),
                ClientSiteKeys = QueryStrings(conn, @"
IF OBJECT_ID('dbo.ClientSites','U') IS NOT NULL AND OBJECT_ID('dbo.B2BClients','U') IS NOT NULL
    SELECT c.CompanyName + N'||' + s.SiteName
    FROM ClientSites s
    JOIN B2BClients c ON c.ClientID = s.ClientID
ELSE SELECT CAST(NULL AS NVARCHAR(255)) WHERE 1=0")
            };
        }

        public static ImportPreflightResult ValidateRows(
            ExcelImportModule module,
            IEnumerable<IDictionary<string, string>> rows,
            ImportReferenceSnapshot snapshot)
        {
            var result = new ImportPreflightResult();
            HashSet<string> clients = ToSet(snapshot?.ClientNames);
            HashSet<string> vendors = ToSet(snapshot?.VendorNames);
            HashSet<string> employees = ToSet(snapshot?.EmployeeNames);
            HashSet<string> invoices = ToSet(snapshot?.InvoiceNumbers);
            HashSet<string> clientSites = ToSet(snapshot?.ClientSiteKeys);

            int rowNumber = 2;
            foreach (IDictionary<string, string> row in rows ?? Enumerable.Empty<IDictionary<string, string>>())
            {
                switch (module)
                {
                    case ExcelImportModule.Sites:
                        RequireExists(result, rowNumber, "Client", Get(row, "ClientName"), clients, "Import Clients before Sites.");
                        break;
                    case ExcelImportModule.Invoices:
                        RequireExists(result, rowNumber, "Client", Get(row, "ClientName"), clients, "Import Clients before Invoices.");
                        RequireClientSite(result, rowNumber, row, clientSites, "Import Sites before Invoices, or leave SiteName blank.");
                        break;
                    case ExcelImportModule.Quotations:
                        RequireExists(result, rowNumber, "Client", Get(row, "ClientName"), clients, "Import Clients before Quotations.");
                        RequireClientSite(result, rowNumber, row, clientSites, "Import Sites before Quotations, or leave SiteName blank.");
                        break;
                    case ExcelImportModule.Payments:
                        RequireExists(result, rowNumber, "Invoice", Get(row, "InvoiceNumber"), invoices, "Import Invoices before Payments.");
                        break;
                    case ExcelImportModule.Purchases:
                        RequireExists(result, rowNumber, "Vendor", Get(row, "VendorName"), vendors, "Import Vendors before Purchases.");
                        break;
                    case ExcelImportModule.Jobs:
                        RequireExists(result, rowNumber, "Client", Get(row, "ClientName"), clients, "Import Clients before Jobs.");
                        RequireClientSite(result, rowNumber, row, clientSites, "Import Sites before Jobs.");
                        string technicianName = Get(row, "TechnicianName");
                        if (!string.IsNullOrWhiteSpace(technicianName))
                            RequireExists(result, rowNumber, "Technician/employee", technicianName, employees, "Import Employees before Jobs, or leave TechnicianName blank.");
                        break;
                }

                rowNumber++;
            }

            if (result.Errors.Count > 0 && string.IsNullOrWhiteSpace(result.ImportOrderAdvice))
                result.ImportOrderAdvice = GetDefaultAdvice(module);

            return result;
        }

        private static void RequireClientSite(ImportPreflightResult result, int rowNumber, IDictionary<string, string> row, HashSet<string> clientSites, string advice)
        {
            string siteName = Get(row, "SiteName");
            if (string.IsNullOrWhiteSpace(siteName))
                return;

            string clientName = Get(row, "ClientName");
            string key = Normalize(clientName + "||" + siteName);
            if (!clientSites.Contains(key))
                AddError(result, rowNumber, "Site not found for client: " + siteName + " / " + clientName, advice);
        }

        private static void RequireExists(ImportPreflightResult result, int rowNumber, string label, string value, HashSet<string> knownValues, string advice)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            if (!knownValues.Contains(Normalize(value)))
                AddError(result, rowNumber, label + " not found: " + value, advice);
        }

        private static void AddError(ImportPreflightResult result, int rowNumber, string message, string advice)
        {
            result.Errors.Add("Row " + rowNumber + " - " + message);
            if (string.IsNullOrWhiteSpace(result.ImportOrderAdvice))
                result.ImportOrderAdvice = advice;
        }

        private static string GetDefaultAdvice(ExcelImportModule module)
        {
            switch (module)
            {
                case ExcelImportModule.Sites:
                    return "Import Clients before Sites.";
                case ExcelImportModule.Invoices:
                    return "Import Clients and Sites before Invoices.";
                case ExcelImportModule.Quotations:
                    return "Import Clients and Sites before Quotations.";
                case ExcelImportModule.Purchases:
                    return "Import Vendors before Purchases.";
                case ExcelImportModule.Payments:
                    return "Import Invoices before Payments.";
                case ExcelImportModule.Jobs:
                    return "Import Clients, Sites, and Employees before Jobs.";
                default:
                    return string.Empty;
            }
        }

        private static HashSet<string> ToSet(IEnumerable<string> values)
        {
            return new HashSet<string>((values ?? Enumerable.Empty<string>()).Select(Normalize).Where(v => !string.IsNullOrWhiteSpace(v)), StringComparer.OrdinalIgnoreCase);
        }

        private static string Get(IDictionary<string, string> row, string key)
        {
            if (row == null)
                return string.Empty;
            string value;
            return row.TryGetValue(key, out value) ? (value ?? string.Empty).Trim() : string.Empty;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            return Regex.Replace(value.Trim(), @"\s+", " ").ToUpperInvariant();
        }

        private static IEnumerable<string> QueryStrings(SqlConnection conn, string sql)
        {
            var values = new List<string>();
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.CommandTimeout = 30;
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (!reader.IsDBNull(0))
                            values.Add(Convert.ToString(reader.GetValue(0)));
                    }
                }
            }

            return values;
        }
    }
}

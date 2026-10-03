using System;
using System.Collections.Generic;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.Tests
{
    public static class SmartImportDuplicateDetectorSmokeTests
    {
        public static List<string> RunAll()
        {
            var passed = new List<string>();
            var detector = new SmartImportDuplicateDetector();

            var employees = new List<Dictionary<string, string>>
            {
                Row("EmployeeCode", "EMP-001", "EmployeeName", "Ramesh Patil", "Phone", "+91 98765-43210"),
                Row("EmployeeCode", " emp 001 ", "EmployeeName", "Ramesh  Patil", "Phone", "9876543210")
            };
            SmartImportDuplicateScan employeeScan = detector.ScanUploadOnly(ExcelImportModule.Employees, employees);
            Expect(employeeScan.UploadDuplicateRows == 1, "formatted employee identities should be detected as one duplicate row");
            passed.Add("employee duplicate detection ignores case, spaces, punctuation, and phone formatting");

            var clients = new List<Dictionary<string, string>>
            {
                Row("ClientName", "ABC Cooling Pvt. Ltd.", "GSTIN", "27ABCDE1234F1Z0"),
                Row("ClientName", "ABC COOLING PVT LTD", "GSTIN", "27abcde1234f1z0")
            };
            Expect(detector.ScanUploadOnly(ExcelImportModule.Clients, clients).UploadDuplicateRows == 1,
                "client GST/name duplicates should be detected");
            passed.Add("client duplicate detection uses GSTIN and normalized company name");

            var clientEmail = new List<Dictionary<string, string>>
            {
                Row("ClientName", "North Plant", "Email", " Accounts@Example.com "),
                Row("ClientName", "North Plant Division", "Email", "accounts@example.com")
            };
            Expect(detector.ScanUploadOnly(ExcelImportModule.Clients, clientEmail).UploadDuplicateRows == 1,
                "client email duplicates should be detected independently of name");
            passed.Add("client duplicate detection checks email independently of name");

            var employeeIdentity = new List<Dictionary<string, string>>
            {
                Row("EmployeeName", "Technician One", "UAN", "1002 0030 0400"),
                Row("EmployeeName", "Technician Two", "UAN", "100200300400")
            };
            Expect(detector.ScanUploadOnly(ExcelImportModule.Employees, employeeIdentity).UploadDuplicateRows == 1,
                "employee UAN duplicates should be detected independently of name");
            passed.Add("employee duplicate detection checks payroll and bank identities");

            List<SmartImportDuplicateGroup> connectedGroups = SmartImportDuplicateDetector.FindExistingGroups(
                ExcelImportModule.Clients,
                new List<Dictionary<string, string>>
                {
                    Row("RecordID", "1", "DisplayName", "Alpha", "ClientName", "Alpha", "Email", "alpha@example.com"),
                    Row("RecordID", "2", "DisplayName", "Alpha branch", "ClientName", "Alpha", "Email", "branch@example.com"),
                    Row("RecordID", "3", "DisplayName", "Different spelling", "ClientName", "Different", "Email", "branch@example.com")
                });
            Expect(connectedGroups.Count == 1 && connectedGroups[0].Records.Count == 3,
                "overlapping name/email matches should become one safe review group");
            passed.Add("overlapping matches are consolidated into one duplicate review group");

            var wideHeaders = new List<string> { "Quotation Number", "Client Name", "Description", "Qty", "Rate", "Amount", "Description [2]", "Qty [2]", "Rate [2]", "Amount [2]" };
            var wideSource = Row("Quotation Number", "Q-WIDE-1", "Client Name", "ABC", "Description", "Copper pipe", "Qty", "2", "Rate", "500", "Amount", "1000",
                "Description [2]", "Insulation", "Qty [2]", "4", "Rate [2]", "100", "Amount [2]", "400");
            var wideCanonical = Row("QuotationNumber", "Q-WIDE-1", "ClientName", "ABC", "Description", "HVAC materials", "Amount", "1400");
            List<Dictionary<string, string>> expanded = MultiColumnDocumentRowExpander.Expand(ExcelImportModule.Quotations, wideHeaders, wideSource, wideCanonical);
            Expect(expanded.Count == 2 && expanded[0]["LineDescription"] == "Copper pipe" && expanded[1]["LineDescription"] == "Insulation",
                "repeated quotation item columns should expand into separate line rows");
            Expect(detector.ScanUploadOnly(ExcelImportModule.Quotations, expanded).UploadDuplicateRows == 0,
                "different line items belonging to one quotation must not be treated as duplicate quotations");
            passed.Add("quotation, invoice, and PO imports recognize repeated line-item column groups");

            var modules = new Dictionary<ExcelImportModule, Dictionary<string, string>>
            {
                { ExcelImportModule.Quotations, Row("QuotationNumber", "Q-1") },
                { ExcelImportModule.Invoices, Row("InvoiceNumber", "I-1") },
                { ExcelImportModule.Payments, Row("ReferenceNumber", "UTR-1") },
                { ExcelImportModule.Purchases, Row("PONumber", "PO-1", "ItemDescription", "Copper") },
                { ExcelImportModule.Jobs, Row("ClientName", "ABC", "SiteName", "Plant", "ScheduledDate", "01/04/2026", "Description", "PM") },
                { ExcelImportModule.Clients, Row("ClientName", "ABC") },
                { ExcelImportModule.Employees, Row("EmployeeCode", "E-1") },
                { ExcelImportModule.Vendors, Row("SupplierName", "Parts Co") },
                { ExcelImportModule.Sites, Row("ClientName", "ABC", "SiteName", "Plant") },
                { ExcelImportModule.Inventory, Row("ItemName", "Copper Pipe") },
                { ExcelImportModule.SupplierItemPrices, Row("ItemName", "Copper Pipe", "SupplierName", "Parts Co", "EffectiveDate", "01/04/2026") },
                { ExcelImportModule.AMC, Row("ContractNumber", "AMC-1") }
            };

            foreach (KeyValuePair<ExcelImportModule, Dictionary<string, string>> item in modules)
            {
                var repeated = new List<Dictionary<string, string>> { item.Value, new Dictionary<string, string>(item.Value, StringComparer.OrdinalIgnoreCase) };
                Expect(detector.ScanUploadOnly(item.Key, repeated).UploadDuplicateRows == 1,
                    item.Key + " Smart Upload card did not produce a duplicate identity key");
            }
            passed.Add("all twelve Smart Upload modules detect repeated upload identities");

            foreach (KeyValuePair<ExcelImportModule, Dictionary<string, string>> item in modules)
            {
                SmartImportDuplicateScan databaseScan = detector.Scan(item.Key,
                    new List<Dictionary<string, string>> { item.Value }, true);
                Expect(!databaseScan.Details.Exists(detail => detail.IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) >= 0),
                    item.Key + " existing-record duplicate scan could not query SQL Server");
            }
            passed.Add("all twelve Smart Upload modules can compare identities with the current SQL Server database");
            return passed;
        }

        private static Dictionary<string, string> Row(params string[] values)
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index + 1 < values.Length; index += 2)
                row[values[index]] = values[index + 1];
            return row;
        }

        private static void Expect(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}

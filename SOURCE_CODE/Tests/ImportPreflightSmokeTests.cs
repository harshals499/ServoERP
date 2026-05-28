using System;
using System.Collections.Generic;
using System.Linq;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.Tests
{
    public static class ImportPreflightSmokeTests
    {
        public static List<string> RunAll()
        {
            var passed = new List<string>();

            var rows = new[]
            {
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "SiteName", "GPL HO" },
                    { "ClientName", "GPL HO" }
                },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "SiteName", "Atul" },
                    { "ClientName", "Atul " }
                }
            };

            ImportPreflightResult result = ImportPreflightService.ValidateRows(
                ExcelImportModule.Sites,
                rows,
                new ImportReferenceSnapshot
                {
                    ClientNames = new[] { "Atul" },
                    VendorNames = Enumerable.Empty<string>(),
                    EmployeeNames = Enumerable.Empty<string>(),
                    InvoiceNumbers = Enumerable.Empty<string>(),
                    ClientSiteKeys = Enumerable.Empty<string>()
                });

            if (result.CanImport)
                throw new InvalidOperationException("Sites preflight should block import when referenced clients are missing.");

            if (!result.Errors.Any(error => error.Contains("Row 2") && error.Contains("GPL HO") && error.Contains("Client not found")))
                throw new InvalidOperationException("Sites preflight did not report the missing client row.");

            if (result.ImportOrderAdvice.IndexOf("Clients", StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException("Sites preflight did not include client-first import order advice.");

            passed.Add("sites import preflight blocks missing clients before SQL import");

            return passed;
        }
    }
}

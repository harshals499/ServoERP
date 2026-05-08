using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public static class EmployeeExportService
    {
        public static void ExportEmployeeList(string filePath, List<EmployeeSummaryDto> employees)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("A file path is required.", nameof(filePath));

            Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? @"C:\HVAC_PRO_MSE");
            using (var writer = new StreamWriter(filePath, false, new UTF8Encoding(true)))
            {
                writer.WriteLine("Code,Name,Designation,Department,Site,Phone,Status");
                foreach (EmployeeSummaryDto employee in employees ?? new List<EmployeeSummaryDto>())
                {
                    writer.WriteLine(string.Join(",",
                        Csv(employee.EmployeeCode),
                        Csv(employee.Name),
                        Csv(employee.Designation),
                        Csv(employee.Department),
                        Csv(employee.ClientSite),
                        Csv(employee.Phone),
                        Csv(employee.Status)));
                }
            }
        }

        private static string Csv(string value)
        {
            string safe = (value ?? string.Empty).Replace("\"", "\"\"");
            return "\"" + safe + "\"";
        }
    }
}

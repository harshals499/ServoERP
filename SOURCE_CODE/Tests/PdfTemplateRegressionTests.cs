using System;
using System.Drawing;
using System.IO;
using HVAC_Pro_Desktop.Models;
using ServoERP.Infrastructure;

namespace HVAC_Pro_Desktop.Tests
{
    /// <summary>Produces deterministic sample documents for the separate rendered-PDF regression gate.</summary>
    public static class PdfTemplateRegressionTests
    {
        public static string WriteReport()
        {
            string directory = Path.Combine(@"C:\HVAC_PRO_MSE", "TEST_RESULTS", "pdf-regression", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(directory);
            string reportPath = Path.Combine(directory, "report.txt");

            try
            {
                string invoicePath = Path.Combine(directory, "invoice-template-regression.pdf");
                string jobCardPath = Path.Combine(directory, "job-card-template-regression.pdf");
                File.WriteAllBytes(invoicePath, PDFGenerator.GenerateInvoice(BuildInvoice()));
                File.WriteAllBytes(jobCardPath, PDFGenerator.GenerateJobCard(BuildJobCard()));
                AssertPdf(invoicePath, "invoice");
                AssertPdf(jobCardPath, "job card");
                File.WriteAllLines(reportPath, new[]
                {
                    "PASS PDF template regression documents generated",
                    "PDF " + invoicePath,
                    "PDF " + jobCardPath
                });
            }
            catch (Exception ex)
            {
                File.WriteAllText(reportPath, "FAIL " + ex);
            }

            return reportPath;
        }

        private static Invoice BuildInvoice()
        {
            return new Invoice
            {
                InvoiceTitle = "TAX INVOICE",
                InvoiceNumber = "PDF-REG-0001",
                InvoiceDate = new DateTime(2026, 8, 10),
                ClientName = "Madhusuman Enterprises - Long Customer Name Regression Check",
                SiteName = "Pune Service Location",
                SubTotal = 12500m,
                TaxAmount = 2250m,
                TotalAmount = 14750m,
                LineItems = new System.Collections.Generic.List<InvoiceLineItem>
                {
                    new InvoiceLineItem { Description = "Copper pipe supply, installation and pressure testing", Quantity = 10m, Rate = 1250m, Amount = 12500m }
                }
            };
        }

        private static JobDetailDto BuildJobCard()
        {
            return new JobDetailDto
            {
                Job = new Job { JobNumber = "JOB-PDF-0001", Priority = "High", Status = "Scheduled", ScheduledDate = new DateTime(2026, 8, 11), Notes = "Verify layout, long values, and service notes." },
                Client = new B2BClient { CompanyName = "Madhusuman Enterprises - Long Customer Name Regression Check" },
                Site = new ClientSite { SiteName = "Pune Service Location" },
                Technician = new Employee { Name = "ServoERP QA Technician" }
            };
        }

        private static void AssertPdf(string path, string name)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 1024 || bytes[0] != (byte)'%' || bytes[1] != (byte)'P' || bytes[2] != (byte)'D' || bytes[3] != (byte)'F')
                throw new InvalidOperationException("Generated " + name + " is not a valid PDF payload.");
        }
    }
}

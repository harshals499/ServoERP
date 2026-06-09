using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ServoERP.Infrastructure
{
    /// <summary>Generates null-safe ServoERP PDF documents through QuestPDF.</summary>
    public static class PDFGenerator
    {
        private static readonly CultureInfo India = new CultureInfo("en-IN");

        /// <summary>Generates a job card PDF and returns the bytes.</summary>
        public static byte[] GenerateJobCard(JobDetailDto detail)
        {
            Job job = detail == null ? null : detail.Job;
            B2BClient client = detail == null ? null : detail.Client;
            ClientSite site = detail == null ? null : detail.Site;
            Employee technician = detail == null ? null : detail.Technician;

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Lato"));
                    page.Header().Element(c => ComposeHeader(c, "Job Card"));
                    page.Content().Column(col =>
                    {
                        col.Spacing(10);
                        col.Item().Element(c => ComposeKeyValueTable(c, new[]
                        {
                            Pair("Job Number", Safe(job == null ? null : job.JobNumber)),
                            Pair("Client", Safe(client == null ? null : client.CompanyName)),
                            Pair("Site", Safe(site == null ? null : SiteService.GetDisplayName(site))),
                            Pair("Technician", Safe(technician == null ? null : technician.Name, "Not assigned")),
                            Pair("Priority", Safe(job == null ? null : job.Priority)),
                            Pair("Status", Safe(job == null ? null : (job.PipelineStatus ?? job.Status))),
                            Pair("Scheduled", FormatDate(job == null ? (DateTime?)null : job.ScheduledDate)),
                            Pair("Completed", FormatDate(job == null ? null : job.CompletedDate)),
                            Pair("Work Done", Safe(job == null ? null : job.Notes))
                        }));
                    });
                    page.Footer().AlignCenter().Text("ServoERP | Made in India | Page ").FontSize(9);
                });
            }).GeneratePdf();
        }

        /// <summary>Generates an invoice PDF and returns the bytes.</summary>
        public static byte[] GenerateInvoice(Invoice invoice)
        {
            invoice = invoice ?? new Invoice();

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Lato"));
                    page.Header().Element(c => ComposeHeader(c, Safe(invoice.InvoiceTitle, "TAX INVOICE")));
                    page.Content().Column(col =>
                    {
                        col.Spacing(10);
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text("Invoice No: " + Safe(invoice.InvoiceNumber)).Bold();
                            row.RelativeItem().AlignRight().Text("Date: " + FormatDate(invoice.InvoiceDate));
                        });
                        col.Item().Text("Bill To: " + Safe(invoice.ClientName)).Bold();
                        col.Item().Text("Site: " + Safe(invoice.SiteName));

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(28);
                                columns.RelativeColumn(4);
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            table.Header(header =>
                            {
                                HeaderCell(header.Cell(), "#");
                                HeaderCell(header.Cell(), "Description");
                                HeaderCell(header.Cell(), "Qty");
                                HeaderCell(header.Cell(), "Rate");
                                HeaderCell(header.Cell(), "Amount");
                            });

                            int index = 1;
                            foreach (InvoiceLineItem item in invoice.LineItems ?? Enumerable.Empty<InvoiceLineItem>())
                            {
                                BodyCell(table.Cell(), index++.ToString(CultureInfo.InvariantCulture));
                                BodyCell(table.Cell(), Safe(item.Description));
                                BodyCell(table.Cell(), item.Quantity.ToString("N2", India));
                                BodyCell(table.Cell(), item.Rate.ToString("N2", India));
                                BodyCell(table.Cell(), item.Amount.ToString("N2", India));
                            }
                        });

                        col.Item().AlignRight().Column(totals =>
                        {
                            totals.Item().Text("Subtotal: " + invoice.SubTotal.ToString("C2", India));
                            totals.Item().Text("GST: " + invoice.TaxAmount.ToString("C2", India));
                            totals.Item().Text("Total: " + invoice.TotalAmount.ToString("C2", India)).Bold();
                        });
                    });
                    page.Footer().AlignCenter().Text("ServoERP | Made in India").FontSize(9);
                });
            }).GeneratePdf();
        }

        /// <summary>Writes a PDF byte array to temp storage and opens it in the default system viewer.</summary>
        public static string OpenPDF(byte[] pdfBytes, string filename)
        {
            string safeFilename = string.IsNullOrWhiteSpace(filename) ? "ServoERP.pdf" : filename;
            string folder = Path.Combine(Path.GetTempPath(), "ServoERP", "QuestPDF");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, safeFilename);
            File.WriteAllBytes(path, pdfBytes ?? new byte[0]);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return path;
        }

        private static Tuple<string, string> Pair(string label, string value)
        {
            return Tuple.Create(label, value);
        }

        private static void ComposeHeader(IContainer container, string title)
        {
            container.Column(col =>
            {
                col.Item().Text("ServoERP").FontSize(18).Bold();
                col.Item().Text(Safe(title)).FontSize(13).FontColor(Colors.Grey.Darken2);
                col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            });
        }

        private static void ComposeKeyValueTable(IContainer container, Tuple<string, string>[] rows)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn(2);
                });

                foreach (Tuple<string, string> row in rows)
                {
                    BodyCell(table.Cell(), row.Item1, true);
                    BodyCell(table.Cell(), row.Item2, false);
                }
            });
        }

        private static void HeaderCell(IContainer cell, string text)
        {
            cell.Background(Colors.Grey.Lighten3).Padding(5).Text(text).Bold();
        }

        private static void BodyCell(IContainer cell, string text)
        {
            BodyCell(cell, text, false);
        }

        private static void BodyCell(IContainer cell, string text, bool bold)
        {
            var descriptor = cell.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(Safe(text));
            if (bold)
                descriptor.Bold();
        }

        private static string Safe(string value, string fallback = "-")
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string FormatDate(DateTime value)
        {
            return value == DateTime.MinValue ? "-" : value.ToString("dd/MM/yyyy", India);
        }

        private static string FormatDate(DateTime? value)
        {
            return value.HasValue ? value.Value.ToString("dd/MM/yyyy", India) : "-";
        }
    }
}

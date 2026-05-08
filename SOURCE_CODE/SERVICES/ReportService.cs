using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public class ReportService
    {
        private ContractRepository _contractRepo;
        private InvoiceRepository _invoiceRepo;
        private ClientRepository _clientRepo;
        private InvoiceService _invoiceService;

        public ReportService()
        {
            _contractRepo = new ContractRepository();
            _invoiceRepo = new InvoiceRepository();
            _clientRepo = new ClientRepository();
            _invoiceService = new InvoiceService();
        }

        public DataTable GenerateRevenueForecasting(int monthsAhead)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("Month", typeof(string));
            dt.Columns.Add("Forecasted Revenue (₹)", typeof(decimal));
            dt.Columns.Add("Annual Run Rate (₹)", typeof(decimal));

            decimal mrr = _contractRepo.GetMonthlyRecurringRevenue();

            for (int i = 0; i < monthsAhead; i++)
            {
                DataRow row = dt.NewRow();
                row["Month"] = DateTime.Now.AddMonths(i).ToString("MMM yyyy");
                row["Forecasted Revenue (₹)"] = mrr;
                row["Annual Run Rate (₹)"] = mrr * 12;
                dt.Rows.Add(row);
            }

            return dt;
        }

        public DataTable GenerateContractSummary()
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("Status", typeof(string));
            dt.Columns.Add("Count", typeof(int));
            dt.Columns.Add("Total Value (₹)", typeof(decimal));

            List<AMCContract> all = _contractRepo.GetAll();
            Dictionary<string, (int count, decimal value)> summary = new Dictionary<string, (int, decimal)>();

            foreach (AMCContract c in all)
            {
                string status = c.ContractStatus ?? "Unknown";
                if (!summary.ContainsKey(status))
                    summary[status] = (0, 0);
                summary[status] = (summary[status].count + 1, summary[status].value + c.AnnualValue);
            }

            foreach (var kvp in summary)
            {
                DataRow row = dt.NewRow();
                row["Status"] = kvp.Key;
                row["Count"] = kvp.Value.count;
                row["Total Value (₹)"] = kvp.Value.value;
                dt.Rows.Add(row);
            }

            return dt;
        }

        public DataTable GenerateInvoiceSummary()
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("Status", typeof(string));
            dt.Columns.Add("Count", typeof(int));
            dt.Columns.Add("Total Amount (₹)", typeof(decimal));

            List<Invoice> all = _invoiceRepo.GetAll();
            Dictionary<string, (int count, decimal amount)> summary = new Dictionary<string, (int, decimal)>();

            foreach (Invoice inv in all)
            {
                string status = inv.PaymentStatus ?? "Unknown";
                if (!summary.ContainsKey(status))
                    summary[status] = (0, 0);
                summary[status] = (summary[status].count + 1, summary[status].amount + inv.TotalAmount);
            }

            foreach (var kvp in summary)
            {
                DataRow row = dt.NewRow();
                row["Status"] = kvp.Key;
                row["Count"] = kvp.Value.count;
                row["Total Amount (₹)"] = kvp.Value.amount;
                dt.Rows.Add(row);
            }

            return dt;
        }

        public DataTable GenerateClientRevenueReport()
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("Client", typeof(string));
            dt.Columns.Add("Industry", typeof(string));
            dt.Columns.Add("Annual Value (₹)", typeof(decimal));
            dt.Columns.Add("Monthly Value (₹)", typeof(decimal));

            List<B2BClient> clients = _clientRepo.GetAll();
            List<AMCContract> contracts = _contractRepo.GetAll();

            foreach (B2BClient client in clients)
            {
                decimal annualTotal = 0;
                decimal monthlyTotal = 0;

                foreach (AMCContract c in contracts)
                {
                    if (c.ClientID == client.ClientID && c.ContractStatus == "Active")
                    {
                        annualTotal += c.AnnualValue;
                        monthlyTotal += c.MonthlyValue;
                    }
                }

                if (annualTotal > 0)
                {
                    DataRow row = dt.NewRow();
                    row["Client"] = client.CompanyName;
                    row["Industry"] = client.IndustryType;
                    row["Annual Value (₹)"] = annualTotal;
                    row["Monthly Value (₹)"] = monthlyTotal;
                    dt.Rows.Add(row);
                }
            }

            return dt;
        }

        public DataTable GeneratePendingClientChargesReport(bool unbilledOnly)
        {
            return _invoiceService.GetPendingChargesReport(unbilledOnly);
        }

        public void ExportPendingClientChargesCsv(string filePath, bool unbilledOnly)
        {
            DataTable table = GeneratePendingClientChargesReport(unbilledOnly);
            var lines = new List<string>();
            lines.Add(string.Join(",", table.Columns.Cast<DataColumn>().Select(c => EscapeCsv(c.ColumnName))));
            foreach (DataRow row in table.Rows)
                lines.Add(string.Join(",", table.Columns.Cast<DataColumn>().Select(c => EscapeCsv(Convert.ToString(row[c])))));
            File.WriteAllLines(filePath, lines, Encoding.UTF8);
        }

        private static string EscapeCsv(string value)
        {
            string text = value ?? string.Empty;
            if (text.Contains(",") || text.Contains("\"") || text.Contains("\n"))
                return "\"" + text.Replace("\"", "\"\"") + "\"";
            return text;
        }
    }
}

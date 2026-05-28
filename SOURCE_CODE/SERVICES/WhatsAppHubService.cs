using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class WhatsAppHubService
    {
        private static readonly string LogPath = @"C:\HVAC_PRO_MSE\LOGS\whatsapp_actions.csv";

        public List<WhatsAppContact> LoadContacts()
        {
            List<WhatsAppContact> contacts = new List<WhatsAppContact>();
            try
            {
                List<Invoice> invoices = Safe(() => new InvoiceService().GetAllInvoices(), new List<Invoice>());
                List<Job> jobs = Safe(() => new JobService().GetAll(), new List<Job>());

                foreach (B2BClient client in Safe(() => new ClientService().GetAllClients(), new List<B2BClient>()))
                {
                    if (client == null || string.IsNullOrWhiteSpace(client.CompanyName))
                        continue;

                    contacts.Add(new WhatsAppContact
                    {
                        SourceType = "Client",
                        SourceId = client.ClientID,
                        Name = client.CompanyName,
                        Phone = FirstNonEmpty(client.Phone, client.SecondaryContact),
                        Email = client.Email,
                        Location = FirstNonEmpty(client.City, client.BillingAddress),
                        LastMessage = "Manual WhatsApp action ready",
                        LastMessageAt = DateTime.Now.AddMinutes(-contacts.Count * 37 - 12),
                        UnreadCount = 0,
                        InvoiceCount = invoices.Count(i => i.ClientID == client.ClientID),
                        JobCount = jobs.Count(j => j.ClientID == client.ClientID)
                    });
                }

                foreach (Vendor vendor in Safe(() => new VendorService().GetAll(), new List<Vendor>()))
                {
                    if (vendor == null || string.IsNullOrWhiteSpace(vendor.VendorName))
                        continue;

                    contacts.Add(new WhatsAppContact
                    {
                        SourceType = "Vendor",
                        SourceId = vendor.VendorID,
                        Name = vendor.VendorName,
                        Phone = FirstNonEmpty(vendor.WhatsAppNumber, vendor.Phone),
                        Email = vendor.Email,
                        Location = FirstNonEmpty(vendor.City, vendor.Address),
                        LastMessage = "Manual WhatsApp action ready",
                        LastMessageAt = DateTime.Now.AddHours(-contacts.Count - 1),
                        UnreadCount = 0
                    });
                }

                foreach (Employee employee in Safe(() => new EmployeeService().GetActiveTechnicians(), new List<Employee>()))
                {
                    if (employee == null || string.IsNullOrWhiteSpace(employee.Name))
                        continue;

                    contacts.Add(new WhatsAppContact
                    {
                        SourceType = "Team",
                        SourceId = employee.EmployeeID,
                        Name = employee.Name,
                        Phone = FirstNonEmpty(employee.WhatsAppNumber, employee.Phone),
                        Email = string.Empty,
                        Location = FirstNonEmpty(employee.ClientSite, employee.Department),
                        LastMessage = "Manual WhatsApp action ready",
                        LastMessageAt = DateTime.Now.AddHours(-contacts.Count - 2),
                        UnreadCount = 0,
                        JobCount = jobs.Count(j => j.AssignedEmployeeID == employee.EmployeeID)
                    });
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("WhatsAppHubService.LoadContacts", ex);
            }

            if (contacts.Count == 0)
                contacts.AddRange(BuildSampleContacts());

            return contacts
                .GroupBy(c => (c.SourceType + ":" + c.SourceId + ":" + c.Name).ToUpperInvariant())
                .Select(g => g.First())
                .OrderByDescending(c => c.LastMessageAt)
                .Take(40)
                .ToList();
        }

        public List<WhatsAppTemplate> LoadTemplates()
        {
            return new List<WhatsAppTemplate>
            {
                Template("Invoice sent", "Invoice sent", "Hi {{CustomerName}},\n\nYour invoice {{InvoiceNo}} of {{InvoiceAmount}} is ready. Due date: {{DueDate}}.\n\nRegards,\n{{CompanyName}}", "invoice", "customer"),
                Template("Quotation sent", "Quotation sent", "Hi {{CustomerName}},\n\nWe have shared quotation {{QuotationNo}} for {{QuotationAmount}}. Please review and confirm.\n\n{{CompanyName}}", "quotation", "sales"),
                Template("Payment reminder", "Payment reminder", "Hi {{CustomerName}},\n\nThis is a reminder for invoice {{InvoiceNo}} amount {{InvoiceAmount}} due on {{DueDate}}. Please arrange payment when possible.\n\n{{CompanyName}}", "payment", "invoice"),
                Template("Payment received", "Payment received", "Hi {{CustomerName}},\n\nPayment received for {{InvoiceNo}}. Thank you.\n\n{{CompanyName}}", "payment", "receipt"),
                Template("Job scheduled", "Job scheduled", "Hi {{CustomerName}},\n\nYour job {{JobNo}} is scheduled. Technician: {{TechnicianName}}. ETA: {{ETA}}.\nSite: {{SiteAddress}}", "job", "technician"),
                Template("Technician on the way", "Technician on the way", "Hi {{CustomerName}},\n\nTechnician {{TechnicianName}} is on the way. ETA: {{ETA}}.\n\n{{CompanyName}}", "job", "technician"),
                Template("Job completed", "Job completed", "Hi {{CustomerName}},\n\nJob {{JobNo}} has been completed. Please confirm if everything is working as expected.\n\n{{CompanyName}}", "job", "completion"),
                Template("AMC visit reminder", "AMC visit reminder", "Hi {{CustomerName}},\n\nThis is a reminder for your AMC visit {{AMCNo}}. Scheduled technician: {{TechnicianName}}.\n\n{{CompanyName}}", "amc", "service"),
                Template("AMC renewal reminder", "AMC renewal reminder", "Hi {{CustomerName}},\n\nYour AMC {{AMCNo}} expires on {{AMCExpiryDate}}. Please contact us for renewal.\n\n{{CompanyName}}", "amc", "renewal"),
                Template("General update", "General update", "Hi {{CustomerName}},\n\nSharing an update from {{CompanyName}}.\n\nThank you.", "general")
            };
        }

        public string RenderTemplate(WhatsAppTemplate template, WhatsAppContact contact)
        {
            string body = template == null ? string.Empty : template.Body ?? string.Empty;
            string customer = contact == null ? "Customer" : contact.Name;
            return body
                .Replace("{{CustomerName}}", customer)
                .Replace("{{CompanyName}}", "ServoERP")
                .Replace("{{InvoiceNo}}", "INV-0245")
                .Replace("{{InvoiceAmount}}", "Rs 48,750")
                .Replace("{{DueDate}}", DateTime.Today.AddDays(7).ToString("dd MMM yyyy"))
                .Replace("{{QuotationNo}}", "QTN-0188")
                .Replace("{{QuotationAmount}}", "Rs 52,000")
                .Replace("{{JobNo}}", "JOB-260510-0001")
                .Replace("{{TechnicianName}}", "Assigned Technician")
                .Replace("{{ETA}}", "Today, 4:30 PM")
                .Replace("{{SiteAddress}}", contact == null ? "Client site" : FirstNonEmpty(contact.Location, "Client site"))
                .Replace("{{AMCNo}}", "AMC-2026-001")
                .Replace("{{AMCExpiryDate}}", DateTime.Today.AddMonths(1).ToString("dd MMM yyyy"));
        }

        public string ValidatePhone(string phone)
        {
            string normalized = NormalizePhone(phone);
            if (normalized.Length < 10)
                return string.Empty;
            return normalized;
        }

        public string BuildWhatsAppUrl(string phone, string message)
        {
            string normalizedPhone = ValidatePhone(phone);
            if (string.IsNullOrWhiteSpace(normalizedPhone))
                throw new InvalidOperationException("Please add a valid WhatsApp phone number before sending.");

            return "https://wa.me/" + normalizedPhone + "?text=" + HttpUtility.UrlEncode(message ?? string.Empty);
        }

        public string BuildWhatsAppWebUrl(string phone, string message)
        {
            string normalizedPhone = ValidatePhone(phone);
            if (string.IsNullOrWhiteSpace(normalizedPhone))
                throw new InvalidOperationException("Please add a valid WhatsApp phone number before sending.");

            return "https://web.whatsapp.com/send?phone=" + normalizedPhone + "&text=" + HttpUtility.UrlEncode(message ?? string.Empty);
        }

        public string OpenWhatsApp(WhatsAppContact contact, WhatsAppTemplate template, string message)
        {
            string url = BuildWhatsAppUrl(contact == null ? string.Empty : contact.Phone, message);
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            LogAction(BuildLog(
                contact == null ? "Contact" : contact.SourceType,
                contact == null ? 0 : contact.SourceId,
                contact == null ? string.Empty : contact.Name,
                contact == null ? string.Empty : contact.Phone,
                template == null ? "General update" : template.TemplateType,
                message,
                ResolveLinkedRecord(template),
                ResolveLinkedRecordType(template),
                0,
                "Opened"));
            return url;
        }

        public WhatsAppActionLog BuildLog(WhatsAppQuickActionContext context, string status)
        {
            if (context == null)
                context = new WhatsAppQuickActionContext();

            return BuildLog(
                context.Module,
                context.SourceId,
                context.ContactName,
                context.Phone,
                context.TemplateType,
                context.Message,
                context.LinkedRecord,
                context.LinkedRecordType,
                context.LinkedRecordId,
                status);
        }

        public WhatsAppActionLog BuildLog(string module, int sourceId, string contactName, string phone, string templateType, string message, string linkedRecord, string linkedRecordType, int linkedRecordId, string status)
        {
            return new WhatsAppActionLog
            {
                ActionId = Guid.NewGuid().ToString("N"),
                ActionDate = DateTime.Now,
                User = SessionManager.CurrentUser == null ? Environment.UserName : (SessionManager.CurrentUser.DisplayName ?? SessionManager.CurrentUser.Username),
                ContactName = contactName ?? string.Empty,
                Phone = ValidatePhone(phone),
                Module = module ?? string.Empty,
                SourceId = sourceId,
                TemplateType = templateType ?? "General update",
                Message = message ?? string.Empty,
                LinkedRecord = linkedRecord ?? string.Empty,
                LinkedRecordType = linkedRecordType ?? string.Empty,
                LinkedRecordId = linkedRecordId,
                Status = status ?? "Prepared"
            };
        }

        public void LogAction(WhatsAppActionLog log)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath) ?? @"C:\HVAC_PRO_MSE\LOGS");
            bool writeHeader = !File.Exists(LogPath);
            using (StreamWriter writer = new StreamWriter(LogPath, true, Encoding.UTF8))
            {
                if (writeHeader)
                    writer.WriteLine("ActionId,DateTime,User,ContactName,Phone,Module,SourceId,TemplateType,LinkedRecordType,LinkedRecordId,LinkedRecord,Status,Message");
                writer.WriteLine(string.Join(",", new[]
                {
                    Csv(log.ActionId),
                    Csv(log.ActionDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                    Csv(log.User),
                    Csv(log.ContactName),
                    Csv(log.Phone),
                    Csv(log.Module),
                    Csv(log.SourceId.ToString(CultureInfo.InvariantCulture)),
                    Csv(log.TemplateType),
                    Csv(log.LinkedRecordType),
                    Csv(log.LinkedRecordId.ToString(CultureInfo.InvariantCulture)),
                    Csv(log.LinkedRecord),
                    Csv(log.Status),
                    Csv(log.Message)
                }));
            }
            AppLogger.LogInfo("WhatsApp Hub action logged: " + log.TemplateType + " to " + log.ContactName + " [" + log.Status + "]");
        }

        public List<WhatsAppActionLog> LoadRecentActions(int take = 8)
        {
            List<WhatsAppActionLog> logs = new List<WhatsAppActionLog>();
            if (!File.Exists(LogPath))
                return logs;

            try
            {
                foreach (string line in File.ReadLines(LogPath).Skip(1).Reverse().Take(Math.Max(1, take)))
                {
                    List<string> cells = ParseCsvLine(line);
                    if (cells.Count >= 13)
                    {
                        logs.Add(new WhatsAppActionLog
                        {
                            ActionId = cells[0],
                            ActionDate = ParseDate(cells[1]),
                            User = cells[2],
                            ContactName = cells[3],
                            Phone = cells[4],
                            Module = cells[5],
                            SourceId = ParseInt(cells[6]),
                            TemplateType = cells[7],
                            LinkedRecordType = cells[8],
                            LinkedRecordId = ParseInt(cells[9]),
                            LinkedRecord = cells[10],
                            Status = cells[11],
                            Message = cells[12]
                        });
                    }
                    else if (cells.Count >= 8)
                    {
                        logs.Add(new WhatsAppActionLog
                        {
                            ActionDate = ParseDate(cells[0]),
                            User = cells[1],
                            ContactName = cells[2],
                            Phone = cells[3],
                            Module = cells[4],
                            TemplateType = cells[5],
                            LinkedRecord = cells[6],
                            Status = cells[7]
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("WhatsAppHubService.LoadRecentActions", ex);
            }

            return logs;
        }

        private static string ResolveLinkedRecord(WhatsAppTemplate template)
        {
            string type = template == null ? string.Empty : template.TemplateType ?? string.Empty;
            if (type.IndexOf("Invoice", StringComparison.OrdinalIgnoreCase) >= 0 || type.IndexOf("Payment", StringComparison.OrdinalIgnoreCase) >= 0)
                return "INV-0245";
            if (type.IndexOf("Quotation", StringComparison.OrdinalIgnoreCase) >= 0)
                return "QTN-0188";
            if (type.IndexOf("Job", StringComparison.OrdinalIgnoreCase) >= 0 || type.IndexOf("Technician", StringComparison.OrdinalIgnoreCase) >= 0)
                return "JOB-260510-0001";
            if (type.IndexOf("AMC", StringComparison.OrdinalIgnoreCase) >= 0)
                return "AMC-2026-001";
            return string.Empty;
        }

        private static string ResolveLinkedRecordType(WhatsAppTemplate template)
        {
            string type = template == null ? string.Empty : template.TemplateType ?? string.Empty;
            if (type.IndexOf("Invoice", StringComparison.OrdinalIgnoreCase) >= 0 || type.IndexOf("Payment", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Invoice";
            if (type.IndexOf("Quotation", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Quotation";
            if (type.IndexOf("Job", StringComparison.OrdinalIgnoreCase) >= 0 || type.IndexOf("Technician", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Job";
            if (type.IndexOf("AMC", StringComparison.OrdinalIgnoreCase) >= 0)
                return "AMC";
            return string.Empty;
        }

        private static WhatsAppTemplate Template(string type, string title, string body, params string[] tags)
        {
            return new WhatsAppTemplate { TemplateType = type, Title = title, Body = body, Tags = tags.ToList() };
        }

        private static string NormalizePhone(string phone)
        {
            string digits = Regex.Replace(phone ?? string.Empty, "[^0-9]", string.Empty);
            if (digits.Length == 10)
                return "91" + digits;
            if (digits.StartsWith("0") && digits.Length == 11)
                return "91" + digits.Substring(1);
            return digits;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            return string.Empty;
        }

        private static T Safe<T>(Func<T> action, T fallback)
        {
            try { return action(); }
            catch { return fallback; }
        }

        private static string Csv(string value)
        {
            value = value ?? string.Empty;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static List<string> ParseCsvLine(string line)
        {
            List<string> values = new List<string>();
            StringBuilder current = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < (line ?? string.Empty).Length; i++)
            {
                char ch = line[i];
                if (ch == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                }
                else if (ch == ',' && !quoted)
                {
                    values.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(ch);
                }
            }
            values.Add(current.ToString());
            return values;
        }

        private static DateTime ParseDate(string value)
        {
            DateTime parsed;
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsed) ? parsed : DateTime.Now;
        }

        private static int ParseInt(string value)
        {
            int parsed;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : 0;
        }

        private static IEnumerable<WhatsAppContact> BuildSampleContacts()
        {
            return new[]
            {
                new WhatsAppContact { SourceType = "Client", Name = "ABC Engineering Pvt. Ltd.", Phone = "9876543210", Email = "accounts@abcengineering.co.in", Location = "Mumbai, Maharashtra", LastMessage = "Manual WhatsApp action ready", LastMessageAt = DateTime.Now.AddMinutes(-20), InvoiceCount = 5, QuoteCount = 3, JobCount = 2, ContractCount = 1 },
                new WhatsAppContact { SourceType = "Vendor", Name = "Sharma Industries", Phone = "9876500011", Email = "sales@sharma.example", Location = "Pune", LastMessage = "Manual WhatsApp action ready", LastMessageAt = DateTime.Now.AddHours(-2) },
                new WhatsAppContact { SourceType = "Vendor", Name = "Kumar Electricals", Phone = "9876500012", LastMessage = "Manual WhatsApp action ready", LastMessageAt = DateTime.Now.AddDays(-1) },
                new WhatsAppContact { SourceType = "Client", Name = "Global Cooling Systems", Phone = "9876500013", LastMessage = "Manual WhatsApp action ready", LastMessageAt = DateTime.Now.AddDays(-1) },
                new WhatsAppContact { SourceType = "Team", Name = "Techno Services", Phone = "9876500014", LastMessage = "Manual WhatsApp action ready", LastMessageAt = DateTime.Now.AddDays(-2) }
            };
        }
    }
}

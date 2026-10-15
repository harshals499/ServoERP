using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Dapper;
using OfficeOpenXml;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.UI;

namespace HVAC_Pro_Desktop.Tests
{
    public static class FinancialRecordProtectionSmokeTests
    {
        private sealed class DatabaseFile { public string Name { get; set; } public string PhysicalName { get; set; } }
        public static string RunAll()
        {
            string name = "ServoERP_FinanceQA_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var builder = new SqlConnectionStringBuilder(DatabaseManager.RequireConfiguredConnectionString());
            string source = builder.InitialCatalog;
            using (var connection = new SqlConnection(builder.ConnectionString))
            {
                connection.Open();
                string directory = connection.QuerySingle<string>("SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath'))");
                string backup = Path.Combine(directory, name + ".bak");
                // Backup/restore and metadata are intentionally SQL DDL. Restore never replaces the authoritative database.
                connection.Execute("BACKUP DATABASE [" + source.Replace("]", "]]") + "] TO DISK=@Backup WITH COPY_ONLY,CHECKSUM", new { Backup = backup }, commandTimeout: 600);
                VerifyBackup(connection, backup);
                string restore = "RESTORE DATABASE [" + name + "] FROM DISK=@Backup WITH ";
                var parameters = new DynamicParameters(new { Backup = backup });
                int index = 0;
                foreach (var file in connection.Query<DatabaseFile>("SELECT name Name,physical_name PhysicalName FROM sys.database_files ORDER BY file_id"))
                {
                    if (index > 0) restore += ",";
                    restore += "MOVE @Logical" + index + " TO @Target" + index;
                    parameters.Add("Logical" + index, file.Name);
                    parameters.Add("Target" + index, Path.Combine(Path.GetDirectoryName(file.PhysicalName), name + "_" + index + Path.GetExtension(file.PhysicalName)));
                    index++;
                }
                connection.Execute(restore, parameters, commandTimeout: 600);
            }
            builder.InitialCatalog = name;
            string cs = builder.ConnectionString;
            DatabaseUpgradeService.Run(cs, "1.1.510.0", () => { }, DatabaseUpgradeService.ReadScripts(), false);
            var originalUser = SessionManager.CurrentUser;
            try
            {
                DatabaseConnectionFactory.DiagnosticConnectionString = cs;
                SessionManager.SetSession(new AppUserDto { UserId = 999, Username = "finance-qa", DisplayName = "Finance QA" });
                var invoices = new InvoiceRepository();
                var jobs = new JobRepository();
                using (var c = DatabaseConnectionFactory.CreateConnection())
                {
                    c.Open();
                    int client = c.QueryFirst<int>("SELECT TOP(1) ClientID FROM dbo.B2BClients ORDER BY ClientID");
                    int site = c.QueryFirstOrDefault<int>("SELECT TOP(1) SiteID FROM dbo.ClientSites WHERE ClientID=@Client", new { Client = client });
                    var inv = new Invoice { ClientID = client, SiteID = site, InvoiceNumber = name,
                        InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), PaymentStatus = "Draft", SubTotal = 100,
                        TaxAmount = 18, GSTPercent = 18, TotalAmount = 118, BalanceDue = 118,
                        LineItems = new List<InvoiceLineItem> { new InvoiceLineItem { Description = "QA service", Quantity = 1, Rate = 100, Amount = 100, TaxAmount = 18, GSTPercent = 18, IsBillable = true } } };
                    inv.InvoiceID = invoices.Create(inv);
                    Assert(!invoices.GetById(inv.InvoiceID).IsPosted, "Draft was posted");
                    inv.LineItems[0].Rate = 200; inv.LineItems[0].Amount = 200; inv.LineItems[0].TaxAmount = 36;
                    inv.SubTotal = 200; inv.TaxAmount = 36; inv.TotalAmount = 236; inv.BalanceDue = 236;
                    invoices.UpdateWithLineItems(inv);
                    Assert(c.QuerySingle<int>("SELECT COUNT(*) FROM dbo.FinancialRecordAudit WHERE Module='Invoices' AND RecordId=@Id AND JSON_VALUE(OldValues,'$.TotalAmount')='118.00' AND JSON_VALUE(NewValues,'$.TotalAmount')='236.00' AND UserName='Finance QA'", new { Id = inv.InvoiceID }) > 0, "Old/new financial values or actor missing");
                    inv.PaymentStatus = "Pending"; invoices.Update(inv);
                    Assert(invoices.GetById(inv.InvoiceID).IsPosted, "Final posting did not lock invoice");
                    ExpectBlocked(() => invoices.SaveLineItems(inv.InvoiceID, inv.LineItems), "Repository line edit accepted");
                    inv.TotalAmount = 999;
                    ExpectBlocked(() => invoices.Update(inv), "Posted header edit accepted");
                    inv.TotalAmount = 236;
                    ExpectBlocked(() => c.Execute("UPDATE dbo.InvoiceLineItems SET Rate=999 WHERE InvoiceID=@Id", new { Id = inv.InvoiceID }), "Direct SQL line edit accepted");
                    ExpectBlocked(() => c.Execute("UPDATE dbo.Invoices SET IsPosted=0 WHERE InvoiceID=@Id", new { Id = inv.InvoiceID }), "Unposting accepted");
                    ExpectBlocked(() => c.Execute("DELETE dbo.Invoices WHERE InvoiceID=@Id", new { Id = inv.InvoiceID }), "Physical invoice delete accepted");
                    invoices.UpdatePaymentStatus(inv.InvoiceID, 50, "Partial");
                    Assert(c.QuerySingle<decimal>("SELECT TotalAmount FROM dbo.Invoices WHERE InvoiceID=@Id", new { Id = inv.InvoiceID }) == 236, "Blocked edit leaked changes");
                    c.Execute("INSERT dbo.Payments(PaymentNumber,InvoiceID,ClientID,AmountPaid,PaymentDate) VALUES(@Number,@Id,@Client,50,GETDATE())", new { Number = name, Id = inv.InvoiceID, Client = client });
                    var stockInvoice = new Invoice { ClientID = client, SiteID = site, InvoiceNumber = name + "-STOCK", InvoiceDate = DateTime.Today,
                        DueDate = DateTime.Today.AddDays(30), PaymentStatus = "Draft", TotalAmount = 100, SubTotal = 100, BalanceDue = 100 };
                    int stock = c.QuerySingle<int>("INSERT dbo.StockItems(ItemName,CurrentStock,ReservedStock) OUTPUT inserted.ItemID VALUES('QA protected stock',10,0)");
                    stockInvoice.LineItems = new List<InvoiceLineItem> { new InvoiceLineItem { Description = "QA stock", StockItemID = stock, IsStockItem = true, IsBillable = true, Quantity = 2, Rate = 50, Amount = 100 } };
                    stockInvoice.InvoiceID = invoices.Create(stockInvoice);
                    var inventory = new InvoiceInventoryService();
                    inventory.SyncDraftReservations(stockInvoice.InvoiceID, stockInvoice.LineItems);
                    Assert(c.QuerySingle<decimal>("SELECT ReservedStock FROM dbo.StockItems WHERE ItemID=@Id", new { Id = stock }) == 2, "Draft reservation missing");
                    invoices.Delete(stockInvoice.InvoiceID);
                    Assert(c.QuerySingle<decimal>("SELECT ReservedStock FROM dbo.StockItems WHERE ItemID=@Id", new { Id = stock }) == 0, "Archive did not release draft reservation");
                    Assert(c.QuerySingle<decimal>("SELECT CurrentStock FROM dbo.StockItems WHERE ItemID=@Id", new { Id = stock }) == 10, "Draft archive changed physical stock");
                    FinancialRecordProtection.Restore("Invoices", stockInvoice.InvoiceID);
                    inventory.SyncDraftReservations(stockInvoice.InvoiceID, stockInvoice.LineItems);
                    stockInvoice.PaymentStatus = "Pending"; stockInvoice.InventoryReservationStatus = "Issued";
                    inventory.FinalizeInvoice(stockInvoice);
                    Assert(invoices.GetById(stockInvoice.InvoiceID).IsPosted, "Stock posting did not lock header");
                    invoices.Delete(stockInvoice.InvoiceID);
                    Assert(c.QuerySingle<decimal>("SELECT CurrentStock FROM dbo.StockItems WHERE ItemID=@Id", new { Id = stock }) == 8, "Posted archive restored issued stock");
                    Assert(c.QuerySingle<decimal>("SELECT SUM(QuantityIssued) FROM dbo.InvoiceInventoryReservations WHERE InvoiceID=@Id", new { Id = stockInvoice.InvoiceID }) == 2, "Archive deleted issued stock history");
                    ExpectBlocked(() => inventory.CancelReservations(stockInvoice.InvoiceID), "Posted inventory cancellation accepted");
                    var job = new Job { ClientID = client, SiteID = site, JobNumber = name, Title = "QA preserved job", JobTitle = "QA preserved job", ScheduledDate = DateTime.Today,
                        Status = "Open", Priority = "Normal", JobType = "Service", InvoiceId = inv.InvoiceID, EstimatedCost = 20, Revenue = 200 };
                    job.JobID = jobs.Create(job);
                    c.Execute("INSERT dbo.JobChecklistItems(JobId,ItemText,IsCompleted,SortOrder) VALUES(@Id,'QA preserved checklist',0,0)", new { Id = job.JobID });
                    int lines = c.QuerySingle<int>("SELECT COUNT(*) FROM dbo.InvoiceLineItems WHERE InvoiceID=@Id", new { Id = inv.InvoiceID });
                    invoices.Delete(inv.InvoiceID);
                    Assert(c.QuerySingle<int>("SELECT COUNT(*) FROM dbo.Payments WHERE InvoiceID=@Id", new { Id = inv.InvoiceID }) == 1, "Archive deleted payment");
                    Assert(invoices.GetById(inv.InvoiceID).IsDeleted, "Archived invoice unavailable by historical ID");
                    Assert(c.QuerySingle<int>("SELECT COUNT(*) FROM dbo.ActiveInvoices WHERE InvoiceID=@Id", new { Id = inv.InvoiceID }) == 0, "Archived invoice remains active");
                    Assert(c.QuerySingle<decimal>("SELECT BalanceDue FROM dbo.FinancialInvoices WHERE InvoiceID=@Id", new { Id = inv.InvoiceID }) == 186, "Archive removed receivable");
                    Assert(c.QuerySingle<int>("SELECT COUNT(*) FROM dbo.InvoiceLineItems WHERE InvoiceID=@Id", new { Id = inv.InvoiceID }) == lines, "Archive deleted lines");
                    Assert(c.QuerySingle<int>("SELECT InvoiceId FROM dbo.Jobs WHERE JobID=@Id", new { Id = job.JobID }) == inv.InvoiceID, "Archive severed job link");
                    long count = c.QuerySingle<long>("SELECT COUNT_BIG(*) FROM dbo.FinancialRecordAudit");
                    invoices.Delete(inv.InvoiceID);
                    Assert(c.QuerySingle<long>("SELECT COUNT_BIG(*) FROM dbo.FinancialRecordAudit") == count, "Repeated archive changed data");
                    FinancialRecordProtection.Restore("Invoices", inv.InvoiceID);
                    Assert(invoices.GetById(inv.InvoiceID).IsPosted, "Restore unlocked posted invoice");
                    jobs.Delete(job.JobID);
                    Assert(c.QuerySingle<int>("SELECT COUNT(*) FROM dbo.ActiveJobs WHERE JobID=@Id", new { Id = job.JobID }) == 0, "Archived job remains active");
                    Assert(c.QuerySingle<int>("SELECT COUNT(*) FROM dbo.JobChecklistItems WHERE JobId=@Id", new { Id = job.JobID }) == 1, "Archive deleted checklist");
                    ExpectBlocked(() => jobs.Update(job), "Archived job edit accepted");
                    ExpectBlocked(() => c.Execute("DELETE dbo.Jobs WHERE JobID=@Id", new { Id = job.JobID }), "Physical job delete accepted");
                    FinancialRecordProtection.Restore("Jobs", job.JobID);
                    Assert(jobs.GetById(job.JobID).InvoiceId == inv.InvoiceID, "Job restore lost links");
                    // Fresh Start's explicit maintenance context remains valid, tested with rollback only.
                    FinancialRecordProtection.SetMaintenance(c, true);
                    using (var tx = c.BeginTransaction())
                    {
                        c.Execute("UPDATE dbo.Invoices SET TotalAmount=999 WHERE InvoiceID=@Id", new { Id = inv.InvoiceID }, tx);
                        tx.Rollback();
                    }
                    FinancialRecordProtection.StampActor(c);
                    SessionManager.SetSession(new AppUserDto { UserId = 998, Username = "second-qa", DisplayName = "Second QA" });
                    using (var pooled = DatabaseConnectionFactory.CreateConnection())
                    {
                        pooled.Open();
                        pooled.Execute("UPDATE dbo.Jobs SET Notes='Actor changed' WHERE JobID=@Id", new { Id = job.JobID });
                    }
                    Assert(c.QuerySingle<string>("SELECT TOP(1) UserName FROM dbo.FinancialRecordAudit WHERE Module='Jobs' AND RecordId=@Id ORDER BY AuditId DESC", new { Id = job.JobID }) == "Second QA", "Pooled audit actor stale");
                    string workbook = @"C:\HVAC_PRO_MSE\TEST_RESULTS\financial-import-qa.xlsx";
                    using (var package = new ExcelPackage())
                    {
                        var sheet = package.Workbook.Worksheets.Add("Invoices");
                        string[] headers = { "InvoiceNumber", "ClientName", "Description", "Amount", "TotalAmount", "TaxAmount", "Status", "InvoiceDate", "DueDate", "LineDescription", "LineQuantity", "LineRate", "LineAmount" };
                        for (int column = 0; column < headers.Length; column++) sheet.Cells[1, column + 1].Value = headers[column];
                        string company = c.QuerySingle<string>("SELECT CompanyName FROM dbo.B2BClients WHERE ClientID=@Id", new { Id = client });
                        for (int row = 2; row <= 3; row++)
                        {
                            object[] values = { name + "-IMPORT", company, "QA imported services", 200m, 236m, 36m, "Sent", DateTime.Today.ToString("dd/MM/yyyy"), DateTime.Today.AddDays(30).ToString("dd/MM/yyyy"), "QA import line " + row, 1m, 100m, 100m };
                            for (int column = 0; column < values.Length; column++) sheet.Cells[row, column + 1].Value = values[column];
                        }
                        package.SaveAs(new FileInfo(workbook));
                    }
                    var importer = new ExcelImportService();
                    var imported = importer.Import(ExcelImportModule.Invoices, workbook);
                    Assert(imported.SuccessCount == 2, "Multi-line invoice import failed: " + string.Join(";", imported.Errors));
                    int importedId = c.QuerySingle<int>("SELECT InvoiceID FROM dbo.Invoices WHERE InvoiceNumber=@Number", new { Number = name + "-IMPORT" });
                    Assert(invoices.GetById(importedId).IsPosted && invoices.GetLineItems(importedId).Count == 2, "Imported invoice posted before complete lines");
                    var reimport = importer.Import(ExcelImportModule.Invoices, workbook);
                    Assert(reimport.SuccessCount == 0 && invoices.GetLineItems(importedId).Count == 2, "Reimport overwrote posted financial records");
                    invoices.Delete(inv.InvoiceID); jobs.Delete(job.JobID);
                    using (var dialog = new FinancialRecordHistoryDialog("Invoices", inv.InvoiceID))
                    {
                        dialog.Show(); dialog.LoadForVisualTest(); Application.DoEvents();
                        Capture(dialog, "financial-history.png");
                        var tabs = dialog.Controls.OfType<TabControl>().Single(); tabs.SelectedIndex = 1; Application.DoEvents();
                        Capture(dialog, "financial-archives.png"); dialog.Close();
                    }
                    Assert(c.QuerySingle<int>("SELECT COUNT(*) FROM dbo.FinancialRecordAudit WHERE RecordId=@Id AND Module='Invoices' AND Action IN('POST','ARCHIVE','RESTORE')", new { Id = inv.InvoiceID }) >= 4, "State history incomplete");
                }
            }
            finally { DatabaseConnectionFactory.DiagnosticConnectionString = null; SessionManager.SetSession(originalUser); }
            return "PASS migration on verified restored copy, atomic draft header/lines, reservation release, restored reservations, atomic stock/posting, issued stock retention, payment record retention, multi-line import and posted reimport refusal, posting lock, direct SQL lock, irreversible unposting/delete refusal, payment compatibility, archived receivables, line/checklist/link retention, repeat archive, restore preserving posting, archived job lock, transactional maintenance rollback, pooled actor reset, old/new snapshots and history/archive visual captures. QA database retained: " + name;
        }
        private static void Capture(Form form, string file)
        {
            Directory.CreateDirectory(@"C:\HVAC_PRO_MSE\TEST_RESULTS");
            using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(Path.Combine(@"C:\HVAC_PRO_MSE\TEST_RESULTS", file)); }
        }
        private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void ExpectBlocked(Action action, string message)
        {
            try { action(); } catch (SqlException ex) { if (ex.Number == 547 || (ex.Number >= 51020 && ex.Number <= 51025)) return; throw; }
            catch (InvalidOperationException ex) { if (ex.Message.Contains("posted") || ex.Message.Contains("archived")) return; throw; }
            throw new InvalidOperationException(message);
        }
        private static void VerifyBackup(SqlConnection connection, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("The QA backup path is missing.");
            // Verify the backup identity and checksum metadata as well as physical readability.
            using (var header = new SqlCommand("RESTORE HEADERONLY FROM DISK=@Path", connection))
            {
                header.Parameters.AddWithValue("@Path", path);
                header.CommandTimeout = 600;
                using (var reader = header.ExecuteReader())
                {
                    if (!reader.Read() || !string.Equals(Convert.ToString(reader["DatabaseName"]), connection.Database, StringComparison.OrdinalIgnoreCase)
                        || Convert.ToInt32(reader["BackupType"]) != 1 || !Convert.ToBoolean(reader["HasBackupChecksums"]) || !Convert.ToBoolean(reader["IsCopyOnly"]))
                        throw new InvalidOperationException("The recovery file is not a checksum-protected full COPY_ONLY backup of this office database.");
                }
            }
            using (var command = new SqlCommand("RESTORE VERIFYONLY FROM DISK=@Path WITH CHECKSUM", connection))
            {
                command.Parameters.AddWithValue("@Path", path);
                command.CommandTimeout = 600;
                command.ExecuteNonQuery();
            }
        }

    }
}


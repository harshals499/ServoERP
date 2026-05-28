using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class SupportCenterService
    {
        private static readonly string DiagnosticsRoot = Path.Combine(@"C:\HVAC_PRO_MSE", "DIAGNOSTICS");
        private static readonly string[] LayoutPageKeys =
        {
            "Dashboard", "Invoices", "Vendors", "Purchases", "Clients", "Contracts",
            "Jobs", "Payments", "Payroll", "Inventory", "ServiceDesk", "Reports",
            "Settings", "MasterData"
        };

        public List<SupportArticle> GetArticles()
        {
            return BuildArticles();
        }

        public List<SupportArticle> SearchArticles(string query)
        {
            string needle = (query ?? string.Empty).Trim().ToLowerInvariant();
            List<SupportArticle> articles = GetArticles();
            if (string.IsNullOrWhiteSpace(needle))
                return articles;

            return articles.Where(article =>
                Contains(article.Title, needle) ||
                Contains(article.Summary, needle) ||
                Contains(article.Category, needle) ||
                article.Tags.Any(tag => Contains(tag, needle)) ||
                article.Steps.Any(step => Contains(step, needle))).ToList();
        }

        public SupportToolResult CheckDatabase()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(DatabaseManager.RequireConfiguredConnectionString()))
                using (SqlCommand cmd = new SqlCommand("SELECT 1", conn))
                {
                    conn.Open();
                    cmd.ExecuteScalar();
                    SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(conn.ConnectionString);
                    AppLogger.LogInfo("Support Center: database checked.");
                    return Ok("Database check complete", "ServoERP connected to the configured database.", "Server: " + SafeValue(builder.DataSource) + Environment.NewLine + "Database: " + SafeValue(builder.InitialCatalog));
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SupportCenterService.CheckDatabase", ex);
                return Fail("Database check failed", ex.Message);
            }
        }

        public SupportToolResult BackupDatabase()
        {
            BackupResult backup = new BackupService().CreateDatabaseBackup("Support Center database backup");
            if (backup.Success)
            {
                AppLogger.LogInfo("Support Center: database backup completed.");
                return WithPath(Ok("Backup complete", backup.Message, backup.BackupPath), backup.BackupPath);
            }

            return Fail("Backup failed", backup.Message, backup.BackupPath);
        }

        public SupportToolResult ClearCache()
        {
            try
            {
                AppDataCache.Clear();
                AppLogger.LogInfo("Support Center: app cache cleared.");
                return Ok("Cache cleared", "In-memory module cache was cleared. Open pages will reload data as needed.", string.Empty);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SupportCenterService.ClearCache", ex);
                return Fail("Cache clear failed", ex.Message);
            }
        }

        public SupportToolResult ResetLayout()
        {
            try
            {
                int userId = CardLayoutService.ResolveCurrentUserId();
                CardLayoutService cardLayout = new CardLayoutService();
                foreach (string pageKey in LayoutPageKeys)
                    cardLayout.ResetPageLayout(userId, pageKey);

                int deleted = DeleteLayoutMemoryFiles();
                AppLogger.LogInfo("Support Center: layout reset for user " + userId + ". Local layout files removed=" + deleted);
                return Ok("Layout reset complete", "Saved page layouts were reset for the current user.", "Pages reset: " + string.Join(", ", LayoutPageKeys) + Environment.NewLine + "Local layout files removed: " + deleted);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SupportCenterService.ResetLayout", ex);
                return Fail("Layout reset failed", ex.Message);
            }
        }

        public SupportToolResult VerifyAppFiles()
        {
            try
            {
                List<string> missing = new List<string>();
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string exePath = Assembly.GetExecutingAssembly().Location;
                string configPath = Path.Combine(baseDir, "HVACPro.config");
                string iconPath = Path.Combine(baseDir, "app.ico");

                if (!File.Exists(exePath)) missing.Add(Path.GetFileName(exePath));
                if (!File.Exists(configPath)) missing.Add("HVACPro.config");
                if (!File.Exists(iconPath)) missing.Add("app.ico");

                if (missing.Count == 0)
                {
                    AppLogger.LogInfo("Support Center: app files verified.");
                    return Ok("App files verified", "Core application files are present.", "Checked: " + Path.GetFileName(exePath) + ", HVACPro.config, app.ico");
                }

                return Fail("App file verification found issues", "Missing: " + string.Join(", ", missing));
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SupportCenterService.VerifyAppFiles", ex);
                return Fail("App file verification failed", ex.Message);
            }
        }

        public SupportToolResult RepairConfig()
        {
            try
            {
                string[] directories =
                {
                    @"C:\HVAC_PRO_MSE\LOGS",
                    @"C:\HVAC_PRO_MSE\UPDATES",
                    @"C:\HVAC_PRO_MSE\DIAGNOSTICS",
                    BackupService.BackupRoot
                };

                foreach (string directory in directories)
                    Directory.CreateDirectory(directory);

                AppLogger.LogInfo("Support Center: config repair checked required folders.");
                return Ok("Config repair complete", "Required ServoERP folders exist and are writable.", string.Join(Environment.NewLine, directories));
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SupportCenterService.RepairConfig", ex);
                return Fail("Config repair failed", ex.Message);
            }
        }

        public SupportToolResult ExportDiagnosticsPackage()
        {
            try
            {
                Directory.CreateDirectory(DiagnosticsRoot);
                string exportFolder = Path.Combine(DiagnosticsRoot, "ServoERP_Diagnostics_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                Directory.CreateDirectory(exportFolder);

                WriteText(exportFolder, "app_info.txt", BuildAppInfo());
                WriteText(exportFolder, "system_health.txt", BuildSystemHealth());
                WriteText(exportFolder, "installed_modules.txt", BuildInstalledModules());
                WriteText(exportFolder, "config_summary.txt", BuildConfigSummary());
                WriteText(exportFolder, "recent_logs.txt", BuildRecentLogs());
                WriteText(exportFolder, "layout_summary.txt", BuildLayoutSummary());

                string zipPath = exportFolder + ".zip";
                if (File.Exists(zipPath))
                    File.Delete(zipPath);
                System.IO.Compression.ZipFile.CreateFromDirectory(exportFolder, zipPath);
                Directory.Delete(exportFolder, true);

                AppLogger.LogInfo("Support Center: diagnostics exported to " + zipPath);
                return WithPath(Ok("Diagnostics exported", "A safe diagnostics package was created.", zipPath), zipPath);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SupportCenterService.ExportDiagnosticsPackage", ex);
                return Fail("Diagnostics export failed", ex.Message);
            }
        }

        public SupportToolResult GenerateClientServerSetupPackage()
        {
            try
            {
                Directory.CreateDirectory(DiagnosticsRoot);
                ServerSetupProfile profile = BuildServerSetupProfile();
                string folder = Path.Combine(DiagnosticsRoot, "Client_Server_Setup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                Directory.CreateDirectory(folder);

                string configPath = Path.Combine(folder, "HVACPro.config");
                string scriptPath = Path.Combine(folder, "Apply-ServoERP-ClientConnection.ps1");
                string guidePath = Path.Combine(folder, "READ_ME_CLIENT_CONNECTION.txt");
                File.WriteAllText(configPath, BuildClientConfigXml(profile), Encoding.UTF8);
                File.WriteAllText(scriptPath, BuildClientConnectionScript(), Encoding.UTF8);
                File.WriteAllText(guidePath, BuildClientServerGuide(profile), Encoding.UTF8);

                string zipPath = folder + ".zip";
                if (File.Exists(zipPath))
                    File.Delete(zipPath);
                ZipFile.CreateFromDirectory(folder, zipPath);
                Directory.Delete(folder, true);

                return WithPath(Ok("Client server setup package ready", "Copy this ZIP to each client PC and run the included PowerShell script as Administrator.", BuildClientServerGuide(profile)), zipPath);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SupportCenterService.GenerateClientServerSetupPackage", ex);
                return Fail("Client server setup package failed", ex.Message);
            }
        }

        public SupportToolResult CreateOfficeHealthReport()
        {
            try
            {
                Directory.CreateDirectory(DiagnosticsRoot);
                string path = Path.Combine(DiagnosticsRoot, "office-health-" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
                string report = BuildOfficeHealthText();
                File.WriteAllText(path, report, Encoding.UTF8);
                return WithPath(Ok("Office sync health report ready", "Connection, SQL, app version, backup, and row-count health were checked.", report), path);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SupportCenterService.CreateOfficeHealthReport", ex);
                return Fail("Office health report failed", ex.Message);
            }
        }

        public SupportToolResult CreateOperationsCommandCenterReport()
        {
            try
            {
                Directory.CreateDirectory(DiagnosticsRoot);
                string path = Path.Combine(DiagnosticsRoot, "operations-command-center-" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
                string report = BuildOperationsCommandCenterText();
                File.WriteAllText(path, report, Encoding.UTF8);
                return WithPath(Ok("Operations command report ready", "Sales, jobs, AMC, invoice, vendor, inventory, technician, and quotation signals were summarized.", report), path);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SupportCenterService.CreateOperationsCommandCenterReport", ex);
                return Fail("Operations command report failed", ex.Message);
            }
        }

        public SupportToolResult CreateMaterialPriceIntelligenceReport()
        {
            try
            {
                Directory.CreateDirectory(DiagnosticsRoot);
                string path = Path.Combine(DiagnosticsRoot, "material-price-intelligence-" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
                string report = BuildMaterialPriceText();
                File.WriteAllText(path, report, Encoding.UTF8);
                return WithPath(Ok("Material price intelligence ready", "Supplier prices, best-rate candidates, purchase variance, and quotation memory were summarized.", report), path);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SupportCenterService.CreateMaterialPriceIntelligenceReport", ex);
                return Fail("Material price intelligence failed", ex.Message);
            }
        }

        public SupportToolResult CreateDocumentAutomationReport()
        {
            try
            {
                Directory.CreateDirectory(DiagnosticsRoot);
                string path = Path.Combine(DiagnosticsRoot, "document-automation-" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
                string report = BuildDocumentAutomationText();
                File.WriteAllText(path, report, Encoding.UTF8);
                return WithPath(Ok("Document automation report ready", "Template coverage and default document mappings were summarized.", report), path);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SupportCenterService.CreateDocumentAutomationReport", ex);
                return Fail("Document automation report failed", ex.Message);
            }
        }

        public SupportToolResult CreateFreshClientDeploymentReport()
        {
            try
            {
                Directory.CreateDirectory(DiagnosticsRoot);
                string path = Path.Combine(DiagnosticsRoot, "fresh-client-deployment-" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
                string report = BuildFreshDeploymentText();
                File.WriteAllText(path, report, Encoding.UTF8);
                return WithPath(Ok("Fresh client deployment report ready", "Deployment readiness, dummy-data status, connection setup, and import order were summarized.", report), path);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SupportCenterService.CreateFreshClientDeploymentReport", ex);
                return Fail("Fresh client deployment report failed", ex.Message);
            }
        }

        public SupportToolResult CreateDataCleanRoomReport(string sourcePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath) && !Directory.Exists(sourcePath))
                    return Fail("Data Clean Room failed", "Choose an Excel file, ZIP file, or folder to analyze.");

                Directory.CreateDirectory(DiagnosticsRoot);
                string folder = Path.Combine(DiagnosticsRoot, "Data_Clean_Room_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                Directory.CreateDirectory(folder);
                string reportPath = Path.Combine(folder, "Data_Clean_Room_Report.txt");
                File.WriteAllText(reportPath, BuildDataCleanRoomText(sourcePath), Encoding.UTF8);

                string zipPath = folder + ".zip";
                if (File.Exists(zipPath))
                    File.Delete(zipPath);
                ZipFile.CreateFromDirectory(folder, zipPath);
                Directory.Delete(folder, true);
                return WithPath(Ok("Data Clean Room report ready", "Source files were classified and mapped to ServoERP import modules. Use the report before upload.", "Source: " + sourcePath), zipPath);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SupportCenterService.CreateDataCleanRoomReport", ex);
                return Fail("Data Clean Room failed", ex.Message);
            }
        }

        private string BuildAppInfo()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            return "ServoERP Diagnostics" + Environment.NewLine +
                   "Generated At: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine +
                   "App Version: " + ConfigService.GetAppVersion() + Environment.NewLine +
                   "Assembly Version: " + assembly.GetName().Version + Environment.NewLine +
                   "Machine Name: " + Environment.MachineName + Environment.NewLine +
                   "OS Version: " + Environment.OSVersion + Environment.NewLine +
                   ".NET Version: " + Environment.Version + Environment.NewLine +
                   "Current Role: " + SafeValue(SessionManager.CurrentUser == null ? "Not logged in" : SessionManager.CurrentUser.RoleName);
        }

        private ServerSetupProfile BuildServerSetupProfile()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(DatabaseManager.RequireConfiguredConnectionString());
            string instance = string.Empty;
            string dataSource = builder.DataSource ?? string.Empty;
            int slash = dataSource.IndexOf('\\');
            if (slash >= 0 && slash + 1 < dataSource.Length)
                instance = dataSource.Substring(slash + 1);
            if (string.IsNullOrWhiteSpace(instance))
                instance = "SQLEXPRESS";

            string ip = DetectPrimaryIpAddress();
            string target = string.IsNullOrWhiteSpace(instance) ? ip : ip + "\\" + instance;
            return new ServerSetupProfile
            {
                MachineName = Environment.MachineName,
                PrimaryIpAddress = ip,
                SqlInstance = instance,
                DatabaseName = string.IsNullOrWhiteSpace(builder.InitialCatalog) ? "HVAC_PRO" : builder.InitialCatalog,
                ConnectionTarget = target
            };
        }

        private static string BuildClientConfigXml(ServerSetupProfile profile)
        {
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>" + Environment.NewLine +
                   "<HVACProConfig>" + Environment.NewLine +
                   "  <Database>" + Environment.NewLine +
                   "    <Server>" + EscapeXml(profile.ConnectionTarget) + "</Server>" + Environment.NewLine +
                   "    <DatabaseName>" + EscapeXml(profile.DatabaseName) + "</DatabaseName>" + Environment.NewLine +
                   "    <UseWindowsAuth>true</UseWindowsAuth>" + Environment.NewLine +
                   "    <Username></Username>" + Environment.NewLine +
                   "    <Password></Password>" + Environment.NewLine +
                   "  </Database>" + Environment.NewLine +
                   "  <Tenant>" + Environment.NewLine +
                   "    <SeedDemoData>false</SeedDemoData>" + Environment.NewLine +
                   "  </Tenant>" + Environment.NewLine +
                   "</HVACProConfig>" + Environment.NewLine;
        }

        private static string BuildClientConnectionScript()
        {
            return "$ErrorActionPreference = 'Stop'" + Environment.NewLine +
                   "$target = 'C:\\HVAC_PRO_MSE\\HVACPro.config'" + Environment.NewLine +
                   "$source = Join-Path $PSScriptRoot 'HVACPro.config'" + Environment.NewLine +
                   "if (!(Test-Path $source)) { throw 'HVACPro.config not found beside this script.' }" + Environment.NewLine +
                   "New-Item -ItemType Directory -Force -Path 'C:\\HVAC_PRO_MSE' | Out-Null" + Environment.NewLine +
                   "Copy-Item -LiteralPath $source -Destination $target -Force" + Environment.NewLine +
                   "Write-Host 'ServoERP client connection applied:' $target -ForegroundColor Green" + Environment.NewLine;
        }

        private static string BuildClientServerGuide(ServerSetupProfile profile)
        {
            return "ServoERP Client Server Setup" + Environment.NewLine +
                   "Server PC: " + profile.MachineName + Environment.NewLine +
                   "Server IP: " + profile.PrimaryIpAddress + Environment.NewLine +
                   "SQL Target: " + profile.ConnectionTarget + Environment.NewLine +
                   "Database: " + profile.DatabaseName + Environment.NewLine +
                   Environment.NewLine +
                   "Client PC steps:" + Environment.NewLine +
                   "1. Extract this ZIP on the client PC." + Environment.NewLine +
                   "2. Right-click Apply-ServoERP-ClientConnection.ps1 and run with PowerShell as Administrator." + Environment.NewLine +
                   "3. Open ServoERP and use Help & Support > System Health > Check Database." + Environment.NewLine +
                   Environment.NewLine +
                   "Firewall requirement on server PC: allow SQL Server TCP 1433 and SQL Browser UDP 1434 if using named instances." + Environment.NewLine;
        }

        private string BuildOfficeHealthText()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(DatabaseManager.RequireConfiguredConnectionString());
            SupportToolResult db = CheckDatabase();
            return "Office Sync / Multi-PC Health" + Environment.NewLine +
                   "Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine +
                   "Machine: " + Environment.MachineName + Environment.NewLine +
                   "App Version: " + ConfigService.GetAppVersion() + Environment.NewLine +
                   "Database: " + SafeValue(builder.InitialCatalog) + Environment.NewLine +
                   "Server: " + SafeValue(builder.DataSource) + Environment.NewLine +
                   "SQL Reachability: " + (db.Success ? "OK" : "FAILED") + Environment.NewLine +
                   "Backup Root: " + BackupService.BackupRoot + Environment.NewLine +
                   "Last Backup: " + LastFileInfo(BackupService.BackupRoot, "*.bak") + Environment.NewLine +
                   Environment.NewLine +
                   BuildCountsBlock();
        }

        private string BuildOperationsCommandCenterText()
        {
            return "Operations Command Center" + Environment.NewLine +
                   "Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine +
                   Environment.NewLine +
                   "Sales & Collections" + Environment.NewLine +
                   QueryMetric("Open invoices", "SELECT COUNT(*) FROM Invoices WHERE ISNULL(PaymentStatus,'') NOT IN ('Paid','Closed')") + Environment.NewLine +
                   QueryMoney("Invoice outstanding", "SELECT ISNULL(SUM(CASE WHEN BalanceDue IS NOT NULL THEN BalanceDue ELSE TotalAmount-ISNULL(PaidAmount,0) END),0) FROM Invoices WHERE ISNULL(PaymentStatus,'') NOT IN ('Paid','Closed')") + Environment.NewLine +
                   Environment.NewLine +
                   "Operations" + Environment.NewLine +
                   QueryMetric("Open jobs", "SELECT COUNT(*) FROM Jobs WHERE ISNULL(Status,'') NOT IN ('Closed','Completed','Cancelled')") + Environment.NewLine +
                   QueryMetric("AMC contracts expiring in 45 days", "SELECT COUNT(*) FROM AMCContracts WHERE EndDate BETWEEN GETDATE() AND DATEADD(day,45,GETDATE())") + Environment.NewLine +
                   QueryMetric("Inventory reorder risk", "SELECT COUNT(*) FROM StockItems WHERE CurrentStock <= ReorderLevel") + Environment.NewLine +
                   Environment.NewLine +
                   "Supply Chain" + Environment.NewLine +
                   QueryMetric("Open purchase orders", "SELECT COUNT(*) FROM PurchaseOrders WHERE ISNULL(Status,'') NOT IN ('Paid','Closed','Cancelled')") + Environment.NewLine +
                   QueryMoney("Vendor payables", "SELECT ISNULL(SUM(TotalAmount-ISNULL(PaidAmount,0)),0) FROM PurchaseOrders WHERE ISNULL(Status,'') NOT IN ('Paid','Closed','Cancelled')") + Environment.NewLine +
                   Environment.NewLine +
                   "Commercial" + Environment.NewLine +
                   QueryMetric("Open quotations", "SELECT COUNT(*) FROM TenderBids WHERE ISNULL(Status,'') NOT IN ('Won','Lost','Cancelled')") + Environment.NewLine;
        }

        private string BuildMaterialPriceText()
        {
            return "Material Price Intelligence" + Environment.NewLine +
                   "Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine +
                   QueryMetric("Supplier item price rows", "SELECT COUNT(*) FROM SupplierItemPrices") + Environment.NewLine +
                   QueryMetric("Quoted lines with supplier mapping", "SELECT COUNT(*) FROM TenderBidLineItems WHERE BestSupplierId IS NOT NULL") + Environment.NewLine +
                   QueryMetric("Purchase lines with price variance", "SELECT COUNT(*) FROM PurchaseLineItems WHERE PriceVariance > 10") + Environment.NewLine +
                   QueryMetric("Accepted client price memory rows", "SELECT COUNT(*) FROM ClientPriceMemory WHERE WasAccepted=1") + Environment.NewLine +
                   Environment.NewLine +
                   QueryRows("Top supplier rates", @"
SELECT TOP 12 sip.ItemName + N' | ' + v.VendorName + N' | Rs ' + CONVERT(NVARCHAR(30), sip.Rate) + N'/' + ISNULL(sip.Unit,N'Nos')
FROM SupplierItemPrices sip
JOIN Vendors v ON v.VendorID=sip.VendorID
ORDER BY sip.ItemName, sip.Rate");
        }

        private string BuildDocumentAutomationText()
        {
            var manager = new CompanyTemplateManager();
            var templates = manager.GetTemplates();
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Document Automation");
            builder.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            builder.AppendLine("Templates loaded: " + templates.Count);
            foreach (var group in templates.GroupBy(t => t.DocumentType).OrderBy(g => g.Key.ToString()))
            {
                builder.AppendLine(group.Key + ": " + group.Count() + " template(s), default=" + (group.Any(t => t.IsDefault) ? "Yes" : "No"));
            }
            builder.AppendLine();
            builder.AppendLine("Required defaults: Letterhead, Quotation, Invoice, PurchaseOrder, Contract, DeliveryNote.");
            return builder.ToString();
        }

        private string BuildFreshDeploymentText()
        {
            return "Fresh Client Deployment Mode" + Environment.NewLine +
                   "Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine +
                   "SeedDemoData: " + ConfigService.Get("Tenant", "SeedDemoData", "false") + Environment.NewLine +
                   "Database connection: " + (CheckDatabase().Success ? "OK" : "FAILED") + Environment.NewLine +
                   Environment.NewLine +
                   "Business data row counts after cleanup/import:" + Environment.NewLine +
                   BuildCountsBlock() + Environment.NewLine +
                   "Recommended import order:" + Environment.NewLine +
                   "1. Clients  2. Sites  3. Vendors  4. Employees  5. Inventory  6. Purchases  7. Invoices  8. Quotations" + Environment.NewLine +
                   Environment.NewLine +
                   "One-click destructive data clearing remains behind Settings Fresh Start confirmation. This report does not delete data.";
        }

        private string BuildDataCleanRoomText(string sourcePath)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Data Clean Room");
            builder.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            builder.AppendLine("Source: " + sourcePath);
            builder.AppendLine();
            IEnumerable<string> files = EnumerateCleanRoomFiles(sourcePath);
            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                builder.AppendLine(name + " => " + ClassifyImportTarget(name));
            }
            builder.AppendLine();
            builder.AppendLine("Upload order enforced by ServoERP preflight: Clients before Sites; Clients/Sites before Invoices/Quotations/Jobs; Vendors before Purchases; Invoices before Payments.");
            return builder.ToString();
        }

        private static IEnumerable<string> EnumerateCleanRoomFiles(string sourcePath)
        {
            if (Directory.Exists(sourcePath))
                return Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories).Where(IsImportCandidate).OrderBy(x => x).ToList();

            if (Path.GetExtension(sourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                using (ZipArchive archive = ZipFile.OpenRead(sourcePath))
                    return archive.Entries.Where(e => !string.IsNullOrWhiteSpace(e.Name) && IsImportCandidate(e.Name)).Select(e => e.FullName).OrderBy(x => x).ToList();
            }

            return new[] { sourcePath };
        }

        private static bool IsImportCandidate(string path)
        {
            string name = Path.GetFileName(path);
            if (name.StartsWith("~$", StringComparison.OrdinalIgnoreCase))
                return false;
            string ext = Path.GetExtension(name).ToLowerInvariant();
            return ext == ".xlsx" || ext == ".xls" || ext == ".csv" || ext == ".pdf" || ext == ".docx";
        }

        private static string ClassifyImportTarget(string name)
        {
            string n = (name ?? string.Empty).ToLowerInvariant();
            if (n.Contains("sales") || n.Contains("invoice")) return "Invoices / Documents";
            if (n.Contains("purchase") || n.Contains("expense")) return "Purchases and Vendors";
            if (n.Contains("vendor") || n.Contains("supplier")) return "Vendors";
            if (n.Contains("employee") || n.Contains("attendance") || n.Contains("payroll")) return "Employees / Payroll";
            if (n.Contains("stock") || n.Contains("inventory")) return "Inventory";
            if (n.Contains("quotation") || n.Contains("quote") || n.Contains("proforma")) return "Quotations";
            if (n.Contains("tds") || n.Contains("challan")) return "Supporting Documents";
            return "Manual Review";
        }

        private string BuildSystemHealth()
        {
            SupportToolResult database = CheckDatabase();
            string updateUrl = ConfigService.GetVersionCheckUrl();
            Uri uri;
            string updateHost = Uri.TryCreate(updateUrl, UriKind.Absolute, out uri) ? uri.Host : "Not configured";
            return "Database: " + (database.Success ? "Connected" : "Failed") + Environment.NewLine +
                   "Database Detail: " + SanitizeText(database.Detail ?? database.Message) + Environment.NewLine +
                   "Version Check Enabled: " + ConfigService.IsVersionCheckEnabled() + Environment.NewLine +
                   "Update Host: " + SafeValue(updateHost) + Environment.NewLine +
                   "Backup Folder: " + BackupService.BackupRoot + Environment.NewLine +
                   "Diagnostics Folder: " + DiagnosticsRoot;
        }

        private static string BuildInstalledModules()
        {
            return string.Join(Environment.NewLine, new[]
            {
                "Dashboard", "Quotations", "Invoices", "Service Desk", "Dispatch Center",
                "Inventory", "Purchases", "Clients", "Vendors", "Payments", "Contracts",
                "Jobs", "Payroll", "Employees", "Reports", "Settings", "Master Data",
                "TallyPrime Integration", "WhatsApp Cloud API Integration", "Calendar Dispatch Integration",
                "Cloud Backup Integration", "GST/e-Invoice Integration"
            });
        }

        private static string BuildConfigSummary()
        {
            Dictionary<string, string> summary = new Dictionary<string, string>
            {
                { "App.Version", ConfigService.GetAppVersion() },
                { "App.VersionCheckEnabled", ConfigService.IsVersionCheckEnabled().ToString() },
                { "App.VersionCheckIntervalHours", ConfigService.GetVersionCheckIntervalHours().ToString() },
                { "App.VersionCheckHost", SafeHost(ConfigService.GetVersionCheckUrl()) }
            };

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            return serializer.Serialize(summary);
        }

        private static string BuildCountsBlock()
        {
            string[] tables =
            {
                "B2BClients", "ClientSites", "Vendors", "Employees", "StockItems", "PurchaseOrders",
                "Invoices", "Payments", "TenderBids", "Jobs", "AMCContracts", "SupplierItemPrices"
            };
            return string.Join(Environment.NewLine, tables.Select(t => t + ": " + SafeCount(t)));
        }

        private static int SafeCount(string tableName)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(DatabaseManager.RequireConfiguredConnectionString()))
                using (SqlCommand cmd = new SqlCommand("IF OBJECT_ID('dbo." + tableName.Replace("'", "''") + "','U') IS NOT NULL EXEC('SELECT COUNT(*) FROM dbo." + tableName.Replace("]", "]]") + "') ELSE SELECT 0", conn))
                {
                    conn.Open();
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
            catch
            {
                return 0;
            }
        }

        private static string QueryMetric(string label, string sql)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(DatabaseManager.RequireConfiguredConnectionString()))
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.CommandTimeout = 30;
                    conn.Open();
                    object value = cmd.ExecuteScalar();
                    return label + ": " + Convert.ToString(value ?? 0);
                }
            }
            catch (Exception ex)
            {
                return label + ": unavailable (" + ex.Message + ")";
            }
        }

        private static string QueryMoney(string label, string sql)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(DatabaseManager.RequireConfiguredConnectionString()))
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.CommandTimeout = 30;
                    conn.Open();
                    decimal value = Convert.ToDecimal(cmd.ExecuteScalar());
                    return label + ": Rs " + value.ToString("N2");
                }
            }
            catch (Exception ex)
            {
                return label + ": unavailable (" + ex.Message + ")";
            }
        }

        private static string QueryRows(string label, string sql)
        {
            try
            {
                List<string> rows = new List<string>();
                using (SqlConnection conn = new SqlConnection(DatabaseManager.RequireConfiguredConnectionString()))
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.CommandTimeout = 30;
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            rows.Add(Convert.ToString(reader.GetValue(0)));
                    }
                }

                return label + ":" + Environment.NewLine + (rows.Count == 0 ? "No records found." : string.Join(Environment.NewLine, rows.Select(x => "- " + x)));
            }
            catch (Exception ex)
            {
                return label + ": unavailable (" + ex.Message + ")";
            }
        }

        private static string LastFileInfo(string folder, string pattern)
        {
            try
            {
                if (!Directory.Exists(folder))
                    return "None";
                FileInfo file = new DirectoryInfo(folder).GetFiles(pattern).OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
                return file == null ? "None" : file.FullName + " | " + file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch
            {
                return "Unavailable";
            }
        }

        private static string DetectPrimaryIpAddress()
        {
            try
            {
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up))
                {
                    foreach (UnicastIPAddressInformation address in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (address.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address.Address))
                            return address.Address.ToString();
                    }
                }
            }
            catch
            {
            }

            return "127.0.0.1";
        }

        private static string EscapeXml(string value)
        {
            return (value ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        private static string BuildRecentLogs()
        {
            string path = @"C:\HVAC_PRO_MSE\LOGS\app.log";
            if (!File.Exists(path))
                return "No app log found.";

            string[] lines = File.ReadAllLines(path);
            string text = string.Join(Environment.NewLine, lines.Skip(Math.Max(0, lines.Length - 500)));
            return SanitizeText(text);
        }

        private static string BuildLayoutSummary()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ServoERP", "LayoutMemory");
            if (!Directory.Exists(root))
                return "No local layout memory folder found.";

            StringBuilder builder = new StringBuilder();
            foreach (string file in Directory.GetFiles(root, "*.json", SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName))
            {
                FileInfo info = new FileInfo(file);
                builder.AppendLine(info.Name + " | " + info.Length + " bytes | " + info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            return builder.ToString();
        }

        private static void WriteText(string folder, string fileName, string content)
        {
            File.WriteAllText(Path.Combine(folder, fileName), content ?? string.Empty, Encoding.UTF8);
        }

        private static int DeleteLayoutMemoryFiles()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ServoERP", "LayoutMemory");
            if (!Directory.Exists(root))
                return 0;

            int count = 0;
            foreach (string file in Directory.GetFiles(root, "*.json", SearchOption.TopDirectoryOnly))
            {
                File.Delete(file);
                count++;
            }
            return count;
        }

        private static string SanitizeText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            string sanitized = text;
            sanitized = Regex.Replace(sanitized, @"(?i)(password|pwd|secret|token|api[_ -]?key|license[_ -]?key|connectionstring)\s*[:=]\s*[^\r\n;]+", "$1=[REDACTED]");
            sanitized = Regex.Replace(sanitized, @"(?i)(User ID|UID)\s*=\s*[^;]+", "$1=[REDACTED]");
            sanitized = Regex.Replace(sanitized, @"(?i)(Password|PWD)\s*=\s*[^;]+", "$1=[REDACTED]");
            return sanitized;
        }

        private static string SafeHost(string url)
        {
            Uri uri;
            return Uri.TryCreate(url, UriKind.Absolute, out uri) ? uri.Host : "Not configured";
        }

        private static string SafeValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        }

        private static bool Contains(string value, string needle)
        {
            return (value ?? string.Empty).ToLowerInvariant().Contains(needle);
        }

        private static SupportToolResult Ok(string title, string message, string detail)
        {
            return new SupportToolResult { Success = true, Title = title, Message = message, Detail = detail };
        }

        private static SupportToolResult Fail(string title, string message, string detail = "")
        {
            return new SupportToolResult { Success = false, Title = title, Message = message, Detail = detail };
        }

        private static SupportToolResult WithPath(SupportToolResult result, string path)
        {
            result.OutputPath = path;
            return result;
        }

        private static List<SupportArticle> BuildArticles()
        {
            return new List<SupportArticle>
            {
                Article("Getting Started", "How to create an invoice", "Create and send a GST-ready customer invoice from ServoERP.", "invoice,gst,billing,customer",
                    "Open Invoices from the sidebar.",
                    "Select or create the client and site.",
                    "Review invoice dates, warranty, payment terms, and GST mode.",
                    "Add line items or service details.",
                    "Save Draft, then generate PDF or email the invoice."),
                Article("Vendors", "How to add a vendor", "Create a clean vendor profile for purchasing and payables.", "vendor,purchase,pan,gst",
                    "Open Vendors.",
                    "Click New Vendor.",
                    "Enter vendor identity, contact, GST/PAN, payment terms, and bank details.",
                    "Use tags for trade or specialization.",
                    "Save the profile before raising purchase orders."),
                Article("Purchase Orders", "How to create a purchase order", "Raise a purchase order and track vendor fulfilment.", "po,purchase,vendor,inventory",
                    "Open Purchases.",
                    "Click New PO and select the vendor.",
                    "Fill PO information, payment terms, delivery details, and department.",
                    "Add item lines with quantity, GST, and unit cost.",
                    "Save PO, then print or email it to the vendor."),
                Article("Jobs", "How to create a job", "Create a field-service job and assign technician workflow.", "job,technician,dispatch,service",
                    "Open Jobs.",
                    "Click New Job.",
                    "Enter job title, client, site, job type, schedule date, and linked contract if applicable.",
                    "Assign technician and priority.",
                    "Use checklist, parts, notes, and activity log to track execution."),
                Article("Accounting", "How to backup data", "Create a manual safety backup before maintenance or updates.", "backup,database,settings",
                    "Open Help & Support.",
                    "Go to System Health.",
                    "Click Backup Database.",
                    "Wait for the success message and note the backup path.",
                    "Store backup files in a secure company location."),
                Article("Troubleshooting", "How to reset layout", "Reset saved page/card layouts if screens look misaligned.", "layout,reset,screen,ui",
                    "Open Help & Support.",
                    "Go to System Health.",
                    "Click Reset Layout.",
                    "Close and reopen affected pages.",
                    "Use this when cards, columns, or saved sizes look wrong."),
                Article("Updates & Licensing", "How to check for updates", "Verify whether a newer ServoERP version is available.", "update,version,release",
                    "Open Settings or Help & Support.",
                    "Choose Check Updates.",
                    "Review the latest version and changelog if available.",
                    "Download and install updates only from approved ServoERP channels."),
                Article("Troubleshooting", "How to export diagnostics", "Create a safe package for support without customer financial data.", "diagnostics,logs,support,export",
                    "Open Help & Support.",
                    "Go to System Health.",
                    "Click Export Diagnostics Package.",
                    "Share the generated ZIP path with support.",
                    "The package excludes passwords, API keys, and customer financial data."),
                Article("Accounting", "How to restore backup", "Restore a database backup after confirming the recovery point.", "restore,backup,database",
                    "Open Settings.",
                    "Go to Backup Restore.",
                    "Select a verified .bak file.",
                    "Create a safety backup before restore.",
                    "Restore only after all users close ServoERP."),
                Article("Service Desk", "How to use dispatch board", "Coordinate incidents, jobs, and technician scheduling.", "dispatch,technician,service desk,jobs",
                    "Open Dispatch Center when enabled.",
                    "Filter by technician, job status, or date.",
                    "Assign jobs based on availability and priority.",
                    "Use job activity and service desk notes for field updates.",
                    "Review exceptions before the workday closes.")
            };
        }

        private static SupportArticle Article(string category, string title, string summary, string tags, params string[] steps)
        {
            return new SupportArticle
            {
                Category = category,
                Title = title,
                Summary = summary,
                Tags = (tags ?? string.Empty).Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToList(),
                Steps = steps.ToList()
            };
        }
    }
}

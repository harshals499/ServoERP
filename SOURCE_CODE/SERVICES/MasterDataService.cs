using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Models.Validation;
using HVAC_Pro_Desktop.Services.Audit;
using HVAC_Pro_Desktop.Services.Validation;

namespace HVAC_Pro_Desktop.Services
{
    public class MasterDataService
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
        private readonly MasterDataRepository _repo = new MasterDataRepository();
        private readonly BusinessRuleEngine _businessRules = new BusinessRuleEngine();
        private readonly GlobalValidationEngine _validation = new GlobalValidationEngine();
        private readonly AuditTrailService _audit = new AuditTrailService();

        public List<ClientAsset> GetAssets() => AppDataCache.GetOrCreate("masterdata:assets", CacheTtl, _repo.GetAssets);
        public List<ClientDocument> GetDocuments() => AppDataCache.GetOrCreate("masterdata:documents", CacheTtl, _repo.GetDocuments);
        public List<ServiceRateCard> GetRateCards() => AppDataCache.GetOrCreate("masterdata:rates", CacheTtl, _repo.GetRateCards);
        public List<PrivateServerConnection> GetPrivateServerConnections() => AppDataCache.GetOrCreate("masterdata:connections", CacheTtl, _repo.GetPrivateServerConnections);
        public List<DataImportBatch> GetImportBatches() => AppDataCache.GetOrCreate("masterdata:imports", CacheTtl, _repo.GetImportBatches);

        public int SaveAsset(ClientAsset asset)
        {
            ValidateAssetForSave(asset);
            asset.IsActive = true;
            int id = _repo.SaveAsset(asset);
            AppDataCache.RemovePrefix("masterdata:");
            SessionManager.LogAction(asset.AssetId > 0 ? "EDIT" : "CREATE", "MasterData", id, "Client asset saved");
            _audit.Record(asset.AssetId > 0 ? "EDIT" : "CREATE", "Assets", id, "Asset saved with data-quality validation");
            return id;
        }

        public int SaveDocument(ClientDocument document, string sourceFilePath)
        {
            if (document == null)
                throw new Exception("Document details are missing.");
            if (string.IsNullOrWhiteSpace(document.Title))
                throw new Exception("Document title is required.");
            if (!string.IsNullOrWhiteSpace(sourceFilePath))
                document.FilePath = CopyDocumentToVault(sourceFilePath);
            if (string.IsNullOrWhiteSpace(document.FilePath))
                throw new Exception("Choose a file before saving the document.");
            document.OriginalFileName = Path.GetFileName(sourceFilePath ?? document.FilePath);
            if (string.IsNullOrWhiteSpace(document.UploadedBy) && SessionManager.IsLoggedIn)
                document.UploadedBy = SessionManager.CurrentUser.DisplayName ?? SessionManager.CurrentUser.Username;

            int id = _repo.SaveDocument(document);
            AppDataCache.RemovePrefix("masterdata:");
            SessionManager.LogAction("CREATE", "MasterData", id, "Client document registered");
            _audit.Record("CREATE", "Documents", id, "Document registered with data-quality validation");
            return id;
        }

        public int SaveRateCard(ServiceRateCard rate)
        {
            if (rate == null || string.IsNullOrWhiteSpace(rate.ServiceName))
                throw new Exception("Service name is required.");
            if (rate.Rate < 0)
                throw new Exception("Rate cannot be negative.");
            if (rate.EffectiveFrom == default(DateTime))
                rate.EffectiveFrom = DateTime.Today;
            rate.IsActive = true;
            int id = _repo.SaveRateCard(rate);
            AppDataCache.RemovePrefix("masterdata:");
            SessionManager.LogAction(rate.RateId > 0 ? "EDIT" : "CREATE", "MasterData", id, "Service rate card saved");
            _audit.Record(rate.RateId > 0 ? "EDIT" : "CREATE", "RateCards", id, "Rate card saved with data-quality validation");
            return id;
        }

        public int SavePrivateServerConnection(PrivateServerConnection connection, string secret)
        {
            if (connection == null || string.IsNullOrWhiteSpace(connection.ConnectionName))
                throw new Exception("Connection name is required.");
            if (string.IsNullOrWhiteSpace(connection.ServerType))
                connection.ServerType = "SQL Server";
            if (!string.IsNullOrWhiteSpace(secret))
                connection.EncryptedSecret = ProtectSecret(secret);
            connection.IsActive = true;
            connection.LastSyncStatus = string.IsNullOrWhiteSpace(connection.LastSyncStatus) ? "Configured" : connection.LastSyncStatus;
            int id = _repo.SavePrivateServerConnection(connection);
            AppDataCache.RemovePrefix("masterdata:");
            SessionManager.LogAction(connection.ConnectionId > 0 ? "EDIT" : "CREATE", "MasterData", id, "Private server connection saved");
            return id;
        }

        public List<MasterDataStatus> GetSetupStatus()
        {
            var status = new List<MasterDataStatus>();
            AddStatus(status, "Company profile", _repo.Count("CompanySettings"), "Fill company settings");
            AddStatus(status, "Clients", _repo.Count("B2BClients"), "Upload or create client records");
            AddStatus(status, "Sites", _repo.Count("ClientSites"), "Add service sites");
            AddStatus(status, "HVAC assets", _repo.Count("ClientAssets"), "Add equipment register");
            AddStatus(status, "Contracts", _repo.Count("AMCContracts"), "Upload AMC contracts");
            AddStatus(status, "Documents", _repo.Count("ClientDocuments"), "Upload contracts, licenses and warranty files");
            AddStatus(status, "Rate cards", _repo.Count("ServiceRateCards"), "Add labor and service pricing");
            AddStatus(status, "Inventory", _repo.Count("StockItems"), "Import parts and stock");
            AddStatus(status, "Private server", _repo.Count("PrivateServerConnections"), "Configure private server or API");
            return status;
        }

        /// <summary>Returns live record counts for the Master Data smart upload cards.</summary>
        public int GetUploadRecordCount(ExcelImportModule module)
        {
            return _repo.CountUploadRecords(GetUploadRecordCountKey(module));
        }

        /// <summary>Maps import modules to whitelisted repository count keys.</summary>
        private static string GetUploadRecordCountKey(ExcelImportModule module)
        {
            switch (module)
            {
                case ExcelImportModule.Clients: return "Clients";
                case ExcelImportModule.Vendors: return "Suppliers";
                case ExcelImportModule.Sites: return "Sites";
                case ExcelImportModule.Inventory: return "Inventory";
                case ExcelImportModule.Purchases: return "Purchases";
                case ExcelImportModule.Invoices: return "Invoices";
                case ExcelImportModule.Payments: return "Payments";
                case ExcelImportModule.Quotations: return "Quotations";
                case ExcelImportModule.Jobs: return "Jobs";
                case ExcelImportModule.Employees: return "Employees";
                default: return string.Empty;
            }
        }

        public string BuildConnectionPreview(PrivateServerConnection connection)
        {
            if (connection == null)
                return "No connection selected.";
            string port = connection.Port.HasValue ? ":" + connection.Port.Value : string.Empty;
            return (connection.ServerType ?? "Server") + " | " + (connection.Host ?? connection.ApiBaseUrl ?? "") + port + " | " + (connection.DatabaseName ?? "");
        }

        private static void AddStatus(List<MasterDataStatus> list, string category, int count, string nextAction)
        {
            list.Add(new MasterDataStatus
            {
                Category = category,
                Count = count,
                IsComplete = count > 0,
                NextAction = count > 0 ? "Complete" : nextAction
            });
        }

        private static string CopyDocumentToVault(string sourceFilePath)
        {
            if (!File.Exists(sourceFilePath))
                throw new FileNotFoundException("Selected file was not found.", sourceFilePath);

            string dir = Path.Combine(@"C:\HVAC_PRO_MSE", "CLIENT_DATA_UPLOADS", DateTime.Now.ToString("yyyy"), DateTime.Now.ToString("MM"));
            Directory.CreateDirectory(dir);
            string safeName = Path.GetFileNameWithoutExtension(sourceFilePath);
            foreach (char c in Path.GetInvalidFileNameChars())
                safeName = safeName.Replace(c, '_');
            string extension = Path.GetExtension(sourceFilePath);
            string target = Path.Combine(dir, safeName + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + extension);
            File.Copy(sourceFilePath, target, false);
            return target;
        }

        private static string ProtectSecret(string secret)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(secret ?? string.Empty);
            byte[] protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }

        private void ValidateAssetForSave(ClientAsset asset)
        {
            ValidationResult result = _businessRules.ValidateAsset(asset);
            _validation.EnsureValid(result, "Asset validation failed");
        }
    }
}

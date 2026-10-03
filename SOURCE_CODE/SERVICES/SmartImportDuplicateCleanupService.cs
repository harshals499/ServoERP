using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using HVAC_Pro_Desktop.DAL;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class DuplicateCleanupResult
    {
        public int ArchivedRecords { get; set; }
        public int MergedGroups { get; set; }
        public int ReassignedReferences { get; set; }
        public string Message { get; set; }
    }

    public sealed class DuplicateCleanupPlan
    {
        public string SurvivorId { get; set; }
        public IEnumerable<string> DuplicateIds { get; set; }
    }

    public sealed class SmartImportDuplicateCleanupService
    {
        private readonly DatabaseManager _database = new DatabaseManager();

        public SmartImportDuplicateScan GetGroups(ExcelImportModule module)
        {
            EnsureSchema();
            return new SmartImportDuplicateDetector().ScanExisting(module);
        }

        public DuplicateCleanupResult MergeAndArchive(ExcelImportModule module, string survivorId, IEnumerable<string> duplicateIds)
        {
            return MergeAndArchiveGroups(module, new[]
            {
                new DuplicateCleanupPlan { SurvivorId = survivorId, DuplicateIds = duplicateIds }
            });
        }

        public DuplicateCleanupResult MergeAndArchiveGroups(ExcelImportModule module, IEnumerable<DuplicateCleanupPlan> cleanupPlans)
        {
            ModuleMap map = GetMap(module);
            List<NormalizedCleanupPlan> plans = NormalizePlans(cleanupPlans);
            int archivedRecords = plans.Sum(plan => plan.DuplicateIds.Count);

            EnsureSchema();
            int reassigned = 0;
            using (SqlConnection connection = _database.GetConnection())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        List<ForeignKeyRow> foreignKeys = LoadForeignKeys(connection, transaction, map.Table, map.Key);
                        foreach (NormalizedCleanupPlan plan in plans)
                        {
                            int survivorExists = connection.ExecuteScalar<int>("SELECT COUNT(1) FROM " + Q(map.Table) + " WHERE " + Q(map.Key) + "=TRY_CONVERT(int,@id)", new { id = plan.SurvivorId }, transaction);
                            if (survivorExists != 1)
                                throw new InvalidOperationException("A selected survivor record no longer exists. Refresh the duplicate list and try again.");

                            foreach (string duplicateId in plan.DuplicateIds)
                            {
                                foreach (ForeignKeyRow fk in foreignKeys)
                                {
                                    string sql = "UPDATE " + Q(fk.SchemaName) + "." + Q(fk.TableName) + " SET " + Q(fk.ColumnName) + "=TRY_CONVERT(int,@survivor) WHERE " + Q(fk.ColumnName) + "=TRY_CONVERT(int,@duplicate)";
                                    reassigned += connection.Execute(sql, new { survivor = plan.SurvivorId, duplicate = duplicateId }, transaction);
                                }

                                if (!string.IsNullOrWhiteSpace(map.ArchiveColumn))
                                    connection.Execute("UPDATE " + Q(map.Table) + " SET " + Q(map.ArchiveColumn) + "=@value WHERE " + Q(map.Key) + "=TRY_CONVERT(int,@id)", new { value = map.ArchiveValue, id = duplicateId }, transaction);

                                connection.Execute(@"INSERT INTO DuplicateMergeArchive(ModuleName,TableName,PrimaryKeyName,SurvivorRecordID,DuplicateRecordID,MergedBy)
VALUES(@module,@table,@key,@survivor,@duplicate,@userName)", new
                                {
                                    module = module.ToString(), table = map.Table, key = map.Key, survivor = plan.SurvivorId, duplicate = duplicateId,
                                    userName = SessionManager.CurrentUser == null ? "System" : (SessionManager.CurrentUser.DisplayName ?? SessionManager.CurrentUser.Username ?? "User")
                                }, transaction);
                            }
                        }
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }

            int auditRecordId;
            if (!int.TryParse(plans[0].SurvivorId, out auditRecordId))
                auditRecordId = 0;
            SessionManager.LogAction("MERGE", module.ToString(), auditRecordId, "Smart Upload bulk duplicate cleanup archived " + archivedRecords + " record(s) across " + plans.Count + " group(s); reassigned " + reassigned + " reference(s).");
            return new DuplicateCleanupResult
            {
                ArchivedRecords = archivedRecords,
                MergedGroups = plans.Count,
                ReassignedReferences = reassigned,
                Message = archivedRecords + " duplicate record(s) across " + plans.Count + " group(s) archived. " + reassigned + " linked reference(s) moved to the selected survivors."
            };
        }

        internal static void ValidateBulkSelection(IEnumerable<DuplicateCleanupPlan> cleanupPlans)
        {
            NormalizePlans(cleanupPlans);
        }

        private static List<NormalizedCleanupPlan> NormalizePlans(IEnumerable<DuplicateCleanupPlan> cleanupPlans)
        {
            var normalized = new List<NormalizedCleanupPlan>();
            foreach (DuplicateCleanupPlan source in cleanupPlans ?? Enumerable.Empty<DuplicateCleanupPlan>())
            {
                if (source == null || string.IsNullOrWhiteSpace(source.SurvivorId))
                    continue;

                string survivorId = source.SurvivorId.Trim();
                List<string> duplicateIds = (source.DuplicateIds ?? Enumerable.Empty<string>())
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Select(id => id.Trim())
                    .Where(id => !string.Equals(id, survivorId, StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (duplicateIds.Count > 0)
                    normalized.Add(new NormalizedCleanupPlan(survivorId, duplicateIds));
            }

            if (normalized.Count == 0)
                throw new InvalidOperationException("Select at least one duplicate group with a survivor and records to archive.");

            var duplicateOwners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (NormalizedCleanupPlan plan in normalized)
                foreach (string duplicateId in plan.DuplicateIds)
                    if (!duplicateOwners.Add(duplicateId))
                        throw new InvalidOperationException("Some selected duplicate groups overlap. Resolve the overlapping group separately, then select all remaining groups.");

            if (normalized.Any(plan => duplicateOwners.Contains(plan.SurvivorId)))
                throw new InvalidOperationException("A selected survivor is also marked for archival in another group. Review the overlapping groups before bulk cleanup.");

            return normalized;
        }

        private void EnsureSchema()
        {
            using (SqlConnection connection = _database.GetConnection())
            {
                connection.Open();
                connection.Execute(@"IF OBJECT_ID(N'dbo.DuplicateMergeArchive',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.DuplicateMergeArchive(
  ArchiveID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
  ModuleName NVARCHAR(50) NOT NULL, TableName SYSNAME NOT NULL, PrimaryKeyName SYSNAME NOT NULL,
  SurvivorRecordID NVARCHAR(50) NOT NULL, DuplicateRecordID NVARCHAR(50) NOT NULL,
  MergedAt DATETIME2 NOT NULL CONSTRAINT DF_DuplicateMergeArchive_MergedAt DEFAULT SYSUTCDATETIME(),
  MergedBy NVARCHAR(150) NULL,
  CONSTRAINT UQ_DuplicateMergeArchive UNIQUE(ModuleName,DuplicateRecordID)
 );
END");
            }
        }

        private static List<ForeignKeyRow> LoadForeignKeys(SqlConnection connection, SqlTransaction transaction, string table, string key)
        {
            return connection.Query<ForeignKeyRow>(@"SELECT OBJECT_SCHEMA_NAME(fkc.parent_object_id) SchemaName,
OBJECT_NAME(fkc.parent_object_id) TableName, COL_NAME(fkc.parent_object_id,fkc.parent_column_id) ColumnName
FROM sys.foreign_key_columns fkc
WHERE OBJECT_NAME(fkc.referenced_object_id)=@table AND COL_NAME(fkc.referenced_object_id,fkc.referenced_column_id)=@key", new { table, key }, transaction).ToList();
        }

        private static string Q(string value) { return "[" + value.Replace("]", "]]" ) + "]"; }

        private static ModuleMap GetMap(ExcelImportModule module)
        {
            switch (module)
            {
                case ExcelImportModule.Quotations: return new ModuleMap("Quotations", "BidID");
                case ExcelImportModule.Invoices: return new ModuleMap("Invoices", "InvoiceID");
                case ExcelImportModule.Payments: return new ModuleMap("Payments", "PaymentID");
                case ExcelImportModule.Purchases: return new ModuleMap("PurchaseOrders", "POID");
                case ExcelImportModule.Jobs: return new ModuleMap("Jobs", "JobID");
                case ExcelImportModule.Clients: return new ModuleMap("B2BClients", "ClientID", "IsActive", false);
                case ExcelImportModule.Employees: return new ModuleMap("Employees", "EmployeeID");
                case ExcelImportModule.Vendors: return new ModuleMap("Vendors", "VendorID", "IsArchived", true);
                case ExcelImportModule.Sites: return new ModuleMap("ClientSites", "SiteID");
                case ExcelImportModule.Inventory: return new ModuleMap("StockItems", "ItemID", "IsActive", false);
                case ExcelImportModule.SupplierItemPrices: return new ModuleMap("SupplierItemPrices", "PriceID", "IsActive", false);
                case ExcelImportModule.AMC: return new ModuleMap("AMCContracts", "ContractID");
                default: throw new NotSupportedException("Duplicate cleanup is not available for " + module + ".");
            }
        }

        private sealed class ModuleMap
        {
            public ModuleMap(string table, string key, string archiveColumn = null, bool archiveValue = true) { Table = table; Key = key; ArchiveColumn = archiveColumn; ArchiveValue = archiveValue; }
            public string Table { get; private set; }
            public string Key { get; private set; }
            public string ArchiveColumn { get; private set; }
            public bool ArchiveValue { get; private set; }
        }
        private sealed class NormalizedCleanupPlan
        {
            public NormalizedCleanupPlan(string survivorId, List<string> duplicateIds) { SurvivorId = survivorId; DuplicateIds = duplicateIds; }
            public string SurvivorId { get; private set; }
            public List<string> DuplicateIds { get; private set; }
        }
        private sealed class ForeignKeyRow { public string SchemaName { get; set; } public string TableName { get; set; } public string ColumnName { get; set; } }
    }
}

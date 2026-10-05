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
        public int ResolvedChildConflicts { get; set; }
        public int DeletedRecords { get; set; }
        public string Message { get; set; }
    }

    public sealed class DuplicateCleanupPlan
    {
        public string SurvivorId { get; set; }
        public IEnumerable<string> DuplicateIds { get; set; }
    }

    public sealed class DuplicateCleanupPlanningResult
    {
        public List<DuplicateCleanupPlan> Plans { get; set; } = new List<DuplicateCleanupPlan>();
        public int SourceGroupCount { get; set; }
        public int ConsolidatedOverlapCount { get; set; }
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
            return ExecuteCleanup(module, cleanupPlans, false);
        }

        public DuplicateCleanupResult MergeAndDeleteGroups(ExcelImportModule module, IEnumerable<DuplicateCleanupPlan> cleanupPlans)
        {
            return ExecuteCleanup(module, cleanupPlans, true);
        }

        private DuplicateCleanupResult ExecuteCleanup(ExcelImportModule module, IEnumerable<DuplicateCleanupPlan> cleanupPlans, bool deleteParentRecords)
        {
            ModuleMap map = GetMap(module);
            DuplicateCleanupPlanningResult planning = BuildSmartBulkPlan(cleanupPlans);
            List<NormalizedCleanupPlan> plans = NormalizePlans(planning.Plans);
            int archivedRecords = plans.Sum(plan => plan.DuplicateIds.Count);

            EnsureSchema();
            int reassigned = 0;
            int resolvedChildConflicts = 0;
            int deletedRecords = 0;
            using (SqlConnection connection = _database.GetConnection())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        List<ForeignKeyRow> foreignKeys = LoadForeignKeys(connection, transaction, map.Table, map.Key);
                        Dictionary<string, List<UniqueIndexDefinition>> uniqueIndexesByForeignKey = foreignKeys
                            .GroupBy(ForeignKeyCacheKey, StringComparer.OrdinalIgnoreCase)
                            .ToDictionary(group => group.Key, group => LoadUniqueIndexes(connection, transaction, group.First()), StringComparer.OrdinalIgnoreCase);
                        foreach (NormalizedCleanupPlan plan in plans)
                        {
                            int survivorExists = connection.ExecuteScalar<int>("SELECT COUNT(1) FROM " + Q(map.Table) + " WHERE " + Q(map.Key) + "=TRY_CONVERT(int,@id)", new { id = plan.SurvivorId }, transaction);
                            if (survivorExists != 1)
                                throw new InvalidOperationException("A selected survivor record no longer exists. Refresh the duplicate list and try again.");

                            foreach (string duplicateId in plan.DuplicateIds)
                            {
                                if (module == ExcelImportModule.Sites)
                                {
                                    int matchingClient = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM ClientSites s INNER JOIN ClientSites d ON d.ClientID=s.ClientID WHERE s.SiteID=TRY_CONVERT(int,@survivor) AND d.SiteID=TRY_CONVERT(int,@duplicate)", new { survivor = plan.SurvivorId, duplicate = duplicateId }, transaction);
                                    if (matchingClient != 1) throw new InvalidOperationException("Sites belonging to different clients cannot be merged. Correct the client ownership first.");
                                }
                                if (module == ExcelImportModule.Clients)
                                    connection.Execute(@"UPDATE q SET ClientName=c.CompanyName FROM Quotations q INNER JOIN B2BClients c ON c.ClientID=TRY_CONVERT(int,@survivor) WHERE q.ClientID=TRY_CONVERT(int,@duplicate)", new { survivor = plan.SurvivorId, duplicate = duplicateId }, transaction);

                                foreach (ForeignKeyRow fk in foreignKeys)
                                {
                                    foreach (UniqueIndexDefinition uniqueIndex in uniqueIndexesByForeignKey[ForeignKeyCacheKey(fk)])
                                    {
                                        string conflictSql = BuildUniqueChildConflictDeleteSql(fk.SchemaName, fk.TableName, fk.ColumnName, uniqueIndex.OtherColumns);
                                        resolvedChildConflicts += connection.Execute(conflictSql, new { survivor = plan.SurvivorId, duplicate = duplicateId }, transaction);
                                    }
                                    string sql = "UPDATE " + Q(fk.SchemaName) + "." + Q(fk.TableName) + " SET " + Q(fk.ColumnName) + "=TRY_CONVERT(int,@survivor) WHERE " + Q(fk.ColumnName) + "=TRY_CONVERT(int,@duplicate)";
                                    reassigned += connection.Execute(sql, new { survivor = plan.SurvivorId, duplicate = duplicateId }, transaction);
                                }

                                if (deleteParentRecords)
                                    deletedRecords += connection.Execute("DELETE FROM " + Q(map.Table) + " WHERE " + Q(map.Key) + "=TRY_CONVERT(int,@id)", new { id = duplicateId }, transaction);
                                else if (!string.IsNullOrWhiteSpace(map.ArchiveColumn))
                                    connection.Execute("UPDATE " + Q(map.Table) + " SET " + Q(map.ArchiveColumn) + "=@value WHERE " + Q(map.Key) + "=TRY_CONVERT(int,@id)", new { value = map.ArchiveValue, id = duplicateId }, transaction);

                                // Keep previous aliases pointing at the current survivor after later merges.
                                connection.Execute("UPDATE DuplicateMergeArchive SET SurvivorRecordID=@survivor WHERE ModuleName=@module AND SurvivorRecordID=@duplicate", new { survivor = plan.SurvivorId, duplicate = duplicateId, module = module.ToString() }, transaction);

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

            foreach (string prefix in new[] { "clients:", "sites:", "tenders:", "quotations:", "contracts:", "amc:", "invoices:", "jobs:", "purchases:" })
                AppDataCache.RemovePrefix(prefix);

            int auditRecordId;
            if (!int.TryParse(plans[0].SurvivorId, out auditRecordId))
                auditRecordId = 0;
            string action = deleteParentRecords ? "DELETE" : "MERGE";
            SessionManager.LogAction(action, module.ToString(), auditRecordId, "Smart Upload duplicate cleanup " + (deleteParentRecords ? "deleted " : "archived ") + archivedRecords + " record(s) across " + plans.Count + " group(s); reassigned " + reassigned + " reference(s); resolved " + resolvedChildConflicts + " unique child conflict(s).");
            return new DuplicateCleanupResult
            {
                ArchivedRecords = archivedRecords,
                MergedGroups = plans.Count,
                ReassignedReferences = reassigned,
                ResolvedChildConflicts = resolvedChildConflicts,
                DeletedRecords = deletedRecords,
                Message = archivedRecords + " duplicate record(s) across " + plans.Count + " group(s) " + (deleteParentRecords ? "deleted" : "archived") + ". " + reassigned + " linked reference(s) moved to the selected survivors. " + resolvedChildConflicts + " duplicate child record conflict(s) safely retained on the survivor."
            };
        }

        public static string BuildUniqueChildConflictDeleteSql(string schemaName, string tableName, string foreignKeyColumn, IEnumerable<string> otherUniqueColumns)
        {
            List<string> columns = (otherUniqueColumns ?? Enumerable.Empty<string>()).Where(column => !string.IsNullOrWhiteSpace(column)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            string equality = columns.Count == 0
                ? "1=1"
                : string.Join(" AND ", columns.Select(column => "((s." + Q(column) + "=d." + Q(column) + ") OR (s." + Q(column) + " IS NULL AND d." + Q(column) + " IS NULL))"));
            return "DELETE d FROM " + Q(schemaName) + "." + Q(tableName) + " d WHERE d." + Q(foreignKeyColumn) + "=TRY_CONVERT(int,@duplicate) AND EXISTS (SELECT 1 FROM " + Q(schemaName) + "." + Q(tableName) + " s WHERE s." + Q(foreignKeyColumn) + "=TRY_CONVERT(int,@survivor) AND " + equality + ")";
        }

        internal static void ValidateBulkSelection(IEnumerable<DuplicateCleanupPlan> cleanupPlans)
        {
            NormalizePlans(BuildSmartBulkPlan(cleanupPlans).Plans);
        }

        public static DuplicateCleanupPlanningResult BuildSmartBulkPlan(IEnumerable<DuplicateCleanupPlan> cleanupPlans)
        {
            List<DuplicateCleanupPlan> sources = (cleanupPlans ?? Enumerable.Empty<DuplicateCleanupPlan>())
                .Where(plan => plan != null && !string.IsNullOrWhiteSpace(plan.SurvivorId))
                .Select(plan => new DuplicateCleanupPlan
                {
                    SurvivorId = plan.SurvivorId.Trim(),
                    DuplicateIds = (plan.DuplicateIds ?? Enumerable.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                })
                .Where(plan => plan.DuplicateIds.Any(id => !string.Equals(id, plan.SurvivorId, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (sources.Count == 0)
                throw new InvalidOperationException("Select at least one duplicate group with a survivor and records to archive.");

            var components = new List<List<DuplicateCleanupPlan>>();
            foreach (DuplicateCleanupPlan source in sources)
            {
                var sourceIds = new HashSet<string>((source.DuplicateIds ?? Enumerable.Empty<string>()).Concat(new[] { source.SurvivorId }), StringComparer.OrdinalIgnoreCase);
                List<List<DuplicateCleanupPlan>> matches = components.Where(component => component.Any(plan =>
                    (plan.DuplicateIds ?? Enumerable.Empty<string>()).Concat(new[] { plan.SurvivorId }).Any(sourceIds.Contains))).ToList();
                if (matches.Count == 0)
                {
                    components.Add(new List<DuplicateCleanupPlan> { source });
                    continue;
                }
                List<DuplicateCleanupPlan> target = matches[0];
                target.Add(source);
                foreach (List<DuplicateCleanupPlan> extra in matches.Skip(1).ToList())
                {
                    target.AddRange(extra);
                    components.Remove(extra);
                }
            }

            var result = new DuplicateCleanupPlanningResult { SourceGroupCount = sources.Count };
            foreach (List<DuplicateCleanupPlan> component in components)
            {
                var allIds = new HashSet<string>(component.SelectMany(plan => (plan.DuplicateIds ?? Enumerable.Empty<string>()).Concat(new[] { plan.SurvivorId })), StringComparer.OrdinalIgnoreCase);
                string survivor = component.GroupBy(plan => plan.SurvivorId, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(group => group.Count())
                    .ThenBy(group => ParseSortableId(group.Key))
                    .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.Key)
                    .First();
                result.Plans.Add(new DuplicateCleanupPlan { SurvivorId = survivor, DuplicateIds = allIds.Where(id => !string.Equals(id, survivor, StringComparison.OrdinalIgnoreCase)).ToList() });
                if (component.Count > 1) result.ConsolidatedOverlapCount += component.Count - 1;
            }
            return result;
        }

        private static long ParseSortableId(string value)
        {
            return long.TryParse(value, out long parsed) ? parsed : long.MaxValue;
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

        private static List<UniqueIndexDefinition> LoadUniqueIndexes(SqlConnection connection, SqlTransaction transaction, ForeignKeyRow foreignKey)
        {
            List<UniqueIndexColumnRow> rows = connection.Query<UniqueIndexColumnRow>(@"SELECT i.name IndexName,c.name ColumnName,ic.key_ordinal KeyOrdinal
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
INNER JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
WHERE i.object_id=OBJECT_ID(QUOTENAME(@schemaName)+'.'+QUOTENAME(@tableName))
  AND i.is_unique=1 AND i.has_filter=0 AND ic.is_included_column=0
  AND EXISTS(SELECT 1 FROM sys.index_columns fkic INNER JOIN sys.columns fkc ON fkc.object_id=fkic.object_id AND fkc.column_id=fkic.column_id
             WHERE fkic.object_id=i.object_id AND fkic.index_id=i.index_id AND fkc.name=@columnName)
ORDER BY i.name,ic.key_ordinal", new { schemaName = foreignKey.SchemaName, tableName = foreignKey.TableName, columnName = foreignKey.ColumnName }, transaction).ToList();

            return rows.GroupBy(row => row.IndexName, StringComparer.OrdinalIgnoreCase)
                .Select(group => new UniqueIndexDefinition
                {
                    Name = group.Key,
                    OtherColumns = group.OrderBy(row => row.KeyOrdinal).Where(row => !string.Equals(row.ColumnName, foreignKey.ColumnName, StringComparison.OrdinalIgnoreCase)).Select(row => row.ColumnName).ToList()
                }).ToList();
        }

        private static string ForeignKeyCacheKey(ForeignKeyRow foreignKey)
        {
            return foreignKey.SchemaName + "." + foreignKey.TableName + "." + foreignKey.ColumnName;
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
        private sealed class UniqueIndexColumnRow { public string IndexName { get; set; } public string ColumnName { get; set; } public int KeyOrdinal { get; set; } }
        private sealed class UniqueIndexDefinition { public string Name { get; set; } public List<string> OtherColumns { get; set; } }
    }
}

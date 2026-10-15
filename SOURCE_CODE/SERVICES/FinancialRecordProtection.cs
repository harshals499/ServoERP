using System;
using System.Data.SqlClient;
using Dapper;
using HVAC_Pro_Desktop.DAL;

namespace HVAC_Pro_Desktop.Services
{
    public static class FinancialRecordProtection
    {
        private sealed class RecordState { public bool IsPosted { get; set; } public bool IsDeleted { get; set; } }
        internal static void StampActor(SqlConnection connection)
        {
            connection.Execute(@"EXEC sys.sp_set_session_context @key=N'ServoERP.UserId', @value=@UserId;
EXEC sys.sp_set_session_context @key=N'ServoERP.UserName', @value=@UserName;
EXEC sys.sp_set_session_context @key=N'ServoERP.Maintenance', @value=@Maintenance;
EXEC sys.sp_set_session_context @key=N'ServoERP.SkipAudit', @value=@SkipAudit;",
                new { UserId = (int?)SessionManager.CurrentUser?.UserId, UserName = SessionManager.CurrentUser?.DisplayName ?? Environment.UserName,
                    Maintenance = DatabaseUpgradeService.IsLegacyUpgrade ? 1 : 0,
                    // Schema backfills must not replay business audit history on every startup.
                    SkipAudit = DatabaseUpgradeService.IsLegacyUpgrade ? (int?)1 : null });
        }
        internal static void SetMaintenance(SqlConnection connection, bool skipAudit = false)
        {
            connection.Execute(@"EXEC sys.sp_set_session_context @key=N'ServoERP.Maintenance', @value=1;
EXEC sys.sp_set_session_context @key=N'ServoERP.SkipAudit', @value=@Skip;", new { Skip = skipAudit ? 1 : 0 });
        }
        internal static void EnsureDraft(SqlConnection connection, SqlTransaction transaction, int id)
        {
            var state = connection.QuerySingleOrDefault<RecordState>("SELECT IsPosted,IsDeleted FROM dbo.Invoices WITH(UPDLOCK,HOLDLOCK) WHERE InvoiceID=@Id", new { Id = id }, transaction);
            if (state == null) throw new InvalidOperationException("Invoice not found.");
            if (state.IsDeleted) throw new InvalidOperationException("This invoice is archived. Restore it before editing.");
            if (state.IsPosted) throw new InvalidOperationException("This invoice is posted and locked. Use a credit note for financial corrections.");
        }
        public static void ArchiveInvoice(int id)
        {
            using (var connection = DatabaseConnectionFactory.CreateConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    var state = connection.QuerySingleOrDefault<RecordState>("SELECT IsPosted,IsDeleted FROM dbo.Invoices WITH(UPDLOCK,HOLDLOCK) WHERE InvoiceID=@Id", new { Id = id }, transaction);
                    if (state == null) throw new InvalidOperationException("Invoice not found.");
                    if (state.IsDeleted) return;
                    if (!state.IsPosted)
                    {
                        connection.Execute(@"UPDATE s SET ReservedStock=CASE WHEN ISNULL(s.ReservedStock,0)>=r.Quantity THEN ISNULL(s.ReservedStock,0)-r.Quantity ELSE 0 END, LastUpdated=GETDATE()
FROM dbo.StockItems s JOIN (SELECT StockItemID,SUM(QuantityReserved) Quantity FROM dbo.InvoiceInventoryReservations WHERE InvoiceID=@Id AND Status='DraftReserved' GROUP BY StockItemID) r ON s.ItemID=r.StockItemID;
UPDATE dbo.InvoiceInventoryReservations SET QuantityReserved=0,Status='Cancelled',UpdatedDate=GETDATE() WHERE InvoiceID=@Id AND Status='DraftReserved';", new { Id = id }, transaction);
                    }
                    connection.Execute(@"UPDATE dbo.Invoices SET IsDeleted=1,DeletedUtc=SYSUTCDATETIME(),DeletedByUserId=@UserId,DeletedByName=@UserName WHERE InvoiceID=@Id AND IsDeleted=0;",
                        new { Id = id, UserId = (int?)SessionManager.CurrentUser?.UserId, UserName = SessionManager.CurrentUser?.DisplayName ?? Environment.UserName }, transaction);
                    transaction.Commit();
                }
            }
            AppDataCache.RemovePrefix("inventory:");
        }
        public static void ArchiveJob(int id)
        {
            using (var connection = DatabaseConnectionFactory.CreateConnection())
            {
                connection.Open();
                connection.Execute(@"UPDATE dbo.Jobs SET IsDeleted=1,DeletedUtc=SYSUTCDATETIME(),DeletedByUserId=@UserId,DeletedByName=@UserName WHERE JobID=@Id AND IsDeleted=0",
                    new { Id = id, UserId = (int?)SessionManager.CurrentUser?.UserId, UserName = SessionManager.CurrentUser?.DisplayName ?? Environment.UserName });
            }
        }
        public static void Restore(string module, int id)
        {
            if (module != "Invoices" && module != "Jobs") throw new ArgumentException("Unsupported module.");
            SessionManager.DemandPermission(module == "Jobs" ? "WorkOrders" : module, "Edit");
            using (var connection = DatabaseConnectionFactory.CreateConnection())
            {
                connection.Open();
                string sql = module == "Invoices"
                    ? "UPDATE dbo.Invoices SET IsDeleted=0,DeletedUtc=NULL,DeletedByUserId=NULL,DeletedByName=NULL WHERE InvoiceID=@Id AND IsDeleted=1"
                    : "UPDATE dbo.Jobs SET IsDeleted=0,DeletedUtc=NULL,DeletedByUserId=NULL,DeletedByName=NULL WHERE JobID=@Id AND IsDeleted=1";
                connection.Execute(sql, new { Id = id });
            }
            AppDataCache.RemovePrefix("invoices:"); AppDataCache.RemovePrefix("jobs:");
            DashboardRefreshService.NotifyChanged(module);
        }
    }
}

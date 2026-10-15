using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using System.Linq;

namespace HVAC_Pro_Desktop.Services
{
    // Retained for public API compatibility. Startup no longer uses upgrade recovery exceptions.
    public sealed class DatabaseUpgradeException : Exception
    {
        public DatabaseUpgradeException(string message, Exception inner = null) : base(message, inner) { }
    }

    public static class DatabaseUpgradeService
    {
        [ThreadStatic] private static bool _legacyUpgrade;
        internal static bool IsLegacyUpgrade => _legacyUpgrade;

        // Compatibility entry point: ordinary schema guards, without an upgrade journal or recovery gate.
        public static void Upgrade(string connectionString, Action legacySchemaUpgrade)
        {
            Run(connectionString, null, legacySchemaUpgrade, ReadScripts(), false);
        }

        internal static void Run(string connectionString, string version, Action legacySchemaUpgrade,
            IDictionary<string, string> scripts, bool retry)
        {
            _legacyUpgrade = true;
            try { legacySchemaUpgrade(); }
            finally { _legacyUpgrade = false; }
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                foreach (var script in scripts.OrderBy(s => s.Key, StringComparer.Ordinal))
                {
                    // Retired policy must never be installed again. Financial schema remains required.
                    if (script.Key == "0003_SafeUpgradePolicy") continue;
                    if (script.Key == "0004_FinancialRecordProtection" && HasFinancialSchema(connection)) continue;
                    using (var transaction = connection.BeginTransaction())
                    using (var command = new SqlCommand(script.Value, connection, transaction))
                    {
                        // Embedded schema DDL intentionally uses ADO.NET.
                        command.CommandTimeout = 600;
                        command.ExecuteNonQuery();
                        transaction.Commit();
                    }
                }
            }
        }

        private static bool HasFinancialSchema(SqlConnection connection)
        {
            // Metadata query intentionally uses ADO.NET. Avoid replaying financial backfills on every launch.
            const string sql = @"SELECT CASE WHEN
OBJECT_ID('dbo.FinancialRecordAudit','U') IS NOT NULL
AND OBJECT_ID('dbo.ActiveInvoices','V') IS NOT NULL
AND OBJECT_ID('dbo.FinancialInvoices','V') IS NOT NULL
AND OBJECT_ID('dbo.ActiveJobs','V') IS NOT NULL
AND OBJECT_ID('dbo.TR_Invoices_FinancialProtection','TR') IS NOT NULL
AND OBJECT_ID('dbo.TR_Jobs_FinancialProtection','TR') IS NOT NULL
AND OBJECT_ID('dbo.TR_InvoiceLineItems_FinancialProtection','TR') IS NOT NULL
AND COL_LENGTH('dbo.Invoices','IsDeleted') IS NOT NULL
AND COL_LENGTH('dbo.Invoices','DeletedUtc') IS NOT NULL
AND COL_LENGTH('dbo.Invoices','DeletedByUserId') IS NOT NULL
AND COL_LENGTH('dbo.Invoices','DeletedByName') IS NOT NULL
AND COL_LENGTH('dbo.Invoices','IsPosted') IS NOT NULL
AND COL_LENGTH('dbo.Invoices','PostedUtc') IS NOT NULL
AND COL_LENGTH('dbo.Jobs','IsDeleted') IS NOT NULL
AND COL_LENGTH('dbo.Jobs','DeletedUtc') IS NOT NULL
AND COL_LENGTH('dbo.Jobs','DeletedByUserId') IS NOT NULL
AND COL_LENGTH('dbo.Jobs','DeletedByName') IS NOT NULL
AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.FinancialRecordAudit') AND name='IX_FinancialRecordAudit_Record')
THEN 1 ELSE 0 END";
            using (var command = new SqlCommand(sql, connection)) return Convert.ToInt32(command.ExecuteScalar()) == 1;
        }

        internal static SortedDictionary<string, string> ReadScripts()
        {
            var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var assembly = Assembly.GetExecutingAssembly();
            const string prefix = "ServoERP.NumberedMigrations.";
            foreach (string name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(prefix, StringComparison.Ordinal) && n.EndsWith(".sql", StringComparison.Ordinal)))
                using (var reader = new StreamReader(assembly.GetManifestResourceStream(name)))
                    result.Add(name.Substring(prefix.Length, name.Length - prefix.Length - 4), reader.ReadToEnd());
            return result;
        }

    }
}

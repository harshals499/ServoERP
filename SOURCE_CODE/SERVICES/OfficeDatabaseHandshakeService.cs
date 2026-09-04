using System;
using System.Data.SqlClient;
using Dapper;
using HVAC_Pro_Desktop.DAL;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class OfficeDatabaseIdentityMismatchException : InvalidOperationException
    {
        public OfficeDatabaseIdentityMismatchException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Pins each installed terminal to one authoritative office database identity.
    /// This prevents a reachable but unrelated HVAC_PRO database from accepting writes.
    /// </summary>
    public static class OfficeDatabaseHandshakeService
    {
        private const string ConfigSection = "Database";
        private const string ConfigKey = "OfficeDatabaseId";

        public static void VerifyBeforeSchemaUpgrade(string connectionString)
        {
            Guid? expected = GetPinnedOfficeDatabaseId();
            Guid? actual = ReadOfficeDatabaseId(connectionString);

            if (expected.HasValue && !actual.HasValue)
                throw new OfficeDatabaseIdentityMismatchException(
                    "The SQL Server is reachable, but it does not contain the office database handshake. " +
                    "ServoERP blocked startup before changing data. Connect this terminal to its original office database.");

            ValidateIdentity(expected, actual);
        }

        public static Guid VerifyAndPinConfiguredDatabase()
        {
            return VerifyAndPin(DatabaseManager.RequireConfiguredConnectionString());
        }

        public static Guid VerifyAndPin(string connectionString)
        {
            Guid? actual = ReadOfficeDatabaseId(connectionString);
            if (!actual.HasValue || actual.Value == Guid.Empty)
                throw new InvalidOperationException("The connected database has not completed ServoERP office handshake setup.");

            Guid? expected = GetPinnedOfficeDatabaseId();
            ValidateIdentity(expected, actual);
            if (!expected.HasValue)
                ConfigService.Set(ConfigSection, ConfigKey, actual.Value.ToString("D"));

            return actual.Value;
        }

        public static void VerifyCandidateDatabase(string connectionString)
        {
            Guid? actual = ReadOfficeDatabaseId(connectionString);
            Guid? expected = GetPinnedOfficeDatabaseId();
            if (expected.HasValue && !actual.HasValue)
                throw new OfficeDatabaseIdentityMismatchException(
                    "This terminal is already enrolled with another office database. The selected database has no matching handshake.");

            ValidateIdentity(expected, actual);
        }

        public static void VerifyOpenConnection(SqlConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));
            if (connection.State != System.Data.ConnectionState.Open)
                throw new InvalidOperationException("The office database handshake requires an open SQL connection.");

            Guid? expected = GetPinnedOfficeDatabaseId();
            if (!expected.HasValue)
                return;

            Guid? actual = connection.QuerySingleOrDefault<Guid?>(
                "IF OBJECT_ID('dbo.OfficeDatabaseIdentity', 'U') IS NULL SELECT CAST(NULL AS UNIQUEIDENTIFIER) " +
                "ELSE SELECT TOP (1) OfficeDatabaseId FROM dbo.OfficeDatabaseIdentity WHERE IdentityKey = 1;");
            ValidateIdentity(expected, actual);
        }

        public static Guid? GetPinnedOfficeDatabaseId()
        {
            Guid parsed;
            string raw = ConfigService.Get(ConfigSection, ConfigKey, string.Empty);
            return Guid.TryParse(raw, out parsed) && parsed != Guid.Empty ? (Guid?)parsed : null;
        }

        internal static void ValidateIdentity(Guid? expected, Guid? actual)
        {
            if (!expected.HasValue)
                return;

            if (!actual.HasValue || actual.Value == Guid.Empty || actual.Value != expected.Value)
                throw new OfficeDatabaseIdentityMismatchException(
                    "Office database handshake mismatch. ServoERP blocked business writes to prevent data loss or split-office records. " +
                    "Expected office database " + expected.Value.ToString("D") +
                    ", but the connected SQL database reported " + (actual.HasValue ? actual.Value.ToString("D") : "no identity") + ".");
        }

        private static Guid? ReadOfficeDatabaseId(string connectionString)
        {
            using (SqlConnection connection = DatabaseConnectionFactory.CreateConnection(connectionString))
            {
                DatabaseConnectionFactory.Open(connection, "OfficeDatabaseHandshakeService.ReadOfficeDatabaseId");
                return connection.QuerySingleOrDefault<Guid?>(
                    "IF OBJECT_ID('dbo.OfficeDatabaseIdentity', 'U') IS NULL SELECT CAST(NULL AS UNIQUEIDENTIFIER) " +
                    "ELSE SELECT TOP (1) OfficeDatabaseId FROM dbo.OfficeDatabaseIdentity WHERE IdentityKey = 1;");
            }
        }
    }
}

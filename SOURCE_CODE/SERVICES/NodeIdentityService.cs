using System;
using System.Data.SqlClient;
using System.Reflection;
using System.Threading;
using Dapper;
using HVAC_Pro_Desktop.DAL;

namespace HVAC_Pro_Desktop.Services
{
    public static class NodeIdentityService
    {
        private const string NodeIdSection = "Sync";
        private const string NodeIdKey = "NodePublicId";
        private const string NodeNameKey = "NodeName";
        private static readonly object Sync = new object();
        private static Guid? _cachedNodeId;
        private static Timer _heartbeatTimer;

        public static Guid GetOrCreateNodePublicId()
        {
            lock (Sync)
            {
                if (_cachedNodeId.HasValue && _cachedNodeId.Value != Guid.Empty)
                    return _cachedNodeId.Value;

                string raw = ConfigService.Get(NodeIdSection, NodeIdKey, string.Empty);
                Guid nodeId;
                if (!Guid.TryParse(raw, out nodeId) || nodeId == Guid.Empty)
                {
                    nodeId = Guid.NewGuid();
                    ConfigService.Set(NodeIdSection, NodeIdKey, nodeId.ToString("D"));
                }

                if (string.IsNullOrWhiteSpace(ConfigService.Get(NodeIdSection, NodeNameKey, string.Empty)))
                    ConfigService.Set(NodeIdSection, NodeNameKey, Environment.MachineName);

                _cachedNodeId = nodeId;
                return nodeId;
            }
        }

        public static string GetNodeName()
        {
            string configured = ConfigService.Get(NodeIdSection, NodeNameKey, string.Empty);
            return string.IsNullOrWhiteSpace(configured) ? Environment.MachineName : configured.Trim();
        }

        public static void EnsureRegistered()
        {
            Guid nodeId = GetOrCreateNodePublicId();
            string connectionString = DatabaseManager.GetConfiguredConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString))
                return;

            try
            {
                using (SqlConnection conn = DatabaseConnectionFactory.CreateConnection(connectionString))
                {
                    DatabaseConnectionFactory.Open(conn, "NodeIdentityService.EnsureRegistered");
                    SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString);
                    Version version = Assembly.GetExecutingAssembly().GetName().Version;
                    Guid? officeDatabaseId = OfficeDatabaseHandshakeService.GetPinnedOfficeDatabaseId();
                    conn.Execute(@"
IF EXISTS (SELECT 1 FROM dbo.SyncNodes WHERE NodePublicId = @nodePublicId)
BEGIN
                    UPDATE dbo.SyncNodes
                    SET NodeName = @nodeName,
                        MachineName = @machineName,
                        ServerRole = @serverRole,
                        IsActive = 1,
                        AppVersion = @appVersion,
                        DatabaseServer = @databaseServer,
                        DatabaseName = @databaseName,
                        LastHealthStatus = 'Healthy',
                        LastHealthDetail = 'Office database handshake verified.',
                        PinnedOfficeDatabaseId = @officeDatabaseId,
                        LastHandshakeUtc = GETUTCDATE(),
                        LastHandshakeStatus = 'Verified',
                        LastSeenUtc = GETUTCDATE()
    WHERE NodePublicId = @nodePublicId;
END
ELSE
BEGIN
    INSERT INTO dbo.SyncNodes
        (NodePublicId, NodeName, MachineName, ServerRole, IsActive, AppVersion, DatabaseServer, DatabaseName, LastHealthStatus, LastHealthDetail, PinnedOfficeDatabaseId, LastHandshakeUtc, LastHandshakeStatus, LastSeenUtc, CreatedUtc)
    VALUES
        (@nodePublicId, @nodeName, @machineName, @serverRole, 1, @appVersion, @databaseServer, @databaseName, 'Healthy', 'Office database handshake verified.', @officeDatabaseId, GETUTCDATE(), 'Verified', GETUTCDATE(), GETUTCDATE());
END",
                        new
                        {
                            nodePublicId = nodeId,
                            nodeName = GetNodeName(),
                            machineName = Environment.MachineName,
                            serverRole = ConfigService.Get("Database", "ServerRole", "AlwaysOnOfficeServer"),
                            appVersion = version == null ? "Unknown" : version.ToString(),
                            databaseServer = builder.DataSource ?? string.Empty,
                            databaseName = builder.InitialCatalog ?? string.Empty,
                            officeDatabaseId
                        });
                }

                OfficeLanControlService.ProcessPendingCommandsForCurrentNode();
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("NodeIdentityService.EnsureRegistered", ex);
            }
        }

        /// <summary>Keeps this enrolled workstation visible to the office server while ServoERP is open.</summary>
        public static void StartHeartbeat()
        {
            lock (Sync)
            {
                if (_heartbeatTimer != null)
                    return;

                _heartbeatTimer = new Timer(_ => EnsureRegistered(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            }
        }
    }
}

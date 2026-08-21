using System;
using System.Data.SqlClient;
using System.Reflection;
using System.Threading;
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
                using (SqlCommand cmd = new SqlCommand(@"
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
                        LastHealthDetail = 'Connected to the shared office SQL Server.',
                        LastSeenUtc = GETUTCDATE()
    WHERE NodePublicId = @nodePublicId;
END
ELSE
BEGIN
    INSERT INTO dbo.SyncNodes
        (NodePublicId, NodeName, MachineName, ServerRole, IsActive, AppVersion, DatabaseServer, DatabaseName, LastHealthStatus, LastHealthDetail, LastSeenUtc, CreatedUtc)
    VALUES
        (@nodePublicId, @nodeName, @machineName, @serverRole, 1, @appVersion, @databaseServer, @databaseName, 'Healthy', 'Connected to the shared office SQL Server.', GETUTCDATE(), GETUTCDATE());
END", conn))
                {
                    DatabaseConnectionFactory.Open(conn, "NodeIdentityService.EnsureRegistered");
                    cmd.Parameters.AddWithValue("@nodePublicId", nodeId);
                    cmd.Parameters.AddWithValue("@nodeName", GetNodeName());
                    cmd.Parameters.AddWithValue("@machineName", Environment.MachineName);
                    cmd.Parameters.AddWithValue("@serverRole", ConfigService.Get("Database", "ServerRole", "AlwaysOnOfficeServer"));
                    SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString);
                    Version version = Assembly.GetExecutingAssembly().GetName().Version;
                    cmd.Parameters.AddWithValue("@appVersion", version == null ? "Unknown" : version.ToString());
                    cmd.Parameters.AddWithValue("@databaseServer", builder.DataSource ?? string.Empty);
                    cmd.Parameters.AddWithValue("@databaseName", builder.InitialCatalog ?? string.Empty);
                    cmd.ExecuteNonQuery();
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

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    /// <summary>Performs safe, read-only checks before an administrator starts a terminal deployment.</summary>
    public sealed class OfficeLanReadinessService
    {
        public async Task<OfficeLanReadinessResult> CheckAsync(OfficeLanComputer computer, CancellationToken cancellationToken)
        {
            if (computer == null)
                throw new ArgumentNullException(nameof(computer));

            var checks = new List<OfficeLanReadinessCheck>();
            bool reachable = computer.IsReachable || await CanConnectAsync(computer.IpAddress, 445, cancellationToken).ConfigureAwait(false);
            checks.Add(Check("Network", "Network reachability", reachable ? "Passed" : "Failed",
                reachable ? "The terminal responds on the office network." : "The terminal did not respond to the network checks.",
                reachable ? string.Empty : "Confirm that the PC is on, connected to the same routed office network, and not isolated by the firewall.", !reachable));

            bool winRm = computer.SupportsRemoteManagement || await CanConnectAsync(computer.IpAddress, 5985, cancellationToken).ConfigureAwait(false) ||
                         await CanConnectAsync(computer.IpAddress, 5986, cancellationToken).ConfigureAwait(false);
            checks.Add(Check("WinRM", "Remote management", winRm ? "Passed" : "Needs preparation",
                winRm ? "WinRM is accepting terminal-management connections." : "WinRM is not currently reachable. LAN Control will attempt the approved WMI bootstrap during deployment.",
                winRm ? string.Empty : "If automatic preparation fails, run Enable-ServoERP-RemoteManagement.ps1 once as Administrator on this PC.", false));

            checks.Add(Check("Administrator", "Administrator access", "Pending",
                "Windows administrator credentials are requested and verified immediately before files are copied.",
                "Use a local or domain administrator account that is valid on every selected terminal.", false));

            checks.Add(Check("OperatingSystem", "Supported Windows", computer.IsLocalComputer ? "Passed" : "Pending",
                computer.IsLocalComputer ? Environment.OSVersion.VersionString : "Windows version is verified by the terminal agent or authenticated deployment preflight.",
                "ServoERP supports currently serviced Windows 10 and Windows 11 terminals.", false));

            checks.Add(Check("Disk", "Free disk space", "Pending",
                "At least 1.5 GB free space is verified after the authenticated remote session opens.",
                "Free space on drive C: before installation if this check fails.", false));

            checks.Add(Check("DotNet", ".NET Framework 4.7.2+", "Pending",
                "The terminal installer detects and installs the required .NET Framework when necessary.", string.Empty, false));
            checks.Add(Check("WebView2", "Microsoft WebView2", "Pending",
                "The terminal installer detects and installs WebView2 when necessary.", string.Empty, false));

            OfficeLanReadinessCheck sqlCheck = await CheckSqlAsync(cancellationToken).ConfigureAwait(false);
            checks.Add(sqlCheck);

            string overall = EvaluateOverallStatus(checks, winRm);
            return new OfficeLanReadinessResult
            {
                HostName = string.IsNullOrWhiteSpace(computer.HostName) ? computer.IpAddress : computer.HostName,
                OverallStatus = overall,
                CheckedUtc = DateTime.UtcNow,
                Checks = checks
            };
        }

        internal static string EvaluateOverallStatus(IEnumerable<OfficeLanReadinessCheck> checks, bool winRmReady)
        {
            List<OfficeLanReadinessCheck> list = (checks ?? Enumerable.Empty<OfficeLanReadinessCheck>()).ToList();
            if (list.Any(item => item.IsBlocking && string.Equals(item.Status, "Failed", StringComparison.OrdinalIgnoreCase)))
                return "Blocked";
            return winRmReady ? "Ready" : "Needs preparation";
        }

        private static async Task<OfficeLanReadinessCheck> CheckSqlAsync(CancellationToken cancellationToken)
        {
            try
            {
                string configured = DatabaseManager.GetConfiguredConnectionString();
                if (string.IsNullOrWhiteSpace(configured))
                    return Check("Sql", "Shared SQL connection", "Failed", "The office server has no shared SQL connection configured.",
                        "Configure and test the office SQL connection before deploying terminals.", true);

                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var builder = new SqlConnectionStringBuilder(configured) { ConnectTimeout = 5 };
                    using (SqlConnection connection = DatabaseConnectionFactory.CreateConnection(builder.ConnectionString))
                    {
                        DatabaseConnectionFactory.Open(connection, "OfficeLanReadinessService");
                        using (SqlCommand command = new SqlCommand("SELECT 1", connection))
                            command.ExecuteScalar();
                    }
                }, cancellationToken).ConfigureAwait(false);

                return Check("Sql", "Shared SQL connection", "Passed", "The configured SQL login can open the shared HVAC_PRO database.", string.Empty, false);
            }
            catch (Exception ex)
            {
                return Check("Sql", "Shared SQL connection", "Failed", "SQL verification failed: " + SafeMessage(ex),
                    "Correct the SQL server, database, username, or password on the server before deployment.", true);
            }
        }

        private static OfficeLanReadinessCheck Check(string key, string name, string status, string detail, string recommendation, bool blocking)
        {
            return new OfficeLanReadinessCheck
            {
                CheckKey = key,
                Name = name,
                Status = status,
                Detail = detail,
                Recommendation = recommendation,
                IsBlocking = blocking
            };
        }

        private static async Task<bool> CanConnectAsync(string host, int port, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(host))
                return false;
            try
            {
                using (var client = new TcpClient())
                {
                    Task connect = client.ConnectAsync(host, port);
                    Task finished = await Task.WhenAny(connect, Task.Delay(900, cancellationToken)).ConfigureAwait(false);
                    return finished == connect && client.Connected;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string SafeMessage(Exception ex)
        {
            string message = ex == null ? "Unknown error" : ex.Message;
            return message.Length <= 240 ? message : message.Substring(0, 240);
        }
    }
}

using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

namespace HVAC_Pro_Desktop.Services
{
    /// <summary>
    /// Keeps enrolled terminals visible and processes approved SQL-backed commands even when
    /// the desktop UI is closed. The service performs no business-data synchronization.
    /// </summary>
    public sealed class TerminalAgentWindowsService : ServiceBase
    {
        private Timer _timer;
        private int _running;

        public TerminalAgentWindowsService()
        {
            ServiceName = "ServoERPTerminalAgent";
            CanStop = true;
            CanPauseAndContinue = false;
            AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            _timer = new Timer(_ => RunTick(), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        }

        protected override void OnStop()
        {
            Timer timer = Interlocked.Exchange(ref _timer, null);
            timer?.Dispose();
        }

        private void RunTick()
        {
            if (Interlocked.Exchange(ref _running, 1) != 0)
                return;
            Task.Run(() =>
            {
                try
                {
                    RunOnce();
                }
                catch (Exception ex)
                {
                    AppRuntime.LogException("TerminalAgentWindowsService", ex);
                }
                finally
                {
                    Interlocked.Exchange(ref _running, 0);
                }
            });
        }

        public static void RunOnce()
        {
            ConfigService.EnsureLocalConfigFile();
            NodeIdentityService.EnsureRegistered();
            // Drain a small bounded batch so one long queue does not monopolise the service.
            for (int i = 0; i < 4; i++)
                OfficeLanControlService.ProcessPendingCommandsForCurrentNode();
        }
    }
}

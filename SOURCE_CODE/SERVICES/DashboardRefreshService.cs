using System;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class DashboardRefreshEventArgs : EventArgs
    {
        public DashboardRefreshEventArgs(string moduleKey)
        {
            ModuleKey = string.IsNullOrWhiteSpace(moduleKey) ? "General" : moduleKey.Trim();
            OccurredAt = DateTime.Now;
        }

        public string ModuleKey { get; private set; }
        public DateTime OccurredAt { get; private set; }
    }

    internal static class DashboardRefreshService
    {
        public static event EventHandler<DashboardRefreshEventArgs> RefreshRequested;

        public static void NotifyChanged(string moduleKey)
        {
            EventHandler<DashboardRefreshEventArgs> handler = RefreshRequested;
            if (handler == null)
                return;

            handler(null, new DashboardRefreshEventArgs(moduleKey));
        }
    }
}

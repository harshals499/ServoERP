namespace HVAC_Pro_Desktop.Services
{
    /// <summary>Stores one-time workflow launch context between related pages.</summary>
    public static class WorkflowLaunchContext
    {
        private static readonly object Sync = new object();
        private static JobDraftLaunchContext _jobDraft;

        public static void SetJobDraft(int clientId, int siteId)
        {
            lock (Sync)
            {
                _jobDraft = clientId > 0
                    ? new JobDraftLaunchContext { ClientId = clientId, SiteId = siteId > 0 ? siteId : 0 }
                    : null;
            }
        }

        public static JobDraftLaunchContext TakeJobDraft()
        {
            lock (Sync)
            {
                JobDraftLaunchContext context = _jobDraft;
                _jobDraft = null;
                return context;
            }
        }
    }

    public sealed class JobDraftLaunchContext
    {
        public int ClientId { get; set; }
        public int SiteId { get; set; }
    }
}

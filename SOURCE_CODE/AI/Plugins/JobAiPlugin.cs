using System;
using System.Linq;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.AI.Plugins
{
    /// <summary>
    /// Read-only job and technician helper.
    /// </summary>
    public class JobAiPlugin
    {
        public AiPluginResult Build(string prompt)
        {
            var jobs = new JobService().GetAll().Where(j => j.IsOverdue || (j.ScheduledDate.Date < DateTime.Today && !string.Equals(j.Status, "Completed", StringComparison.OrdinalIgnoreCase))).OrderBy(j => j.ScheduledDate).Take(10).ToList();
            var result = new AiPluginResult { Intent = "Job/Technician Assistant" };
            result.Context = jobs.Count == 0
                ? "No delayed jobs found in the limited job summary."
                : "Delayed jobs: " + string.Join("; ", jobs.Select(j => (j.JobNumber ?? ("Job #" + j.JobID)) + " | " + (j.ClientName ?? "Client unknown") + " | " + (j.SiteName ?? "Site unknown") + " | " + j.Status + " | Scheduled " + IndiaFormatHelper.FormatDate(j.ScheduledDate)));
            result.SuggestedActions.Add(new AiSuggestedAction
            {
                Title = "Suggest delay recovery plan",
                Description = "Lists likely causes and next follow-up steps. No job status is changed.",
                TargetModule = "Jobs",
                IsWriteAction = false
            });
            return result;
        }
    }
}

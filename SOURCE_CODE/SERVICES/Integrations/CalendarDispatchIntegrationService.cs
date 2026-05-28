using System;
using System.Globalization;
using System.IO;
using System.Text;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services.Integrations
{
    public sealed class CalendarDispatchIntegrationService
    {
        private const string Provider = "Calendar Dispatch";

        public string ExportFolder => IntegrationConfig.Get(
            "CalendarDispatch",
            "ExportFolder",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ServoERP", "Calendar"));

        public IntegrationOperationResult ExportJobIcs(Job job)
        {
            if (job == null)
                return IntegrationOperationResult.Fail(Provider, "ExportJobIcs", "Job is required.");

            var calendarEvent = new CalendarDispatchEvent
            {
                Subject = FirstNonEmpty(job.JobTitle, job.Title, job.JobNumber, "ServoERP Job"),
                Description = BuildJobDescription(job),
                Location = FirstNonEmpty(job.SiteName, "Customer site"),
                StartsAt = job.ScheduledDate == default(DateTime) ? DateTime.Now.AddHours(1) : job.ScheduledDate,
                EndsAt = (job.ScheduledDate == default(DateTime) ? DateTime.Now.AddHours(2) : job.ScheduledDate.AddHours(1)),
                ExternalId = FirstNonEmpty(job.JobNumber, "JOB-" + job.JobID.ToString(CultureInfo.InvariantCulture))
            };

            return ExportIcs(calendarEvent);
        }

        public IntegrationOperationResult ExportIcs(CalendarDispatchEvent calendarEvent)
        {
            if (calendarEvent == null)
                return IntegrationOperationResult.Fail(Provider, "ExportIcs", "Calendar event is required.");

            try
            {
                Directory.CreateDirectory(ExportFolder);
                string fileName = SafeFileName(FirstNonEmpty(calendarEvent.ExternalId, calendarEvent.Subject, "ServoERP-event")) + ".ics";
                string path = Path.Combine(ExportFolder, fileName);
                File.WriteAllText(path, BuildIcs(calendarEvent), Encoding.UTF8);

                var result = IntegrationOperationResult.Ok(Provider, "ExportIcs", "Calendar event file exported.");
                result.LocalPath = path;
                result.ReferenceId = calendarEvent.ExternalId;
                return result;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("CalendarDispatchIntegrationService.ExportIcs", ex);
                return IntegrationOperationResult.Fail(Provider, "ExportIcs", ex.Message);
            }
        }

        private static string BuildIcs(CalendarDispatchEvent calendarEvent)
        {
            DateTime startUtc = calendarEvent.StartsAt.ToUniversalTime();
            DateTime endUtc = calendarEvent.EndsAt <= calendarEvent.StartsAt
                ? calendarEvent.StartsAt.AddHours(1).ToUniversalTime()
                : calendarEvent.EndsAt.ToUniversalTime();

            var sb = new StringBuilder();
            sb.AppendLine("BEGIN:VCALENDAR");
            sb.AppendLine("VERSION:2.0");
            sb.AppendLine("PRODID:-//ServoERP//Dispatch Calendar//EN");
            sb.AppendLine("CALSCALE:GREGORIAN");
            sb.AppendLine("METHOD:PUBLISH");
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine("UID:" + EscapeIcs(FirstNonEmpty(calendarEvent.ExternalId, Guid.NewGuid().ToString("N"))) + "@servoerp");
            sb.AppendLine("DTSTAMP:" + FormatUtc(DateTime.UtcNow));
            sb.AppendLine("DTSTART:" + FormatUtc(startUtc));
            sb.AppendLine("DTEND:" + FormatUtc(endUtc));
            sb.AppendLine("SUMMARY:" + EscapeIcs(calendarEvent.Subject));
            sb.AppendLine("DESCRIPTION:" + EscapeIcs(calendarEvent.Description));
            sb.AppendLine("LOCATION:" + EscapeIcs(calendarEvent.Location));
            if (!string.IsNullOrWhiteSpace(calendarEvent.OrganizerEmail))
                sb.AppendLine("ORGANIZER:MAILTO:" + EscapeIcs(calendarEvent.OrganizerEmail));
            if (!string.IsNullOrWhiteSpace(calendarEvent.AttendeeEmail))
                sb.AppendLine("ATTENDEE;ROLE=REQ-PARTICIPANT;PARTSTAT=NEEDS-ACTION:MAILTO:" + EscapeIcs(calendarEvent.AttendeeEmail));
            sb.AppendLine("END:VEVENT");
            sb.AppendLine("END:VCALENDAR");
            return sb.ToString();
        }

        private static string BuildJobDescription(Job job)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Job: " + FirstNonEmpty(job.JobNumber, job.JobID.ToString(CultureInfo.InvariantCulture)));
            sb.AppendLine("Client: " + FirstNonEmpty(job.ClientName, "N/A"));
            sb.AppendLine("Site: " + FirstNonEmpty(job.SiteName, "N/A"));
            sb.AppendLine("Priority: " + FirstNonEmpty(job.Priority, "N/A"));
            sb.AppendLine("Status: " + FirstNonEmpty(job.Status, job.PipelineStatus, "N/A"));
            if (!string.IsNullOrWhiteSpace(job.Description))
                sb.AppendLine("Description: " + job.Description);
            return sb.ToString();
        }

        private static string FormatUtc(DateTime value)
        {
            return value.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        }

        private static string EscapeIcs(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace(";", "\\;")
                .Replace(",", "\\,")
                .Replace("\r\n", "\\n")
                .Replace("\n", "\\n");
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }
            return string.Empty;
        }

        private static string SafeFileName(string value)
        {
            string text = string.IsNullOrWhiteSpace(value) ? "event" : value.Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
                text = text.Replace(c, '-');
            return text;
        }
    }
}

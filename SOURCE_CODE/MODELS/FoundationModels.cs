using System;

namespace HVAC_Pro_Desktop.Models
{
    public sealed class GlobalSearchResult
    {
        public string Module { get; set; }
        public string Title { get; set; }
        public string Detail { get; set; }
        public int RecordId { get; set; }
        public string PageKey { get; set; }
        public DateTime? RecordDate { get; set; }
    }

    public sealed class FoundationNotification
    {
        public string NotificationKey { get; set; }
        public string Severity { get; set; }
        public string Module { get; set; }
        public string Title { get; set; }
        public string Detail { get; set; }
        public int? RecordId { get; set; }
        public string PageKey { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class SiteTimelineItem
    {
        public DateTime EventDate { get; set; }
        public string EventType { get; set; }
        public string Title { get; set; }
        public string Detail { get; set; }
        public int? RecordId { get; set; }
        public string PageKey { get; set; }
    }

    public sealed class BackupResult
    {
        public bool Success { get; set; }
        public string BackupPath { get; set; }
        public string Message { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

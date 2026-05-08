using System;

namespace HVAC_Pro_Desktop.Models
{
    public class ConnectedMailAccount
    {
        public int AccountId { get; set; }
        public int UserId { get; set; }
        public string Provider { get; set; }
        public string EmailAddress { get; set; }
        public string DisplayName { get; set; }
        public string AccessTokenEncrypted { get; set; }
        public string RefreshTokenEncrypted { get; set; }
        public DateTime? TokenExpiresAtUtc { get; set; }
        public DateTime? LastSyncUtc { get; set; }
        public string LastSyncStatus { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class SyncedServiceDeskEmail
    {
        public int EmailId { get; set; }
        public int AccountId { get; set; }
        public int? IncidentId { get; set; }
        public string ProviderMessageId { get; set; }
        public string ThreadId { get; set; }
        public string FromAddress { get; set; }
        public string FromName { get; set; }
        public string Subject { get; set; }
        public string BodyPreview { get; set; }
        public DateTime? ReceivedAtUtc { get; set; }
        public DateTime SyncedAtUtc { get; set; }
    }

    public class MailSyncResult
    {
        public int Scanned { get; set; }
        public int CreatedIncidents { get; set; }
        public int UpdatedIncidents { get; set; }
        public int Skipped { get; set; }
        public string Message { get; set; }
    }

    public class MailMessageSyncItem
    {
        public string ProviderMessageId { get; set; }
        public string ThreadId { get; set; }
        public string FromAddress { get; set; }
        public string FromName { get; set; }
        public string Subject { get; set; }
        public string BodyPreview { get; set; }
        public string BodyText { get; set; }
        public DateTime? ReceivedAtUtc { get; set; }
    }
}

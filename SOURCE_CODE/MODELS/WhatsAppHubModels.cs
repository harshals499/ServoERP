using System;
using System.Collections.Generic;

namespace HVAC_Pro_Desktop.Models
{
    public sealed class WhatsAppContact
    {
        public string SourceType { get; set; }
        public int SourceId { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Location { get; set; }
        public string LastMessage { get; set; }
        public DateTime LastMessageAt { get; set; }
        public int UnreadCount { get; set; }
        public int InvoiceCount { get; set; }
        public int QuoteCount { get; set; }
        public int JobCount { get; set; }
        public int ContractCount { get; set; }
    }

    public sealed class WhatsAppTemplate
    {
        public string TemplateType { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
    }

    public sealed class WhatsAppActionLog
    {
        public string ActionId { get; set; }
        public DateTime ActionDate { get; set; }
        public string User { get; set; }
        public string ContactName { get; set; }
        public string Phone { get; set; }
        public string Module { get; set; }
        public int SourceId { get; set; }
        public string TemplateType { get; set; }
        public string Message { get; set; }
        public string LinkedRecord { get; set; }
        public string LinkedRecordType { get; set; }
        public int LinkedRecordId { get; set; }
        public string Status { get; set; }
    }

    public sealed class WhatsAppQuickActionContext
    {
        public string Module { get; set; }
        public int SourceId { get; set; }
        public string ContactName { get; set; }
        public string Phone { get; set; }
        public string TemplateType { get; set; }
        public string Message { get; set; }
        public string LinkedRecord { get; set; }
        public string LinkedRecordType { get; set; }
        public int LinkedRecordId { get; set; }
    }
}

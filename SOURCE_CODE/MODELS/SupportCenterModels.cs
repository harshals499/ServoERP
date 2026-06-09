using System;
using System.Collections.Generic;

namespace HVAC_Pro_Desktop.Models
{
    public sealed class SupportArticle
    {
        public string Category { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public List<string> Steps { get; set; } = new List<string>();
        public List<string> Tags { get; set; } = new List<string>();
    }

    public sealed class SupportToolResult
    {
        public bool Success { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string Detail { get; set; }
        public string OutputPath { get; set; }
        public DateTime CompletedAt { get; set; } = DateTime.Now;
    }

    public sealed class ServerSetupProfile
    {
        public string MachineName { get; set; }
        public string PrimaryIpAddress { get; set; }
        public string SqlInstance { get; set; }
        public string DatabaseName { get; set; }
        public string ConnectionTarget { get; set; }
        public string FallbackSqlitePath { get; set; }
        public string ClientConfigPath { get; set; }
        public string ClientScriptPath { get; set; }
    }
}

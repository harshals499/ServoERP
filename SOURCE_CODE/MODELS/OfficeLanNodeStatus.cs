using System;
using System.Collections.Generic;

namespace HVAC_Pro_Desktop.Models
{
    public sealed class OfficeLanNodeStatus
    {
        public Guid NodePublicId { get; set; }
        public string NodeName { get; set; }
        public string MachineName { get; set; }
        public string ServerRole { get; set; }
        public string AppVersion { get; set; }
        public string DatabaseServer { get; set; }
        public string DatabaseName { get; set; }
        public string LastHealthStatus { get; set; }
        public string LastHealthDetail { get; set; }
        public DateTime? LastSeenUtc { get; set; }
        public string ConnectionStatus { get; set; }
    }

    public sealed class OfficeLanComputer
    {
        public bool Selected { get; set; }
        public string HostName { get; set; }
        public string IpAddress { get; set; }
        public bool IsReachable { get; set; }
        public bool IsLocalComputer { get; set; }
        public bool SupportsRemoteManagement { get; set; }
        public bool IsEnrolled { get; set; }
        public Guid? NodePublicId { get; set; }
        public string AppVersion { get; set; }
        public string ConnectionStatus { get; set; }
        public string DeploymentStatus { get; set; }
        public string ManagementState { get; set; }
        public string TargetVersion { get; set; }
        public string ReadinessStatus { get; set; }
        public string SqlStatus { get; set; }
        public string OperatingSystem { get; set; }
        public string LastSeenDisplay { get; set; }
        public string CurrentStage { get; set; }
        public int ProgressPercent { get; set; }
        public string LastResult { get; set; }
        public bool IsUpdateAvailable { get; set; }
        public bool IsSavedTerminal { get; set; }
        public IList<OfficeLanReadinessCheck> ReadinessChecks { get; set; } = new List<OfficeLanReadinessCheck>();
    }

    public sealed class OfficeLanReadinessCheck
    {
        public string CheckKey { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public string Detail { get; set; }
        public string Recommendation { get; set; }
        public bool IsBlocking { get; set; }
    }

    public sealed class OfficeLanReadinessResult
    {
        public string HostName { get; set; }
        public string OverallStatus { get; set; }
        public DateTime CheckedUtc { get; set; }
        public IList<OfficeLanReadinessCheck> Checks { get; set; } = new List<OfficeLanReadinessCheck>();
    }

    public sealed class OfficeLanDeploymentProgress
    {
        public Guid JobPublicId { get; set; }
        public string Computer { get; set; }
        public string Stage { get; set; }
        public int ProgressPercent { get; set; }
        public string Status { get; set; }
        public string Detail { get; set; }
        public DateTime TimestampUtc { get; set; }
    }

    public sealed class OfficeLanDeploymentPackage
    {
        public string FolderPath { get; set; }
        public string ScriptPath { get; set; }
        public string BootstrapScriptPath { get; set; }
        public int TargetCount { get; set; }
        public Guid JobPublicId { get; set; }
        public string ProgressPath { get; set; }
    }
}

using System;
using System.Collections.Generic;

namespace HVAC_Pro_Desktop.Models
{
    public sealed class DevTeamAgentStatus
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Role { get; set; }
        public string Status { get; set; }
        public int? CurrentTaskId { get; set; }
        public string LastAction { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class DevTeamTask
    {
        public int Id { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public bool RequiresHumanReview { get; set; }
        public string BuildStatus { get; set; }
        public string TestStatus { get; set; }
        public bool StopRequested { get; set; }
        public string ReportPath { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class DevTeamTaskStep
    {
        public int Id { get; set; }
        public int TaskId { get; set; }
        public string AgentName { get; set; }
        public int StepOrder { get; set; }
        public string Action { get; set; }
        public string Status { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
        public string Summary { get; set; }
    }

    public sealed class DevTeamLogEntry
    {
        public int Id { get; set; }
        public int? TaskId { get; set; }
        public string AgentName { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class DevTeamFileChange
    {
        public int Id { get; set; }
        public int TaskId { get; set; }
        public string FilePath { get; set; }
        public string ChangeType { get; set; }
        public string StagedPath { get; set; }
        public bool Applied { get; set; }
    }

    public sealed class DevTeamTestResult
    {
        public int Id { get; set; }
        public int TaskId { get; set; }
        public string Kind { get; set; }
        public bool Passed { get; set; }
        public string OutputSummary { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class DevTeamBrainSnapshot
    {
        public List<DevTeamAgentStatus> Agents { get; set; } = new List<DevTeamAgentStatus>();
        public List<DevTeamTask> Tasks { get; set; } = new List<DevTeamTask>();
    }

    public sealed class DevTeamTaskDetail
    {
        public DevTeamTask Task { get; set; }
        public List<DevTeamTaskStep> Steps { get; set; } = new List<DevTeamTaskStep>();
        public List<DevTeamLogEntry> Logs { get; set; } = new List<DevTeamLogEntry>();
        public List<DevTeamFileChange> FileChanges { get; set; } = new List<DevTeamFileChange>();
        public List<DevTeamTestResult> TestResults { get; set; } = new List<DevTeamTestResult>();
    }

    public sealed class DevTeamBrainProgressEventArgs : EventArgs
    {
        public DevTeamBrainSnapshot Snapshot { get; private set; }

        public DevTeamBrainProgressEventArgs(DevTeamBrainSnapshot snapshot)
        {
            Snapshot = snapshot;
        }
    }
}

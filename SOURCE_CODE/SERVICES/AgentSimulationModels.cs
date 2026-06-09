using System;
using System.Collections.Generic;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class AgentRecordRef
    {
        public string Module { get; set; }
        public string TableName { get; set; }
        public int Id { get; set; }
        public string Number { get; set; }
        public string Label { get; set; }
        public string PdfPath { get; set; }
        public DateTime CreatedOn { get; set; }
    }

    public sealed class AgentSimulationNote
    {
        public DateTime LoggedAt { get; set; }
        public int SimulatedDay { get; set; }
        public string Category { get; set; }
        public string Message { get; set; }
    }

    public sealed class AgentSimulationIssue
    {
        public DateTime LoggedAt { get; set; }
        public int SimulatedDay { get; set; }
        public string Category { get; set; }
        public string Severity { get; set; }
        public string Message { get; set; }
        public string ResolutionHint { get; set; }
    }

    public sealed class AgentSimulationState
    {
        public string RunId { get; set; }
        public bool IsRunning { get; set; }
        public bool IsPaused { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime BaseDate { get; set; }
        public DateTime SimulatedDate { get; set; }
        public int SimulatedDay { get; set; }
        public int MaxDays { get; set; }
        public int Score { get; set; }
        public int LastScore { get; set; }
        public int StalledDays { get; set; }
        public string Approach { get; set; }
        public string ReportPath { get; set; }
        public List<AgentRecordRef> Records { get; set; }
        public List<AgentSimulationNote> Notes { get; set; }
        public List<AgentSimulationIssue> Issues { get; set; }
        public Dictionary<string, bool> Checklist { get; set; }

        public AgentSimulationState()
        {
            MaxDays = 60;
            Records = new List<AgentRecordRef>();
            Notes = new List<AgentSimulationNote>();
            Issues = new List<AgentSimulationIssue>();
            Checklist = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public sealed class AgentSimulationProgressEventArgs : EventArgs
    {
        public AgentSimulationState State { get; private set; }

        public AgentSimulationProgressEventArgs(AgentSimulationState state)
        {
            State = state;
        }
    }

    public sealed class AgentSimulationCompletedEventArgs : EventArgs
    {
        public AgentSimulationState State { get; private set; }
        public string ReportPath { get; private set; }

        public AgentSimulationCompletedEventArgs(AgentSimulationState state, string reportPath)
        {
            State = state;
            ReportPath = reportPath;
        }
    }
}

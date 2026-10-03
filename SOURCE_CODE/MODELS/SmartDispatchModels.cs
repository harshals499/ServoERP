using System;
using System.Collections.Generic;

namespace HVAC_Pro_Desktop.Models
{
    public sealed class SmartDispatchPlan
    {
        public Job Job { get; set; }
        public ClientSite Site { get; set; }
        public string Urgency { get; set; }
        public string ReadinessSummary { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public List<SmartDispatchRecommendation> Recommendations { get; set; } = new List<SmartDispatchRecommendation>();
    }

    public sealed class SmartDispatchRecommendation
    {
        public int Rank { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string Role { get; set; }
        public int Score { get; set; }
        public string Fit { get; set; }
        public int OpenJobCount { get; set; }
        public int SameDayJobCount { get; set; }
        public bool CheckedInToday { get; set; }
        public bool OnLeaveToday { get; set; }
        public double? DistanceKm { get; set; }
        public string Availability { get; set; }
        public string Workload { get; set; }
        public string Distance { get; set; }
        public string ReasonSummary { get; set; }
        public List<string> Reasons { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public bool CanAssign => !OnLeaveToday;
    }

    public sealed class SmartDispatchCandidateContext
    {
        public Employee Employee { get; set; }
        public EmployeeSummaryDto Attendance { get; set; }
        public List<EmployeeSkillDto> Skills { get; set; } = new List<EmployeeSkillDto>();
        public EmployeeAttendanceDayDto LatestAttendance { get; set; }
        public List<JobSummaryDto> Jobs { get; set; } = new List<JobSummaryDto>();
    }
}

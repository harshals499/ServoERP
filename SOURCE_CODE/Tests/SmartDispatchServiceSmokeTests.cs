using System;
using System.Collections.Generic;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.Tests
{
    public static class SmartDispatchServiceSmokeTests
    {
        public static IEnumerable<string> RunAll()
        {
            Job job = new Job { JobID = 7, JobType = "AC Repair", JobTitle = "Split AC cooling issue", ScheduledDate = DateTime.Today };
            ClientSite site = new ClientSite { SiteID = 4, SiteName = "Pune Plant", City = "Pune", GeoLatitude = 18.5204, GeoLongitude = 73.8567, AssignedTechnicianID = 10 };
            Employee technician = new Employee { EmployeeID = 10, Name = "Asha Technician", Status = "Active", Designation = "AC Technician", NatureOfWork = "HVAC Service", ClientSite = "Pune" };

            SmartDispatchRecommendation strong = SmartDispatchService.ScoreCandidate(job, site, new SmartDispatchCandidateContext
            {
                Employee = technician,
                Attendance = new EmployeeSummaryDto { EmployeeID = 10, CheckedInToday = true },
                Skills = new List<EmployeeSkillDto> { new EmployeeSkillDto { SkillName = "AC Repair", IsExpired = false } },
                LatestAttendance = new EmployeeAttendanceDayDto { AttendanceDate = DateTime.Today, CheckInLatitude = 18.5205m, CheckInLongitude = 73.8568m },
                Jobs = new List<JobSummaryDto>()
            });
            Assert(strong.Score >= 75 && strong.CanAssign, "strong candidate should be recommended");
            yield return "Smart Dispatch ranks a skilled, available, nearby technician strongly.";

            var busyJobs = new List<JobSummaryDto>();
            for (int i = 0; i < 4; i++)
                busyJobs.Add(new JobSummaryDto { JobId = 100 + i, JobType = "AC Repair", PipelineStatus = "Assigned", ScheduledDate = DateTime.Today });
            SmartDispatchRecommendation overloaded = SmartDispatchService.ScoreCandidate(job, site, new SmartDispatchCandidateContext
            {
                Employee = technician,
                Attendance = new EmployeeSummaryDto { EmployeeID = 10, CheckedInToday = true },
                Skills = new List<EmployeeSkillDto> { new EmployeeSkillDto { SkillName = "AC Repair", IsExpired = false } },
                Jobs = busyJobs
            });
            Assert(overloaded.Score < strong.Score, "workload should reduce recommendation score");
            yield return "Smart Dispatch penalizes overloaded same-day schedules.";

            SmartDispatchRecommendation onLeave = SmartDispatchService.ScoreCandidate(job, site, new SmartDispatchCandidateContext
            {
                Employee = technician,
                Attendance = new EmployeeSummaryDto { EmployeeID = 10, OnLeaveToday = true },
                Skills = new List<EmployeeSkillDto> { new EmployeeSkillDto { SkillName = "AC Repair", IsExpired = false } }
            });
            Assert(!onLeave.CanAssign && onLeave.Score <= 35 && onLeave.Warnings.Count > 0, "leave must block assignment");
            yield return "Smart Dispatch blocks technicians marked on leave.";
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Smart Dispatch smoke test failed: " + message);
        }
    }
}

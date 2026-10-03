using System;
using System.Collections.Generic;
using System.Linq;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    /// <summary>
    /// Builds deterministic, explainable technician recommendations. It never assigns a technician
    /// until a user confirms the recommendation in the Smart Dispatch workspace.
    /// </summary>
    public sealed class SmartDispatchService
    {
        private readonly JobService _jobService = new JobService();
        private readonly EmployeeService _employeeService = new EmployeeService();
        private readonly SiteService _siteService = new SiteService();

        public SmartDispatchPlan BuildPlan(int jobId)
        {
            JobDetailDto detail = _jobService.GetJobDetail(jobId);
            if (detail == null || detail.Job == null)
                throw new InvalidOperationException("The selected job could not be found.");

            List<Employee> technicians = _employeeService.GetActiveTechnicians() ?? new List<Employee>();
            List<EmployeeSummaryDto> attendance = SafeLoad(_employeeService.GetEmployeeSummaries, "SmartDispatch.Attendance");
            List<JobSummaryDto> jobs = _jobService.GetAllJobsWithSummary() ?? new List<JobSummaryDto>();
            var plan = new SmartDispatchPlan
            {
                Job = detail.Job,
                Site = detail.Site ?? _siteService.GetById(detail.Job.SiteID),
                Urgency = BuildUrgency(detail.Job),
                ReadinessSummary = BuildReadiness(detail)
            };

            if (detail.PartsUsed.Any(p => !string.Equals(p.StockStatus, "InStock", StringComparison.OrdinalIgnoreCase)))
                plan.Warnings.Add("One or more required materials are not fully available. Confirm the parts plan before dispatch.");
            if (plan.Site == null)
                plan.Warnings.Add("No service site is selected, so site continuity and distance cannot be scored.");
            else if (!plan.Site.GeoLatitude.HasValue || !plan.Site.GeoLongitude.HasValue)
                plan.Warnings.Add("The service site has no coordinates. Distance is not included in the ranking.");

            foreach (Employee technician in technicians)
            {
                EmployeeSummaryDto day = attendance.FirstOrDefault(a => a.EmployeeID == technician.EmployeeID);
                var context = new SmartDispatchCandidateContext
                {
                    Employee = technician,
                    Attendance = day,
                    Skills = SafeLoad(() => _employeeService.GetEmployeeSkills(technician.EmployeeID), "SmartDispatch.Skills"),
                    LatestAttendance = LoadLatestAttendance(technician.EmployeeID, detail.Job.ScheduledDate),
                    Jobs = jobs.Where(j => j.TechnicianId == technician.EmployeeID).ToList()
                };
                plan.Recommendations.Add(ScoreCandidate(detail.Job, plan.Site, context));
            }

            plan.Recommendations = plan.Recommendations
                .OrderByDescending(r => r.CanAssign)
                .ThenByDescending(r => r.Score)
                .ThenBy(r => r.OpenJobCount)
                .ThenBy(r => r.EmployeeName)
                .Take(12)
                .ToList();
            for (int i = 0; i < plan.Recommendations.Count; i++)
                plan.Recommendations[i].Rank = i + 1;

            if (plan.Recommendations.Count == 0)
                plan.Warnings.Add("No active field technicians were found. Review employee roles and active status.");

            return plan;
        }

        public void Assign(int jobId, int employeeId, int recommendationScore)
        {
            Job job = _jobService.GetById(jobId);
            Employee employee = _employeeService.GetById(employeeId);
            if (job == null)
                throw new InvalidOperationException("The selected job could not be found.");
            if (employee == null || !EmployeeService.IsDispatchTechnicianRole(employee) || !string.Equals(employee.Status, "Active", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The selected employee is not an active field technician.");
            if (IsTerminal(job.PipelineStatus) || IsTerminal(job.Status))
                throw new InvalidOperationException("Closed, invoiced, or cancelled jobs cannot be dispatched.");

            job.AssignedEmployeeID = employeeId;
            job.AssignedEmployeeName = employee.Name;
            if (string.Equals(NormalizeStage(job.PipelineStatus), "Created", StringComparison.OrdinalIgnoreCase))
                job.PipelineStatus = "Assigned";
            _jobService.Update(job);
            _jobService.LogActivity(jobId,
                "Smart Dispatch assigned " + employee.Name + " after dispatcher confirmation (fit score " + recommendationScore + "/100).",
                "Success");
        }

        public static SmartDispatchRecommendation ScoreCandidate(Job job, ClientSite site, SmartDispatchCandidateContext context)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));
            if (context == null || context.Employee == null) throw new ArgumentNullException(nameof(context));

            Employee employee = context.Employee;
            List<JobSummaryDto> employeeJobs = context.Jobs ?? new List<JobSummaryDto>();
            DateTime targetDate = job.ScheduledDate.Date;
            int openJobs = employeeJobs.Count(j => !IsTerminal(j.PipelineStatus));
            int sameDayJobs = employeeJobs.Count(j => !IsTerminal(j.PipelineStatus) && j.ScheduledDate.Date == targetDate && j.JobId != job.JobID);
            bool onLeave = context.Attendance != null && context.Attendance.OnLeaveToday && targetDate == DateTime.Today;
            bool checkedIn = context.Attendance != null && context.Attendance.CheckedInToday && targetDate == DateTime.Today;
            var result = new SmartDispatchRecommendation
            {
                EmployeeId = employee.EmployeeID,
                EmployeeName = First(employee.Name, "Technician #" + employee.EmployeeID),
                Role = EmployeeService.GetDispatchTechnicianRole(employee),
                OpenJobCount = openJobs,
                SameDayJobCount = sameDayJobs,
                CheckedInToday = checkedIn,
                OnLeaveToday = onLeave
            };

            int score = 0;

            if (onLeave)
            {
                result.Availability = "On leave";
                result.Warnings.Add("Attendance marks this technician on leave today.");
            }
            else if (checkedIn)
            {
                score += 15;
                result.Availability = "Checked in";
                result.Reasons.Add("Checked in and available today (+15).");
            }
            else
            {
                score += 8;
                result.Availability = targetDate == DateTime.Today ? "Not checked in" : "No leave recorded";
                result.Reasons.Add("No leave conflict is recorded (+8).");
            }

            int workloadScore = Math.Max(0, 25 - (openJobs * 3) - (sameDayJobs * 7));
            score += workloadScore;
            result.Workload = openJobs + " open / " + sameDayJobs + " same day";
            result.Reasons.Add("Current workload contributes " + workloadScore + "/25.");
            if (sameDayJobs >= 2)
                result.Warnings.Add("Already has " + sameDayJobs + " open jobs on the scheduled date.");

            string jobText = Normalize(job.JobType + " " + job.JobTitle + " " + job.Title + " " + job.Description);
            List<string> matchingSkills = (context.Skills ?? new List<EmployeeSkillDto>())
                .Where(s => !s.IsExpired && TextMatches(jobText, s.SkillName))
                .Select(s => s.SkillName)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            bool roleMatch = TextMatches(jobText, employee.Designation) || TextMatches(jobText, employee.NatureOfWork) || TextMatches(jobText, employee.Department);
            int similarJobs = employeeJobs.Count(j => SameWorkType(job, j));
            int skillScore = matchingSkills.Count > 0 ? 22 : (roleMatch ? 14 : 7);
            int experienceScore = Math.Min(8, similarJobs * 2);
            score += skillScore + experienceScore;
            if (matchingSkills.Count > 0)
                result.Reasons.Add("Recorded skill match: " + string.Join(", ", matchingSkills.Take(3)) + " (+" + skillScore + ").");
            else if (roleMatch)
                result.Reasons.Add("Role profile matches this work type (+" + skillScore + ").");
            else
                result.Reasons.Add("Eligible field technician; no exact skill is recorded (+7).");
            if (similarJobs > 0)
                result.Reasons.Add(similarJobs + " similar assigned job(s) contribute +" + experienceScore + ".");

            int locationScore = 0;
            if (site != null && site.AssignedTechnicianID == employee.EmployeeID)
            {
                locationScore += 10;
                result.Reasons.Add("Preferred technician already linked to this site (+10).");
            }

            double? distance = CalculateDistance(site, context.LatestAttendance);
            result.DistanceKm = distance;
            if (distance.HasValue)
            {
                int distanceScore = distance.Value <= 5d ? 10 : distance.Value <= 15d ? 8 : distance.Value <= 35d ? 5 : 1;
                locationScore += distanceScore;
                result.Distance = distance.Value.ToString("0.0") + " km";
                result.Reasons.Add("Last recorded check-in is " + result.Distance + " from the site (+" + distanceScore + ").");
            }
            else if (site != null && TextLocationMatches(employee.ClientSite, site))
            {
                locationScore += 8;
                result.Distance = "Area match";
                result.Reasons.Add("Employee work location matches the service area (+8).");
            }
            else
            {
                locationScore += 3;
                result.Distance = "Not available";
                result.Reasons.Add("Location data is incomplete; neutral proximity score (+3).");
            }
            score += Math.Min(20, locationScore);

            score += 10;
            result.Reasons.Add("Active field-service role (+10).");
            if (onLeave)
                score = Math.Min(score, 35);

            result.Score = Math.Max(0, Math.Min(100, score));
            result.Fit = result.Score >= 75 ? "Strong" : result.Score >= 60 ? "Good" : result.Score >= 45 ? "Consider" : "Low";
            result.ReasonSummary = string.Join(" ", result.Reasons.Take(3));
            return result;
        }

        private EmployeeAttendanceDayDto LoadLatestAttendance(int employeeId, DateTime scheduledDate)
        {
            try
            {
                return (_employeeService.GetEmployeeAttendance(employeeId, scheduledDate.Year, scheduledDate.Month) ?? new List<EmployeeAttendanceDayDto>())
                    .Where(a => a.AttendanceDate.Date <= scheduledDate.Date && a.CheckInLatitude.HasValue && a.CheckInLongitude.HasValue)
                    .OrderByDescending(a => a.AttendanceDate)
                    .FirstOrDefault();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SmartDispatch.LatestAttendance", ex);
                return null;
            }
        }

        private static List<T> SafeLoad<T>(Func<List<T>> loader, string context)
        {
            try { return loader() ?? new List<T>(); }
            catch (Exception ex)
            {
                AppLogger.LogError(context, ex);
                return new List<T>();
            }
        }

        private static string BuildUrgency(Job job)
        {
            int days = (DateTime.Today - job.ScheduledDate.Date).Days;
            if (days > 0) return "Overdue by " + days + " day(s)";
            if (days == 0) return "Due today";
            return "Scheduled in " + Math.Abs(days) + " day(s)";
        }

        private static string BuildReadiness(JobDetailDto detail)
        {
            int shortages = detail.PartsUsed.Count(p => !string.Equals(p.StockStatus, "InStock", StringComparison.OrdinalIgnoreCase));
            string checklist = detail.ChecklistTotalCount == 0 ? "No checklist" : detail.ChecklistTotalCount + " checklist item(s)";
            return checklist + " | " + detail.PartsUsed.Count + " material item(s) | " + shortages + " shortage(s)";
        }

        private static double? CalculateDistance(ClientSite site, EmployeeAttendanceDayDto attendance)
        {
            if (site == null || attendance == null || !site.GeoLatitude.HasValue || !site.GeoLongitude.HasValue ||
                !attendance.CheckInLatitude.HasValue || !attendance.CheckInLongitude.HasValue)
                return null;

            double lat1 = site.GeoLatitude.Value * Math.PI / 180d;
            double lat2 = Convert.ToDouble(attendance.CheckInLatitude.Value) * Math.PI / 180d;
            double dLat = lat2 - lat1;
            double dLon = (Convert.ToDouble(attendance.CheckInLongitude.Value) - site.GeoLongitude.Value) * Math.PI / 180d;
            double a = Math.Sin(dLat / 2d) * Math.Sin(dLat / 2d) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2d) * Math.Sin(dLon / 2d);
            return 6371d * 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
        }

        private static bool TextLocationMatches(string employeeLocation, ClientSite site)
        {
            string employee = Normalize(employeeLocation);
            if (string.IsNullOrWhiteSpace(employee) || site == null) return false;
            return ContainsMeaningful(employee, Normalize(site.SiteName)) || ContainsMeaningful(employee, Normalize(site.City)) || ContainsMeaningful(employee, Normalize(site.Address));
        }

        private static bool SameWorkType(Job job, JobSummaryDto previous)
        {
            string current = Normalize(job.JobType);
            string other = Normalize(previous.JobType);
            return !string.IsNullOrWhiteSpace(current) && !string.IsNullOrWhiteSpace(other) && (current.Contains(other) || other.Contains(current));
        }

        private static bool TextMatches(string jobText, string candidate)
        {
            string value = Normalize(candidate);
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (ContainsMeaningful(jobText, value)) return true;
            return value.Split(' ').Any(token => token.Length >= 4 && jobText.Contains(token));
        }

        private static bool ContainsMeaningful(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) && right.Length >= 3 && (left.Contains(right) || right.Contains(left));
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant().Replace("-", " ").Replace("/", " ");
        }

        private static string NormalizeStage(string value)
        {
            string stage = (value ?? string.Empty).Replace(" ", string.Empty).Trim();
            return string.IsNullOrWhiteSpace(stage) ? "Created" : stage;
        }

        private static bool IsTerminal(string value)
        {
            string stage = NormalizeStage(value);
            return stage.Equals("Closed", StringComparison.OrdinalIgnoreCase) || stage.Equals("Invoiced", StringComparison.OrdinalIgnoreCase) ||
                   stage.Equals("Completed", StringComparison.OrdinalIgnoreCase) || stage.Equals("Cancelled", StringComparison.OrdinalIgnoreCase);
        }

        private static string First(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}

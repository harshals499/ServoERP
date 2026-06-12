using System;
using System.Collections.Generic;
using System.Linq;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Models.Validation;
using HVAC_Pro_Desktop.Services.Audit;
using HVAC_Pro_Desktop.Services.Validation;

namespace HVAC_Pro_Desktop.Services
{
    public class JobService
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(20);
        private readonly JobRepository _repo = new JobRepository();
        private readonly ClientService _clientService = new ClientService();
        private readonly SiteService _siteService = new SiteService();
        private readonly EmployeeService _employeeService = new EmployeeService();
        private readonly ContractService _contractService = new ContractService();
        private readonly InventoryService _inventoryService = new InventoryService();
        private readonly InvoiceService _invoiceService = new InvoiceService();
        private readonly BusinessRuleEngine _businessRules = new BusinessRuleEngine();
        private readonly GlobalValidationEngine _validation = new GlobalValidationEngine();
        private readonly AuditTrailService _audit = new AuditTrailService();

        public List<Job> GetAll() => AppDataCache.GetOrCreate("jobs:all", CacheTtl, _repo.GetAll);

        public Job GetById(int id)
        {
            return _repo.GetById(id);
        }

        public List<Job> GetByStatus(string status) => _repo.GetByStatus(status);

        public List<JobSummaryDto> GetAllJobsWithSummary()
        {
            return AppDataCache.GetOrCreate("jobs:summaries", CacheTtl, _repo.GetAllWithSummary);
        }

        public JobDetailDto GetJobDetail(int jobId)
        {
            Job job = _repo.GetById(jobId);
            if (job == null)
                return null;

            List<JobChecklistItem> checklist = _repo.GetChecklistItems(jobId);
            List<JobPartUsed> parts = _repo.GetPartsUsed(jobId);
            ClientSite site = _siteService.GetById(job.SiteID);
            decimal partsCost = parts.Sum(p => p.TotalCost);
            decimal labourCost = job.EstimatedCost;
            decimal travelCost = site?.TravelRateINR ?? 0m;
            decimal revenue = job.ActualRevenue > 0 ? job.ActualRevenue : (job.QuotedRevenue > 0 ? job.QuotedRevenue : job.Revenue);
            decimal profit = revenue - labourCost - travelCost - partsCost;
            decimal margin = revenue <= 0 ? 0m : Math.Round((profit / revenue) * 100m, 2);

            return new JobDetailDto
            {
                Job = job,
                Client = _clientService.GetClientById(job.ClientID),
                Site = site,
                Technician = job.AssignedEmployeeID.HasValue ? _employeeService.GetById(job.AssignedEmployeeID.Value) : null,
                Contract = job.LinkedContractId.HasValue ? _contractService.GetContractDetails(job.LinkedContractId.Value) : null,
                ChecklistItems = checklist,
                PartsUsed = parts,
                ActivityLog = _repo.GetActivityLog(jobId, 10),
                PartsCost = partsCost,
                LabourCost = labourCost,
                TravelCost = travelCost,
                EstimatedProfit = profit,
                EstimatedMarginPct = margin,
                ChecklistCompletedCount = checklist.Count(i => i.IsCompleted),
                ChecklistTotalCount = checklist.Count
            };
        }

        public int Create(Job job)
        {
            SessionManager.DemandPermission("WorkOrders", "Create");
            if (job == null)
                throw new Exception("Job payload is missing.");

            NormalizeJob(job, isNewJob: true);
            if (SessionManager.IsLoggedIn)
            {
                job.CreatedByUserId = SessionManager.CurrentUser.UserId;
                job.CreatedByName = SessionManager.CurrentUser.DisplayName;
            }

            int id = _repo.Create(job);
            if (!string.IsNullOrWhiteSpace(job.JobType))
                _repo.ReplaceChecklistFromTemplate(id, job.JobType);

            LogActivity(id, "Job created by " + GetCurrentUserLabel(), "Success");
            AppDataCache.RemovePrefix("jobs:");
            SessionManager.LogAction("CREATE", "WorkOrders", id, "Work order saved");
            _audit.Record("CREATE", "WorkOrders", id, "Work order saved with data-quality validation");
            return id;
        }

        public void Update(Job job)
        {
            SessionManager.DemandPermission("WorkOrders", "Edit");
            if (job == null || job.JobID <= 0)
                throw new Exception("Job not found.");

            NormalizeJob(job, isNewJob: false);
            if (SessionManager.IsLoggedIn)
            {
                job.ModifiedByUserId = SessionManager.CurrentUser.UserId;
                job.ModifiedByName = SessionManager.CurrentUser.DisplayName;
                job.ModifiedDate = DateTime.Now;
            }

            _repo.Update(job);
            AppDataCache.RemovePrefix("jobs:");
            SessionManager.LogAction("EDIT", "WorkOrders", job.JobID, "Work order saved");
            _audit.Record("EDIT", "WorkOrders", job.JobID, "Work order saved with data-quality validation");
            LogActivity(job.JobID, "Job details updated by " + GetCurrentUserLabel(), "Info");
        }

        public void Delete(int jobId)
        {
            SessionManager.DemandPermission("WorkOrders", "Delete");
            Job existing = _repo.GetById(jobId);
            if (existing == null)
                throw new Exception("Job not found.");

            _repo.Delete(jobId);
            AppDataCache.RemovePrefix("jobs:");
            AppDataCache.RemovePrefix("purchases:");
            AppDataCache.RemovePrefix("invoices:");
            SessionManager.LogAction("DELETE", "WorkOrders", jobId, "Work order deleted");
            _audit.Record("DELETE", "WorkOrders", jobId, "Work order and child records deleted");
        }

        public int CreateJobFromContract(int contractId, string visitType)
        {
            SessionManager.DemandPermission("WorkOrders", "Create");
            AMCContract contract = _contractService.GetContractDetails(contractId);
            if (contract == null)
                throw new Exception("Contract not found.");

            Job job = new Job
            {
                JobNumber = GenerateJobNumber(),
                ClientID = contract.ClientID,
                SiteID = contract.SiteID,
                Title = (visitType ?? "AMC Visit") + " - " + (contract.ContractType ?? "Contract"),
                JobTitle = (visitType ?? "AMC Visit") + " - " + (contract.ContractType ?? "Contract"),
                JobType = string.IsNullOrWhiteSpace(visitType) ? "AMC Visit" : visitType.Trim(),
                LinkedContractId = contract.ContractID,
                PipelineStatus = "Created",
                Status = "Pending",
                Priority = "Medium",
                ScheduledDate = DateTime.Today,
                QuotedRevenue = contract.MonthlyValue > 0 ? contract.MonthlyValue : contract.AnnualValue,
                Revenue = contract.MonthlyValue > 0 ? contract.MonthlyValue : contract.AnnualValue,
                Notes = "Job created from contract AMC-" + contract.ContractID
            };

            int id = Create(job);
            LogActivity(id, "Job created from contract AMC-" + contract.ContractID, "System");
            return id;
        }

        public List<JobChecklistTemplate> GetChecklistTemplates(string jobType)
        {
            return _repo.GetChecklistTemplates(jobType);
        }

        public void ApplyChecklistTemplate(int jobId, string jobType)
        {
            SessionManager.DemandPermission("WorkOrders", "Edit");
            _repo.ReplaceChecklistFromTemplate(jobId, jobType);
            AppDataCache.RemovePrefix("jobs:");
            LogActivity(jobId, "Checklist loaded for " + (jobType ?? "General"), "System");
        }

        public int AddChecklistItem(int jobId, string itemText)
        {
            SessionManager.DemandPermission("WorkOrders", "Edit");
            if (string.IsNullOrWhiteSpace(itemText))
                throw new Exception("Checklist item text is required.");

            int itemId = _repo.AddChecklistItem(jobId, itemText.Trim());
            LogActivity(jobId, "Checklist item added: " + itemText.Trim(), "Info");
            AppDataCache.RemovePrefix("jobs:");
            return itemId;
        }

        public void CompleteChecklistItem(int checklistItemId)
        {
            SessionManager.DemandPermission("WorkOrders", "Edit");
            JobChecklistItem item = _repo.GetChecklistItem(checklistItemId);
            if (item == null)
                throw new Exception("Checklist item not found.");
            if (item.IsCompleted)
                return;

            string user = GetCurrentUserLabel();
            _repo.CompleteChecklistItem(checklistItemId, user);
            LogActivity(item.JobId, item.ItemText + " completed by " + user, "Success");

            if (_repo.GetChecklistCompletedCount(item.JobId) == _repo.GetChecklistTotalCount(item.JobId) && _repo.GetChecklistTotalCount(item.JobId) > 0)
                LogActivity(item.JobId, "All checklist items are complete. Job is ready for checklist completion review.", "System");

            AppDataCache.RemovePrefix("jobs:");
        }

        public JobPartUsed AddPartUsed(int jobId, int? inventoryItemId, decimal qty, string itemDescription = null, decimal? unitCostOverride = null)
        {
            SessionManager.DemandPermission("WorkOrders", "Edit");
            if (qty <= 0)
                throw new Exception("Quantity must be greater than zero.");
            if (unitCostOverride.HasValue && unitCostOverride.Value < 0)
                throw new Exception("Material rate cannot be negative.");

            StockItem item = inventoryItemId.HasValue ? _inventoryService.GetById(inventoryItemId.Value) : _inventoryService.GetByName(itemDescription);
            if (item == null && string.IsNullOrWhiteSpace(itemDescription))
                throw new Exception("Select a valid inventory item.");

            string resolvedDescription = item?.ItemName ?? itemDescription?.Trim();
            decimal unitCost = unitCostOverride.HasValue ? unitCostOverride.Value : (item?.LastPurchaseRate ?? 0m);
            decimal available = item?.AvailableStock ?? 0m;
            string stockStatus = "InStock";
            if (item != null)
            {
                if (available < qty)
                    throw new InvalidOperationException("Cannot add part because stock would go negative for " + item.ItemName + ". Available: " + available.ToString("0.###") + ", requested: " + qty.ToString("0.###") + ".");
                if (available <= 0)
                    stockStatus = "OutOfStock";
                else if (available - qty <= item.ReorderLevel)
                    stockStatus = "LowStock";
            }

            var part = new JobPartUsed
            {
                JobId = jobId,
                InventoryItemId = item?.ItemID,
                ItemDescription = resolvedDescription,
                QuantityUsed = qty,
                Unit = item?.Unit ?? "Nos",
                UnitCost = unitCost,
                TotalCost = Math.Round(qty * unitCost, 2),
                IsFromInventory = item != null,
                StockStatus = stockStatus,
                AvailableStock = available
            };

            _repo.AddPartUsed(part);
            if (item != null)
            {
                if (unitCostOverride.HasValue && Math.Abs(unitCostOverride.Value - item.LastPurchaseRate) >= 0.01m)
                    _inventoryService.UpdateMaterialRate(item.ItemID, unitCostOverride.Value, "job material selection");
                _inventoryService.AddStock(item.ItemID, -qty);
            }

            LogActivity(jobId, "Part added: " + resolvedDescription + " x" + qty.ToString("0.###"), stockStatus == "InStock" ? "Info" : "Warning");
            AppDataCache.RemovePrefix("jobs:");
            return part;
        }

        public string AdvancePipeline(int jobId, string requestedStatus = null)
        {
            SessionManager.DemandPermission("WorkOrders", "Edit");
            Job job = _repo.GetById(jobId);
            if (job == null)
                throw new Exception("Job not found.");

            string current = NormalizePipeline(job.PipelineStatus);
            string target = string.IsNullOrWhiteSpace(requestedStatus) ? GetNextPipeline(current) : NormalizePipeline(requestedStatus);
            if (string.Equals(current, target, StringComparison.OrdinalIgnoreCase))
                return current;

            ValidatePipelineTransition(jobId, job, current, target);
            string legacyStatus = ResolveLegacyStatus(target);
            DateTime? completedDate = string.Equals(target, "Closed", StringComparison.OrdinalIgnoreCase) || string.Equals(target, "Invoiced", StringComparison.OrdinalIgnoreCase)
                ? (job.CompletedDate ?? DateTime.Now)
                : job.CompletedDate;
            DateTime? closedDate = string.Equals(target, "Closed", StringComparison.OrdinalIgnoreCase) || string.Equals(target, "Invoiced", StringComparison.OrdinalIgnoreCase)
                ? (job.ClosedDate ?? DateTime.Now)
                : job.ClosedDate;

            _repo.UpdatePipeline(jobId, target, legacyStatus, completedDate, closedDate, job.InvoiceId);
            LogActivity(jobId, "Status advanced to " + target + " by " + GetCurrentUserLabel(), "Success");
            AppDataCache.RemovePrefix("jobs:");
            return target;
        }

        public List<NudgeDto> GenerateNudges(int jobId)
        {
            JobDetailDto detail = GetJobDetail(jobId);
            if (detail == null)
                return new List<NudgeDto>();

            var nudges = new List<NudgeDto>();
            decimal margin = detail.EstimatedMarginPct;

            if (margin > 30m)
            {
                nudges.Add(new NudgeDto
                {
                    NudgeType = "Success",
                    Title = "Margin is strong at " + margin.ToString("0.0") + "%",
                    Body = "This job is priced well. Consider it as a template for similar jobs."
                });
            }
            if (margin > 0m && margin < 15m && detail.Job.QuotedRevenue > 0)
            {
                nudges.Add(new NudgeDto
                {
                    NudgeType = "Warning",
                    Title = "Low margin - only " + margin.ToString("0.0") + "%",
                    Body = "Review job costs or adjust pricing before closing."
                });
            }

            foreach (JobPartUsed part in detail.PartsUsed.Where(p => string.Equals(p.StockStatus, "LowStock", StringComparison.OrdinalIgnoreCase)))
            {
                nudges.Add(new NudgeDto
                {
                    NudgeType = "Warning",
                    Title = part.ItemDescription + " stock is low",
                    Body = "Only " + Math.Max(part.AvailableStock, 0m).ToString("0.###") + " units remaining. Raise a PO before the next visit."
                });
            }

            if (detail.ChecklistCompletedCount < detail.ChecklistTotalCount)
            {
                nudges.Add(new NudgeDto
                {
                    NudgeType = "Info",
                    Title = (detail.ChecklistTotalCount - detail.ChecklistCompletedCount) + " checklist items remaining",
                    Body = "Complete all items before closing this job."
                });
            }

            if (detail.Job.IsOverdue)
            {
                int overdueDays = Math.Max((DateTime.Today - detail.Job.ScheduledDate.Date).Days, 1);
                nudges.Add(new NudgeDto
                {
                    NudgeType = "Danger",
                    Title = "Job is overdue by " + overdueDays + " days",
                    Body = "Scheduled " + IndiaFormatHelper.FormatDate(detail.Job.ScheduledDate) + ". Follow up with the technician immediately."
                });
            }

            if (string.Equals(detail.Job.PipelineStatus, "Closed", StringComparison.OrdinalIgnoreCase) && !detail.Job.InvoiceId.HasValue)
            {
                nudges.Add(new NudgeDto
                {
                    NudgeType = "Warning",
                    Title = "Job closed but not invoiced",
                    Body = "Create an invoice from this job to capture revenue."
                });
            }

            return nudges;
        }

        public void LogActivity(int jobId, string text, string type)
        {
            _repo.LogActivity(jobId, text, GetCurrentUserLabel(), type);
        }

        public WorkloadDto GetTechnicianWorkload(int employeeId, DateTime weekStartDate)
        {
            int jobCount = _repo.GetTechnicianWeekJobCount(employeeId, weekStartDate.Date);
            decimal loadPercent = Math.Min(Math.Round((jobCount / 5m) * 100m, 1), 100m);
            string colour = loadPercent < 40m ? "green" : (loadPercent <= 70m ? "amber" : "red");
            return new WorkloadDto
            {
                EmployeeId = employeeId,
                JobCount = jobCount,
                LoadPercent = loadPercent,
                LoadColour = colour
            };
        }

        public int CloseJob(int jobId, decimal actualRevenue, string closeNotes, bool generateInvoice)
        {
            SessionManager.DemandPermission("WorkOrders", "Edit");
            Job job = _repo.GetById(jobId);
            if (job == null)
                throw new Exception("Job not found.");

            job.ActualRevenue = actualRevenue > 0 ? actualRevenue : (job.QuotedRevenue > 0 ? job.QuotedRevenue : job.Revenue);
            job.CompletedDate = DateTime.Now;
            job.ClosedDate = DateTime.Now;

            int? invoiceId = null;
            string pipeline = "Closed";
            if (generateInvoice)
            {
                invoiceId = CreateInvoiceForJob(job, job.ActualRevenue);
                pipeline = "Invoiced";
            }

            _repo.UpdatePipeline(jobId, pipeline, ResolveLegacyStatus(pipeline), job.CompletedDate, job.ClosedDate, invoiceId);
            if (!string.IsNullOrWhiteSpace(closeNotes))
                _repo.UpdateNotes(jobId, (string.IsNullOrWhiteSpace(job.Notes) ? string.Empty : job.Notes + Environment.NewLine) + closeNotes.Trim());

            LogActivity(jobId, generateInvoice ? "Job closed and invoice created by " + GetCurrentUserLabel() : "Job closed by " + GetCurrentUserLabel(), "Success");
            AppDataCache.RemovePrefix("jobs:");
            return invoiceId ?? 0;
        }

        public void UpdateNotes(int jobId, string notes)
        {
            SessionManager.DemandPermission("WorkOrders", "Edit");
            _repo.UpdateNotes(jobId, notes);
            AppDataCache.RemovePrefix("jobs:");
        }

        public int GetPendingCount() => GetAllJobsWithSummary().Count(j => j.PipelineStatus == "Created" || j.PipelineStatus == "Assigned");
        public int GetInProgressCount() => GetAllJobsWithSummary().Count(j => j.PipelineStatus == "InProgress" || j.PipelineStatus == "ChecklistDone");
        public int GetCompletedCount() => GetAllJobsWithSummary().Count(j => j.PipelineStatus == "Closed" || j.PipelineStatus == "Invoiced");

        public decimal GetRevenueThisMonth()
        {
            return _repo.GetRevenueForMonth(DateTime.Today);
        }

        public decimal GetCostThisMonth()
        {
            return _repo.GetCostForMonth(DateTime.Today);
        }

        public decimal GetProfitThisMonth() => GetRevenueThisMonth() - GetCostThisMonth();

        public decimal GetAverageRevenuePerCompletedJob()
        {
            int completed = GetCompletedCount();
            return completed == 0 ? 0m : Math.Round(GetRevenueThisMonth() / completed, 2);
        }

        private int CreateInvoiceForJob(Job job, decimal actualRevenue)
        {
            Invoice invoice = new Invoice
            {
                ClientID = job.ClientID,
                SiteID = job.SiteID,
                ContractID = job.LinkedContractId ?? 0,
                InvoiceDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(7),
                InvoiceTitle = "TAX INVOICE",
                PaymentStatus = "Pending",
                Notes = "Generated from job " + job.JobNumber,
                LineItems = new List<InvoiceLineItem>
                {
                    new InvoiceLineItem
                    {
                        Description = (job.JobTitle ?? job.Title ?? "Service job") + " - " + (job.JobNumber ?? string.Empty),
                        Quantity = 1,
                        Rate = actualRevenue,
                        Amount = actualRevenue,
                        GSTPercent = 18m,
                        TaxAmount = Math.Round(actualRevenue * 0.18m, 2),
                        Unit = "Job",
                        IsBillable = true,
                        IsStockItem = false,
                        HSNCode = "998719"
                    }
                }
            };

            return _invoiceService.CreateInvoiceWithLineItems(invoice);
        }

        private void NormalizeJob(Job job, bool isNewJob)
        {
            if (job.ClientID <= 0)
                throw new Exception("Select a client.");
            if (string.IsNullOrWhiteSpace(job.JobTitle) && string.IsNullOrWhiteSpace(job.Title))
                throw new Exception("Enter a job title.");

            job.JobNumber = string.IsNullOrWhiteSpace(job.JobNumber) ? GenerateJobNumber() : job.JobNumber.Trim();
            job.JobTitle = string.IsNullOrWhiteSpace(job.JobTitle) ? job.Title?.Trim() : job.JobTitle.Trim();
            job.Title = job.JobTitle;
            job.JobType = string.IsNullOrWhiteSpace(job.JobType) ? "General" : job.JobType.Trim();
            job.Priority = string.IsNullOrWhiteSpace(job.Priority) ? "Medium" : job.Priority.Trim();
            job.PipelineStatus = NormalizePipeline(job.PipelineStatus, isNewJob && job.AssignedEmployeeID.HasValue ? "Assigned" : "Created");
            job.Status = ResolveLegacyStatus(job.PipelineStatus);
            job.QuotedRevenue = job.QuotedRevenue > 0 ? job.QuotedRevenue : job.Revenue;
            job.Revenue = job.QuotedRevenue;
            job.IsOverdue = job.ScheduledDate.Date < DateTime.Today && !string.Equals(job.PipelineStatus, "Closed", StringComparison.OrdinalIgnoreCase) && !string.Equals(job.PipelineStatus, "Invoiced", StringComparison.OrdinalIgnoreCase);
            ValidateJobForSave(job);
        }

        private void ValidateJobForSave(Job job)
        {
            ValidationResult result = _businessRules.ValidateJob(job);
            if (job != null && !string.IsNullOrWhiteSpace(job.JobNumber))
            {
                bool duplicateNumber = _repo.JobNumberExists(job.JobNumber.Trim(), job.JobID);
                if (duplicateNumber)
                    result.Add(ValidationSeverity.Error, "WorkOrders", "JobNumber", "Another work order already uses this job number.", "Open the existing work order or generate a new job number.");
            }
            _validation.EnsureValid(result, "Work order validation failed");
        }

        private void ValidatePipelineTransition(int jobId, Job job, string current, string target)
        {
            if (current == "Created" && target == "Assigned" && !job.AssignedEmployeeID.HasValue)
                throw new Exception("Assign a technician before moving to Assigned.");
            if (current == "Assigned" && target == "InProgress")
                return;
            if (current == "InProgress" && target == "ChecklistDone")
            {
                int completed = _repo.GetChecklistCompletedCount(jobId);
                int total = _repo.GetChecklistTotalCount(jobId);
                if (total == 0 || completed < total)
                    throw new Exception("Complete the checklist before moving to Checklist Done.");
                return;
            }
            if (current == "ChecklistDone" && target == "Closed")
                return;
            if (current == "Closed" && target == "Invoiced")
            {
                if (!job.InvoiceId.HasValue)
                    throw new Exception("Create an invoice before moving to Invoiced.");
                return;
            }

            if (GetPipelineRank(target) < GetPipelineRank(current))
                throw new Exception("This action only supports forward pipeline movement.");

            if (GetPipelineRank(target) - GetPipelineRank(current) > 1)
            {
                if (target == "ChecklistDone")
                {
                    int completed = _repo.GetChecklistCompletedCount(jobId);
                    int total = _repo.GetChecklistTotalCount(jobId);
                    if (total == 0 || completed < total)
                        throw new Exception("Checklist is not complete yet.");
                }
                if (target == "Invoiced" && !job.InvoiceId.HasValue)
                    throw new Exception("Create an invoice before moving to Invoiced.");
            }
        }

        private static string GetNextPipeline(string current)
        {
            switch (NormalizePipeline(current))
            {
                case "Created": return "Assigned";
                case "Assigned": return "InProgress";
                case "InProgress": return "ChecklistDone";
                case "ChecklistDone": return "Closed";
                case "Closed": return "Invoiced";
                default: return NormalizePipeline(current);
            }
        }

        private static int GetPipelineRank(string pipeline)
        {
            switch (NormalizePipeline(pipeline))
            {
                case "Created": return 1;
                case "Assigned": return 2;
                case "InProgress": return 3;
                case "ChecklistDone": return 4;
                case "Closed": return 5;
                case "Invoiced": return 6;
                default: return 0;
            }
        }

        private static string NormalizePipeline(string pipeline, string fallback = "Created")
        {
            string normalized = (pipeline ?? string.Empty).Replace(" ", string.Empty).Trim();
            switch (normalized.ToUpperInvariant())
            {
                case "CREATED": return "Created";
                case "ASSIGNED": return "Assigned";
                case "INPROGRESS": return "InProgress";
                case "CHECKLISTDONE": return "ChecklistDone";
                case "CLOSED": return "Closed";
                case "INVOICED": return "Invoiced";
                case "COMPLETED": return "Closed";
                default: return fallback;
            }
        }

        private static string ResolveLegacyStatus(string pipelineStatus)
        {
            switch (NormalizePipeline(pipelineStatus))
            {
                case "Created":
                case "Assigned":
                    return "Pending";
                case "InProgress":
                case "ChecklistDone":
                    return "In Progress";
                case "Closed":
                case "Invoiced":
                    return "Completed";
                default:
                    return "Pending";
            }
        }

        public string GenerateJobNumber()
        {
            string prefix = "JOB-" + DateTime.Now.ToString("yyMMdd") + "-";
            int nextSequence = _repo.GetJobNumberCountByPrefix(prefix) + 1;
            return prefix + nextSequence.ToString("0000");
        }

        private static string GetCurrentUserLabel()
        {
            return SessionManager.IsLoggedIn
                ? (SessionManager.CurrentUser.DisplayName ?? SessionManager.CurrentUser.Username ?? "System")
                : "System";
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.DAL
{
    public class JobRepository
    {
        private readonly DatabaseManager _db = new DatabaseManager();

        private const string BaseSelect = @"
            SELECT j.*,
                   c.CompanyName AS ClientName,
                   s.SiteName,
                   s.TravelRateINR,
                   e.Name AS AssignedEmployeeName,
                   CASE
                       WHEN CAST(j.ScheduledDate AS DATE) < CAST(GETDATE() AS DATE)
                        AND ISNULL(NULLIF(j.PipelineStatus, ''), ISNULL(j.Status, 'Created')) NOT IN ('Closed','Invoiced','Completed')
                       THEN CAST(1 AS bit)
                       ELSE CAST(0 AS bit)
                   END AS IsOverdueComputed
            FROM Jobs j
            LEFT JOIN B2BClients c ON j.ClientID = c.ClientID
            LEFT JOIN ClientSites s ON j.SiteID = s.SiteID
            LEFT JOIN Employees e ON j.AssignedEmployeeID = e.EmployeeID";

        public List<Job> GetAll()
        {
            var list = new List<Job>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(BaseSelect + " ORDER BY j.ScheduledDate DESC, j.JobID DESC", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        list.Add(MapJob(r));
                }
            }
            return list;
        }

        public Job GetById(int id)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(BaseSelect + " WHERE j.JobID=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        return r.Read() ? MapJob(r) : null;
                }
            }
        }

        public List<Job> GetByStatus(string status)
        {
            var list = new List<Job>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(BaseSelect + " WHERE ISNULL(NULLIF(j.PipelineStatus, ''), j.Status)=@status ORDER BY j.ScheduledDate DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@status", status ?? string.Empty);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            list.Add(MapJob(r));
                    }
                }
            }
            return list;
        }

        public List<JobSummaryDto> GetAllWithSummary()
        {
            var list = new List<JobSummaryDto>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT
                        j.JobID,
                        j.JobNumber,
                        ISNULL(NULLIF(j.JobTitle, ''), j.Title) AS JobTitle,
                        ISNULL(NULLIF(j.JobType, ''), 'General') AS JobType,
                        ISNULL(NULLIF(j.PipelineStatus, ''), ISNULL(NULLIF(j.Status, ''), 'Created')) AS PipelineStatus,
                        ISNULL(NULLIF(j.Priority, ''), 'Medium') AS Priority,
                        c.CompanyName AS ClientName,
                        s.SiteName,
                        e.Name AS TechnicianName,
                        j.AssignedEmployeeID,
                        j.ScheduledDate,
                        CASE
                            WHEN CAST(j.ScheduledDate AS DATE) < CAST(GETDATE() AS DATE)
                             AND ISNULL(NULLIF(j.PipelineStatus, ''), ISNULL(j.Status, 'Created')) NOT IN ('Closed','Invoiced','Completed')
                            THEN CAST(1 AS bit)
                            ELSE CAST(0 AS bit)
                        END AS IsOverdue,
                        CASE WHEN ISNULL(j.QuotedRevenue, 0) = 0 THEN ISNULL(j.Revenue, 0) ELSE j.QuotedRevenue END AS QuotedRevenue,
                        ISNULL((SELECT COUNT(*) FROM JobChecklistItems ci WHERE ci.JobId = j.JobID AND ci.IsCompleted = 1), 0) AS ChecklistCompletedCount,
                        ISNULL((SELECT COUNT(*) FROM JobChecklistItems ci WHERE ci.JobId = j.JobID), 0) AS ChecklistTotalCount,
                        ISNULL((SELECT SUM(p.TotalCost) FROM JobPartsUsed p WHERE p.JobId = j.JobID), 0) AS PartsCost,
                        j.Notes,
                        CASE
                            WHEN CASE WHEN ISNULL(j.QuotedRevenue, 0) = 0 THEN ISNULL(j.Revenue, 0) ELSE j.QuotedRevenue END <= 0 THEN 0
                            ELSE ROUND((
                                (CASE WHEN ISNULL(j.QuotedRevenue, 0) = 0 THEN ISNULL(j.Revenue, 0) ELSE j.QuotedRevenue END)
                                - (ISNULL(j.EstimatedCost, 0)
                                   + ISNULL((SELECT SUM(p.TotalCost) FROM JobPartsUsed p WHERE p.JobId = j.JobID), 0)
                                   + ISNULL(s.TravelRateINR, 0))
                            ) * 100.0
                              / NULLIF((CASE WHEN ISNULL(j.QuotedRevenue, 0) = 0 THEN ISNULL(j.Revenue, 0) ELSE j.QuotedRevenue END), 0), 2)
                        END AS EstimatedMarginPct
                    FROM Jobs j
                    LEFT JOIN B2BClients c ON j.ClientID = c.ClientID
                    LEFT JOIN ClientSites s ON j.SiteID = s.SiteID
                    LEFT JOIN Employees e ON j.AssignedEmployeeID = e.EmployeeID
                    ORDER BY j.ScheduledDate DESC, j.JobID DESC", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new JobSummaryDto
                        {
                            JobId = GetInt(r, "JobID"),
                            JobNumber = GetString(r, "JobNumber"),
                            JobTitle = GetString(r, "JobTitle"),
                            JobType = GetString(r, "JobType"),
                            PipelineStatus = GetString(r, "PipelineStatus"),
                            Priority = GetString(r, "Priority"),
                            ClientName = GetString(r, "ClientName"),
                            SiteName = GetString(r, "SiteName"),
                            TechnicianName = GetString(r, "TechnicianName"),
                            TechnicianId = GetNullableInt(r, "AssignedEmployeeID"),
                            ScheduledDate = GetDateTime(r, "ScheduledDate"),
                            IsOverdue = GetBool(r, "IsOverdue"),
                            QuotedRevenue = GetDecimal(r, "QuotedRevenue"),
                            EstimatedMarginPct = GetDecimal(r, "EstimatedMarginPct"),
                            ChecklistCompletedCount = GetInt(r, "ChecklistCompletedCount"),
                            ChecklistTotalCount = GetInt(r, "ChecklistTotalCount"),
                            Notes = GetString(r, "Notes"),
                            PartsCost = GetDecimal(r, "PartsCost")
                        });
                    }
                }
            }
            return list;
        }

        public List<JobChecklistItem> GetChecklistItems(int jobId)
        {
            var list = new List<JobChecklistItem>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT * FROM JobChecklistItems WHERE JobId=@jobId ORDER BY SortOrder, ChecklistItemId", conn))
                {
                    cmd.Parameters.AddWithValue("@jobId", jobId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            list.Add(MapChecklistItem(r));
                    }
                }
            }
            return list;
        }

        public JobChecklistItem GetChecklistItem(int checklistItemId)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT * FROM JobChecklistItems WHERE ChecklistItemId=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", checklistItemId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        return r.Read() ? MapChecklistItem(r) : null;
                }
            }
        }

        public List<JobChecklistTemplate> GetChecklistTemplates(string jobType)
        {
            var list = new List<JobChecklistTemplate>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT * FROM JobChecklistTemplates WHERE JobType=@jobType ORDER BY SortOrder, TemplateId", conn))
                {
                    cmd.Parameters.AddWithValue("@jobType", string.IsNullOrWhiteSpace(jobType) ? "General" : jobType.Trim());
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new JobChecklistTemplate
                            {
                                TemplateId = GetInt(r, "TemplateId"),
                                JobType = GetString(r, "JobType"),
                                ItemText = GetString(r, "ItemText"),
                                SortOrder = GetInt(r, "SortOrder")
                            });
                        }
                    }
                }
            }
            return list;
        }

        public void ReplaceChecklistFromTemplate(int jobId, string jobType)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlTransaction tx = conn.BeginTransaction())
                {
                    try
                    {
                        using (SqlCommand deleteCmd = new SqlCommand("DELETE FROM JobChecklistItems WHERE JobId=@jobId", conn, tx))
                        {
                            deleteCmd.Parameters.AddWithValue("@jobId", jobId);
                            deleteCmd.ExecuteNonQuery();
                        }

                        using (SqlCommand insertCmd = new SqlCommand(@"
                            INSERT INTO JobChecklistItems (JobId, ItemText, IsCompleted, SortOrder)
                            SELECT @jobId, ItemText, 0, SortOrder
                            FROM JobChecklistTemplates
                            WHERE JobType=@jobType", conn, tx))
                        {
                            insertCmd.Parameters.AddWithValue("@jobId", jobId);
                            insertCmd.Parameters.AddWithValue("@jobType", string.IsNullOrWhiteSpace(jobType) ? "General" : jobType.Trim());
                            insertCmd.ExecuteNonQuery();
                        }

                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        public int AddChecklistItem(int jobId, string itemText)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO JobChecklistItems (JobId, ItemText, IsCompleted, SortOrder)
                    VALUES (
                        @jobId,
                        @itemText,
                        0,
                        ISNULL((SELECT MAX(SortOrder) + 1 FROM JobChecklistItems WHERE JobId=@jobId), 1)
                    );
                    SELECT SCOPE_IDENTITY();", conn))
                {
                    cmd.Parameters.AddWithValue("@jobId", jobId);
                    cmd.Parameters.AddWithValue("@itemText", itemText ?? string.Empty);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public void CompleteChecklistItem(int checklistItemId, string completedBy)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE JobChecklistItems
                    SET IsCompleted = 1,
                        CompletedBy = @completedBy,
                        CompletedDate = GETDATE()
                    WHERE ChecklistItemId = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", checklistItemId);
                    cmd.Parameters.AddWithValue("@completedBy", string.IsNullOrWhiteSpace(completedBy) ? (object)DBNull.Value : completedBy);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<JobPartUsed> GetPartsUsed(int jobId)
        {
            var list = new List<JobPartUsed>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT p.*,
                           ISNULL(s.CurrentStock - ISNULL(s.ReservedStock, 0), 0) AS AvailableStock
                    FROM JobPartsUsed p
                    LEFT JOIN StockItems s ON p.InventoryItemId = s.ItemID
                    WHERE p.JobId = @jobId
                    ORDER BY p.PartUsedId DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@jobId", jobId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            list.Add(MapPartUsed(r));
                    }
                }
            }
            return list;
        }

        public int AddPartUsed(JobPartUsed part)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO JobPartsUsed
                        (JobId, InventoryItemId, ItemDescription, QuantityUsed, Unit, UnitCost, TotalCost, IsFromInventory, StockStatus)
                    VALUES
                        (@jobId, @inventoryItemId, @itemDescription, @quantityUsed, @unit, @unitCost, @totalCost, @isFromInventory, @stockStatus);
                    SELECT SCOPE_IDENTITY();", conn))
                {
                    cmd.Parameters.AddWithValue("@jobId", part.JobId);
                    cmd.Parameters.AddWithValue("@inventoryItemId", part.InventoryItemId.HasValue ? (object)part.InventoryItemId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@itemDescription", part.ItemDescription ?? string.Empty);
                    cmd.Parameters.AddWithValue("@quantityUsed", part.QuantityUsed);
                    cmd.Parameters.AddWithValue("@unit", string.IsNullOrWhiteSpace(part.Unit) ? "Nos" : part.Unit.Trim());
                    cmd.Parameters.AddWithValue("@unitCost", part.UnitCost);
                    cmd.Parameters.AddWithValue("@totalCost", part.TotalCost);
                    cmd.Parameters.AddWithValue("@isFromInventory", part.IsFromInventory);
                    cmd.Parameters.AddWithValue("@stockStatus", string.IsNullOrWhiteSpace(part.StockStatus) ? (object)DBNull.Value : part.StockStatus.Trim());
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public List<JobActivityEntry> GetActivityLog(int jobId, int take = 0)
        {
            var list = new List<JobActivityEntry>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                string sql = "SELECT " + (take > 0 ? "TOP " + take + " " : string.Empty) + "* FROM JobActivityLog WHERE JobId=@jobId ORDER BY ActivityDate DESC, ActivityId DESC";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@jobId", jobId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            list.Add(MapActivityEntry(r));
                    }
                }
            }
            return list;
        }

        public void LogActivity(int jobId, string activityText, string performedBy, string activityType)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO JobActivityLog (JobId, ActivityText, PerformedBy, ActivityDate, ActivityType)
                    VALUES (@jobId, @activityText, @performedBy, GETDATE(), @activityType)", conn))
                {
                    cmd.Parameters.AddWithValue("@jobId", jobId);
                    cmd.Parameters.AddWithValue("@activityText", activityText ?? string.Empty);
                    cmd.Parameters.AddWithValue("@performedBy", string.IsNullOrWhiteSpace(performedBy) ? (object)DBNull.Value : performedBy);
                    cmd.Parameters.AddWithValue("@activityType", string.IsNullOrWhiteSpace(activityType) ? "Info" : activityType.Trim());
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdatePipeline(int jobId, string pipelineStatus, string status, DateTime? completedDate, DateTime? closedDate, int? invoiceId)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE Jobs
                    SET PipelineStatus = @pipelineStatus,
                        Status = @status,
                        CompletedDate = @completedDate,
                        ClosedDate = @closedDate,
                        InvoiceId = @invoiceId,
                        IsOverdue = CASE
                            WHEN CAST(ScheduledDate AS DATE) < CAST(GETDATE() AS DATE)
                             AND @pipelineStatus NOT IN ('Closed', 'Invoiced', 'Completed')
                            THEN 1 ELSE 0 END
                    WHERE JobID = @jobId", conn))
                {
                    cmd.Parameters.AddWithValue("@jobId", jobId);
                    cmd.Parameters.AddWithValue("@pipelineStatus", pipelineStatus ?? "Created");
                    cmd.Parameters.AddWithValue("@status", status ?? "Pending");
                    cmd.Parameters.AddWithValue("@completedDate", completedDate.HasValue ? (object)completedDate.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@closedDate", closedDate.HasValue ? (object)closedDate.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@invoiceId", invoiceId.HasValue ? (object)invoiceId.Value : DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdateNotes(int jobId, string notes)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("UPDATE Jobs SET Notes=@notes WHERE JobID=@jobId", conn))
                {
                    cmd.Parameters.AddWithValue("@jobId", jobId);
                    cmd.Parameters.AddWithValue("@notes", string.IsNullOrWhiteSpace(notes) ? (object)DBNull.Value : notes.Trim());
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public int GetChecklistCompletedCount(int jobId)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM JobChecklistItems WHERE JobId=@jobId AND IsCompleted=1", conn))
                {
                    cmd.Parameters.AddWithValue("@jobId", jobId);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public int GetChecklistTotalCount(int jobId)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM JobChecklistItems WHERE JobId=@jobId", conn))
                {
                    cmd.Parameters.AddWithValue("@jobId", jobId);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public decimal GetPartsCost(int jobId)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT ISNULL(SUM(TotalCost), 0) FROM JobPartsUsed WHERE JobId=@jobId", conn))
                {
                    cmd.Parameters.AddWithValue("@jobId", jobId);
                    return Convert.ToDecimal(cmd.ExecuteScalar());
                }
            }
        }

        public int Create(Job job)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO Jobs
                    (JobNumber, ClientID, SiteID, Title, JobTitle, Description, AssignedEmployeeID, ScheduledDate, CompletedDate, ClosedDate, Priority, Status, PipelineStatus, JobType, LinkedContractId, EstimatedCost, Revenue, QuotedRevenue, ActualRevenue, IsOverdue, InvoiceId, Notes, CreatedByUserId, CreatedByName)
                    VALUES
                    (@jobNo, @clientId, @siteId, @title, @jobTitle, @desc, @employeeId, @scheduled, @completed, @closedDate, @priority, @status, @pipelineStatus, @jobType, @linkedContractId, @cost, @revenue, @quotedRevenue, @actualRevenue, @isOverdue, @invoiceId, @notes, @createdByUserId, @createdByName);
                    SELECT SCOPE_IDENTITY();", conn))
                {
                    AddParams(cmd, job);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public void Update(Job job)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE Jobs SET
                        JobNumber=@jobNo,
                        ClientID=@clientId,
                        SiteID=@siteId,
                        Title=@title,
                        JobTitle=@jobTitle,
                        Description=@desc,
                        AssignedEmployeeID=@employeeId,
                        ScheduledDate=@scheduled,
                        CompletedDate=@completed,
                        ClosedDate=@closedDate,
                        Priority=@priority,
                        Status=@status,
                        PipelineStatus=@pipelineStatus,
                        JobType=@jobType,
                        LinkedContractId=@linkedContractId,
                        EstimatedCost=@cost,
                        Revenue=@revenue,
                        QuotedRevenue=@quotedRevenue,
                        ActualRevenue=@actualRevenue,
                        IsOverdue=@isOverdue,
                        InvoiceId=@invoiceId,
                        Notes=@notes,
                        ModifiedByUserId=@modifiedByUserId,
                        ModifiedByName=@modifiedByName,
                        ModifiedDate=@modifiedDate
                    WHERE JobID=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", job.JobID);
                    AddParams(cmd, job);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void Delete(int jobId)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlTransaction tx = conn.BeginTransaction())
                {
                    try
                    {
                        ExecuteDelete(conn, tx, "DELETE FROM PendingCharges WHERE WorkOrderId=@id", jobId);
                        ExecuteDelete(conn, tx, "UPDATE PurchaseLineItems SET LinkedWorkOrderId=NULL WHERE LinkedWorkOrderId=@id", jobId);
                        ExecuteDelete(conn, tx, "DELETE FROM JobActivityLog WHERE JobId=@id", jobId);
                        ExecuteDelete(conn, tx, "DELETE FROM JobPartsUsed WHERE JobId=@id", jobId);
                        ExecuteDelete(conn, tx, "DELETE FROM JobChecklistItems WHERE JobId=@id", jobId);
                        ExecuteDelete(conn, tx, "DELETE FROM Jobs WHERE JobID=@id", jobId);
                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        private static void ExecuteDelete(SqlConnection conn, SqlTransaction tx, string sql, int id)
        {
            using (SqlCommand cmd = new SqlCommand(sql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        public int GetCountByStatus(string status)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM Jobs WHERE ISNULL(NULLIF(PipelineStatus, ''), Status)=@status", conn))
                {
                    cmd.Parameters.AddWithValue("@status", status ?? string.Empty);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public decimal GetRevenueForMonth(DateTime month)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT ISNULL(SUM(CASE WHEN ActualRevenue > 0 THEN ActualRevenue ELSE ISNULL(NULLIF(QuotedRevenue, 0), Revenue) END), 0)
                    FROM Jobs
                    WHERE ISNULL(NULLIF(PipelineStatus, ''), Status) IN ('Closed','Invoiced','Completed')
                      AND YEAR(ISNULL(ClosedDate, ISNULL(CompletedDate, ScheduledDate))) = @year
                      AND MONTH(ISNULL(ClosedDate, ISNULL(CompletedDate, ScheduledDate))) = @month", conn))
                {
                    cmd.Parameters.AddWithValue("@year", month.Year);
                    cmd.Parameters.AddWithValue("@month", month.Month);
                    return Convert.ToDecimal(cmd.ExecuteScalar());
                }
            }
        }

        public decimal GetCostForMonth(DateTime month)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT ISNULL(SUM(EstimatedCost), 0)
                    FROM Jobs
                    WHERE YEAR(ScheduledDate) = @year
                      AND MONTH(ScheduledDate) = @month", conn))
                {
                    cmd.Parameters.AddWithValue("@year", month.Year);
                    cmd.Parameters.AddWithValue("@month", month.Month);
                    return Convert.ToDecimal(cmd.ExecuteScalar());
                }
            }
        }

        public int GetTechnicianWeekJobCount(int employeeId, DateTime weekStartDate)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT COUNT(*)
                    FROM Jobs
                    WHERE AssignedEmployeeID = @employeeId
                      AND ScheduledDate >= @weekStartDate
                      AND ScheduledDate < DATEADD(day, 7, @weekStartDate)
                      AND ISNULL(NULLIF(PipelineStatus, ''), Status) NOT IN ('Closed', 'Invoiced', 'Completed')", conn))
                {
                    cmd.Parameters.AddWithValue("@employeeId", employeeId);
                    cmd.Parameters.AddWithValue("@weekStartDate", weekStartDate.Date);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        private static void AddParams(SqlCommand cmd, Job job)
        {
            string title = string.IsNullOrWhiteSpace(job.JobTitle) ? job.Title : job.JobTitle;
            string pipeline = string.IsNullOrWhiteSpace(job.PipelineStatus) ? ResolvePipelineStatus(job) : job.PipelineStatus.Trim();
            string status = string.IsNullOrWhiteSpace(job.Status) ? ResolveLegacyStatus(pipeline) : job.Status.Trim();
            decimal quotedRevenue = job.QuotedRevenue > 0 ? job.QuotedRevenue : job.Revenue;
            bool isOverdue = job.IsOverdue || (job.ScheduledDate.Date < DateTime.Today && !IsClosedPipeline(pipeline));

            cmd.Parameters.AddWithValue("@jobNo", job.JobNumber ?? string.Empty);
            cmd.Parameters.AddWithValue("@clientId", job.ClientID);
            cmd.Parameters.AddWithValue("@siteId", job.SiteID);
            cmd.Parameters.AddWithValue("@title", title ?? string.Empty);
            cmd.Parameters.AddWithValue("@jobTitle", string.IsNullOrWhiteSpace(job.JobTitle) ? (object)(title ?? string.Empty) : job.JobTitle.Trim());
            cmd.Parameters.AddWithValue("@desc", (object)job.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@employeeId", job.AssignedEmployeeID.HasValue ? (object)job.AssignedEmployeeID.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@scheduled", job.ScheduledDate == default(DateTime) ? DateTime.Today : job.ScheduledDate);
            cmd.Parameters.AddWithValue("@completed", job.CompletedDate.HasValue ? (object)job.CompletedDate.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@closedDate", job.ClosedDate.HasValue ? (object)job.ClosedDate.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@priority", string.IsNullOrWhiteSpace(job.Priority) ? "Medium" : job.Priority);
            cmd.Parameters.AddWithValue("@status", status);
            cmd.Parameters.AddWithValue("@pipelineStatus", pipeline);
            cmd.Parameters.AddWithValue("@jobType", string.IsNullOrWhiteSpace(job.JobType) ? "General" : job.JobType.Trim());
            cmd.Parameters.AddWithValue("@linkedContractId", job.LinkedContractId.HasValue ? (object)job.LinkedContractId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@cost", job.EstimatedCost);
            cmd.Parameters.AddWithValue("@revenue", quotedRevenue);
            cmd.Parameters.AddWithValue("@quotedRevenue", quotedRevenue);
            cmd.Parameters.AddWithValue("@actualRevenue", job.ActualRevenue);
            cmd.Parameters.AddWithValue("@isOverdue", isOverdue);
            cmd.Parameters.AddWithValue("@invoiceId", job.InvoiceId.HasValue ? (object)job.InvoiceId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@notes", (object)job.Notes ?? DBNull.Value);
            if (cmd.CommandText.IndexOf("@createdByUserId", StringComparison.OrdinalIgnoreCase) >= 0)
                cmd.Parameters.AddWithValue("@createdByUserId", job.CreatedByUserId.HasValue ? (object)job.CreatedByUserId.Value : DBNull.Value);
            if (cmd.CommandText.IndexOf("@createdByName", StringComparison.OrdinalIgnoreCase) >= 0)
                cmd.Parameters.AddWithValue("@createdByName", string.IsNullOrWhiteSpace(job.CreatedByName) ? (object)DBNull.Value : job.CreatedByName);
            if (cmd.CommandText.IndexOf("@modifiedByUserId", StringComparison.OrdinalIgnoreCase) >= 0)
                cmd.Parameters.AddWithValue("@modifiedByUserId", job.ModifiedByUserId.HasValue ? (object)job.ModifiedByUserId.Value : DBNull.Value);
            if (cmd.CommandText.IndexOf("@modifiedByName", StringComparison.OrdinalIgnoreCase) >= 0)
                cmd.Parameters.AddWithValue("@modifiedByName", string.IsNullOrWhiteSpace(job.ModifiedByName) ? (object)DBNull.Value : job.ModifiedByName);
            if (cmd.CommandText.IndexOf("@modifiedDate", StringComparison.OrdinalIgnoreCase) >= 0)
                cmd.Parameters.AddWithValue("@modifiedDate", job.ModifiedDate.HasValue ? (object)job.ModifiedDate.Value : DBNull.Value);
        }

        private static Job MapJob(SqlDataReader r)
        {
            string pipelineStatus = GetString(r, "PipelineStatus");
            string jobTitle = GetString(r, "JobTitle");
            return new Job
            {
                JobID = GetInt(r, "JobID"),
                JobNumber = GetString(r, "JobNumber"),
                ClientID = GetInt(r, "ClientID"),
                SiteID = GetInt(r, "SiteID"),
                Title = string.IsNullOrWhiteSpace(jobTitle) ? GetString(r, "Title") : jobTitle,
                JobTitle = string.IsNullOrWhiteSpace(jobTitle) ? GetString(r, "Title") : jobTitle,
                Description = GetString(r, "Description"),
                AssignedEmployeeID = GetNullableInt(r, "AssignedEmployeeID"),
                AssignedEmployeeName = GetString(r, "AssignedEmployeeName"),
                ScheduledDate = GetDateTime(r, "ScheduledDate"),
                CompletedDate = GetNullableDateTime(r, "CompletedDate"),
                ClosedDate = GetNullableDateTime(r, "ClosedDate"),
                Priority = GetString(r, "Priority"),
                Status = string.IsNullOrWhiteSpace(GetString(r, "Status")) ? ResolveLegacyStatus(pipelineStatus) : GetString(r, "Status"),
                PipelineStatus = string.IsNullOrWhiteSpace(pipelineStatus) ? ResolvePipelineStatus(GetString(r, "Status"), GetNullableInt(r, "AssignedEmployeeID")) : pipelineStatus,
                JobType = string.IsNullOrWhiteSpace(GetString(r, "JobType")) ? "General" : GetString(r, "JobType"),
                LinkedContractId = GetNullableInt(r, "LinkedContractId"),
                EstimatedCost = GetDecimal(r, "EstimatedCost"),
                Revenue = GetDecimal(r, "Revenue"),
                QuotedRevenue = GetDecimal(r, "QuotedRevenue") > 0 ? GetDecimal(r, "QuotedRevenue") : GetDecimal(r, "Revenue"),
                ActualRevenue = GetDecimal(r, "ActualRevenue"),
                IsOverdue = HasColumn(r, "IsOverdueComputed") ? GetBool(r, "IsOverdueComputed") : GetBool(r, "IsOverdue"),
                InvoiceId = GetNullableInt(r, "InvoiceId"),
                Notes = GetString(r, "Notes"),
                ClientName = GetString(r, "ClientName"),
                SiteName = GetString(r, "SiteName"),
                CreatedByUserId = GetNullableInt(r, "CreatedByUserId"),
                CreatedByName = GetString(r, "CreatedByName"),
                ModifiedByUserId = GetNullableInt(r, "ModifiedByUserId"),
                ModifiedByName = GetString(r, "ModifiedByName"),
                ModifiedDate = GetNullableDateTime(r, "ModifiedDate"),
                CreatedDate = GetNullableDateTime(r, "CreatedDate") ?? DateTime.Now
            };
        }

        private static JobChecklistItem MapChecklistItem(SqlDataReader r)
        {
            return new JobChecklistItem
            {
                ChecklistItemId = GetInt(r, "ChecklistItemId"),
                JobId = GetInt(r, "JobId"),
                ItemText = GetString(r, "ItemText"),
                IsCompleted = GetBool(r, "IsCompleted"),
                CompletedBy = GetString(r, "CompletedBy"),
                CompletedDate = GetNullableDateTime(r, "CompletedDate"),
                SortOrder = GetInt(r, "SortOrder")
            };
        }

        private static JobPartUsed MapPartUsed(SqlDataReader r)
        {
            return new JobPartUsed
            {
                PartUsedId = GetInt(r, "PartUsedId"),
                JobId = GetInt(r, "JobId"),
                InventoryItemId = GetNullableInt(r, "InventoryItemId"),
                ItemDescription = GetString(r, "ItemDescription"),
                QuantityUsed = GetDecimal(r, "QuantityUsed"),
                Unit = GetString(r, "Unit"),
                UnitCost = GetDecimal(r, "UnitCost"),
                TotalCost = GetDecimal(r, "TotalCost"),
                IsFromInventory = GetBool(r, "IsFromInventory"),
                StockStatus = GetString(r, "StockStatus"),
                AvailableStock = HasColumn(r, "AvailableStock") ? GetDecimal(r, "AvailableStock") : 0m
            };
        }

        private static JobActivityEntry MapActivityEntry(SqlDataReader r)
        {
            return new JobActivityEntry
            {
                ActivityId = GetInt(r, "ActivityId"),
                JobId = GetInt(r, "JobId"),
                ActivityText = GetString(r, "ActivityText"),
                PerformedBy = GetString(r, "PerformedBy"),
                ActivityDate = GetDateTime(r, "ActivityDate"),
                ActivityType = GetString(r, "ActivityType")
            };
        }

        private static string ResolvePipelineStatus(Job job)
        {
            if (!string.IsNullOrWhiteSpace(job.PipelineStatus))
                return job.PipelineStatus.Trim();

            return ResolvePipelineStatus(job.Status, job.AssignedEmployeeID);
        }

        private static string ResolvePipelineStatus(string status, int? assignedEmployeeId)
        {
            string normalized = (status ?? string.Empty).Trim();
            if (string.Equals(normalized, "Invoiced", StringComparison.OrdinalIgnoreCase))
                return "Invoiced";
            if (string.Equals(normalized, "Completed", StringComparison.OrdinalIgnoreCase))
                return "Closed";
            if (string.Equals(normalized, "In Progress", StringComparison.OrdinalIgnoreCase))
                return "InProgress";
            if (assignedEmployeeId.HasValue && assignedEmployeeId.Value > 0)
                return "Assigned";
            return "Created";
        }

        private static string ResolveLegacyStatus(string pipelineStatus)
        {
            switch ((pipelineStatus ?? string.Empty).Trim())
            {
                case "Assigned":
                case "Created":
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

        private static bool IsClosedPipeline(string pipelineStatus)
        {
            string normalized = (pipelineStatus ?? string.Empty).Trim();
            return string.Equals(normalized, "Closed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Invoiced", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Completed", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasColumn(SqlDataReader r, string name)
        {
            for (int i = 0; i < r.FieldCount; i++)
            {
                if (string.Equals(r.GetName(i), name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string GetString(SqlDataReader r, string name) => r[name] == DBNull.Value ? null : Convert.ToString(r[name]);
        private static int GetInt(SqlDataReader r, string name) => r[name] == DBNull.Value ? 0 : Convert.ToInt32(r[name]);
        private static int? GetNullableInt(SqlDataReader r, string name) => r[name] == DBNull.Value ? (int?)null : Convert.ToInt32(r[name]);
        private static DateTime GetDateTime(SqlDataReader r, string name) => r[name] == DBNull.Value ? DateTime.Today : Convert.ToDateTime(r[name]);
        private static DateTime? GetNullableDateTime(SqlDataReader r, string name) => r[name] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r[name]);
        private static decimal GetDecimal(SqlDataReader r, string name) => r[name] == DBNull.Value ? 0m : Convert.ToDecimal(r[name]);
        private static bool GetBool(SqlDataReader r, string name) => r[name] != DBNull.Value && Convert.ToBoolean(r[name]);
    }
}

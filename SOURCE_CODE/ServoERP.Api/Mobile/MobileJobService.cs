using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using ServoERP.Engineer;

namespace ServoERP.Api.Mobile;

public sealed class MobileJobService(string connectionString)
{
    private sealed class JobRow
    {
        public int JobId { get; set; } public Guid PublicId { get; set; } public long OfficeVersion { get; set; }
        public string Number { get; set; } = ""; public string Priority { get; set; } = ""; public string Customer { get; set; } = "";
        public string Site { get; set; } = ""; public string Address { get; set; } = ""; public string Contact { get; set; } = ""; public string Phone { get; set; } = "";
        public string Fault { get; set; } = ""; public DateTime ScheduledDate { get; set; } public string OfficeStatus { get; set; } = "";
        public string? ReportJson { get; set; } public string? EngineerRequiredPhotos { get; set; }
        public int ClientId { get; set; } public int SiteId { get; set; } public int AssignedEmployeeId { get; set; }
        public DateTime? ModifiedDate { get; set; } public string? InternalNotes { get; set; }
    }
    private sealed class ReceiptRow { public string RequestHash { get; set; } = ""; public string ReceiptJson { get; set; } = ""; public int JobId { get; set; } }
    public async Task<FieldReport?> OfficeReport(int userId, int companyId, int jobId, CancellationToken ct)
    {
        await using var c = new SqlConnection(connectionString); await c.OpenAsync(ct);
        var allowed = await c.ExecuteScalarAsync<int>(new CommandDefinition(@"SELECT COUNT(1) FROM AppUsers u JOIN UserCompanies uc ON uc.UserId=u.UserId AND uc.CompanyId=@companyId AND uc.IsActive=1
JOIN RolePermissions p ON p.RoleId=ISNULL(uc.RoleId,u.RoleId) AND p.ModuleKey='WorkOrders' AND p.CanView=1 WHERE u.UserId=@userId AND u.IsActive=1", new { userId, companyId }, cancellationToken: ct));
        if (allowed == 0) throw new BadHttpRequestException("Your account cannot view this work-order report.", 403);
        var json = await c.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT r.ReportJson FROM EngineerReports r JOIN Jobs j ON j.JobID=r.JobId AND j.CompanyId=r.CompanyId WHERE r.JobId=@jobId AND r.CompanyId=@companyId", new { jobId, companyId }, cancellationToken: ct));
        return json is null ? null : JsonSerializer.Deserialize<FieldReport>(json);
    }
    private const string SelectJobs = @"SELECT j.JobID JobId,j.SyncPublicId PublicId,ISNULL(j.SyncVersion,0) OfficeVersion,j.JobNumber Number,j.Priority,
c.CompanyName Customer,s.SiteName Site,s.Address,c.PrimaryContact Contact,c.Phone,j.Description Fault,j.ScheduledDate,
ISNULL(j.PipelineStatus,j.Status) OfficeStatus,r.ReportJson,j.EngineerRequiredPhotos,j.ClientID,j.SiteID,j.AssignedEmployeeID,j.ModifiedDate,j.Notes InternalNotes
FROM Jobs j WITH(UPDLOCK,HOLDLOCK)
JOIN B2BClients c ON c.ClientID=j.ClientID AND c.CompanyId=j.CompanyId
JOIN ClientSites s ON s.SiteID=j.SiteID AND s.CompanyId=j.CompanyId
LEFT JOIN EngineerReports r ON r.JobId=j.JobID AND r.CompanyId=j.CompanyId
WHERE j.CompanyId=@CompanyId AND j.AssignedEmployeeID=@EmployeeId AND j.DeletedUtc IS NULL AND j.SyncPublicId IS NOT NULL";
    public async Task<List<JobSnapshot>> Jobs(EngineerIdentity identity, CancellationToken ct)
    {
        await using var c = new SqlConnection(connectionString); await c.OpenAsync(ct);
        var rows = (await c.QueryAsync<JobRow>(new CommandDefinition(SelectJobs + " ORDER BY j.ScheduledDate DESC", identity, cancellationToken: ct))).ToList();
        var jobs = new List<JobSnapshot>();
        foreach (var row in rows) jobs.Add(await Snapshot(c, null, row, ct));
        return jobs;
    }
    public async Task<List<PartStock>> Stock(EngineerIdentity identity, CancellationToken ct)
    {
        await using var c = new SqlConnection(connectionString);
        return (await c.QueryAsync<PartStock>(new CommandDefinition(@"SELECT s.ItemID ItemId,s.ItemName Name,s.Unit,
CASE WHEN v.Quantity < s.CurrentStock-ISNULL(s.ReservedStock,0) THEN v.Quantity ELSE s.CurrentStock-ISNULL(s.ReservedStock,0) END Available
FROM EngineerVanStock v JOIN StockItems s ON s.ItemID=v.ItemId AND s.CompanyId=v.CompanyId
WHERE v.CompanyId=@CompanyId AND v.EmployeeId=@EmployeeId AND s.IsActive=1 ORDER BY s.ItemName", identity, cancellationToken: ct))).ToList();
    }
    public async Task<int> AuthorizedJob(EngineerIdentity identity, Guid publicId, CancellationToken ct)
    {
        await using var c = new SqlConnection(connectionString);
        var id = await c.QuerySingleOrDefaultAsync<int?>(new CommandDefinition("SELECT JobID FROM Jobs WHERE SyncPublicId=@publicId AND CompanyId=@CompanyId AND AssignedEmployeeID=@EmployeeId AND DeletedUtc IS NULL AND EXISTS(SELECT 1 FROM RolePermissions WHERE RoleId=@RoleId AND ModuleKey='WorkOrders' AND CanEdit=1)", new { publicId, identity.CompanyId, identity.EmployeeId, identity.RoleId }, cancellationToken: ct));
        return id ?? throw new MobileHttpException(403, "This job is not assigned to your account.");
    }
    public async Task<OperationReceipt> Submit(EngineerIdentity identity, MobileOperation op, UploadReceipt? upload, CancellationToken ct)
    {
        await using var c = new SqlConnection(connectionString); await c.OpenAsync(ct); await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        // Business writes, idempotency, stock and audit share one SQL transaction; Dapper supplies all parameters.
        var parameters = new { identity.CompanyId, identity.EmployeeId, identity.UserId, identity.RoleId, publicId = op.JobPublicId, op.OperationId };
        var row = await c.QuerySingleOrDefaultAsync<JobRow>(new CommandDefinition(SelectJobs + " AND j.SyncPublicId=@publicId", parameters, tx, cancellationToken: ct))
            ?? throw new MobileHttpException(403, "This job is no longer assigned to you. Your work is preserved for office review.");
        await DemandPermission(c, tx, identity, "WorkOrders", ct);
        var requestHash = MobileAuthService.Hash(JsonSerializer.Serialize(op));
        var existing = await c.QuerySingleOrDefaultAsync<ReceiptRow>(new CommandDefinition("SELECT * FROM EngineerReceipts WITH(UPDLOCK,HOLDLOCK) WHERE CompanyId=@CompanyId AND UserId=@UserId AND OperationId=@OperationId", parameters, tx, cancellationToken: ct));
        if (existing is not null)
        {
            if (existing.JobId != row.JobId || existing.RequestHash != requestHash) throw new MobileHttpException(409, "This operation ID was used for a different action.");
            await tx.CommitAsync(ct); return JsonSerializer.Deserialize<OperationReceipt>(existing.ReceiptJson)!;
        }
        if (row.OfficeStatus is "Cancelled" or "Closed" or "Invoiced" or "Completed") throw new MobileHttpException(409, "The office changed or closed this job. Your work is preserved for review.");
        var snapshot = await Snapshot(c, tx, row, ct); var report = snapshot.Report;
        long expectedRevision = op.BaseRevision;
        if (op.PredecessorId.HasValue)
        {
            var predecessor = await c.QuerySingleOrDefaultAsync<ReceiptRow>(new CommandDefinition("SELECT * FROM EngineerReceipts WHERE CompanyId=@CompanyId AND UserId=@UserId AND OperationId=@id AND JobId=@JobId", new { identity.CompanyId, identity.UserId, id = op.PredecessorId, row.JobId }, tx, cancellationToken: ct));
            if (predecessor is null) throw new MobileHttpException(409, "An earlier job action must synchronize first.");
            expectedRevision = JsonSerializer.Deserialize<OperationReceipt>(predecessor.ReceiptJson)!.Revision;
        }
        if (op.OfficeVersion != row.OfficeVersion || op.OfficeFingerprint != snapshot.OfficeFingerprint || expectedRevision != report.Revision) throw new MobileHttpException(409, "This job was updated by the office or another device. Your draft is saved; office review is required.");
        if (op.CreatedUtc > DateTimeOffset.UtcNow.AddMinutes(10) || op.CreatedUtc < DateTimeOffset.UtcNow.AddYears(-1)) throw new MobileHttpException(400, "Check the device date and time before synchronizing.");
        var before = report.State;
        if (op.Type == OperationType.Part)
        {
            await DemandPermission(c, tx, identity, "Inventory", ct);
            op.PartName = await c.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT ItemName FROM StockItems WITH(UPDLOCK,HOLDLOCK) WHERE ItemID=@ItemId AND CompanyId=@CompanyId AND IsActive=1", new { op.ItemId, identity.CompanyId }, tx, cancellationToken: ct)) ?? throw new MobileHttpException(400, "This part is not available.");
        }
        if (op.Type == OperationType.Photo)
        {
            if (op.Photo is null || upload is null || upload.AttachmentId != op.Photo.AttachmentId || upload.Hash != op.Photo.Hash) throw new MobileHttpException(400, "Upload the job photo before synchronizing this action.");
            var valid = await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(1) FROM EngineerAttachments WHERE AttachmentId=@AttachmentId AND OperationId=@OperationId AND CompanyId=@CompanyId AND UserId=@UserId AND JobId=@JobId AND SourceHash=@Hash", new { upload.AttachmentId, op.OperationId, identity.CompanyId, identity.UserId, row.JobId, upload.Hash }, tx, cancellationToken: ct));
            if (valid != 1) throw new MobileHttpException(403, "This attachment does not belong to your job.");
        }
        FieldRules.Apply(report, op, identity.UserId);
        if (op.Type == OperationType.Part) await Consume(c, tx, identity, row.JobId, op, ct);
        if (op.Type == OperationType.Photo) await c.ExecuteAsync(new CommandDefinition("UPDATE EngineerAttachments SET Linked=1 WHERE AttachmentId=@AttachmentId", op.Photo, tx, cancellationToken: ct));
        if (op.Type == OperationType.Checklist)
        {
            await c.ExecuteAsync(new CommandDefinition(@"UPDATE JobChecklistItems SET EngineerOutcome=@Outcome,EngineerReading=@Reading,EngineerNote=@Note,IsCompleted=@done,CompletedBy=@actor,CompletedDate=GETDATE()
WHERE ChecklistItemId=@Id AND JobId=@JobId", new { op.Checklist!.Id, op.Checklist.Outcome, op.Checklist.Reading, op.Checklist.Note, done = op.Checklist.Outcome is "Pass" or "N/A", actor = identity.DisplayName, row.JobId }, tx, cancellationToken: ct));
        }
        var json = JsonSerializer.Serialize(report);
        await c.ExecuteAsync(new CommandDefinition(@"UPDATE EngineerReports SET Revision=@Revision,ReportJson=@json,UpdatedUtc=SYSUTCDATETIME() WHERE JobId=@JobId AND CompanyId=@CompanyId;
IF @@ROWCOUNT=0 INSERT EngineerReports(JobId,CompanyId,Revision,ReportJson,UpdatedUtc) VALUES(@JobId,@CompanyId,@Revision,@json,SYSUTCDATETIME());", new { row.JobId, identity.CompanyId, report.Revision, json }, tx, cancellationToken: ct));
        var action = op.Type == OperationType.Transition ? op.TargetState.ToString() : op.Type.ToString();
        await c.ExecuteAsync(new CommandDefinition(@"INSERT EngineerAudit(CompanyId,UserId,JobId,OperationId,DeviceId,Action,CapturedUtc,ReceivedUtc,BeforeState,AfterState)
VALUES(@CompanyId,@UserId,@JobId,@OperationId,@DeviceId,@action,@captured,SYSUTCDATETIME(),@before,@after);
INSERT JobActivityLog(JobId,ActivityText,PerformedBy,ActivityDate,ActivityType) VALUES(@JobId,@text,@actor,GETDATE(),'Info');
INSERT AuditLog(UserId,Username,Action,ModuleKey,RecordId,Description) VALUES(@UserId,@actor,@action,'WorkOrders',@JobId,@text);", new { identity.CompanyId, identity.UserId, row.JobId, op.OperationId, op.DeviceId, action, captured = op.CreatedUtc.UtcDateTime, before = before.ToString(), after = report.State.ToString(), text = $"Engineer {action}; operation {op.OperationId}", actor = identity.DisplayName }, tx, cancellationToken: ct));
        if (op.Type == OperationType.Complete)
        {
            foreach (var summary in new[] { "Diagnosis: " + report.Diagnosis, "Work performed: " + report.WorkPerformed, "Test results: " + report.TestResults })
                await c.ExecuteAsync(new CommandDefinition("INSERT JobActivityLog(JobId,ActivityText,PerformedBy,ActivityDate,ActivityType) VALUES(@JobId,@text,@actor,GETDATE(),'Success')", new { row.JobId, text = summary.Length > 480 ? summary[..480] + "…" : summary, actor = identity.DisplayName }, tx, cancellationToken: ct));
        }
        var receipt = new OperationReceipt(op.OperationId, report.Revision, report);
        await c.ExecuteAsync(new CommandDefinition("INSERT EngineerReceipts(OperationId,CompanyId,UserId,JobId,RequestHash,ReceiptJson,CommittedUtc) VALUES(@OperationId,@CompanyId,@UserId,@JobId,@requestHash,@receipt,SYSUTCDATETIME())", new { op.OperationId, identity.CompanyId, identity.UserId, row.JobId, requestHash, receipt = JsonSerializer.Serialize(receipt) }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct); return receipt;
    }
    private static async Task DemandPermission(SqlConnection c, SqlTransaction tx, EngineerIdentity identity, string module, CancellationToken ct)
    {
        if (await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(1) FROM RolePermissions WHERE RoleId=@RoleId AND ModuleKey=@module AND CanEdit=1", new { identity.RoleId, module }, tx, cancellationToken: ct)) == 0) throw new MobileHttpException(403, "Your account cannot perform this action. Contact the office.");
    }
    private static async Task<JobSnapshot> Snapshot(SqlConnection c, SqlTransaction? tx, JobRow row, CancellationToken ct)
    {
        var report = row.ReportJson is null ? new FieldReport() : JsonSerializer.Deserialize<FieldReport>(row.ReportJson)!;
        report.JobPublicId = row.PublicId;
        var items = (await c.QueryAsync<ChecklistResult>(new CommandDefinition("SELECT ChecklistItemId Id,ItemText Text,EngineerRequired Required,ISNULL(EngineerOutcome,'') Outcome,ISNULL(EngineerReading,'') Reading,ISNULL(EngineerNote,'') Note FROM JobChecklistItems WHERE JobId=@JobId ORDER BY SortOrder,ChecklistItemId", row, tx, cancellationToken: ct))).ToList();
        // Always refresh office-configured checklist/policy, including centrally added requirements.
        report.Checklist = items;
        report.RequiredPhotoCategories = (row.EngineerRequiredPhotos ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var fingerprint = MobileAuthService.Hash(JsonSerializer.Serialize(new { row.OfficeStatus, row.Fault, row.ScheduledDate, row.ClientId, row.SiteId, row.AssignedEmployeeId, row.ModifiedDate, row.InternalNotes, row.EngineerRequiredPhotos }));
        return new() { PublicId = row.PublicId, OfficeVersion = row.OfficeVersion, OfficeFingerprint = fingerprint, Number = row.Number, Priority = row.Priority, Customer = row.Customer, Site = row.Site, Address = row.Address, Contact = row.Contact, Phone = row.Phone, Fault = row.Fault, ScheduledDate = row.ScheduledDate, OfficeStatus = row.OfficeStatus, Report = report };
    }
    private static async Task Consume(SqlConnection c, SqlTransaction tx, EngineerIdentity identity, int jobId, MobileOperation op, CancellationToken ct)
    {
        var stock = await c.QuerySingleAsync<dynamic>(new CommandDefinition("SELECT CurrentStock,ISNULL(ReservedStock,0) ReservedStock,LastPurchaseRate,Unit FROM StockItems WITH(UPDLOCK,HOLDLOCK) WHERE ItemID=@ItemId AND CompanyId=@CompanyId AND IsActive=1", new { op.ItemId, identity.CompanyId }, tx, cancellationToken: ct));
        decimal before = stock.CurrentStock;
        var van = await c.QuerySingleOrDefaultAsync<decimal?>(new CommandDefinition("SELECT Quantity FROM EngineerVanStock WITH(UPDLOCK,HOLDLOCK) WHERE CompanyId=@CompanyId AND EmployeeId=@EmployeeId AND ItemId=@ItemId", new { identity.CompanyId, identity.EmployeeId, op.ItemId }, tx, cancellationToken: ct));
        if (van is null || van < op.Quantity || before - (decimal)stock.ReservedStock < op.Quantity) throw new MobileHttpException(409, "Not enough available van stock. Your parts entry is saved for office review.");
        await c.ExecuteAsync(new CommandDefinition(@"UPDATE EngineerVanStock SET Quantity=Quantity-@Quantity WHERE CompanyId=@CompanyId AND EmployeeId=@EmployeeId AND ItemId=@ItemId;
UPDATE StockItems SET CurrentStock=CurrentStock-@Quantity,LastUpdated=GETDATE() WHERE CompanyId=@CompanyId AND ItemID=@ItemId;
INSERT StockMovements(CompanyId,ItemID,MovementType,Quantity,StockBefore,StockAfter,FromLocation,ToLocation,ReferenceNo,Notes,CreatedByUserId,CreatedByName,CreatedDate)
VALUES(@CompanyId,@ItemId,'Issue',@Quantity,@before,@after,@van,@job,@reference,'Engineer service consumption',@UserId,@actor,GETDATE());
INSERT JobPartsUsed(JobId,InventoryItemId,ItemDescription,QuantityUsed,Unit,UnitCost,TotalCost,IsFromInventory,StockStatus)
VALUES(@jobId,@ItemId,@PartName,@Quantity,@unit,@rate,@rate*@Quantity,1,'Consumed');", new { identity.CompanyId, identity.EmployeeId, identity.UserId, op.ItemId, op.Quantity, op.PartName, before, after = before - op.Quantity, van = $"Engineer van {identity.EmployeeId}", job = $"Job {jobId}", reference = op.OperationId.ToString(), actor = identity.DisplayName, jobId, unit = (string)stock.Unit, rate = (decimal)stock.LastPurchaseRate }, tx, cancellationToken: ct));
    }
}

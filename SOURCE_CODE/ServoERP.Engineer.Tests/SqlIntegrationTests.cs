using Dapper;
using Microsoft.Data.SqlClient;
using ServoERP.Api.Mobile;
using Xunit;

namespace ServoERP.Engineer.Tests;

public sealed class SqlIntegrationTests
{
    // This test can only use the dedicated LocalDB test instance. It never accepts an office connection.
    [SqlIntegrationFact] public async Task AuthenticationStockIdempotencyConflictsAndCompletionUseRealSqlTransactions()
    {
        string database = "EngineerTests_" + Guid.NewGuid().ToString("N");
        string master = "Server=(localdb)\\ServoEngineerTests;Database=master;Integrated Security=true;Encrypt=false";
        await using (var c = new SqlConnection(master))
        {
            await c.OpenAsync();
            // DDL is deliberately limited to a generated test-only identifier on a test-only LocalDB instance.
            await c.ExecuteAsync($"CREATE DATABASE [{database}]");
        }
        string connection = new SqlConnectionStringBuilder(master) { InitialCatalog = database }.ConnectionString;
        await using var db = new SqlConnection(connection); await db.OpenAsync();
        await db.ExecuteAsync(FixtureSchema);
        await db.ExecuteAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "MobileSchema.sql")));
        // Guarded migration must be repeatable.
        await db.ExecuteAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "MobileSchema.sql")));
        await db.ExecuteAsync("INSERT EngineerVanStock VALUES(1,1,1,4)");
        string password = "Test-Only-Password9";
        await db.ExecuteAsync("UPDATE AppUsers SET PasswordHash=@hash,PasswordSalt='bcrypt'", new { hash = BCrypt.Net.BCrypt.HashPassword(password, 4) });
        var auth = new MobileAuthService(connection);
        var invalid = await Assert.ThrowsAsync<MobileHttpException>(() => auth.Login(new("engineer", "wrong", 1, "test-device"), default)); Assert.Equal(401, invalid.Status);
        var tokens = await auth.Login(new("engineer", password, 1, "test-device"), default);
        var identity = await auth.Authenticate(tokens.AccessToken, default); Assert.Equal(1, identity.EmployeeId);
        var refreshed = await auth.Refresh(new(tokens.RefreshToken), default);
        await Assert.ThrowsAsync<MobileHttpException>(() => auth.Authenticate(tokens.AccessToken, default));
        identity = await auth.Authenticate(refreshed.AccessToken, default);
        var jobs = new MobileJobService(connection); var job = Assert.Single(await jobs.Jobs(identity, default));
        await Assert.ThrowsAsync<MobileHttpException>(() => jobs.AuthorizedJob(identity with { EmployeeId = 2 }, job.PublicId, default));
        var invalidTransition = new MobileOperation { DeviceId = "test", JobPublicId = job.PublicId, Type = OperationType.Transition, OfficeVersion = 1, OfficeFingerprint = job.OfficeFingerprint, TargetState = FieldState.Repair };
        await Assert.ThrowsAsync<FieldRuleException>(() => jobs.Submit(identity, invalidTransition, null, default));
        Guid? prior = null;
        foreach (var state in new[] { FieldState.Accepted, FieldState.Travelling, FieldState.Arrived, FieldState.WorkStarted })
        {
            var transition = new MobileOperation { DeviceId = "test", JobPublicId = job.PublicId, Type = OperationType.Transition, OfficeVersion = 1, OfficeFingerprint = job.OfficeFingerprint, PredecessorId = prior, TargetState = state };
            await jobs.Submit(identity, transition, null, default); prior = transition.OperationId;
        }
        var part = new MobileOperation { DeviceId = "test", JobPublicId = job.PublicId, Type = OperationType.Part, OfficeVersion = 1, OfficeFingerprint = job.OfficeFingerprint, PredecessorId = prior, ItemId = 1, PartName = "CT-440 Contactor", Quantity = 1 };
        var receipts = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => jobs.Submit(identity, Clone(part), null, default)));
        Assert.All(receipts, r => Assert.Equal(receipts[0].Revision, r.Revision));
        Assert.Equal(3m, await db.ExecuteScalarAsync<decimal>("SELECT CurrentStock FROM StockItems WHERE ItemID=1"));
        Assert.Equal(3m, await db.ExecuteScalarAsync<decimal>("SELECT Quantity FROM EngineerVanStock WHERE ItemId=1"));
        Assert.Equal(1, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM JobPartsUsed"));
        var changed = Clone(part); changed.Quantity = 2; await Assert.ThrowsAsync<MobileHttpException>(() => jobs.Submit(identity, changed, null, default));
        var tooMuch = Clone(part); tooMuch.OperationId = Guid.NewGuid(); tooMuch.PredecessorId = part.OperationId; tooMuch.Quantity = 4;
        await Assert.ThrowsAsync<MobileHttpException>(() => jobs.Submit(identity, tooMuch, null, default));
        Assert.Equal(3m, await db.ExecuteScalarAsync<decimal>("SELECT CurrentStock FROM StockItems WHERE ItemID=1"));
        // Legacy writers cannot take stock assigned to vans.
        await Assert.ThrowsAsync<SqlException>(() => db.ExecuteAsync("UPDATE StockItems SET CurrentStock=2 WHERE ItemID=1"));
        var report = new MobileOperation { DeviceId = "test", JobPublicId = job.PublicId, Type = OperationType.Report, OfficeVersion = 1, OfficeFingerprint = job.OfficeFingerprint, PredecessorId = part.OperationId, Diagnosis = "Failed contactor", WorkPerformed = "Replaced", TestResults = "Passed" };
        await jobs.Submit(identity, report, null, default); prior = report.OperationId;
        foreach (var state in new[] { FieldState.Diagnosis, FieldState.Repair, FieldState.Testing, FieldState.AwaitingSignature })
        {
            var transition = new MobileOperation { DeviceId = "test", JobPublicId = job.PublicId, Type = OperationType.Transition, OfficeVersion = 1, OfficeFingerprint = job.OfficeFingerprint, PredecessorId = prior, TargetState = state };
            await jobs.Submit(identity, transition, null, default); prior = transition.OperationId;
        }
        var completion = new MobileOperation { DeviceId = "test", JobPublicId = job.PublicId, Type = OperationType.Complete, OfficeVersion = 1, OfficeFingerprint = job.OfficeFingerprint, PredecessorId = prior };
        await Assert.ThrowsAsync<FieldRuleException>(() => jobs.Submit(identity, completion, null, default));
        var checklist = new MobileOperation { DeviceId = "test", JobPublicId = job.PublicId, Type = OperationType.Checklist, OfficeVersion = 1, OfficeFingerprint = job.OfficeFingerprint, PredecessorId = prior, Checklist = new() { Id = 1, Outcome = "Pass" } };
        var checklistReceipt = await jobs.Submit(identity, checklist, null, default);
        var sign = new MobileOperation { DeviceId = "test", JobPublicId = job.PublicId, Type = OperationType.Signature, OfficeVersion = 1, OfficeFingerprint = job.OfficeFingerprint, PredecessorId = checklist.OperationId, SignOff = new("Test customer", FieldRules.ContentHash(checklistReceipt.Report), [], "Customer unavailable", DateTimeOffset.UtcNow) };
        await jobs.Submit(identity, sign, null, default);
        completion.PredecessorId = sign.OperationId; var completed = await jobs.Submit(identity, completion, null, default); Assert.Equal(FieldState.Completed, completed.Report.State);
        var duplicate = await jobs.Submit(identity, completion, null, default); Assert.Equal(completed.Revision, duplicate.Revision);
        Assert.Equal("Assigned", await db.ExecuteScalarAsync<string>("SELECT PipelineStatus FROM Jobs")); // Office closure is separate.
        await db.ExecuteAsync("UPDATE Jobs SET SyncVersion=2");
        var stale = Clone(report); stale.OperationId = Guid.NewGuid(); await Assert.ThrowsAsync<MobileHttpException>(() => jobs.Submit(identity, stale, null, default));
        await db.ExecuteAsync("UPDATE RolePermissions SET CanEdit=0 WHERE ModuleKey='Inventory'");
        var denied = Clone(part); denied.OperationId = Guid.NewGuid(); denied.OfficeVersion = 2; denied.PredecessorId = completion.OperationId;
        await Assert.ThrowsAsync<MobileHttpException>(() => jobs.Submit(identity with { EmployeeId = 2 }, denied, null, default));
        await auth.Logout(refreshed.AccessToken, default); await Assert.ThrowsAsync<MobileHttpException>(() => auth.Authenticate(refreshed.AccessToken, default));
        var rotation = await auth.Login(new("engineer", password, 1, "test"), default); var next = await auth.Refresh(new(rotation.RefreshToken), default);
        await Assert.ThrowsAsync<MobileHttpException>(() => auth.Refresh(new(rotation.RefreshToken), default)); await Assert.ThrowsAsync<MobileHttpException>(() => auth.Authenticate(next.AccessToken, default));
        // Retain the isolated fixture for inspection. No production database or destructive cleanup is used.
    }
    private static MobileOperation Clone(MobileOperation op) => System.Text.Json.JsonSerializer.Deserialize<MobileOperation>(System.Text.Json.JsonSerializer.Serialize(op))!;
    private const string FixtureSchema = """
CREATE TABLE Companies(CompanyId int PRIMARY KEY,IsActive bit); INSERT Companies VALUES(1,1);
CREATE TABLE Employees(EmployeeID int PRIMARY KEY,CompanyId int); INSERT Employees VALUES(1,1),(2,1);
CREATE TABLE AppUsers(UserId int PRIMARY KEY,Username nvarchar(50),Email nvarchar(255),DisplayName nvarchar(100),EmployeeID int,RoleId int,PasswordHash nvarchar(256),PasswordSalt nvarchar(64),IsActive bit,ForcePasswordChange bit,FailedAttempts int,LockoutUntil datetime,LastLoginDate datetime);
CREATE TABLE LoginAudit(AuditId int IDENTITY PRIMARY KEY,UserId int NULL,Username nvarchar(255),Success bit,FailureReason nvarchar(200),DeviceName nvarchar(128));
INSERT AppUsers VALUES(1,'engineer',NULL,'Test Engineer',1,1,'','bcrypt',1,0,0,NULL,NULL);
CREATE TABLE UserCompanies(UserId int,CompanyId int,IsActive bit,RoleId int); INSERT UserCompanies VALUES(1,1,1,1);
CREATE TABLE RolePermissions(RoleId int,ModuleKey nvarchar(50),CanView bit,CanEdit bit); INSERT RolePermissions VALUES(1,'WorkOrders',1,1),(1,'Inventory',1,1);
CREATE TABLE B2BClients(ClientID int PRIMARY KEY,CompanyId int,CompanyName nvarchar(100),PrimaryContact nvarchar(100),Phone nvarchar(20)); INSERT B2BClients VALUES(1,1,'Test Customer','Customer','9999999999');
CREATE TABLE ClientSites(SiteID int PRIMARY KEY,CompanyId int,SiteName nvarchar(100),Address nvarchar(255)); INSERT ClientSites VALUES(1,1,'Test Site','Test Address');
CREATE TABLE Jobs(JobID int PRIMARY KEY,CompanyId int,SyncPublicId uniqueidentifier,SyncVersion bigint,AssignedEmployeeID int,DeletedUtc datetime2,JobNumber nvarchar(50),Priority nvarchar(50),ClientID int,SiteID int,Description nvarchar(1000),ScheduledDate datetime,PipelineStatus nvarchar(50),Status nvarchar(50),ModifiedDate datetime NULL,Notes nvarchar(max) NULL);
INSERT Jobs VALUES(1,1,NEWID(),1,1,NULL,'SRV-TEST','Urgent',1,1,'Cooling failure',GETDATE(),'Assigned','Pending',NULL,NULL);
CREATE TABLE StockItems(ItemID int PRIMARY KEY,CompanyId int,ItemName nvarchar(255),CurrentStock decimal(10,2),ReservedStock decimal(10,2),LastPurchaseRate decimal(12,2),Unit nvarchar(50),IsActive bit,LastUpdated datetime);
INSERT StockItems VALUES(1,1,'CT-440 Contactor',4,0,100,'Nos',1,GETDATE());
CREATE TABLE StockMovements(MovementID int IDENTITY PRIMARY KEY,CompanyId int,ItemID int,MovementType nvarchar(30),Quantity decimal(10,2),StockBefore decimal(10,2),StockAfter decimal(10,2),FromLocation nvarchar(120),ToLocation nvarchar(120),ReferenceNo nvarchar(80),Notes nvarchar(max),CreatedByUserId int,CreatedByName nvarchar(120),CreatedDate datetime);
CREATE TABLE JobPartsUsed(PartUsedId int IDENTITY PRIMARY KEY,JobId int,InventoryItemId int,ItemDescription nvarchar(255),QuantityUsed decimal(10,3),Unit nvarchar(30),UnitCost decimal(18,2),TotalCost decimal(18,2),IsFromInventory bit,StockStatus nvarchar(20));
CREATE TABLE JobChecklistItems(ChecklistItemId int PRIMARY KEY,JobId int,ItemText nvarchar(200),IsCompleted bit,CompletedBy nvarchar(100),CompletedDate datetime,SortOrder int); INSERT JobChecklistItems VALUES(1,1,'Voltage checked',0,NULL,NULL,1);
CREATE TABLE JobActivityLog(ActivityId int IDENTITY PRIMARY KEY,JobId int,ActivityText nvarchar(500),PerformedBy nvarchar(100),ActivityDate datetime,ActivityType nvarchar(20));
CREATE TABLE AuditLog(LogId int IDENTITY PRIMARY KEY,UserId int,Username nvarchar(50),Action nvarchar(100),ModuleKey nvarchar(50),RecordId int,Description nvarchar(500));
""";
}

public sealed class SqlIntegrationFactAttribute : FactAttribute
{
    public SqlIntegrationFactAttribute()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft SQL Server");
        if (Environment.GetEnvironmentVariable("SERVOERP_RUN_SQL_TESTS") != "1" || !new[] { "160", "170", "150" }.Any(version => File.Exists(Path.Combine(root, version, "Tools", "Binn", "SqlLocalDB.exe"))))
            Skip = "Requires the isolated ServoEngineerTests LocalDB instance. Office/production SQL is deliberately never used.";
    }
}

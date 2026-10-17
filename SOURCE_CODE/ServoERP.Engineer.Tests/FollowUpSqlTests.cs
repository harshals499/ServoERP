using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using ServoERP.Api.Mobile;
using Xunit;

namespace ServoERP.Engineer.Tests;

public sealed class FollowUpSqlTests
{
    [SqlIntegrationFact]
    public async Task RealOutboxAndSqlReceiptsPreserveFindingsReturnsAndRollback()
    {
        var connection = await SqlTestHost.Create();
        await using var db = new SqlConnection(connection); await db.OpenAsync();
        await db.ExecuteAsync(SqlIntegrationTests.FixtureSchema);
        var migration = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "MobileSchema.sql"));
        await db.ExecuteAsync(migration); await db.ExecuteAsync(migration);
        var identity = new EngineerIdentity(1, 1, 1, 1, "Test engineer");
        var service = new MobileJobService(connection);
        var snapshot = Assert.Single(await service.Jobs(identity, default));
        snapshot.Report.State = FieldState.WorkStarted; snapshot.Report.Checklist[0].Outcome = "Fail";
        await db.ExecuteAsync("INSERT EngineerReports(JobId,CompanyId,Revision,ReportJson,UpdatedUtc) VALUES(1,1,0,@json,SYSUTCDATETIME()); UPDATE JobChecklistItems SET EngineerOutcome='Fail' WHERE ChecklistItemId=1",
            new { json = System.Text.Json.JsonSerializer.Serialize(snapshot.Report) });
        snapshot = Assert.Single(await service.Jobs(identity, default));
        var staleSnapshot = FollowUpTests.Copy(snapshot);
        var file = Path.Combine(Path.GetTempPath(), "ServoPriority1-Sql-" + Guid.NewGuid().ToString("N"), "workspace.db3");
        var add = FollowUpTests.Finding(snapshot.PublicId); var handoff = FollowUpTests.Handoff(snapshot.PublicId, add.Finding!.Id);
        using (var local = new LocalWorkspace(file))
        {
            local.MergeSnapshots([snapshot]); local.Enqueue(add, 1); local.Enqueue(handoff, 1);
            var transport = new SqlTransport(service, identity); var sync = new SyncService(local, transport);
            await sync.Run(); // The finding committed, but its response was lost.
            Assert.Equal(QueueState.Pending, local.Queue()[0].State);
            var queuedFinding = System.Text.Json.JsonSerializer.Deserialize<MobileOperation>(local.Queue()[0].PayloadJson)!;
            var duplicates = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => service.Submit(identity, FollowUpTests.Copy(queuedFinding), null, default)));
            Assert.All(duplicates, x => Assert.Equal(1, x.Revision));
            local.Retry(add.OperationId); await sync.Run(); // Its receipt recovers; handoff commits and loses its response.
            Assert.Equal(QueueState.Synced, local.Queue()[0].State); Assert.Equal(QueueState.Pending, local.Queue()[1].State);
        }
        using (var local = new LocalWorkspace(file))
        {
            Assert.Single(local.Job(snapshot.PublicId).Report.ReturnVisits);
            local.Retry(handoff.OperationId); await new SyncService(local, new SqlTransport(service, identity) { LoseResponses = false }).Run();
            Assert.All(local.Queue(), q => Assert.Equal(QueueState.Synced, q.State));
            var saved = Assert.Single(await service.Jobs(identity, default)).Report;
            Assert.Single(saved.Findings); Assert.Single(saved.Handoffs); Assert.Single(saved.ReturnVisits);
            Assert.Equal("Fail", saved.Checklist[0].Outcome);
            Assert.Equal(2, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM EngineerReceipts"));
            Assert.Equal(2, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM EngineerAudit"));
            Assert.Equal(1, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM JobActivityLog WHERE ActivityText LIKE 'Engineer handoff:%'"));
            Assert.Equal("Assigned", await db.ExecuteScalarAsync<string>("SELECT PipelineStatus FROM Jobs"));
            Assert.Equal(DateTime.Today, (await db.ExecuteScalarAsync<DateTime>("SELECT ScheduledDate FROM Jobs")).Date);
            var office = await service.OfficeReport(1, 1, 1, default); Assert.NotNull(office!.ActiveHandoff); Assert.Single(office.Findings);

            // The same ID with a different payload must never reuse the committed receipt.
            var changed = System.Text.Json.JsonSerializer.Deserialize<MobileOperation>(local.Queue()[1].PayloadJson)!;
            changed.Handoff!.Detail = "Altered payload";
            var mismatch = await Assert.ThrowsAsync<MobileHttpException>(() => service.Submit(identity, changed, null, default)); Assert.Equal(409, mismatch.Status);
            Assert.Empty(await service.Jobs(identity with { CompanyId = 2 }, default));
            await Assert.ThrowsAsync<BadHttpRequestException>(() => service.OfficeReport(1, 2, 1, default));

            // Force an error AFTER report, activity and audit writes, when the receipt is inserted.
            // Test-only DDL: the generated EngineerTests_* database is the sole target.
            await db.ExecuteAsync("CREATE TRIGGER TR_TestReceiptFailure ON EngineerReceipts AFTER INSERT AS THROW 51090, 'Test rollback after projections', 1;");
            var resolution = new MobileOperation { JobPublicId = snapshot.PublicId, DeviceId = "test", Type = OperationType.ResolveFinding,
                BaseRevision = saved.Revision, OfficeVersion = snapshot.OfficeVersion, OfficeFingerprint = snapshot.OfficeFingerprint,
                Resolution = new(add.Finding.Id, "Replaced contactor") };
            await Assert.ThrowsAsync<SqlException>(() => service.Submit(identity, resolution, null, default));
            Assert.Null((await service.OfficeReport(1, 1, 1, default))!.Findings[0].ResolvedUtc);
            Assert.Equal(2, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM EngineerAudit"));
            Assert.Equal(2, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM EngineerReceipts"));
            Assert.Equal(0, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM JobActivityLog WHERE ActivityText LIKE 'Finding % resolved:%'"));
            await db.ExecuteAsync("DISABLE TRIGGER TR_TestReceiptFailure ON EngineerReceipts");

            local.Enqueue(new() { JobPublicId = snapshot.PublicId, Type = OperationType.ResumeVisit, DeviceId = "test",
                Resume = new(handoff.Handoff!.Id, "Dispatch confirmed parts and return schedule") }, 1);
            await new SyncService(local, new SqlTransport(service, identity) { LoseResponses = false }).Run();
            Assert.Null(local.Job(snapshot.PublicId).Report.ActiveHandoff); Assert.Single(local.Job(snapshot.PublicId).Report.ReturnVisits);
            Assert.Equal("Fail", local.Job(snapshot.PublicId).Report.Checklist[0].Outcome);
        }
        // A second device's stale report must be retained as a conflict, not overwrite the handoff/resume.
        using var staleLocal = new LocalWorkspace(Path.Combine(Path.GetTempPath(), "ServoPriority1-Stale-" + Guid.NewGuid().ToString("N"), "workspace.db3"));
        staleLocal.MergeSnapshots([staleSnapshot]); staleLocal.Enqueue(FollowUpTests.Finding(snapshot.PublicId), 1);
        await new SyncService(staleLocal, new SqlTransport(service, identity) { LoseResponses = false }).Run();
        Assert.Equal(QueueState.Conflict, staleLocal.Queue()[0].State);
        Assert.Single(staleLocal.Job(snapshot.PublicId).Report.Findings);
        Assert.Single((await service.OfficeReport(1, 1, 1, default))!.Findings);
        // Reassignment rejects pending work and does not write a receipt.
        await db.ExecuteAsync("UPDATE Jobs SET AssignedEmployeeID=2,SyncVersion=SyncVersion+1");
        var rejected = FollowUpTests.Finding(snapshot.PublicId);
        var denied = await Assert.ThrowsAsync<MobileHttpException>(() => service.Submit(identity, rejected, null, default)); Assert.Equal(403, denied.Status);
        Assert.Equal(3, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM EngineerReceipts"));
    }
    private sealed class SqlTransport(MobileJobService service, EngineerIdentity identity) : IEngineerTransport
    {
        public bool LoseResponses = true;
        private readonly HashSet<Guid> _lostResponses = [];
        public Task<List<JobSnapshot>> Jobs(CancellationToken ct) => service.Jobs(identity, ct);
        public Task<List<PartStock>> Stock(CancellationToken ct) => service.Stock(identity, ct);
        public Task<UploadReceipt> Upload(Guid job, Guid operation, string path, CancellationToken ct) => throw new NotSupportedException();
        public async Task<OperationReceipt> Submit(MobileOperation op, UploadReceipt? upload, CancellationToken ct)
        {
            try
            {
                var receipt = await service.Submit(identity, op, upload, ct);
                if (LoseResponses && _lostResponses.Add(op.OperationId)) throw new HttpRequestException("Simulated response loss after SQL commit");
                return receipt;
            }
            catch (MobileHttpException ex) { throw new TransportException((System.Net.HttpStatusCode)ex.Status, ex.Message); }
        }
    }
}

using System.Net;
using System.Text.Json;
using ServoERP.Engineer;
using Xunit;

namespace ServoERP.Engineer.Tests;

public sealed class OfflineTests
{
    private static JobSnapshot Sample() => new() { PublicId = Guid.NewGuid(), Number = "SRV-TEST", OfficeVersion = 4, Report = new() { State = FieldState.WorkStarted, Checklist = [new() { Id = 1, Text = "Voltage" }] } };
    private static MobileOperation Report(Guid id) => new() { JobPublicId = id, DeviceId = "test", Type = OperationType.Report, Diagnosis = "Failed contactor", WorkPerformed = "Replaced", TestResults = "Cooling verified" };
    [Fact] public void InvalidTransitionsAndPrematureCompletionFail()
    {
        var job = Sample();
        Assert.Throws<FieldRuleException>(() => FieldRules.Apply(job.Report, new() { JobPublicId = job.PublicId, DeviceId = "test", Type = OperationType.Transition, TargetState = FieldState.Arrived }, 1));
        Assert.Throws<FieldRuleException>(() => FieldRules.Apply(job.Report, new() { JobPublicId = job.PublicId, DeviceId = "test", Type = OperationType.Complete }, 1));
    }
    [Fact] public void SignOffBindsToReportAndEditingInvalidatesIt()
    {
        var job = Sample(); FieldRules.Apply(job.Report, Report(job.PublicId), 1); job.Report.State = FieldState.AwaitingSignature; job.Report.Checklist[0].Outcome = "Pass"; job.Report.PartsReviewed = true;
        var op = new MobileOperation { JobPublicId = job.PublicId, DeviceId = "test", Type = OperationType.Signature, SignOff = new("Customer", FieldRules.ContentHash(job.Report), [], "Unavailable", DateTimeOffset.UtcNow) };
        FieldRules.Apply(job.Report, op, 1); Assert.Empty(FieldRules.CompletionIssues(job.Report));
        FieldRules.Apply(job.Report, Report(job.PublicId), 1); Assert.Null(job.Report.SignOff); Assert.NotEmpty(FieldRules.CompletionIssues(job.Report));
        var other = Sample(); other.Report.State = FieldState.AwaitingSignature;
        Assert.Throws<FieldRuleException>(() => FieldRules.Apply(other.Report, op.withOperation(other.PublicId), 1));
    }
    [Fact] public void RequiredChecklistAndPhotoAreEnforced()
    {
        var job = Sample(); FieldRules.Apply(job.Report, Report(job.PublicId), 1); job.Report.RequiredPhotoCategories.Add("After"); job.Report.Checklist[0].Outcome = "Fail";
        var issues = FieldRules.CompletionIssues(job.Report); Assert.Contains(issues, s => s.Contains("checklist")); Assert.Contains(issues, s => s.Contains("After"));
        Assert.Contains(issues, s => s.Contains("parts"));
    }
    [Fact] public void RestartPreservesDraftOperationsAndPhotoPath()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ServoEngineerTest-" + Guid.NewGuid()); var path = Path.Combine(dir, "local.db3"); var job = Sample();
        using (var local = new LocalWorkspace(path))
        {
            local.MergeSnapshots([job]); local.Enqueue(Report(job.PublicId), 1);
            var photo = new MobileOperation { DeviceId = "test", JobPublicId = job.PublicId, Type = OperationType.Photo, Photo = new(Guid.NewGuid(), "Before", new string('A', 64), DateTimeOffset.UtcNow) };
            var row = local.Enqueue(photo, 1, "private/photo.jpg"); row.State = QueueState.Syncing; local.Update(row);
            local.Enqueue(new() { DeviceId = "test", JobPublicId = job.PublicId, Type = OperationType.Checklist, Checklist = new() { Id = 1, Outcome = "Pass" } }, 1);
        }
        using (var local = new LocalWorkspace(path))
        {
            Assert.Equal("Failed contactor", local.Job(job.PublicId).Report.Diagnosis); Assert.Equal("Pass", local.Job(job.PublicId).Report.Checklist[0].Outcome);
            Assert.Equal(3, local.Queue().Count); Assert.All(local.Queue(), q => Assert.Equal(QueueState.Pending, q.State)); Assert.Equal("private/photo.jpg", local.Queue()[1].LocalPhotoPath);
        }
        Directory.Delete(dir, true);
    }
    [Fact] public async Task ConflictPreservesDraftAndStopsDependentCommands()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ServoEngineerTest-" + Guid.NewGuid()); var job = Sample();
        using (var local = new LocalWorkspace(Path.Combine(dir, "local.db3")))
        {
            local.MergeSnapshots([job]); local.Enqueue(Report(job.PublicId), 1); local.Enqueue(Report(job.PublicId), 1);
            var transport = new Fake(job) { Failure = HttpStatusCode.Conflict }; await new SyncService(local, transport).Run();
            Assert.Equal(1, transport.Submissions); Assert.Equal(QueueState.Conflict, local.Queue()[0].State); Assert.Equal(QueueState.Pending, local.Queue()[1].State); Assert.Equal("Failed contactor", local.Job(job.PublicId).Report.Diagnosis);
        }
        Directory.Delete(dir, true);
    }
    [Fact] public async Task TemporaryFailureRetriesSameIdAndAcknowledgesSafely()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ServoEngineerTest-" + Guid.NewGuid()); var job = Sample();
        using (var local = new LocalWorkspace(Path.Combine(dir, "local.db3")))
        {
            local.MergeSnapshots([job]); var op = Report(job.PublicId); local.Enqueue(op, 1);
            var transport = new Fake(job) { Failure = HttpStatusCode.ServiceUnavailable }; var sync = new SyncService(local, transport); await sync.Run();
            Assert.Equal(QueueState.Pending, local.Queue()[0].State); Assert.Equal(1, local.Queue()[0].RetryCount);
            local.Retry(op.OperationId); transport.Failure = null; await sync.Run(); Assert.Equal(QueueState.Synced, local.Queue()[0].State); Assert.All(transport.Ids, id => Assert.Equal(op.OperationId, id));
        }
        Directory.Delete(dir, true);
    }
    private sealed class Fake(JobSnapshot job) : IEngineerTransport
    {
        public HttpStatusCode? Failure; public int Submissions; public List<Guid> Ids = [];
        public Task<List<JobSnapshot>> Jobs(CancellationToken ct) => Task.FromResult(new List<JobSnapshot> { job });
        public Task<List<PartStock>> Stock(CancellationToken ct) => Task.FromResult(new List<PartStock>());
        public Task<UploadReceipt> Upload(Guid id, Guid operation, string path, CancellationToken ct) => Task.FromResult(new UploadReceipt(operation, new string('A', 64)));
        public Task<OperationReceipt> Submit(MobileOperation op, UploadReceipt? upload, CancellationToken ct)
        {
            Submissions++; Ids.Add(op.OperationId); if (Failure.HasValue) throw new TransportException(Failure.Value, "Office conflict");
            FieldRules.Apply(job.Report, op, 1); return Task.FromResult(new OperationReceipt(op.OperationId, job.Report.Revision, job.Report));
        }
    }
}
internal static class TestOperationExtensions
{
    public static MobileOperation withOperation(this MobileOperation op, Guid job) => new() { JobPublicId = job, DeviceId = op.DeviceId, Type = op.Type, SignOff = op.SignOff };
}

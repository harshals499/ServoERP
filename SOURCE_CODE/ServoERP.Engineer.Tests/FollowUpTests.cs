using System.Net;
using System.Text.Json;
using ServoERP.Engineer;
using Xunit;

namespace ServoERP.Engineer.Tests;

public sealed class FollowUpTests
{
    internal static JobSnapshot Job() => new() { PublicId = Guid.NewGuid(), OfficeVersion = 1, Number = "FIELD-TEST", OfficeFingerprint = "office-v1",
        Report = new() { State = FieldState.WorkStarted, Checklist = [new() { Id = 1, Text = "Cooling test", Outcome = "Fail" }] } };
    internal static MobileOperation Finding(Guid job) => new() { JobPublicId = job, DeviceId = "test-device", Type = OperationType.AddFinding,
        Finding = new() { Id = Guid.NewGuid(), Title = "Cooling not restored", Detail = "Contactor failed", NextAction = "Dispatch replacement contactor", ChecklistItemId = 1 } };
    internal static MobileOperation Handoff(Guid job, Guid finding) => new() { JobPublicId = job, DeviceId = "test-device", Type = OperationType.RecordException,
        Handoff = new() { Id = Guid.NewGuid(), Reason = VisitExceptionReason.ReturnVisitRequired, Detail = "Replacement unavailable", NextAction = "Office to confirm return appointment",
            RequestedReturnDate = DateTime.UtcNow.Date.AddDays(1), FindingIds = [finding] } };
    internal static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    [Fact] public void HandoffRetainsFailedCheckAndResumeDoesNotResolveFindingOrCloseJob()
    {
        var job = Job(); var add = Finding(job.PublicId); FieldRules.Apply(job.Report, add, 12);
        var handoff = Handoff(job.PublicId, add.Finding!.Id); FieldRules.Apply(job.Report, handoff, 12);
        Assert.Equal("Fail", job.Report.Checklist[0].Outcome);
        Assert.Equal(FieldState.WorkStarted, job.Report.State);
        Assert.Equal(12, Assert.Single(job.Report.Findings).CreatedBy);
        Assert.Single(job.Report.ReturnVisits); Assert.NotNull(job.Report.ActiveHandoff);
        Assert.Throws<FieldRuleException>(() => FieldRules.Apply(job.Report, new() { DeviceId = "test-device", JobPublicId = job.PublicId,
            Type = OperationType.Transition, TargetState = FieldState.Diagnosis }, 12)); // Includes legacy clients.
        FieldRules.Apply(job.Report, new() { DeviceId = "test-device", JobPublicId = job.PublicId, Type = OperationType.ResumeVisit, Resume = new(handoff.Handoff!.Id, "Dispatch confirmed the replacement and schedule") }, 12);
        Assert.Null(job.Report.ActiveHandoff); Assert.NotNull(job.Report.ReturnVisits[0].ResumedUtc);
        Assert.Null(job.Report.Findings[0].ResolvedUtc); Assert.Equal("Fail", job.Report.Checklist[0].Outcome);
        Assert.Contains(FieldRules.CompletionIssues(job.Report), i => i.Contains("findings"));
    }
    [Fact] public void FailedRequiredChecksNeedLinkedFindingsBeforeHandoff()
    {
        var job = Job(); var op = Handoff(job.PublicId, Guid.NewGuid());
        Assert.Throws<FieldRuleException>(() => FieldRules.Apply(job.Report, op, 1));
        op.Handoff!.FindingIds.Clear();
        Assert.Throws<FieldRuleException>(() => FieldRules.Apply(job.Report, op, 1));
        Assert.Empty(job.Report.Handoffs); Assert.Empty(job.Report.ReturnVisits); Assert.Equal(0, job.Report.Revision);
    }
    [Theory]
    [InlineData(VisitExceptionReason.AwaitingParts)]
    [InlineData(VisitExceptionReason.AwaitingApproval)]
    [InlineData(VisitExceptionReason.CustomerUnavailable)]
    public void AcceptedVisitCanBeHandedOffBeforeWorkStarts(VisitExceptionReason reason)
    {
        var job = Job(); job.Report.State = FieldState.Accepted; job.Report.Checklist[0].Outcome = "";
        var op = Handoff(job.PublicId, Guid.NewGuid()); op.Handoff!.Reason = reason; op.Handoff.RequestedReturnDate = null; op.Handoff.FindingIds.Clear();
        FieldRules.Apply(job.Report, op, 1); Assert.NotNull(job.Report.ActiveHandoff); Assert.Empty(job.Report.ReturnVisits);
    }
    [Fact] public void AllReportChangesInvalidateAcknowledgementAndLegacyHashIsStable()
    {
        var job = Job(); var r = job.Report; r.JobPublicId = job.PublicId;
        var legacy = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            new { r.JobPublicId, r.Diagnosis, r.WorkPerformed, r.TestResults, r.Notes, r.Checklist, r.Parts, r.PartsReviewed, r.Photos }))));
        Assert.Equal(legacy, FieldRules.ContentHash(r));
        r.SignOff = new("Customer", legacy, [], "Customer unavailable", DateTimeOffset.UtcNow);
        var add = Finding(job.PublicId); FieldRules.Apply(r, add, 1); Assert.Null(r.SignOff); Assert.NotEqual(legacy, FieldRules.ContentHash(r));
        r.SignOff = new("Customer", FieldRules.ContentHash(r), [], "Unavailable", DateTimeOffset.UtcNow);
        FieldRules.Apply(r, new() { JobPublicId = job.PublicId, DeviceId = "test-device", Type = OperationType.ResolveFinding, Resolution = new(add.Finding!.Id, "Contactor replaced and tested") }, 1);
        Assert.Null(r.SignOff); Assert.Equal("Fail", r.Checklist[0].Outcome); // Resolution is not a fabricated test pass.
    }
    [Fact] public void DuplicateIdsForeignLinksAndInvalidReturnsFailWithoutMutation()
    {
        var job = Job(); var add = Finding(job.PublicId); FieldRules.Apply(job.Report, add, 1);
        Assert.Throws<FieldRuleException>(() => FieldRules.Apply(job.Report, add, 1));
        var invalid = Finding(job.PublicId); invalid.Finding!.ChecklistItemId = 999;
        Assert.Throws<FieldRuleException>(() => FieldRules.Apply(job.Report, invalid, 1));
        var handoff = Handoff(job.PublicId, add.Finding!.Id); handoff.Handoff!.RequestedReturnDate = DateTime.UtcNow.AddYears(-2);
        Assert.Throws<FieldRuleException>(() => FieldRules.Apply(job.Report, handoff, 1));
        Assert.Single(job.Report.Findings); Assert.Empty(job.Report.Handoffs);
        Assert.Throws<FieldRuleException>(() => FieldRules.Apply(job.Report, new() { JobPublicId = job.PublicId, DeviceId = "test-device", Type = OperationType.ResolveFinding, Resolution = new(Guid.NewGuid(), "Foreign finding") }, 1));
    }
    [Fact] public void SuccessfulCompletionRequiresRealResolutionAndKeepsReturnHistory()
    {
        var job = Job(); var add = Finding(job.PublicId); FieldRules.Apply(job.Report, add, 1);
        var handoff = Handoff(job.PublicId, add.Finding!.Id); FieldRules.Apply(job.Report, handoff, 1);
        FieldRules.Apply(job.Report, new() { JobPublicId = job.PublicId, DeviceId = "test-device", Type = OperationType.ResumeVisit, Resume = new(handoff.Handoff!.Id, "Office instructed return") }, 1);
        FieldRules.Apply(job.Report, new() { JobPublicId = job.PublicId, DeviceId = "test-device", Type = OperationType.ResolveFinding, Resolution = new(add.Finding.Id, "Part replaced; cooling test passed") }, 1);
        var r = job.Report; r.State = FieldState.AwaitingSignature; r.Checklist[0].Outcome = "Pass"; r.PartsReviewed = true; r.Diagnosis = "Contactor"; r.WorkPerformed = "Replaced"; r.TestResults = "Cooling verified";
        FieldRules.Apply(r, new() { JobPublicId = job.PublicId, DeviceId = "test-device", Type = OperationType.Signature, SignOff = new("Customer", FieldRules.ContentHash(r), [], "Customer unavailable", DateTimeOffset.UtcNow) }, 1);
        FieldRules.Apply(r, new() { JobPublicId = job.PublicId, DeviceId = "test-device", Type = OperationType.Complete }, 1);
        Assert.Equal(FieldState.Completed, r.State); Assert.NotNull(r.ReturnVisits[0].CompletedUtc); Assert.Single(r.Handoffs);
        Assert.Equal(r.SignOff!.ReportHash, FieldRules.ContentHash(r));
    }
    [Fact] public async Task LostAcknowledgementRestartAndRetriesDoNotDuplicateFindingHandoffOrReturn()
    {
        var job = Job(); var server = new ReceiptServer(Copy(job)); var dir = Path.Combine(Path.GetTempPath(), "ServoPriority1-" + Guid.NewGuid().ToString("N"));
        var file = Path.Combine(dir, "workspace.db3"); var add = Finding(job.PublicId); var handoff = Handoff(job.PublicId, add.Finding!.Id);
        using (var local = new LocalWorkspace(file))
        {
            local.MergeSnapshots([job]); local.Enqueue(add, 1); local.Enqueue(handoff, 1); server.LoseNextAcknowledgement = true;
            await new SyncService(local, server).Run();
            Assert.Single(server.Job.Report.Findings); Assert.Empty(server.Job.Report.Handoffs);
            Assert.Equal(QueueState.Pending, local.Queue()[0].State); Assert.NotNull(local.Job(job.PublicId).Report.ActiveHandoff);
        }
        using (var local = new LocalWorkspace(file))
        {
            Assert.Single(local.Job(job.PublicId).Report.ReturnVisits); local.Retry(add.OperationId);
            await new SyncService(local, server).Run();
            Assert.All(local.Queue(), p => Assert.Equal(QueueState.Synced, p.State));
            Assert.Single(server.Job.Report.Findings); Assert.Single(server.Job.Report.Handoffs); Assert.Single(server.Job.Report.ReturnVisits);
            Assert.Equal(2, server.Receipts.Count); Assert.Equal(3, server.Submissions);
            Assert.Equal("Fail", local.Job(job.PublicId).Report.Checklist[0].Outcome);
        }
    }
    [Fact] public async Task ConflictRetainsHandoffAndStopsDependantsButOtherJobsCanSync()
    {
        var job = Job(); var other = Job(); var server = new ReceiptServer(Copy(job)) { RejectJob = job.PublicId };
        var file = Path.Combine(Path.GetTempPath(), "ServoPriority1-" + Guid.NewGuid().ToString("N"), "workspace.db3");
        using var local = new LocalWorkspace(file); local.MergeSnapshots([job, other]);
        var add = Finding(job.PublicId); local.Enqueue(add, 1); local.Enqueue(Handoff(job.PublicId, add.Finding!.Id), 1);
        local.Enqueue(Finding(other.PublicId), 1); server.OtherJob = Copy(other);
        await new SyncService(local, server).Run();
        Assert.Equal(QueueState.Conflict, local.Queue()[0].State); Assert.Equal(QueueState.Pending, local.Queue()[1].State); Assert.Equal(QueueState.Synced, local.Queue()[2].State);
        Assert.NotNull(local.Job(job.PublicId).Report.ActiveHandoff);
        Assert.Throws<FieldRuleException>(() => local.Retry(add.OperationId));
    }
    [Fact] public async Task MismatchedReceiptCannotReplaceSavedDraft()
    {
        var job = Job(); var file = Path.Combine(Path.GetTempPath(), "ServoPriority1-" + Guid.NewGuid().ToString("N"), "workspace.db3");
        using var local = new LocalWorkspace(file); local.MergeSnapshots([job]); var row = local.Enqueue(Finding(job.PublicId), 1);
        var transport = new ReceiptServer(Copy(job)) { WrongReceipt = true };
        await new SyncService(local, transport).Run();
        Assert.Equal(QueueState.Conflict, local.Queue()[0].State); Assert.Single(local.Job(job.PublicId).Report.Findings);
    }
    [Fact] public async Task InterruptedPhotoUploadAndLostSubmissionReceiptRecoverBeforeHandoff()
    {
        var job = Job(); var server = new ReceiptServer(Copy(job)) { FailNextUpload = true };
        var file = Path.Combine(Path.GetTempPath(), "ServoPriority1-Media-" + Guid.NewGuid().ToString("N"), "workspace.db3");
        var add = Finding(job.PublicId); var photo = new MobileOperation { JobPublicId = job.PublicId, DeviceId = "test-device",
            Type = OperationType.Photo, Photo = new(Guid.NewGuid(), "Before", new string('A', 64), DateTimeOffset.UtcNow) };
        photo.Photo = photo.Photo with { AttachmentId = photo.OperationId };
        using (var local = new LocalWorkspace(file))
        {
            local.MergeSnapshots([job]); local.Enqueue(add, 1); local.Enqueue(photo, 1, "private/test-photo.jpg"); local.Enqueue(Handoff(job.PublicId, add.Finding!.Id), 1);
            var sync = new SyncService(local, server); await sync.Run();
            Assert.Equal(QueueState.Pending, local.Queue()[1].State); Assert.Empty(server.Job.Report.Photos); Assert.Empty(server.Job.Report.Handoffs);
            local.Retry(photo.OperationId); server.LoseNextAcknowledgement = true; await sync.Run();
            Assert.Single(server.Job.Report.Photos); Assert.NotEmpty(local.Queue()[1].UploadJson); Assert.Empty(server.Job.Report.Handoffs);
        }
        using (var local = new LocalWorkspace(file))
        {
            local.Retry(photo.OperationId); await new SyncService(local, server).Run();
            Assert.All(local.Queue(), q => Assert.Equal(QueueState.Synced, q.State));
            Assert.Equal(2, server.Uploads); Assert.Single(server.Job.Report.Photos); Assert.Single(server.Job.Report.Handoffs);
        }
    }
    private sealed class ReceiptServer(JobSnapshot job) : IEngineerTransport
    {
        public JobSnapshot Job = job; public JobSnapshot? OtherJob; public Guid? RejectJob; public bool LoseNextAcknowledgement, WrongReceipt;
        public int Submissions, Uploads; public bool FailNextUpload; public Dictionary<Guid, OperationReceipt> Receipts = [];
        public Task<List<JobSnapshot>> Jobs(CancellationToken ct) => Task.FromResult(OtherJob is null ? new List<JobSnapshot> { Copy(Job) } : [Copy(Job), Copy(OtherJob)]);
        public Task<List<PartStock>> Stock(CancellationToken ct) => Task.FromResult(new List<PartStock>());
        public Task<UploadReceipt> Upload(Guid id, Guid operation, string path, CancellationToken ct)
        {
            Uploads++;
            if (FailNextUpload) { FailNextUpload = false; throw new IOException("Upload interrupted"); }
            return Task.FromResult(new UploadReceipt(operation, new string('A', 64)));
        }
        public Task<OperationReceipt> Submit(MobileOperation op, UploadReceipt? upload, CancellationToken ct)
        {
            Submissions++;
            if (RejectJob == op.JobPublicId) throw new TransportException(HttpStatusCode.Conflict, "Reassigned or changed by office");
            if (Receipts.TryGetValue(op.OperationId, out var existing)) return Task.FromResult(Copy(existing));
            var target = op.JobPublicId == Job.PublicId ? Job : OtherJob!;
            FieldRules.Apply(target.Report, op, 1);
            var receipt = new OperationReceipt(op.OperationId, target.Report.Revision, Copy(target.Report)); Receipts.Add(op.OperationId, receipt);
            if (LoseNextAcknowledgement) { LoseNextAcknowledgement = false; throw new HttpRequestException("Connection lost after commit"); }
            return Task.FromResult(WrongReceipt ? receipt with { OperationId = Guid.NewGuid() } : Copy(receipt));
        }
    }
}

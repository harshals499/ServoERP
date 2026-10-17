using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ServoERP.Engineer;

public enum FieldState { Assigned, Accepted, Travelling, Arrived, WorkStarted, Diagnosis, Repair, Testing, AwaitingSignature, Completed }
public enum OperationType { Transition, Report, Checklist, Part, Photo, Signature, Complete, PartsReviewed, AddFinding, ResolveFinding, RecordException, ResumeVisit }
public enum QueueState { Pending, Syncing, Synced, Failed, Conflict }
public record LoginRequest(string Username, string Password, int CompanyId, string DeviceId);
public record RefreshRequest(string RefreshToken);
public record SessionTokens(string AccessToken, string RefreshToken, DateTimeOffset ExpiresUtc, int UserId, int CompanyId, string DisplayName);
public record ApiError(string Error, string? CorrelationId = null);
public class ChecklistResult
{
    public int Id { get; set; }
    public string Text { get; set; } = "";
    public bool Required { get; set; } = true;
    public string Outcome { get; set; } = "";
    public string Reading { get; set; } = "";
    public string Note { get; set; } = "";
}
public class PartStock
{
    public int ItemId { get; set; }
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "Nos";
    public decimal Available { get; set; }
}
public record PartIntent(int ItemId, string Name, decimal Quantity, Guid OperationId);
public record PhotoRecord(Guid AttachmentId, string Category, string Hash, DateTimeOffset CapturedUtc);
public record StrokePoint(float X, float Y);
public record CustomerSignOff(string CustomerName, string ReportHash, List<List<StrokePoint>> Strokes, string ExceptionReason, DateTimeOffset CapturedUtc);
public record FieldEvent(Guid OperationId, int ActorId, string Action, DateTimeOffset CapturedUtc, DateTimeOffset ReceivedUtc);
public class FieldReport
{
    public Guid JobPublicId { get; set; }
    public long Revision { get; set; }
    public FieldState State { get; set; }
    public string Diagnosis { get; set; } = "";
    public string WorkPerformed { get; set; } = "";
    public string TestResults { get; set; } = "";
    public string Notes { get; set; } = "";
    public List<ChecklistResult> Checklist { get; set; } = [];
    public List<PartIntent> Parts { get; set; } = [];
    public bool PartsReviewed { get; set; }
    public List<PhotoRecord> Photos { get; set; } = [];
    public List<string> RequiredPhotoCategories { get; set; } = [];
    public CustomerSignOff? SignOff { get; set; }
    public DateTimeOffset? CompletedUtc { get; set; }
    public List<FieldEvent> Events { get; set; } = [];
    public List<FieldFinding> Findings { get; set; } = [];
    public List<VisitHandoff> Handoffs { get; set; } = [];
    public List<ReturnVisitRequest> ReturnVisits { get; set; } = [];
    public VisitHandoff? ActiveHandoff => Handoffs.LastOrDefault(h => h.ResumedUtc is null);
}
public class JobSnapshot
{
    public Guid PublicId { get; set; }
    public long OfficeVersion { get; set; }
    public string OfficeFingerprint { get; set; } = "";
    public string Number { get; set; } = "";
    public string Priority { get; set; } = "";
    public string Customer { get; set; } = "";
    public string Site { get; set; } = "";
    public string Address { get; set; } = "";
    public string Contact { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Fault { get; set; } = "";
    public DateTime ScheduledDate { get; set; }
    public string OfficeStatus { get; set; } = "";
    public FieldReport Report { get; set; } = new();
}
public class MobileOperation
{
    public Guid OperationId { get; set; } = Guid.NewGuid();
    public string DeviceId { get; set; } = "";
    public Guid JobPublicId { get; set; }
    public OperationType Type { get; set; }
    public int PayloadVersion { get; set; } = 1;
    public long BaseRevision { get; set; }
    public long OfficeVersion { get; set; }
    public string OfficeFingerprint { get; set; } = "";
    public Guid? PredecessorId { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public FieldState TargetState { get; set; }
    public string Diagnosis { get; set; } = "";
    public string WorkPerformed { get; set; } = "";
    public string TestResults { get; set; } = "";
    public string Notes { get; set; } = "";
    public ChecklistResult? Checklist { get; set; }
    public int ItemId { get; set; }
    public decimal Quantity { get; set; }
    public string PartName { get; set; } = "";
    public PhotoRecord? Photo { get; set; }
    public CustomerSignOff? SignOff { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public FieldFinding? Finding { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public FindingResolution? Resolution { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public VisitHandoff? Handoff { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public VisitResume? Resume { get; set; }
}
public record OperationReceipt(Guid OperationId, long Revision, FieldReport Report);
public record UploadReceipt(Guid AttachmentId, string Hash);
public class FieldRuleException(string message) : Exception(message);
public static partial class FieldRules
{
    public static string ContentHash(FieldReport report)
    {
        // Preserve acknowledgement hashes for legacy reports until priority-1 content is recorded.
        var content = JsonSerializer.Serialize(new { report.JobPublicId, report.Diagnosis, report.WorkPerformed, report.TestResults, report.Notes, report.Checklist, report.Parts, report.PartsReviewed, report.Photos });
        if (report.Findings.Count > 0 || report.Handoffs.Count > 0)
            content += JsonSerializer.Serialize(new { report.Findings, report.Handoffs,
                ReturnVisits = report.ReturnVisits.Select(v => new { v.Id, v.RequestedDate, v.NextAction, v.ResumedUtc }) });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    }

    public static List<string> CompletionIssues(FieldReport r)
    {
        var issues = new List<string>();
        if (r.ActiveHandoff is not null) issues.Add("This visit is handed to the office. Resume it before successful completion.");
        if (r.Findings.Any(f => f.ResolvedUtc is null)) issues.Add("Resolve outstanding findings or record an exception handoff.");
        if (string.IsNullOrWhiteSpace(r.Diagnosis)) issues.Add("Diagnosis is required.");
        if (string.IsNullOrWhiteSpace(r.WorkPerformed)) issues.Add("Work performed is required.");
        if (string.IsNullOrWhiteSpace(r.TestResults)) issues.Add("Test results are required.");
        if (!r.PartsReviewed) issues.Add("Record parts used or confirm that no parts were used.");
        if (r.Checklist.Count == 0) issues.Add("An office-configured service checklist is required.");
        if (r.Checklist.Any(c => c.Required && c.Outcome != "Pass" && c.Outcome != "N/A")) issues.Add("Complete all required checklist items; failed checks require office review.");
        foreach (var category in r.RequiredPhotoCategories.Where(c => !r.Photos.Any(p => p.Category == c))) issues.Add($"A {category} photo is required.");
        if (r.SignOff is null || r.SignOff.ReportHash != ContentHash(r)) issues.Add("Customer signature or reasoned exception for this report is required.");
        return issues;
    }

    public static void Apply(FieldReport r, MobileOperation op, int actorId)
    {
        if (op.PayloadVersion != 1 || op.OperationId == Guid.Empty || op.JobPublicId == Guid.Empty || string.IsNullOrWhiteSpace(op.DeviceId) || op.DeviceId.Length > 128)
            throw new FieldRuleException("Invalid mobile operation.");
        if (r.JobPublicId != Guid.Empty && r.JobPublicId != op.JobPublicId) throw new FieldRuleException("This action does not belong to this service report.");
        r.JobPublicId = op.JobPublicId;
        if (r.State == FieldState.Completed) throw new FieldRuleException("This service report is already completed.");
        if (r.ActiveHandoff is not null && op.Type is not (OperationType.ResumeVisit or OperationType.ResolveFinding))
            throw new FieldRuleException("This visit is handed to the office. Resume after office instruction before recording more work.");
        bool changesContent = false;
        switch (op.Type)
        {
            case OperationType.AddFinding:
            case OperationType.ResolveFinding:
            case OperationType.RecordException:
            case OperationType.ResumeVisit:
                ApplyFollowUp(r, op, actorId); changesContent = true;
                break;
            case OperationType.Transition:
                if (!Enum.IsDefined(op.TargetState) || (int)op.TargetState != (int)r.State + 1 || op.TargetState == FieldState.Completed)
                    throw new FieldRuleException("This job action is not available at the current stage.");
                r.State = op.TargetState;
                break;
            case OperationType.Report:
                RequireWorking(r);
                if (new[] { op.Diagnosis, op.WorkPerformed, op.TestResults, op.Notes }.Any(s => s is null || s.Length > 8000)) throw new FieldRuleException("Report entries must be 8,000 characters or fewer.");
                r.Diagnosis = op.Diagnosis.Trim(); r.WorkPerformed = op.WorkPerformed.Trim(); r.TestResults = op.TestResults.Trim(); r.Notes = op.Notes.Trim(); changesContent = true;
                break;
            case OperationType.Checklist:
                RequireWorking(r);
                var result = op.Checklist ?? throw new FieldRuleException("Checklist result is required.");
                var item = r.Checklist.SingleOrDefault(c => c.Id == result.Id) ?? throw new FieldRuleException("This checklist item does not belong to the job.");
                if (result.Outcome is not ("Pass" or "Fail" or "N/A") || result.Reading is null || result.Note is null || result.Reading.Length > 200 || result.Note.Length > 1000) throw new FieldRuleException("Enter a valid checklist outcome and note.");
                item.Outcome = result.Outcome; item.Reading = result.Reading; item.Note = result.Note; changesContent = true;
                break;
            case OperationType.Part:
                RequireWorking(r);
                if (op.ItemId <= 0 || op.Quantity <= 0 || op.Quantity > 999999 || decimal.Round(op.Quantity, 2) != op.Quantity) throw new FieldRuleException("Enter a positive parts quantity with up to two decimal places.");
                r.Parts.Add(new(op.ItemId, op.PartName, op.Quantity, op.OperationId)); r.PartsReviewed = true; changesContent = true;
                break;
            case OperationType.PartsReviewed:
                RequireWorking(r); r.PartsReviewed = true; changesContent = true;
                break;
            case OperationType.Photo:
                if (r.State < FieldState.Arrived) throw new FieldRuleException("Arrive at the site before adding photos.");
                var photo = op.Photo ?? throw new FieldRuleException("A photo is required.");
                if (photo.AttachmentId == Guid.Empty || photo.Hash is null || photo.Hash.Length != 64 || photo.Category is not ("Before" or "After" or "Damage" or "SerialPlate" or "Installation" or "Other")) throw new FieldRuleException("Invalid photo attachment.");
                r.Photos.Add(photo); changesContent = true;
                break;
            case OperationType.Signature:
                if (r.State != FieldState.AwaitingSignature) throw new FieldRuleException("Finish testing before customer sign-off.");
                var sign = op.SignOff ?? throw new FieldRuleException("Customer acknowledgement is required.");
                if (string.IsNullOrWhiteSpace(sign.CustomerName) || sign.CustomerName.Length > 150 || sign.ReportHash != ContentHash(r)) throw new FieldRuleException("Sign-off must acknowledge the current service report.");
                if (sign.Strokes is null || sign.Strokes.Any(s => s is null) || sign.ExceptionReason is null || sign.ExceptionReason.Length > 1000 || (sign.Strokes.Sum(s => s.Count) < 2 && string.IsNullOrWhiteSpace(sign.ExceptionReason))) throw new FieldRuleException("Customer signature or an exception reason is required.");
                if (sign.Strokes.Count > 100 || sign.Strokes.Sum(s => s.Count) > 10000 || sign.Strokes.SelectMany(s => s).Any(p => !float.IsFinite(p.X) || !float.IsFinite(p.Y) || p.X < 0 || p.X > 1 || p.Y < 0 || p.Y > 1)) throw new FieldRuleException("Invalid signature strokes.");
                r.SignOff = sign;
                break;
            case OperationType.Complete:
                if (r.State != FieldState.AwaitingSignature) throw new FieldRuleException("Finish the service workflow before completion.");
                var issues = CompletionIssues(r); if (issues.Count > 0) throw new FieldRuleException(string.Join(" ", issues));
                r.State = FieldState.Completed; r.CompletedUtc = op.CreatedUtc;
                foreach (var visit in r.ReturnVisits.Where(v => v.ResumedUtc.HasValue && !v.CompletedUtc.HasValue))
                    visit.CompletedUtc = op.CreatedUtc;
                break;
            default: throw new FieldRuleException("Unsupported mobile operation.");
        }
        if (changesContent) r.SignOff = null;
        r.Revision++;
        r.Events.Add(new(op.OperationId, actorId, op.Type == OperationType.Transition ? op.TargetState.ToString() : op.Type.ToString(), op.CreatedUtc, DateTimeOffset.UtcNow));
    }
    private static void RequireWorking(FieldReport r)
    {
        if (r.State < FieldState.WorkStarted) throw new FieldRuleException("Start work before recording service details.");
    }
}

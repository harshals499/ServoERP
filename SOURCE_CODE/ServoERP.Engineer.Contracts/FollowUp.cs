namespace ServoERP.Engineer;

public enum VisitExceptionReason { AwaitingParts, AwaitingApproval, CustomerUnavailable, ReturnVisitRequired }
public enum FindingOwner { OfficeDispatch, AssignedEngineer }

public sealed class FieldFinding
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
    public string NextAction { get; set; } = "";
    public FindingOwner Owner { get; set; }
    public int? ChecklistItemId { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public int CreatedBy { get; set; }
    public DateTimeOffset? ResolvedUtc { get; set; }
    public int? ResolvedBy { get; set; }
    public string Resolution { get; set; } = "";
}
public sealed record FindingResolution(Guid FindingId, string Note);
public sealed record VisitResume(Guid HandoffId, string Note);
public sealed class VisitHandoff
{
    public Guid Id { get; set; }
    public VisitExceptionReason Reason { get; set; }
    public string Detail { get; set; } = "";
    public string NextAction { get; set; } = "";
    public DateTime? RequestedReturnDate { get; set; }
    public List<Guid> FindingIds { get; set; } = [];
    public DateTimeOffset CapturedUtc { get; set; }
    public int RecordedBy { get; set; }
    public DateTimeOffset? ResumedUtc { get; set; }
    public string ResumeNote { get; set; } = "";
}
public sealed class ReturnVisitRequest
{
    public Guid Id { get; set; }
    public DateTime RequestedDate { get; set; }
    public string NextAction { get; set; } = "";
    public DateTimeOffset? ResumedUtc { get; set; }
    public DateTimeOffset? CompletedUtc { get; set; }
}

public static partial class FieldRules
{
    private static string RequiredText(string? value, int maximum, string label)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum)
            throw new FieldRuleException(label + " is required and must be " + maximum + " characters or fewer.");
        return value.Trim();
    }
    private static void ApplyFollowUp(FieldReport r, MobileOperation op, int actorId)
    {
        if (r.State < FieldState.Accepted) throw new FieldRuleException("Accept the job before recording a finding or handoff.");
        switch (op.Type)
        {
            case OperationType.AddFinding:
                var f = op.Finding ?? throw new FieldRuleException("Finding details are required.");
                var title = RequiredText(f.Title, 150, "Finding title");
                var detail = RequiredText(f.Detail, 2000, "Finding detail");
                var action = RequiredText(f.NextAction, 1000, "Next action");
                if (f.Id == Guid.Empty || !Enum.IsDefined(f.Owner) || r.Findings.Any(x => x.Id == f.Id))
                    throw new FieldRuleException("Use a new finding ID and a valid follow-up owner.");
                if (f.ChecklistItemId.HasValue && !r.Checklist.Any(c => c.Id == f.ChecklistItemId))
                    throw new FieldRuleException("The linked check does not belong to this job.");
                if (r.Findings.Count >= 200) throw new FieldRuleException("Contact the office before adding more findings to this job.");
                r.Findings.Add(new() { Id = f.Id, Title = title, Detail = detail, NextAction = action, Owner = f.Owner,
                    ChecklistItemId = f.ChecklistItemId, CreatedUtc = op.CreatedUtc, CreatedBy = actorId });
                break;
            case OperationType.ResolveFinding:
                var resolution = op.Resolution ?? throw new FieldRuleException("Select a finding to resolve.");
                var note = RequiredText(resolution.Note, 2000, "Resolution note");
                var finding = r.Findings.SingleOrDefault(x => x.Id == resolution.FindingId && x.ResolvedUtc is null)
                    ?? throw new FieldRuleException("This finding is unavailable or already resolved.");
                finding.Resolution = note; finding.ResolvedUtc = op.CreatedUtc; finding.ResolvedBy = actorId;
                break;
            case OperationType.RecordException:
                var h = op.Handoff ?? throw new FieldRuleException("Exception details are required.");
                var reason = RequiredText(h.Detail, 2000, "Exception reason");
                var next = RequiredText(h.NextAction, 1000, "Office next action");
                if (h.Id == Guid.Empty || !Enum.IsDefined(h.Reason) || r.Handoffs.Any(x => x.Id == h.Id) || r.Handoffs.Count >= 100)
                    throw new FieldRuleException("Use a new handoff ID and a valid exception type.");
                if (h.FindingIds is null || h.FindingIds.Count > 200 || h.FindingIds.Distinct().Count() != h.FindingIds.Count ||
                    h.FindingIds.Any(id => !r.Findings.Any(x => x.Id == id && x.ResolvedUtc is null)))
                    throw new FieldRuleException("Link only outstanding findings from this job.");
                if (r.Checklist.Any(c => c.Required && c.Outcome == "Fail" && !h.FindingIds.Any(id => r.Findings.Any(finding => finding.Id == id && finding.ChecklistItemId == c.Id))))
                    throw new FieldRuleException("Add and link a finding for each failed required check before handoff.");
                if (h.Reason == VisitExceptionReason.ReturnVisitRequired && h.RequestedReturnDate is null)
                    throw new FieldRuleException("Choose a requested return date for office scheduling.");
                if (h.RequestedReturnDate.HasValue && (h.Reason != VisitExceptionReason.ReturnVisitRequired ||
                    h.RequestedReturnDate.Value.Date < op.CreatedUtc.UtcDateTime.Date.AddDays(-1) ||
                    h.RequestedReturnDate.Value.Date > op.CreatedUtc.UtcDateTime.Date.AddYears(1)))
                    throw new FieldRuleException("Choose a return date within the next year.");
                r.Handoffs.Add(new() { Id = h.Id, Reason = h.Reason, Detail = reason, NextAction = next,
                    RequestedReturnDate = h.RequestedReturnDate?.Date, FindingIds = [.. h.FindingIds], CapturedUtc = op.CreatedUtc, RecordedBy = actorId });
                if (h.Reason == VisitExceptionReason.ReturnVisitRequired)
                    r.ReturnVisits.Add(new() { Id = h.Id, RequestedDate = h.RequestedReturnDate!.Value.Date, NextAction = next });
                break;
            case OperationType.ResumeVisit:
                var resume = op.Resume ?? throw new FieldRuleException("A resume note is required.");
                var resumeNote = RequiredText(resume.Note, 1000, "Office instruction / resume note");
                var active = r.ActiveHandoff;
                if (active is null || active.Id != resume.HandoffId) throw new FieldRuleException("Select the current handoff before resuming.");
                active.ResumedUtc = op.CreatedUtc; active.ResumeNote = resumeNote;
                var request = r.ReturnVisits.SingleOrDefault(v => v.Id == active.Id);
                if (request is not null) request.ResumedUtc = op.CreatedUtc;
                break;
        }
    }
}

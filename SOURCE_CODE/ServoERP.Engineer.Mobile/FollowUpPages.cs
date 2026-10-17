namespace ServoERP.Engineer.Mobile;

internal static class FollowUpUi
{
    public static string Reason(VisitExceptionReason reason) => reason switch
    {
        VisitExceptionReason.AwaitingParts => "Awaiting parts",
        VisitExceptionReason.AwaitingApproval => "Awaiting approval",
        VisitExceptionReason.CustomerUnavailable => "Customer unavailable",
        _ => "Return visit required"
    };
    public static Editor Notes(string placeholder, int maximum) => new() { Placeholder = placeholder, MaxLength = maximum, MinimumHeightRequest = 100 };
}

public sealed class FindingsPage : ContentPage
{
    public FindingsPage(MobileSession session, Guid jobId)
    {
        Title = "Findings & return visits";
        var report = session.Workspace!.Job(jobId).Report; var stack = FieldUi.Stack();
        stack.Add(FieldUi.Text("FOLLOW-UP REGISTER", 24, true));
        stack.Add(FieldUi.Text("Findings and return requests stay with this job. The office schedules return work through Jobs.", 14));
        if (report.Findings.Count == 0) stack.Add(FieldUi.Text("No findings recorded."));
        foreach (var finding in report.Findings)
        {
            stack.Add(FieldUi.Text((finding.ResolvedUtc.HasValue ? "RESOLVED · " : "OPEN · ") + finding.Title, 18, true));
            stack.Add(FieldUi.Text(finding.Detail + "\nNext: " + finding.NextAction + "\nOwner: " + (finding.Owner == FindingOwner.OfficeDispatch ? "Office dispatch" : "Assigned engineer"), 14));
            if (finding.ChecklistItemId.HasValue) stack.Add(FieldUi.Text("Linked check: " + report.Checklist.FirstOrDefault(c => c.Id == finding.ChecklistItemId)?.Text, 13));
            if (finding.ResolvedUtc.HasValue) stack.Add(FieldUi.Text("Resolution: " + finding.Resolution, 14));
            else if (report.State != FieldState.Completed)
            {
                var note = FollowUpUi.Notes("Resolution evidence / action taken", 2000); stack.Add(note);
                stack.Add(FieldUi.Button("RESOLVE FINDING", async () =>
                {
                    session.Save(new() { JobPublicId = jobId, Type = OperationType.ResolveFinding, Resolution = new(finding.Id, note.Text ?? "") });
                    await Navigation.PopAsync();
                }, true));
            }
        }
        foreach (var visit in report.ReturnVisits)
            stack.Add(FieldUi.Text("RETURN REQUEST · " + visit.RequestedDate.ToString("dd/MM/yyyy") + "\n" + visit.NextAction + "\n" +
                (visit.CompletedUtc.HasValue ? "Service report completed" : visit.ResumedUtc.HasValue ? "Resumed after office instruction" : "Requested · office scheduling required"), 14));
        if (report.State != FieldState.Completed && report.ActiveHandoff is null)
        {
            stack.Add(FieldUi.Text("ADD FINDING", 20, true));
            var title = FieldUi.Entry("Finding title"); title.MaxLength = 150; stack.Add(title);
            var detail = FollowUpUi.Notes("Observed fault / risk / evidence", 2000); stack.Add(detail);
            var action = FollowUpUi.Notes("Next action required", 1000); stack.Add(action);
            var owner = new Picker { Title = "Follow-up owner", ItemsSource = new[] { "Office dispatch", "Assigned engineer" }, SelectedIndex = 0 }; stack.Add(owner);
            var checks = report.Checklist; var check = new Picker { Title = "Link a checklist item", ItemsSource = new[] { "No checklist link" }.Concat(checks.Select(c => c.Text)).ToList(), SelectedIndex = 0 }; stack.Add(check);
            stack.Add(FieldUi.Button("SAVE FINDING", async () =>
            {
                session.Save(new() { JobPublicId = jobId, Type = OperationType.AddFinding, Finding = new()
                {
                    Id = Guid.NewGuid(), Title = title.Text ?? "", Detail = detail.Text ?? "", NextAction = action.Text ?? "",
                    Owner = (FindingOwner)Math.Max(0, owner.SelectedIndex), ChecklistItemId = check.SelectedIndex > 0 ? checks[check.SelectedIndex - 1].Id : null
                } });
                await Navigation.PopAsync();
            }));
        }
        Content = new ScrollView { Content = stack };
    }
}

public sealed class ExceptionPage : ContentPage
{
    public ExceptionPage(MobileSession session, Guid jobId)
    {
        Title = "Exception / return visit"; var stack = FieldUi.Stack();
        stack.Add(FieldUi.Text("HAND OFF TO THE OFFICE", 24, true));
        stack.Add(FieldUi.Text("This keeps the job open and preserves failed checks. Record a finding linked to every failed required check first. Return dates are requests, not confirmed appointments.", 14));
        var reason = new Picker { Title = "Exception type", ItemsSource = Enum.GetValues<VisitExceptionReason>().Select(FollowUpUi.Reason).ToList(), SelectedIndex = 0 }; stack.Add(reason);
        var detail = FollowUpUi.Notes("Why work cannot continue / work already performed", 2000); stack.Add(detail);
        var action = FollowUpUi.Notes("Next action for office dispatch", 1000); stack.Add(action);
        var dateLabel = FieldUi.Text("Requested return date (office confirmation required)", 14); stack.Add(dateLabel);
        var date = new DatePicker { Date = DateTime.Today.AddDays(1), MinimumDate = DateTime.Today, MaximumDate = DateTime.Today.AddYears(1), Format = "dd/MM/yyyy" }; stack.Add(date);
        void DateVisibility() { date.IsVisible = dateLabel.IsVisible = reason.SelectedIndex == (int)VisitExceptionReason.ReturnVisitRequired; }
        reason.SelectedIndexChanged += (_, _) => DateVisibility(); DateVisibility();
        stack.Add(FieldUi.Text("All current outstanding findings will be linked to this handoff.", 14));
        stack.Add(FieldUi.Button("SAVE EXCEPTION HANDOFF", async () =>
        {
            var report = session.Workspace!.Job(jobId).Report;
            session.Save(new() { JobPublicId = jobId, Type = OperationType.RecordException, Handoff = new()
            {
                Id = Guid.NewGuid(), Reason = (VisitExceptionReason)reason.SelectedIndex, Detail = detail.Text ?? "", NextAction = action.Text ?? "",
                RequestedReturnDate = date.IsVisible ? date.Date : null, FindingIds = report.Findings.Where(f => f.ResolvedUtc is null).Select(f => f.Id).ToList()
            } });
            await Navigation.PopAsync();
        }));
        Content = new ScrollView { Content = stack };
    }
}

public sealed class ResumeVisitPage : ContentPage
{
    public ResumeVisitPage(MobileSession session, Guid jobId)
    {
        Title = "Resume service visit"; var stack = FieldUi.Stack();
        stack.Add(FieldUi.Text("RESUME AFTER OFFICE INSTRUCTION", 23, true));
        stack.Add(FieldUi.Text("Confirm the schedule with dispatch. Resuming keeps the previous handoff, findings and failed checks in the history; it does not mark them resolved.", 14));
        var note = FollowUpUi.Notes("Office instruction / why work can now resume", 1000); stack.Add(note);
        stack.Add(FieldUi.Button("RESUME VISIT", async () =>
        {
            var active = session.Workspace!.Job(jobId).Report.ActiveHandoff ?? throw new FieldRuleException("This visit has no active handoff.");
            session.Save(new() { JobPublicId = jobId, Type = OperationType.ResumeVisit, Resume = new(active.Id, note.Text ?? "") });
            await Navigation.PopAsync();
        }));
        Content = new ScrollView { Content = stack };
    }
}

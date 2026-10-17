using Serilog;

namespace ServoERP.Engineer.Mobile;

internal static class FieldUi
{
    public static readonly Color Ink = Color.FromArgb("#172B40"), Teal = Color.FromArgb("#007F78"), Pale = Color.FromArgb("#F3F7F8");
    public static Label Text(string text, int size = 16, bool bold = false) => new() { Text = text, FontSize = size, TextColor = Ink, FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None };
    public static Button Button(string text, Func<Task> action, bool secondary = false)
    {
        var button = new Button { Text = text, MinimumHeightRequest = 56, CornerRadius = 12, BackgroundColor = secondary ? Pale : Teal, TextColor = secondary ? Ink : Colors.White, FontSize = 16, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 3) };
        button.Clicked += async (_, _) =>
        {
            button.IsEnabled = false;
            try { await action(); }
            catch (Exception ex)
            {
                Log.Warning(ex, "Field action failed");
                var page = Application.Current?.Windows.FirstOrDefault()?.Page;
                if (page is not null) await page.DisplayAlert("ServoERP Engineer", ex is FieldRuleException or TransportException ? ex.Message : "Unable to perform this action. Your saved work is retained.", "OK");
            }
            finally { button.IsEnabled = true; }
        };
        return button;
    }
    public static VerticalStackLayout Stack() => new() { Spacing = 12, Padding = 20 };
    public static Entry Entry(string placeholder, bool password = false) => new() { Placeholder = placeholder, IsPassword = password, MinimumHeightRequest = 52, FontSize = 16, TextColor = Ink };
}
public sealed class LoginPage : ContentPage
{
    private readonly MobileSession _session;
    public LoginPage(MobileSession session)
    {
        _session = session; Title = "ServoERP Engineer"; BackgroundColor = Colors.White;
        var stack = FieldUi.Stack(); stack.Add(FieldUi.Text("SERVICE,\nWHERE YOU ARE.", 32, true));
        stack.Add(FieldUi.Text("Your jobs, reports and parts — ready for the field."));
        var server = FieldUi.Entry("Office HTTPS address"); server.Text = Preferences.Default.Get("engineer-server", ""); server.Keyboard = Keyboard.Url;
        var company = FieldUi.Entry("Company number"); company.Keyboard = Keyboard.Numeric;
        var user = FieldUi.Entry("Username"); var password = FieldUi.Entry("Password", true);
        stack.Add(server); stack.Add(company); stack.Add(user); stack.Add(password);
        stack.Add(FieldUi.Button("SIGN IN", async () => { await session.Login(server.Text ?? "", user.Text ?? "", password.Text ?? "", company.Text ?? ""); password.Text = ""; ShowApp(); }));
        stack.Add(FieldUi.Button("EXPLORE TRAINING JOB", () => { session.OpenTraining(); ShowApp(); return Task.CompletedTask; }, true));
        stack.Add(FieldUi.Text("Training uses sample data on this device. It does not connect to your office or change business stock.", 13));
        Content = new ScrollView { Content = stack };
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { if (await _session.Restore()) { ShowApp(); _ = _session.Synchronize(); } }
        catch (Exception ex) { Log.Warning(ex, "Session restore failed"); }
    }
    private void ShowApp() => Application.Current!.Windows[0].Page = new EngineerTabs(_session);
}
public sealed class EngineerTabs : TabbedPage
{
    public EngineerTabs(MobileSession session)
    {
        BarBackgroundColor = Colors.White; BarTextColor = FieldUi.Ink; SelectedTabColor = FieldUi.Teal; UnselectedTabColor = Colors.Gray;
        Children.Add(new NavigationPage(new JobListPage(session, true)) { Title = "TODAY", IconImageSource = "today.png" });
        Children.Add(new NavigationPage(new JobListPage(session, false)) { Title = "JOBS", IconImageSource = "jobs.png" });
        Children.Add(new NavigationPage(new ScanPage(session)) { Title = "SCAN", IconImageSource = "scan.png" });
        Children.Add(new NavigationPage(new SyncPage(session)) { Title = "SYNC", IconImageSource = "sync.png" });
        Children.Add(new NavigationPage(new ProfilePage(session)) { Title = "PROFILE", IconImageSource = "profile.png" });
#if ANDROID
        Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.TabbedPage.SetToolbarPlacement(this, Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.ToolbarPlacement.Bottom);
        Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.TabbedPage.SetIsSwipePagingEnabled(this, false);
#endif
        var timer = Application.Current!.Dispatcher.CreateTimer(); timer.Interval = TimeSpan.FromSeconds(30); timer.Tick += (_, _) => _ = session.Synchronize(); timer.Start();
        Unloaded += (_, _) => timer.Stop();
    }
}
public sealed class JobListPage : ContentPage
{
    private readonly MobileSession _session; private readonly bool _today; private readonly VerticalStackLayout _stack = FieldUi.Stack();
    public JobListPage(MobileSession session, bool today)
    {
        _session = session; _today = today; Title = today ? "Today" : "Your jobs"; BackgroundColor = FieldUi.Pale;
        Content = new ScrollView { Content = _stack };
    }
    protected override void OnAppearing() { base.OnAppearing(); _session.Changed -= Refresh; _session.Changed += Refresh; Refresh(); }
    protected override void OnDisappearing() { _session.Changed -= Refresh; base.OnDisappearing(); }
    private void Refresh()
    {
        if (_session.Workspace is null) return;
        _stack.Clear(); _stack.Add(FieldUi.Text(_today ? "LET’S GET TO WORK" : "ASSIGNED TO YOU", 25, true));
        int pending = _session.Workspace.Queue().Count(p => p.State != QueueState.Synced);
        _stack.Add(FieldUi.Text(_session.Status + (pending > 0 ? $" · {pending} changes waiting" : " · All data synced"), 13));
        var jobs = _session.Workspace.Jobs().Where(j => !_today || (j.ScheduledDate.Date <= DateTime.Today && j.Report.State != FieldState.Completed && j.OfficeStatus is not ("Closed" or "Invoiced" or "Cancelled"))).OrderBy(j => j.ScheduledDate).ToList();
        if (jobs.Count == 0) _stack.Add(FieldUi.Text("No jobs to show. Sync to receive your assigned work."));
        foreach (var job in jobs)
        {
            var card = new VerticalStackLayout { Padding = 16, Spacing = 8, BackgroundColor = Colors.White };
            var priority = FieldUi.Text(job.Priority.ToUpperInvariant() + " · " + job.ScheduledDate.ToString("dd/MM/yyyy HH:mm"), 13, true); if (job.Priority.Equals("Urgent", StringComparison.OrdinalIgnoreCase) || job.Priority.Equals("High", StringComparison.OrdinalIgnoreCase)) priority.TextColor = Color.FromArgb("#A43B18");
            card.Add(priority); card.Add(FieldUi.Text(job.Number, 22, true)); card.Add(FieldUi.Text(job.Customer, 19, true)); card.Add(FieldUi.Text(job.Site)); card.Add(FieldUi.Text(job.Fault)); card.Add(FieldUi.Text(job.Report.ActiveHandoff is { } handoff ? "OFFICE HANDOFF · " + FollowUpUi.Reason(handoff.Reason) : job.Report.State.ToString(), 14));
            card.Add(FieldUi.Button("OPEN JOB", () => Navigation.PushAsync(new JobPage(_session, job.PublicId))));
            _stack.Add(new Border { Content = card, Stroke = Colors.LightGray, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 } });
        }
    }
}
public sealed class JobPage : ContentPage
{
    private readonly MobileSession _session; private readonly Guid _id; private readonly VerticalStackLayout _stack = FieldUi.Stack();
    public JobPage(MobileSession session, Guid id) { _session = session; _id = id; Title = "Service visit"; BackgroundColor = Colors.White; Content = new ScrollView { Content = _stack }; }
    protected override void OnAppearing() { base.OnAppearing(); _session.Changed -= Refresh; _session.Changed += Refresh; Refresh(); }
    protected override void OnDisappearing() { _session.Changed -= Refresh; base.OnDisappearing(); }
    private void Refresh()
    {
        var job = _session.Workspace!.Job(_id); var r = job.Report;
        _stack.Clear(); _stack.Add(FieldUi.Text(job.Number, 25, true)); _stack.Add(FieldUi.Text(job.Customer + " · " + job.Site, 19, true)); _stack.Add(FieldUi.Text(job.Address)); _stack.Add(FieldUi.Text(job.Contact + " " + job.Phone));
        if (!string.IsNullOrWhiteSpace(job.Phone)) _stack.Add(FieldUi.Button("CALL CUSTOMER", () => { PhoneDialer.Default.Open(job.Phone); return Task.CompletedTask; }, true));
        if (!string.IsNullOrWhiteSpace(job.Address)) _stack.Add(FieldUi.Button("NAVIGATE TO SITE", async () => await Launcher.Default.OpenAsync("https://www.google.com/maps/search/?api=1&query=" + Uri.EscapeDataString(job.Address)), true));
        _stack.Add(FieldUi.Text("REPORTED FAULT", 13, true)); _stack.Add(FieldUi.Text(job.Fault, 20, true)); _stack.Add(FieldUi.Text("STATUS · " + r.State, 16, true));
        _stack.Add(FieldUi.Text(_session.Training ? "TRAINING · Local practice only" : "Saved on device · Server confirms stock and completion", 13));
        if (r.State >= FieldState.Accepted)
            _stack.Add(FieldUi.Button("FINDINGS & RETURN VISITS", () => Navigation.PushAsync(new FindingsPage(_session, _id)), true));
        if (r.ActiveHandoff is { } active)
        {
            _stack.Add(FieldUi.Text("OFFICE HANDOFF · " + FollowUpUi.Reason(active.Reason), 18, true));
            _stack.Add(FieldUi.Text(active.Detail + "\nNext: " + active.NextAction));
            if (active.RequestedReturnDate.HasValue) _stack.Add(FieldUi.Text("Return requested " + active.RequestedReturnDate.Value.ToString("dd/MM/yyyy") + " · Office must confirm the schedule."));
            _stack.Add(FieldUi.Text(_session.Workspace.Queue().Any(q => q.JobId == _id.ToString() && q.State != QueueState.Synced) ? "Pending synchronization · not yet confirmed by office" : _session.Training ? "Training · saved on device only" : "Synchronized · office scheduling remains separate", 13));
            _stack.Add(FieldUi.Button("RESUME AFTER OFFICE INSTRUCTION", () => Navigation.PushAsync(new ResumeVisitPage(_session, _id)), true));
        }
        else if (r.State < FieldState.AwaitingSignature)
        {
            var next = (FieldState)((int)r.State + 1);
            string label = next switch { FieldState.Accepted => "ACCEPT JOB", FieldState.Travelling => "START TRAVEL", FieldState.Arrived => "ARRIVED", FieldState.WorkStarted => "START WORK", FieldState.Diagnosis => "BEGIN DIAGNOSIS", FieldState.Repair => "BEGIN REPAIR", FieldState.Testing => "BEGIN TESTING", _ => "READY FOR CUSTOMER SIGN-OFF" };
            _stack.Add(FieldUi.Button(label, () => { _session.Save(new() { JobPublicId = _id, Type = OperationType.Transition, TargetState = next }); Refresh(); return Task.CompletedTask; }));
        }
        if (r.State >= FieldState.Accepted && r.State != FieldState.Completed && r.ActiveHandoff is null)
            _stack.Add(FieldUi.Button("RECORD EXCEPTION / RETURN VISIT", () => Navigation.PushAsync(new ExceptionPage(_session, _id)), true));
        if (r.State >= FieldState.WorkStarted && r.State != FieldState.Completed && r.ActiveHandoff is null)
        {
            _stack.Add(FieldUi.Button("DIAGNOSIS & WORK REPORT", () => Navigation.PushAsync(new ReportPage(_session, job)), true));
            _stack.Add(FieldUi.Button("OPEN CHECKLIST", () => Navigation.PushAsync(new ChecklistPage(_session, job)), true));
            _stack.Add(FieldUi.Button("USE PART", () => Navigation.PushAsync(new PartsPage(_session, job)), true));
        }
        if (r.State >= FieldState.Arrived && r.State != FieldState.Completed && r.ActiveHandoff is null) _stack.Add(FieldUi.Button("ADD PHOTO", () => Navigation.PushAsync(new PhotoPage(_session, _id)), true));
        if (r.State == FieldState.AwaitingSignature && r.ActiveHandoff is null)
        {
            _stack.Add(FieldUi.Button("CUSTOMER SIGNATURE / EXCEPTION", () => Navigation.PushAsync(new SignOffPage(_session, job)), true));
            var issues = FieldRules.CompletionIssues(r); _stack.Add(FieldUi.Text(issues.Count == 0 ? "✓ Required service information complete" : string.Join("\n", issues.Select(i => "• " + i)), 14));
            _stack.Add(FieldUi.Button("COMPLETE SERVICE VISIT", async () =>
            {
                if (!await DisplayAlert("Complete service visit?", "Finalize this service report and queue it for office review. Stock entries must synchronize successfully before server completion.", "COMPLETE VISIT", "BACK")) return;
                _session.Save(new() { JobPublicId = _id, Type = OperationType.Complete }); Refresh();
            }));
        }
        if (r.State == FieldState.Completed) _stack.Add(FieldUi.Text("SERVICE REPORT COMPLETED\n" + (_session.Workspace.Queue().Any(p => p.JobId == _id.ToString() && p.State != QueueState.Synced) ? "Pending synchronization · office closure remains separate" : "Synchronized · ready for office review"), 18, true));
        _stack.Add(FieldUi.Text($"{r.Parts.Sum(p => p.Quantity):0.##} parts recorded · {r.Photos.Count} photos", 14));
        foreach (var photo in _session.Workspace.Queue().Where(p => p.JobId == _id.ToString() && !string.IsNullOrEmpty(p.LocalPhotoPath) && File.Exists(p.LocalPhotoPath)))
            _stack.Add(new Image { Source = ImageSource.FromFile(photo.LocalPhotoPath), HeightRequest = 160, Aspect = Aspect.AspectFit });
        foreach (var e in r.Events.TakeLast(12)) _stack.Add(FieldUi.Text(e.CapturedUtc.ToLocalTime().ToString("HH:mm") + "  " + e.Action, 13));
    }
}
public sealed class ReportPage : ContentPage
{
    public ReportPage(MobileSession session, JobSnapshot job)
    {
        Title = "Engineer report"; var stack = FieldUi.Stack();
        var fields = new[] { ("Diagnosis", job.Report.Diagnosis), ("Work performed", job.Report.WorkPerformed), ("Test results", job.Report.TestResults), ("Additional notes", job.Report.Notes) };
        var editors = new List<Editor>();
        foreach (var field in fields) { stack.Add(FieldUi.Text(field.Item1, 17, true)); var editor = new Editor { Text = field.Item2, AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 100, MaxLength = 8000 }; editors.Add(editor); stack.Add(editor); }
        stack.Add(FieldUi.Button("SAVE REPORT", async () => { session.Save(new() { JobPublicId = job.PublicId, Type = OperationType.Report, Diagnosis = editors[0].Text ?? "", WorkPerformed = editors[1].Text ?? "", TestResults = editors[2].Text ?? "", Notes = editors[3].Text ?? "" }); await Navigation.PopAsync(); }));
        Content = new ScrollView { Content = stack };
    }
}
public sealed class ChecklistPage : ContentPage
{
    public ChecklistPage(MobileSession session, JobSnapshot job)
    {
        Title = "Service checklist"; var stack = FieldUi.Stack(); stack.Add(FieldUi.Text("CHECK. RECORD. VERIFY.", 23, true));
        if (job.Report.Checklist.Count == 0) stack.Add(FieldUi.Text("Ask the office to configure this job’s checklist before completion."));
        foreach (var item in job.Report.Checklist)
        {
            stack.Add(FieldUi.Text(item.Text + (item.Required ? " *" : ""), 18, true)); var outcome = new Picker { Title = "Pass / Fail / N/A", ItemsSource = new[] { "Pass", "Fail", "N/A" }, SelectedItem = item.Outcome, MinimumHeightRequest = 52 };
            var reading = FieldUi.Entry("Reading / value"); reading.Text = item.Reading; reading.MaxLength = 200;
            var note = FieldUi.Entry("Engineer note"); note.Text = item.Note; note.MaxLength = 1000;
            stack.Add(outcome); stack.Add(reading); stack.Add(note);
            stack.Add(FieldUi.Button("SAVE CHECK", () => { session.Save(new() { JobPublicId = job.PublicId, Type = OperationType.Checklist, Checklist = new() { Id = item.Id, Outcome = outcome.SelectedItem as string ?? "", Reading = reading.Text ?? "", Note = note.Text ?? "" } }); return Task.CompletedTask; }, true));
        }
        Content = new ScrollView { Content = stack };
    }
}
public sealed class PartsPage : ContentPage
{
    public PartsPage(MobileSession session, JobSnapshot job)
    {
        Title = "Use a part"; var stack = FieldUi.Stack(); stack.Add(FieldUi.Text("YOUR VAN STOCK", 24, true)); stack.Add(FieldUi.Text("Offline entries are pending consumption. The server confirms availability.", 14));
        if (job.Report.Parts.Count == 0) stack.Add(FieldUi.Button("CONFIRM NO PARTS USED", async () => { session.Save(new() { JobPublicId = job.PublicId, Type = OperationType.PartsReviewed }); await Navigation.PopAsync(); }, true));
        var search = FieldUi.Entry("Search part number or name"); stack.Add(search); var results = new VerticalStackLayout { Spacing = 12 }; stack.Add(results);
        void Refresh()
        {
            results.Clear(); var stock = session.Workspace!.Value<List<PartStock>>("stock") ?? [];
            foreach (var part in stock.Where(p => p.Name.Contains(search.Text ?? "", StringComparison.OrdinalIgnoreCase)))
            {
                decimal pending = session.Workspace.Queue().Where(q => q.State != QueueState.Synced).Select(q => System.Text.Json.JsonSerializer.Deserialize<MobileOperation>(q.PayloadJson)!).Where(o => o.Type == OperationType.Part && o.ItemId == part.ItemId).Sum(o => o.Quantity);
                results.Add(FieldUi.Text(part.Name, 19, true)); results.Add(FieldUi.Text($"Van snapshot: {part.Available:0.##} · pending: {pending:0.##} · provisional: {Math.Max(0, part.Available - pending):0.##} {part.Unit}", 14));
                var quantity = FieldUi.Entry("Quantity"); quantity.Keyboard = Keyboard.Numeric; results.Add(quantity);
                results.Add(FieldUi.Button("USE PART", async () =>
                {
                    if (!decimal.TryParse(quantity.Text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.GetCultureInfo("en-IN"), out var qty) || qty <= 0 || qty > part.Available - pending) throw new FieldRuleException("Enter a quantity within your provisional van availability.");
                    if (!await DisplayAlert("Record parts consumption?", $"{part.Name} × {qty:0.##} for {job.Number}. This will deduct stock when accepted by the office server.", "USE PART", "BACK")) return;
                    session.Save(new() { JobPublicId = job.PublicId, Type = OperationType.Part, ItemId = part.ItemId, PartName = part.Name, Quantity = qty }); Refresh();
                }));
            }
            if (stock.Count == 0) results.Add(FieldUi.Text("No van stock is cached. Sync or ask the office to allocate stock to your van."));
        }
        search.TextChanged += (_, _) => Refresh(); Refresh(); Content = new ScrollView { Content = stack };
    }
}

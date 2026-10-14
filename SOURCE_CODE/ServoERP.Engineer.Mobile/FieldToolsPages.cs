using System.Security.Cryptography;
using Microsoft.Maui.Graphics;

namespace ServoERP.Engineer.Mobile;

public sealed class PhotoPage : ContentPage
{
    public PhotoPage(MobileSession session, Guid jobId)
    {
        Title = "Job photo"; var stack = FieldUi.Stack(); stack.Add(FieldUi.Text("PHOTO EVIDENCE", 24, true));
        var category = new Picker { Title = "Photo category", ItemsSource = new[] { "Before", "After", "Damage", "SerialPlate", "Installation", "Other" }, SelectedIndex = 0, MinimumHeightRequest = 56 }; stack.Add(category);
        stack.Add(FieldUi.Text("Photos are compressed and saved privately on this device. They upload when connected; location metadata is removed.", 14));
        async Task Capture(bool camera)
        {
            var file = camera ? await MediaPicker.Default.CapturePhotoAsync() : await MediaPicker.Default.PickPhotoAsync(); if (file is null) return;
            var id = Guid.NewGuid(); var directory = Path.Combine(FileSystem.AppDataDirectory, "media", session.Training ? "training" : $"{session.Tokens!.CompanyId}-{session.Tokens.UserId}"); Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, id.ToString("N") + ".jpg");
            await using var source = await file.OpenReadAsync(); using var input = new MemoryStream(); await source.CopyToAsync(input);
            if (input.Length > 30 * 1024 * 1024) throw new FieldRuleException("Choose a photo under 30 MB.");
#if ANDROID
            var options = new Android.Graphics.BitmapFactory.Options { InJustDecodeBounds = true };
            var original = input.ToArray(); Android.Graphics.BitmapFactory.DecodeByteArray(original, 0, original.Length, options);
            int sample = 1; while (Math.Max(options.OutWidth, options.OutHeight) / sample > 1600) sample *= 2;
            options.InJustDecodeBounds = false; options.InSampleSize = sample;
            using var bitmap = Android.Graphics.BitmapFactory.DecodeByteArray(original, 0, original.Length, options) ?? throw new FieldRuleException("Unable to read this photo.");
            var originalPath = Path.Combine(FileSystem.CacheDirectory, id.ToString("N") + ".capture");
            await File.WriteAllBytesAsync(originalPath, original);
            using var matrix = new Android.Graphics.Matrix();
            try
            {
                using var exif = new Android.Media.ExifInterface(originalPath);
                int orientation = exif.GetAttributeInt(Android.Media.ExifInterface.TagOrientation!, 1);
                switch (orientation)
                {
                    case 2: matrix.SetScale(-1, 1); break;
                    case 3: matrix.PostRotate(180); break;
                    case 4: matrix.SetScale(1, -1); break;
                    case 5: matrix.PostRotate(90); matrix.PostScale(-1, 1); break;
                    case 6: matrix.PostRotate(90); break;
                    case 7: matrix.PostRotate(270); matrix.PostScale(-1, 1); break;
                    case 8: matrix.PostRotate(270); break;
                }
                var normalized = Android.Graphics.Bitmap.CreateBitmap(bitmap, 0, 0, bitmap.Width, bitmap.Height, matrix, true)!;
                try { await using var target = File.Create(path); await normalized.CompressAsync(Android.Graphics.Bitmap.CompressFormat.Jpeg!, 82, target); }
                finally { if (!ReferenceEquals(normalized, bitmap)) normalized.Dispose(); }
            }
            finally { File.Delete(originalPath); }
#else
            // The server normalizes and strips metadata on every upload; iOS adapter is a future build target.
            await File.WriteAllBytesAsync(path, input.ToArray());
#endif
            var bytes = await File.ReadAllBytesAsync(path); var hash = Convert.ToHexString(SHA256.HashData(bytes));
            session.Save(new() { OperationId = id, JobPublicId = jobId, Type = OperationType.Photo, Photo = new(id, category.SelectedItem as string ?? "Other", hash, DateTimeOffset.UtcNow) }, path);
            await Navigation.PopAsync();
        }
        stack.Add(FieldUi.Button("TAKE PHOTO", () => Capture(true))); stack.Add(FieldUi.Button("CHOOSE PHOTO", () => Capture(false), true)); Content = new ScrollView { Content = stack };
    }
}
public sealed class SignatureCanvas : IDrawable
{
    public List<List<StrokePoint>> Strokes { get; } = [];
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = Colors.White; canvas.FillRectangle(dirtyRect); canvas.StrokeColor = FieldUi.Ink; canvas.StrokeSize = 3;
        foreach (var stroke in Strokes)
            for (int i = 1; i < stroke.Count; i++) canvas.DrawLine(stroke[i - 1].X * dirtyRect.Width, stroke[i - 1].Y * dirtyRect.Height, stroke[i].X * dirtyRect.Width, stroke[i].Y * dirtyRect.Height);
    }
}
public sealed class SignOffPage : ContentPage
{
    public SignOffPage(MobileSession session, JobSnapshot job)
    {
        Title = "Customer acknowledgement"; var stack = FieldUi.Stack(); stack.Add(FieldUi.Text("CUSTOMER SIGN-OFF", 24, true));
        stack.Add(FieldUi.Text($"I acknowledge the service visit and recorded work report for {job.Number}. This acknowledgement does not alter invoice or payment terms.", 16));
        stack.Add(FieldUi.Text($"Diagnosis: {job.Report.Diagnosis}\nWork: {job.Report.WorkPerformed}\nTest: {job.Report.TestResults}\nParts: {string.Join(", ", job.Report.Parts.Select(p => p.Name + " × " + p.Quantity))}", 14));
        var name = FieldUi.Entry("Customer name"); name.MaxLength = 150; stack.Add(name);
        var drawing = new SignatureCanvas(); var view = new GraphicsView { Drawable = drawing, HeightRequest = 220, BackgroundColor = Colors.White };
        void Add(PointF point)
        {
            if (drawing.Strokes.Count == 0 || drawing.Strokes.Sum(s => s.Count) >= 10000) return;
            drawing.Strokes[^1].Add(new(Math.Clamp(point.X / Math.Max(1, (float)view.Width), 0, 1), Math.Clamp(point.Y / Math.Max(1, (float)view.Height), 0, 1))); view.Invalidate();
        }
        view.StartInteraction += (_, e) => { if (drawing.Strokes.Count < 100) { drawing.Strokes.Add([]); if (e.Touches.Length > 0) Add(e.Touches[0]); } };
        view.DragInteraction += (_, e) => { if (e.Touches.Length > 0) Add(e.Touches[0]); };
        stack.Add(new Border { Content = view, Stroke = Colors.Gray });
        stack.Add(FieldUi.Button("CLEAR SIGNATURE", () => { drawing.Strokes.Clear(); view.Invalidate(); return Task.CompletedTask; }, true));
        var unavailable = new Switch(); var label = new HorizontalStackLayout { Spacing = 10 }; label.Add(unavailable); label.Add(FieldUi.Text("Customer unavailable / refused", 14)); stack.Add(label);
        var reason = new Editor { Placeholder = "Exception reason (required if no signature)", MinimumHeightRequest = 90, MaxLength = 1000 }; stack.Add(reason);
        stack.Add(FieldUi.Button("SAVE ACKNOWLEDGEMENT", async () =>
        {
            if (unavailable.IsToggled && string.IsNullOrWhiteSpace(reason.Text)) throw new FieldRuleException("Enter the reason the customer could not sign.");
            session.Save(new() { JobPublicId = job.PublicId, Type = OperationType.Signature, SignOff = new(name.Text ?? "", FieldRules.ContentHash(job.Report), unavailable.IsToggled ? [] : drawing.Strokes, unavailable.IsToggled ? reason.Text ?? "" : "", DateTimeOffset.UtcNow) }); await Navigation.PopAsync();
        }));
        Content = new ScrollView { Content = stack };
    }
}
public sealed class ScanPage : ContentPage
{
    public ScanPage(MobileSession session)
    {
        Title = "Find a job"; var stack = FieldUi.Stack(); stack.Add(FieldUi.Text("QUICK JOB LOOKUP", 25, true));
        stack.Add(FieldUi.Text("Enter the job number to open cached assigned work. Equipment camera scanning and richer asset history follow the first field-workflow MVP.", 15));
        var code = FieldUi.Entry("Job number"); stack.Add(code);
        stack.Add(FieldUi.Button("OPEN ASSIGNED JOB", async () =>
        {
            var job = session.Workspace!.Jobs().SingleOrDefault(j => j.Number.Equals(code.Text?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (job is null) throw new FieldRuleException("No cached assigned job matches this number. Sync to receive the latest jobs.");
            await Navigation.PushAsync(new JobPage(session, job.PublicId));
        })); Content = new ScrollView { Content = stack };
    }
}
public sealed class SyncPage : ContentPage
{
    private readonly MobileSession _session; private readonly VerticalStackLayout _stack = FieldUi.Stack();
    public SyncPage(MobileSession session) { _session = session; Title = "Synchronization"; Content = new ScrollView { Content = _stack }; }
    protected override void OnAppearing() { base.OnAppearing(); _session.Changed -= Refresh; _session.Changed += Refresh; Refresh(); }
    protected override void OnDisappearing() { _session.Changed -= Refresh; base.OnDisappearing(); }
    private void Refresh()
    {
        if (_session.Workspace is null) return;
        _stack.Clear(); _stack.Add(FieldUi.Text("YOUR WORK IS SAVED", 24, true)); _stack.Add(FieldUi.Text(_session.Status));
        _stack.Add(FieldUi.Button("SYNC NOW", _session.Synchronize));
        foreach (var row in _session.Workspace.Queue().Where(q => q.State != QueueState.Synced))
        {
            var op = System.Text.Json.JsonSerializer.Deserialize<MobileOperation>(row.PayloadJson)!;
            _stack.Add(FieldUi.Text(op.Type + " · " + row.State, 18, true)); _stack.Add(FieldUi.Text(op.CreatedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm") + " · Attempts " + row.RetryCount, 13));
            if (!string.IsNullOrEmpty(row.Error)) _stack.Add(FieldUi.Text(row.Error, 14));
            if (row.State is QueueState.Conflict or QueueState.Failed) _stack.Add(FieldUi.Text("Saved for office review. Export your pending work from Profile. No newer office data will be overwritten.", 14));
        }
    }
}
public sealed class ProfilePage : ContentPage
{
    public ProfilePage(MobileSession session)
    {
        Title = "Engineer profile"; var stack = FieldUi.Stack(); stack.Add(FieldUi.Text(session.Training ? "TRAINING ENGINEER" : session.Tokens!.DisplayName, 24, true));
        stack.Add(FieldUi.Text("ServoERP Engineer " + AppInfo.Current.VersionString + "\nLight theme · en-IN · dd/MM/yyyy\nDevice " + session.DeviceId, 14));
        stack.Add(FieldUi.Button("EXPORT PENDING WORK FOR OFFICE REVIEW", async () =>
        {
            var path = Path.Combine(FileSystem.CacheDirectory, "ServoERP-engineer-pending.json");
            var data = session.Workspace!.Queue().Where(q => q.State != QueueState.Synced).Select(q => new { q.Id, q.JobId, q.PayloadJson, q.State, q.Error });
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            await Share.Default.RequestAsync(new ShareFileRequest { Title = "Share saved work with your office", File = new ShareFile(path) });
        }, true));
        stack.Add(FieldUi.Text("Export contains customer and service information. Choose an approved office destination. Unsynced photos remain on this device.", 13));
        stack.Add(FieldUi.Button("LOG OUT", async () =>
        {
            int pending = session.Workspace!.Queue().Count(q => q.State != QueueState.Synced);
            if (!await DisplayAlert("Log out?", $"{pending} changes are waiting. Saved work remains on this device for this account. Logging out revokes the online session.", "LOG OUT", "BACK")) return;
            await session.Logout(); Application.Current!.Windows[0].Page = new NavigationPage(new LoginPage(session));
        })); Content = new ScrollView { Content = stack };
    }
}

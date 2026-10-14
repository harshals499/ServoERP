using System.Net.Http.Json;
using System.Text.Json;
using Serilog;

namespace ServoERP.Engineer.Mobile;

public sealed class MobileSession
{
    public LocalWorkspace? Workspace { get; private set; }
    public SessionTokens? Tokens { get; private set; }
    public HttpClient? Http { get; private set; }
    public SyncService? Sync { get; private set; }
    public bool Training { get; private set; }
    public bool Busy { get; private set; }
    public string Status { get; private set; } = "Sign in to your office server";
    public event Action? Changed;
    public string DeviceId { get; } = Preferences.Default.Get("engineer-device", Guid.NewGuid().ToString());
    public MobileSession()
    {
        Preferences.Default.Set("engineer-device", DeviceId);
        Log.Logger = new LoggerConfiguration().WriteTo.File(Path.Combine(FileSystem.AppDataDirectory, "logs", "engineer-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7).CreateLogger();
        Connectivity.Current.ConnectivityChanged += (_, _) => _ = Synchronize();
    }
    public async Task Login(string server, string username, string password, string company)
    {
        if (!Uri.TryCreate(server.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !int.TryParse(company, out int companyId) || companyId <= 0)
            throw new FieldRuleException("Enter your office HTTPS address and company number.");
        Http = new HttpClient { BaseAddress = new Uri(uri.GetLeftPart(UriPartial.Authority) + "/api/v1/mobile/"), Timeout = TimeSpan.FromSeconds(45) };
        using var response = await Http.PostAsJsonAsync("auth/login", new LoginRequest(username.Trim(), password, companyId, DeviceId));
        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadFromJsonAsync<ApiError>(); throw new FieldRuleException(message?.Error ?? "Unable to sign in. Check your details or contact the office.");
        }
        Tokens = (await response.Content.ReadFromJsonAsync<SessionTokens>())!;
        Preferences.Default.Set("engineer-server", uri.GetLeftPart(UriPartial.Authority));
        await SaveTokens(Tokens); OpenWorkspace();
        await Synchronize();
    }
    public async Task<bool> Restore()
    {
        string? saved = await SecureStorage.Default.GetAsync("engineer-session");
        if (string.IsNullOrEmpty(saved)) return false;
        Tokens = JsonSerializer.Deserialize<SessionTokens>(saved);
        var server = Preferences.Default.Get("engineer-server", "");
        if (Tokens is null || !Uri.TryCreate(server, UriKind.Absolute, out var uri) || uri.Scheme != "https") return false;
        Http = new HttpClient { BaseAddress = new Uri(server + "/api/v1/mobile/"), Timeout = TimeSpan.FromSeconds(45) }; OpenWorkspace(); return true;
    }
    private void OpenWorkspace()
    {
        Training = false; Workspace?.Dispose();
        var origin = Http!.BaseAddress!.GetLeftPart(UriPartial.Authority);
        var hostHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(origin)))[..16];
        Workspace = new LocalWorkspace(Path.Combine(FileSystem.AppDataDirectory, $"workspace-{hostHash}-{Tokens!.CompanyId}-{Tokens.UserId}.db3"));
        Sync = new SyncService(Workspace, new EngineerTransport(Http, () => Task.FromResult<SessionTokens?>(Tokens), SaveTokens));
        Status = "Your cached work is available offline";
    }
    private async Task SaveTokens(SessionTokens tokens)
    { Tokens = tokens; await SecureStorage.Default.SetAsync("engineer-session", JsonSerializer.Serialize(tokens)); }
    public void OpenTraining()
    {
        Training = true; Tokens = null; Sync = null; Workspace?.Dispose();
        Workspace = new LocalWorkspace(Path.Combine(FileSystem.AppDataDirectory, "training.db3"));
        if (Workspace.Jobs().Count == 0)
        {
            Workspace.MergeSnapshots([new JobSnapshot { PublicId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), OfficeVersion = 1, Number = "TRAINING-001", Customer = "Training customer", Site = "Plant room", Address = "Training site", Priority = "Urgent", Fault = "AHU-04: cooling failure — sample data", ScheduledDate = DateTime.Today.AddHours(9), OfficeStatus = "Assigned", Report = new FieldReport { Checklist = [new() { Id = 1, Text = "Supply voltage checked" }, new() { Id = 2, Text = "Connections secure" }, new() { Id = 3, Text = "Airflow and temperature checked" }] } }]);
            Workspace.SaveValue("stock", new List<PartStock> { new() { ItemId = 1, Name = "CT-440 Contactor", Available = 4 } });
        }
        Status = "TRAINING · Saved locally · No server writes";
    }
    public async Task Synchronize()
    {
        if (Training || Sync is null || Busy) return;
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) { Status = "OFFLINE · Your work is saved"; Notify(); return; }
        Busy = true; Status = "Synchronizing…"; Notify();
        try { await Sync.Run(); Status = "Connected · Last sync " + DateTime.Now.ToString("HH:mm"); }
        catch (Exception ex) { Log.Warning(ex, "Engineer sync unavailable"); Status = ex is TransportException te ? te.Message : "Unable to sync. Your work is saved and will retry."; }
        finally { Busy = false; Notify(); }
    }
    public void Save(MobileOperation op, string photoPath = "")
    { op.DeviceId = DeviceId; Workspace!.Enqueue(op, Tokens?.UserId ?? 0, photoPath); Notify(); _ = Synchronize(); }
    private void Notify() => MainThread.BeginInvokeOnMainThread(() => Changed?.Invoke());
    public async Task Logout()
    {
        if (Busy) throw new FieldRuleException("Wait for synchronization to finish before logging out. Your work is saved.");
        if (!Training && Http is not null && Tokens is not null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "auth/logout"); request.Headers.Authorization = new("Bearer", Tokens.AccessToken);
            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) throw new FieldRuleException("Connect and sign in again to revoke your session before logging out. Your saved work is retained.");
            SecureStorage.Default.Remove("engineer-session");
        }
        Workspace?.Dispose(); Workspace = null; Tokens = null; Sync = null; Http?.Dispose(); Http = null;
    }
}

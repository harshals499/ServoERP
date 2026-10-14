using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ServoERP.Engineer;

public class TransportException(HttpStatusCode status, string message) : Exception(message) { public HttpStatusCode Status { get; } = status; }
public interface IEngineerTransport
{
    Task<List<JobSnapshot>> Jobs(CancellationToken ct);
    Task<List<PartStock>> Stock(CancellationToken ct);
    Task<UploadReceipt> Upload(Guid job, Guid operationId, string path, CancellationToken ct);
    Task<OperationReceipt> Submit(MobileOperation op, UploadReceipt? upload, CancellationToken ct);
}
public sealed class SyncService(LocalWorkspace workspace, IEngineerTransport transport)
{
    private readonly SemaphoreSlim _sync = new(1, 1);
    public async Task Run(CancellationToken ct = default)
    {
        if (!await _sync.WaitAsync(0, ct)) return;
        try
        {
            var blocked = new HashSet<string>();
            foreach (var row in workspace.Queue().Where(p => p.State != QueueState.Synced))
            {
                ct.ThrowIfCancellationRequested();
                if (blocked.Contains(row.JobId)) continue;
                if (row.State is QueueState.Conflict or QueueState.Failed || row.NextAttemptTicks > DateTimeOffset.UtcNow.UtcTicks) { blocked.Add(row.JobId); continue; }
                row.State = QueueState.Syncing; workspace.Update(row);
                try
                {
                    var op = JsonSerializer.Deserialize<MobileOperation>(row.PayloadJson)!;
                    UploadReceipt? upload = string.IsNullOrEmpty(row.UploadJson) ? null : JsonSerializer.Deserialize<UploadReceipt>(row.UploadJson);
                    if (!string.IsNullOrEmpty(row.LocalPhotoPath) && upload is null)
                    {
                        upload = await transport.Upload(op.JobPublicId, op.OperationId, row.LocalPhotoPath, ct);
                        row.UploadJson = JsonSerializer.Serialize(upload); workspace.Update(row);
                    }
                    workspace.Acknowledge(row, await transport.Submit(op, upload, ct));
                }
                catch (OperationCanceledException) { row.State = QueueState.Pending; workspace.Update(row); throw; }
                catch (TransportException ex) when (ex.Status == HttpStatusCode.Conflict || ex.Status == HttpStatusCode.Forbidden || ex.Status == HttpStatusCode.NotFound)
                { row.State = QueueState.Conflict; row.Error = ex.Message; workspace.Update(row); blocked.Add(row.JobId); }
                catch (TransportException ex) when (ex.Status == HttpStatusCode.BadRequest || ex.Status == HttpStatusCode.UnprocessableEntity)
                { row.State = QueueState.Failed; row.Error = ex.Message; workspace.Update(row); blocked.Add(row.JobId); }
                catch (Exception ex) when (ex is HttpRequestException or IOException or TransportException)
                {
                    row.State = QueueState.Pending; row.RetryCount++; row.Error = "Unable to sync right now. Your work is saved on this device.";
                    row.NextAttemptTicks = DateTimeOffset.UtcNow.AddSeconds(Math.Min(300, Math.Pow(2, Math.Min(row.RetryCount, 8))) + Random.Shared.Next(1, 5)).UtcTicks;
                    workspace.Update(row); blocked.Add(row.JobId);
                    if (ex is TransportException { Status: HttpStatusCode.Unauthorized }) break;
                }
            }
            workspace.MergeSnapshots(await transport.Jobs(ct)); workspace.SaveValue("stock", await transport.Stock(ct));
        }
        finally { _sync.Release(); }
    }
}
public sealed class EngineerTransport(HttpClient http, Func<Task<SessionTokens?>> readTokens, Func<SessionTokens, Task> saveTokens) : IEngineerTransport
{
    private readonly SemaphoreSlim _refresh = new(1, 1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public Task<List<JobSnapshot>> Jobs(CancellationToken ct) => Get<List<JobSnapshot>>("jobs", ct);
    public Task<List<PartStock>> Stock(CancellationToken ct) => Get<List<PartStock>>("inventory/van", ct);
    private async Task<T> Get<T>(string path, CancellationToken ct)
    { using var response = await Send(() => new(HttpMethod.Get, path), ct); return (await response.Content.ReadFromJsonAsync<T>(Json, ct))!; }
    public async Task<UploadReceipt> Upload(Guid job, Guid operationId, string path, CancellationToken ct)
    {
        using var response = await Send(() =>
        {
            var multipart = new MultipartFormDataContent(); var content = new StreamContent(File.OpenRead(path)); content.Headers.ContentType = new("image/jpeg"); multipart.Add(content, "file", "photo.jpg");
            return new(HttpMethod.Post, $"jobs/{job}/attachments/{operationId}") { Content = multipart };
        }, ct);
        return (await response.Content.ReadFromJsonAsync<UploadReceipt>(Json, ct))!;
    }
    public async Task<OperationReceipt> Submit(MobileOperation op, UploadReceipt? upload, CancellationToken ct)
    {
        using var response = await Send(() => new(HttpMethod.Post, "sync") { Content = JsonContent.Create(new SyncSubmission(op, upload)) }, ct);
        return (await response.Content.ReadFromJsonAsync<OperationReceipt>(Json, ct))!;
    }
    private async Task<HttpResponseMessage> Send(Func<HttpRequestMessage> factory, CancellationToken ct)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            var tokens = await readTokens() ?? throw new TransportException(HttpStatusCode.Unauthorized, "Sign in to synchronize your saved work.");
            using var request = factory(); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
            var response = await http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
            {
                response.Dispose(); await _refresh.WaitAsync(ct);
                try
                {
                    var current = await readTokens();
                    if (current?.AccessToken == tokens.AccessToken)
                    {
                        using var refresh = await http.PostAsJsonAsync("auth/refresh", new RefreshRequest(tokens.RefreshToken), ct);
                        if (!refresh.IsSuccessStatusCode) throw new TransportException(HttpStatusCode.Unauthorized, "Your session has expired. Sign in again; your work is saved.");
                        await saveTokens((await refresh.Content.ReadFromJsonAsync<SessionTokens>(Json, ct))!);
                    }
                }
                finally { _refresh.Release(); }
                continue;
            }
            if (response.IsSuccessStatusCode) return response;
            string message = "Unable to sync right now. Your work is saved.";
            try { message = (await response.Content.ReadFromJsonAsync<ApiError>(Json, ct))?.Error ?? message; } catch (JsonException) { }
            var status = response.StatusCode; response.Dispose(); throw new TransportException(status, message);
        }
        throw new TransportException(HttpStatusCode.Unauthorized, "Sign in again to synchronize your saved work.");
    }
}
public record SyncSubmission(MobileOperation Operation, UploadReceipt? Upload);

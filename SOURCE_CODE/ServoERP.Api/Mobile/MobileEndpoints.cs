using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.RateLimiting;
using Dapper;
using FluentValidation;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.RateLimiting;
using ServoERP.Engineer;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ServoERP.Api.Mobile;

public record MobileSubmission(MobileOperation Operation, UploadReceipt? Upload);
public sealed class MobileSubmissionValidator : AbstractValidator<MobileSubmission>
{
    public MobileSubmissionValidator()
    {
        RuleFor(s => s.Operation).NotNull();
        When(s => s.Operation is not null, () =>
        {
            RuleFor(s => s.Operation.OperationId).NotEmpty(); RuleFor(s => s.Operation.JobPublicId).NotEmpty();
            RuleFor(s => s.Operation.DeviceId).NotEmpty().MaximumLength(128);
            RuleFor(s => s.Operation.Type).IsInEnum(); RuleFor(s => s.Operation.PayloadVersion).Equal(1);
            RuleFor(s => s.Operation.Diagnosis).NotNull().MaximumLength(8000); RuleFor(s => s.Operation.WorkPerformed).NotNull().MaximumLength(8000);
            RuleFor(s => s.Operation.TestResults).NotNull().MaximumLength(8000); RuleFor(s => s.Operation.Notes).NotNull().MaximumLength(8000);
        });
    }
}
public static class MobileEndpoints
{
    public static bool Enabled(IConfiguration configuration) => configuration.GetValue<bool>("ServoERP:EngineerMobileEnabled");
    public static void AddEngineerMobile(this WebApplicationBuilder builder, string connectionString)
    {
        builder.Services.AddSingleton(new MobileAuthService(connectionString));
        builder.Services.AddSingleton(new MobileJobService(connectionString));
        builder.Services.AddSingleton<MobileSubmissionValidator>();
        builder.Services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = 429;
            o.AddPolicy("engineer-auth", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 15, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 12 * 1024 * 1024);
    }
    public static async Task Middleware(HttpContext http, Func<Task> next, IConfiguration configuration)
    {
        if (!http.Request.Path.StartsWithSegments("/api/v1/mobile")) { await next(); return; }
        try
        {
            if (!Enabled(configuration)) throw new MobileHttpException(404, "Engineer mobile access is not enabled on this server.");
            if (!http.Request.IsHttps) throw new MobileHttpException(400, "Use a secure HTTPS connection to ServoERP.");
            var path = http.Request.Path.Value!;
            if (path is not ("/api/v1/mobile/auth/login" or "/api/v1/mobile/auth/refresh"))
            {
                var token = Bearer(http);
                http.Items["EngineerIdentity"] = await http.RequestServices.GetRequiredService<MobileAuthService>().Authenticate(token, http.RequestAborted);
            }
            await next();
        }
        catch (MobileHttpException ex) { await Error(http, ex.Status, ex.Message); }
        catch (FieldRuleException ex) { await Error(http, 422, ex.Message); }
        catch (BadHttpRequestException) { await Error(http, 400, "Check the information and attachment size before trying again."); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("EngineerMobile").LogError(ex, "Mobile request failed {CorrelationId}", http.TraceIdentifier);
            await Error(http, 500, "Unable to sync right now. Your work is saved on this device and can be retried.");
        }
    }
    private static Task Error(HttpContext http, int status, string message)
    { http.Response.StatusCode = status; return http.Response.WriteAsJsonAsync(new ApiError(message, http.TraceIdentifier)); }
    private static string Bearer(HttpContext http)
    {
        string value = http.Request.Headers.Authorization.ToString();
        if (!value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) throw new MobileHttpException(401, "Sign in to access your assigned jobs.");
        return value[7..];
    }
    private static EngineerIdentity Identity(HttpContext http) => (EngineerIdentity)http.Items["EngineerIdentity"]!;
    public static void MapEngineerMobile(this WebApplication app, string connectionString)
    {
        var group = app.MapGroup("/api/v1/mobile");
        group.MapPost("/auth/login", async (LoginRequest request, MobileAuthService auth, CancellationToken ct) => Results.Ok(await auth.Login(request, ct))).RequireRateLimiting("engineer-auth");
        group.MapPost("/auth/refresh", async (RefreshRequest request, MobileAuthService auth, CancellationToken ct) => Results.Ok(await auth.Refresh(request, ct))).RequireRateLimiting("engineer-auth");
        group.MapPost("/auth/logout", async (HttpContext http, MobileAuthService auth, CancellationToken ct) => { await auth.Logout(Bearer(http), ct); return Results.NoContent(); });
        group.MapGet("/jobs", async (HttpContext http, MobileJobService jobs, CancellationToken ct) => Results.Ok(await jobs.Jobs(Identity(http), ct)));
        group.MapGet("/jobs/today", async (HttpContext http, MobileJobService jobs, CancellationToken ct) => Results.Ok((await jobs.Jobs(Identity(http), ct)).Where(j => j.ScheduledDate.Date <= DateTime.Today && j.Report.State != FieldState.Completed && j.OfficeStatus is not ("Closed" or "Invoiced" or "Cancelled"))));
        group.MapGet("/jobs/{id:guid}", async (Guid id, HttpContext http, MobileJobService jobs, CancellationToken ct) => { var job = (await jobs.Jobs(Identity(http), ct)).SingleOrDefault(j => j.PublicId == id); return job is null ? Results.NotFound(new ApiError("This job is not assigned to your account.")) : Results.Ok(job); });
        group.MapGet("/inventory/van", async (HttpContext http, MobileJobService jobs, CancellationToken ct) => Results.Ok(await jobs.Stock(Identity(http), ct)));
        group.MapPost("/sync", async (MobileSubmission request, HttpContext http, MobileJobService jobs, MobileSubmissionValidator validator, CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(request, ct);
            if (!validation.IsValid) throw new MobileHttpException(400, "Check the job action and report fields before synchronizing.");
            return Results.Ok(await jobs.Submit(Identity(http), request.Operation, request.Upload, ct));
        });
        group.MapPost("/jobs/{job:guid}/attachments/{operation:guid}", async (Guid job, Guid operation, HttpContext http, MobileJobService jobs, CancellationToken ct) =>
        {
            if (operation == Guid.Empty) throw new MobileHttpException(400, "An attachment operation ID is required.");
            var identity = Identity(http); var jobId = await jobs.AuthorizedJob(identity, job, ct);
            if (!http.Request.HasFormContentType || http.Request.ContentLength > 11 * 1024 * 1024) throw new MobileHttpException(400, "Choose a JPEG or PNG photo under 10 MB.");
            var form = await http.Request.ReadFormAsync(ct); var file = form.Files.GetFile("file");
            if (file is null || file.Length is <= 0 or > 10 * 1024 * 1024) throw new MobileHttpException(400, "Choose a JPEG or PNG photo under 10 MB.");
            using var stream = new MemoryStream(); await file.CopyToAsync(stream, ct); var bytes = stream.ToArray();
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            var format = Image.DetectFormat(bytes); if (format.Name is not ("JPEG" or "PNG")) throw new MobileHttpException(400, "Only JPEG and PNG photos are supported.");
            var info = Image.Identify(bytes); if (info.Width > 10000 || info.Height > 10000 || (long)info.Width * info.Height > 30_000_000) throw new MobileHttpException(400, "This photo is too large. Choose a smaller image.");
            var root = app.Configuration["ServoERP:EngineerMediaDirectory"];
            if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root)) throw new InvalidOperationException("Configure an absolute private EngineerMediaDirectory.");
            Directory.CreateDirectory(root);
            await using var c = new SqlConnection(connectionString); await c.OpenAsync(ct); await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
            var existing = await c.QuerySingleOrDefaultAsync<UploadReceipt>(new CommandDefinition("SELECT AttachmentId,SourceHash Hash FROM EngineerAttachments WITH(UPDLOCK,HOLDLOCK) WHERE CompanyId=@CompanyId AND UserId=@UserId AND OperationId=@operation AND JobId=@jobId", new { identity.CompanyId, identity.UserId, operation, jobId }, tx, cancellationToken: ct));
            if (existing is not null)
            {
                if (existing.Hash != hash) throw new MobileHttpException(409, "This attachment operation ID was already used for another photo.");
                await tx.CommitAsync(ct); return Results.Ok(existing);
            }
            var id = operation; var storageName = Guid.NewGuid().ToString("N") + ".jpg"; var target = Path.Combine(root, storageName);
            using var image = Image.Load(bytes); image.Mutate(x => x.AutoOrient().Resize(new ResizeOptions { Size = new Size(1600, 1600), Mode = ResizeMode.Max }));
            image.Metadata.ExifProfile = null; image.Metadata.IptcProfile = null; image.Metadata.XmpProfile = null;
            await image.SaveAsJpegAsync(target, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 82 }, ct);
            try
            {
                await c.ExecuteAsync(new CommandDefinition("INSERT EngineerAttachments(AttachmentId,OperationId,CompanyId,UserId,JobId,SourceHash,StorageName,CreatedUtc) VALUES(@id,@operation,@CompanyId,@UserId,@jobId,@hash,@storageName,SYSUTCDATETIME())", new { id, operation, identity.CompanyId, identity.UserId, jobId, hash, storageName }, tx, cancellationToken: ct));
                await tx.CommitAsync(ct);
            }
            catch { File.Delete(target); throw; }
            return Results.Ok(new UploadReceipt(id, hash));
        });
    }
}

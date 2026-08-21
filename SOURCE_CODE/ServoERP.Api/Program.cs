using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration.GetSection("ServoERP");
var connectionString = Environment.GetEnvironmentVariable("SERVOERP_DATABASE_CONNECTION") ?? config["DatabaseConnectionString"];
var apiKey = Environment.GetEnvironmentVariable("SERVOERP_API_KEY") ?? config["ApiKey"];
if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(apiKey))
    throw new InvalidOperationException("Set SERVOERP_DATABASE_CONNECTION and SERVOERP_API_KEY before starting ServoERP.Api.");

builder.Services.AddSingleton(new ApiOptions(connectionString, apiKey));
builder.Services.AddSingleton<BusinessWriteService>();
builder.Services.AddHealthChecks().AddCheck<SqlHealthCheck>("sql-server");
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    var origins = (Environment.GetEnvironmentVariable("SERVOERP_ALLOWED_ORIGINS") ?? config["AllowedOrigins"] ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();
app.UseHttpsRedirection();
app.UseCors();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") && !ApiKeyAuth.IsAuthorized(context, apiKey))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "A valid X-ServoERP-Api-Key is required." });
        return;
    }
    if (context.Request.Path.StartsWithSegments("/api") && !await CompanyContext.TryEstablishAsync(context, connectionString))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { error = "Company authorization failed. Operation blocked." });
        return;
    }
    await next();
});

app.MapGet("/health", () => Results.Ok(new { service = "ServoERP.Api", status = "running" }));
app.MapGet("/api/v1/health", async (ApiOptions options, CancellationToken ct) =>
{
    await using var connection = new SqlConnection(options.ConnectionString);
    await connection.OpenAsync(ct);
    await using var command = new SqlCommand("SELECT CAST(DB_NAME() AS nvarchar(128)); SELECT SettingValue FROM CompanySettings WHERE SettingKey='CompanyIsolationSchemaVersion';", connection);
    await using var reader = await command.ExecuteReaderAsync(ct);
    await reader.ReadAsync(ct);
    string databaseName = reader.IsDBNull(0) ? "HVAC_PRO" : reader.GetString(0);
    await reader.NextResultAsync(ct);
    string schemaVersion = await reader.ReadAsync(ct) && !reader.IsDBNull(0) ? reader.GetString(0) : "0";
    return Results.Ok(new { api = "online", database = "connected", databaseName, server = connection.DataSource, version = "1.1.435.0", companyIsolationSchemaVersion = schemaVersion, minimumDesktopVersion = "1.1.435.0" });
});
app.MapGet("/api/v1/companies", async (HttpContext http, ApiOptions options, CancellationToken ct) =>
{
    var context = CompanyContext.Require(http);
    await using var connection = new SqlConnection(options.ConnectionString);
    await connection.OpenAsync(ct);
    await using var command = new SqlCommand(@"SELECT c.CompanyId, c.CompanyCode, c.CompanyName, ISNULL(r.RoleName, 'User')
FROM UserCompanies uc
JOIN Companies c ON c.CompanyId=uc.CompanyId AND c.IsActive=1
LEFT JOIN Roles r ON r.RoleId=uc.RoleId
WHERE uc.UserId=@user AND uc.IsActive=1
ORDER BY c.CompanyName", connection);
    command.Parameters.AddWithValue("@user", context.UserId);
    var companies = new List<object>();
    await using var reader = await command.ExecuteReaderAsync(ct);
    while (await reader.ReadAsync(ct))
        companies.Add(new { companyId = reader.GetInt32(0), companyCode = reader.IsDBNull(1) ? string.Empty : reader.GetString(1), companyName = reader.GetString(2), role = reader.IsDBNull(3) ? "User" : reader.GetString(3) });
    return Results.Ok(companies);
});

app.MapPost("/api/v1/payments", async (HttpContext http, RecordPaymentRequest request, BusinessWriteService service, CancellationToken ct) =>
    Results.Ok(await service.RecordPaymentAsync(request, CompanyContext.Require(http), ct)));
app.MapPost("/api/v1/inventory/{itemId:int}/movements", async (HttpContext http, int itemId, RecordStockMovementRequest request, BusinessWriteService service, CancellationToken ct) =>
    Results.Ok(await service.RecordStockMovementAsync(itemId, request, CompanyContext.Require(http), ct)));
app.MapPost("/api/v1/purchase-orders/{poId:int}/receive", async (HttpContext http, int poId, BusinessWriteService service, CancellationToken ct) =>
    Results.Ok(await service.ReceivePurchaseOrderAsync(poId, CompanyContext.Require(http), ct)));

app.Run();

sealed record ApiOptions(string ConnectionString, string ApiKey);
sealed record RecordPaymentRequest(int InvoiceId, decimal AmountPaid, DateTime PaymentDate, string? PaymentMode, string? ReferenceNumber, string? Notes, string? RequestedBy);
sealed record RecordStockMovementRequest(decimal Quantity, string MovementType, string? FromLocation, string? ToLocation, string? ReferenceNumber, string? Notes, string? RequestedBy);

sealed record CompanyContext(int UserId, int CompanyId, Guid CorrelationId, string? IdempotencyKey)
{
    public static CompanyContext Require(HttpContext context) => (CompanyContext)context.Items["CompanyContext"]!;
    public static async Task<bool> TryEstablishAsync(HttpContext context, string connectionString)
    {
        if (!int.TryParse(context.Request.Headers["X-ServoERP-User-Id"], out var userId) || !int.TryParse(context.Request.Headers["X-ServoERP-Company-Id"], out var companyId) || !context.Request.Headers.TryGetValue("X-ServoERP-User-Token", out var token)) return false;
        var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(token.ToString()));
        await using var c = new SqlConnection(connectionString); await c.OpenAsync(context.RequestAborted);
        await using var cmd = new SqlCommand(@"SELECT COUNT(1) FROM ApiUserTokens t JOIN AppUsers u ON u.UserId=t.UserId AND u.IsActive=1 JOIN UserCompanies uc ON uc.UserId=t.UserId AND uc.CompanyId=@company AND uc.IsActive=1 JOIN Companies co ON co.CompanyId=uc.CompanyId AND co.IsActive=1 WHERE t.UserId=@user AND t.TokenHash=@hash AND t.IsActive=1 AND (t.ExpiresUtc IS NULL OR t.ExpiresUtc>SYSUTCDATETIME())", c);
        cmd.Parameters.AddWithValue("@company", companyId); cmd.Parameters.AddWithValue("@user", userId); cmd.Parameters.Add("@hash", System.Data.SqlDbType.VarBinary, 32).Value=tokenHash;
        if (Convert.ToInt32(await cmd.ExecuteScalarAsync(context.RequestAborted)) != 1) return false;
        context.Items["CompanyContext"] = new CompanyContext(userId, companyId, Guid.NewGuid(), context.Request.Headers["X-ServoERP-Operation-Id"].ToString()); return true;
    }
}

static class ApiKeyAuth
{
    public static bool IsAuthorized(HttpContext context, string expected)
    {
        if (!context.Request.Headers.TryGetValue("X-ServoERP-Api-Key", out var supplied)) return false;
        var a = Encoding.UTF8.GetBytes(supplied.ToString());
        var b = Encoding.UTF8.GetBytes(expected);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}

sealed class SqlHealthCheck(ApiOptions options) : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try { await using var c = new SqlConnection(options.ConnectionString); await c.OpenAsync(cancellationToken); return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(); }
        catch (Exception ex) { return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("SQL Server is unavailable.", ex); }
    }
}

sealed class BusinessWriteService(ApiOptions options, ILogger<BusinessWriteService> logger)
{
    public async Task<object> RecordPaymentAsync(RecordPaymentRequest request, CompanyContext context, CancellationToken ct)
    {
        if (request.InvoiceId <= 0 || request.AmountPaid <= 0) throw new BadHttpRequestException("Invoice and a positive payment amount are required.");
        await using var c = new SqlConnection(options.ConnectionString); await c.OpenAsync(ct); await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        try
        {
            int? completedId = await BeginOperationAsync(c, tx, context, "Payments", $"{request.InvoiceId}|{request.AmountPaid}|{request.PaymentDate:O}|{request.ReferenceNumber}", ct);
            if (completedId.HasValue) { await tx.CommitAsync(ct); return new { paymentId = completedId.Value, status = "already-processed" }; }
            var invoice = await QueryInvoiceAsync(c, tx, request.InvoiceId, context.CompanyId, ct) ?? throw new BadHttpRequestException("Cross-company data mismatch. Operation blocked.");
            if (invoice.Balance <= 0.01m) throw new BadHttpRequestException("This invoice is already fully paid.");
            if (request.AmountPaid > invoice.Balance + 0.01m) throw new BadHttpRequestException($"Payment exceeds outstanding balance of {invoice.Balance:N2}.");
            if (!string.IsNullOrWhiteSpace(request.ReferenceNumber) && await ScalarAsync<int>(c, tx, "SELECT COUNT(1) FROM Payments WITH (UPDLOCK, HOLDLOCK) WHERE CompanyId=@company AND ReferenceNumber=@ref", ct, ("@company", context.CompanyId), ("@ref", request.ReferenceNumber.Trim())) > 0) throw new BadHttpRequestException("This payment reference or UTR already exists.");
            var number = "PAY-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            var id = await ScalarAsync<decimal>(c, tx, @"INSERT INTO Payments (CompanyId,PaymentNumber,InvoiceID,ClientID,AmountPaid,PaymentDate,PaymentMode,ReferenceNumber,Notes,CreatedDate,CreatedByUserId,CreatedByName) VALUES (@company,@number,@invoice,@client,@amount,@date,@mode,@reference,@notes,GETDATE(),@user,@by); SELECT CAST(SCOPE_IDENTITY() AS decimal(18,0));", ct, ("@company", context.CompanyId), ("@number", number), ("@invoice", request.InvoiceId), ("@client", invoice.ClientId), ("@amount", request.AmountPaid), ("@date", request.PaymentDate == default ? DateTime.Today : request.PaymentDate), ("@mode", request.PaymentMode ?? "Bank Transfer"), ("@reference", request.ReferenceNumber ?? string.Empty), ("@notes", request.Notes ?? string.Empty), ("@user", context.UserId), ("@by", request.RequestedBy ?? "API"));
            await ExecuteAsync(c, tx, "UPDATE Invoices SET PaidAmount=ISNULL(PaidAmount,0)+@amount, BalanceDue=CASE WHEN TotalAmount-ISNULL(PaidAmount,0)-@amount < 0 THEN 0 ELSE TotalAmount-ISNULL(PaidAmount,0)-@amount END, PaymentStatus=CASE WHEN TotalAmount-ISNULL(PaidAmount,0)-@amount <= 0.01 THEN 'Paid' ELSE 'Partial' END WHERE InvoiceID=@invoice AND CompanyId=@company", ct, ("@amount", request.AmountPaid), ("@invoice", request.InvoiceId), ("@company", context.CompanyId));
            await CompleteOperationAsync(c, tx, context, Convert.ToInt32(id), ct); await tx.CommitAsync(ct); logger.LogInformation("Payment {PaymentId} recorded via API for invoice {InvoiceId}", id, request.InvoiceId); return new { paymentId = Convert.ToInt32(id), paymentNumber = number, status = "recorded" };
        }
        catch { await tx.RollbackAsync(ct); throw; }
    }

    public async Task<object> RecordStockMovementAsync(int itemId, RecordStockMovementRequest request, CompanyContext context, CancellationToken ct)
    {
        if (itemId <= 0 || request.Quantity <= 0 || string.IsNullOrWhiteSpace(request.MovementType)) throw new BadHttpRequestException("Item, positive quantity, and movement type are required.");
        await using var c = new SqlConnection(options.ConnectionString); await c.OpenAsync(ct); await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        try
        {
            int? completedId = await BeginOperationAsync(c, tx, context, "Inventory", $"{itemId}|{request.Quantity}|{request.MovementType}|{request.ReferenceNumber}", ct);
            if (completedId.HasValue) { await tx.CommitAsync(ct); return new { movementId = completedId.Value, status = "already-processed" }; }
            var stockBefore = await QueryStockAsync(c, tx, itemId, context.CompanyId, ct) ?? throw new BadHttpRequestException("Cross-company data mismatch. Operation blocked.");
            var signed = request.MovementType.Equals("Issue", StringComparison.OrdinalIgnoreCase) || request.MovementType.Equals("Consume", StringComparison.OrdinalIgnoreCase) || request.MovementType.Equals("TransferOut", StringComparison.OrdinalIgnoreCase) || request.MovementType.Equals("Decrease", StringComparison.OrdinalIgnoreCase) ? -request.Quantity : request.Quantity;
            var after = stockBefore + signed; if (after < 0) throw new BadHttpRequestException($"Stock cannot go negative. Available: {stockBefore:0.###}.");
            await ExecuteAsync(c, tx, "UPDATE StockItems SET CurrentStock=@stock, LastUpdated=GETDATE() WHERE ItemID=@id AND CompanyId=@company", ct, ("@stock", after), ("@id", itemId), ("@company", context.CompanyId));
            var movementId = await ScalarAsync<decimal>(c, tx, "INSERT INTO StockMovements (CompanyId,ItemID,MovementType,Quantity,StockBefore,StockAfter,FromLocation,ToLocation,ReferenceNo,Notes,CreatedByUserId,CreatedByName,CreatedDate) VALUES (@company,@id,@type,@qty,@before,@after,@from,@to,@reference,@notes,@user,@by,GETDATE()); SELECT CAST(SCOPE_IDENTITY() AS decimal(18,0));", ct, ("@company", context.CompanyId), ("@id", itemId), ("@type", request.MovementType), ("@qty", request.Quantity), ("@before", stockBefore), ("@after", after), ("@from", request.FromLocation), ("@to", request.ToLocation), ("@reference", request.ReferenceNumber), ("@notes", request.Notes), ("@user", context.UserId), ("@by", request.RequestedBy ?? "API"));
            await CompleteOperationAsync(c, tx, context, Convert.ToInt32(movementId), ct); await tx.CommitAsync(ct); return new { movementId = Convert.ToInt32(movementId), stockBefore, stockAfter = after, status = "recorded" };
        }
        catch { await tx.RollbackAsync(ct); throw; }
    }

    public async Task<object> ReceivePurchaseOrderAsync(int poId, CompanyContext context, CancellationToken ct)
    {
        if (poId <= 0) throw new BadHttpRequestException("Purchase order id is required.");
        await using var c = new SqlConnection(options.ConnectionString); await c.OpenAsync(ct); await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        try
        {
            int? completedId = await BeginOperationAsync(c, tx, context, "PurchaseReceive", poId.ToString(System.Globalization.CultureInfo.InvariantCulture), ct);
            if (completedId.HasValue) { await tx.CommitAsync(ct); return new { purchaseOrderId = completedId.Value, status = "already-processed" }; }
            var status = await QueryPurchaseOrderAsync(c, tx, poId, context.CompanyId, ct) ?? throw new BadHttpRequestException("Cross-company data mismatch. Operation blocked.");
            if (status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)) throw new BadHttpRequestException("A cancelled purchase order cannot be received.");
            if (status.Equals("Fully Received", StringComparison.OrdinalIgnoreCase)) { await CompleteOperationAsync(c, tx, context, poId, ct); await tx.CommitAsync(ct); return new { purchaseOrderId = poId, status = "already-received" }; }
            await PostReceivedInventoryAsync(c, tx, poId, context.CompanyId, ct);
            await ExecuteAsync(c, tx, "UPDATE PurchaseOrders SET Status='Fully Received', PaidAmount=TotalAmount WHERE POID=@id AND CompanyId=@company", ct, ("@id", poId), ("@company", context.CompanyId));
            await CompleteOperationAsync(c, tx, context, poId, ct); await tx.CommitAsync(ct); return new { purchaseOrderId = poId, status = "received" };
        }
        catch { await tx.RollbackAsync(ct); throw; }
    }

    private static async Task<int?> BeginOperationAsync(SqlConnection c, SqlTransaction tx, CompanyContext context, string module, string requestSignature, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(context.IdempotencyKey) || context.IdempotencyKey.Length > 160)
            throw new BadHttpRequestException("A valid X-ServoERP-Operation-Id is required.");
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(module + "|" + requestSignature)));
        await using (var existing = Command(c, tx, "SELECT RecordId, RequestHash FROM OperationIdempotency WITH (UPDLOCK,HOLDLOCK) WHERE CompanyId=@company AND OperationKey=@key", ("@company", context.CompanyId), ("@key", context.IdempotencyKey)))
        {
            await using var reader = await existing.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var storedHash = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                if (!string.Equals(storedHash, requestHash, StringComparison.Ordinal))
                    throw new BadHttpRequestException("This operation ID was already used for a different request.");
                return reader.IsDBNull(0) ? null : reader.GetInt32(0);
            }
        }
        await ExecuteAsync(c, tx, "INSERT INTO OperationIdempotency(OperationKey,ModuleKey,CompanyId,UserId,RequestHash,CompletedUtc) VALUES(@key,@module,@company,@user,@hash,SYSUTCDATETIME())", ct, ("@key", context.IdempotencyKey), ("@module", module), ("@company", context.CompanyId), ("@user", context.UserId), ("@hash", requestHash));
        return null;
    }

    private static Task CompleteOperationAsync(SqlConnection c, SqlTransaction tx, CompanyContext context, int recordId, CancellationToken ct)
        => ExecuteAsync(c, tx, "UPDATE OperationIdempotency SET RecordId=@record WHERE CompanyId=@company AND OperationKey=@key", ct, ("@record", recordId), ("@company", context.CompanyId), ("@key", context.IdempotencyKey));

    private static async Task PostReceivedInventoryAsync(SqlConnection c, SqlTransaction tx, int poId, int companyId, CancellationToken ct)
    {
        await using var lines = Command(c, tx, "SELECT InventoryItemId, Description, Quantity, Rate FROM PurchaseLineItems WHERE POID=@id AND CompanyId=@company AND InventoryItemId IS NOT NULL AND Quantity > 0", ("@id", poId), ("@company", companyId));
        await using var reader = await lines.ExecuteReaderAsync(ct);
        var received = new List<(int ItemId, string Description, decimal Quantity, decimal Rate)>();
        while (await reader.ReadAsync(ct)) received.Add((reader.GetInt32(0), reader.IsDBNull(1) ? string.Empty : reader.GetString(1), reader.GetDecimal(2), reader.IsDBNull(3) ? 0m : reader.GetDecimal(3)));
        await reader.CloseAsync();
        foreach (var line in received)
        {
            var before = await QueryStockAsync(c, tx, line.ItemId, companyId, ct) ?? throw new BadHttpRequestException("Cross-company data mismatch. Operation blocked.");
            var after = before + line.Quantity;
            await ExecuteAsync(c, tx, "UPDATE StockItems SET CurrentStock=@stock, LastPurchaseRate=CASE WHEN @rate > 0 THEN @rate ELSE LastPurchaseRate END, LastUpdated=GETDATE() WHERE ItemID=@id AND CompanyId=@company", ct, ("@stock", after), ("@rate", line.Rate), ("@id", line.ItemId), ("@company", companyId));
            await ExecuteAsync(c, tx, "INSERT INTO StockMovements (CompanyId,ItemID,MovementType,Quantity,StockBefore,StockAfter,ToLocation,ReferenceNo,Notes,CreatedDate) VALUES (@company,@id,'PurchaseReceive',@qty,@before,@after,'Main Stock',@reference,@notes,GETDATE())", ct, ("@company", companyId), ("@id", line.ItemId), ("@qty", line.Quantity), ("@before", before), ("@after", after), ("@reference", "PO#" + poId), ("@notes", "Received from purchase order #" + poId + ": " + line.Description));
        }
    }

    private static async Task<(int ClientId, decimal Balance)?> QueryInvoiceAsync(SqlConnection c, SqlTransaction tx, int id, int companyId, CancellationToken ct) { await using var cmd = Command(c, tx, "SELECT ClientID, ISNULL(BalanceDue,TotalAmount-ISNULL(PaidAmount,0)) FROM Invoices WITH (UPDLOCK, ROWLOCK) WHERE InvoiceID=@id AND CompanyId=@company", ("@id", id), ("@company", companyId)); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? (r.GetInt32(0), r.GetDecimal(1)) : null; }
    private static async Task<decimal?> QueryStockAsync(SqlConnection c, SqlTransaction tx, int id, int companyId, CancellationToken ct) { await using var cmd = Command(c, tx, "SELECT CurrentStock FROM StockItems WITH (UPDLOCK, ROWLOCK) WHERE ItemID=@id AND CompanyId=@company AND ISNULL(IsActive,1)=1", ("@id", id), ("@company", companyId)); var v = await cmd.ExecuteScalarAsync(ct); return v is null ? null : Convert.ToDecimal(v); }
    private static async Task<string?> QueryPurchaseOrderAsync(SqlConnection c, SqlTransaction tx, int id, int companyId, CancellationToken ct) { await using var cmd = Command(c, tx, "SELECT Status FROM PurchaseOrders WITH (UPDLOCK, ROWLOCK) WHERE POID=@id AND CompanyId=@company", ("@id", id), ("@company", companyId)); var v = await cmd.ExecuteScalarAsync(ct); return v is null ? null : Convert.ToString(v) ?? string.Empty; }
    private static async Task ExecuteAsync(SqlConnection c, SqlTransaction tx, string sql, CancellationToken ct, params (string, object?)[] p) { await using var cmd = Command(c, tx, sql, p); await cmd.ExecuteNonQueryAsync(ct); }
    private static async Task<T> ScalarAsync<T>(SqlConnection c, SqlTransaction tx, string sql, CancellationToken ct, params (string, object?)[] p) { await using var cmd = Command(c, tx, sql, p); return (T)Convert.ChangeType((await cmd.ExecuteScalarAsync(ct))!, typeof(T)); }
    private static SqlCommand Command(SqlConnection c, SqlTransaction tx, string sql, params (string, object?)[] p) { var cmd = new SqlCommand(sql, c, tx); foreach (var (name, value) in p) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value); return cmd; }
}

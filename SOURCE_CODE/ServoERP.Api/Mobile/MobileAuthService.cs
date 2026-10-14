using System.Data;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Data.SqlClient;
using ServoERP.Engineer;

namespace ServoERP.Api.Mobile;

public sealed record EngineerIdentity(int UserId, int CompanyId, int EmployeeId, int RoleId, string DisplayName);
public sealed class MobileHttpException(int status, string message) : Exception(message) { public int Status { get; } = status; }
public sealed class MobileAuthService(string connectionString)
{
    private sealed class User
    {
        public int UserId { get; set; } public int CompanyId { get; set; } public int EmployeeId { get; set; } public int RoleId { get; set; }
        public string DisplayName { get; set; } = ""; public string PasswordHash { get; set; } = ""; public string PasswordSalt { get; set; } = "";
        public bool IsActive { get; set; } public bool ForcePasswordChange { get; set; } public DateTime? LockoutUntil { get; set; }
    }
    private sealed class Session
    {
        public Guid SessionId { get; set; } public Guid FamilyId { get; set; } public int UserId { get; set; } public int CompanyId { get; set; }
        public bool Revoked { get; set; } public DateTime RefreshExpiresUtc { get; set; } public string DeviceId { get; set; } = "";
    }
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public async Task<SessionTokens> Login(LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length > 255 || request.Password.Length is < 1 or > 256 || request.CompanyId <= 0 || string.IsNullOrWhiteSpace(request.DeviceId) || request.DeviceId.Length > 128)
            throw new MobileHttpException(400, "Enter your username, password and company number.");
        await using var c = new SqlConnection(connectionString); await c.OpenAsync(ct); await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        var user = await c.QuerySingleOrDefaultAsync<User>(new CommandDefinition(@"SELECT u.UserId,u.EmployeeID AS EmployeeId,u.RoleId,u.DisplayName,u.PasswordHash,u.PasswordSalt,u.IsActive,u.ForcePasswordChange,u.LockoutUntil
FROM AppUsers u WITH(UPDLOCK,HOLDLOCK) WHERE u.Username=@Username OR u.Email=@Username", request, tx, cancellationToken: ct));
        bool valid = user is not null && user.IsActive && !(user.LockoutUntil > DateTime.Now);
        if (valid)
        {
            try
            {
                valid = user!.PasswordHash.StartsWith("$2", StringComparison.Ordinal) ? BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash) :
                    CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Hash(user.PasswordSalt + request.Password).ToLowerInvariant()), Encoding.UTF8.GetBytes(user.PasswordHash));
            }
            catch (Exception ex) when (ex is ArgumentException or BCrypt.Net.SaltParseException) { valid = false; }
        }
        if (!valid)
        {
            if (user is not null && user.IsActive && !(user.LockoutUntil > DateTime.Now))
                await c.ExecuteAsync(new CommandDefinition("UPDATE AppUsers SET FailedAttempts=ISNULL(FailedAttempts,0)+1,LockoutUntil=CASE WHEN ISNULL(FailedAttempts,0)+1>=5 THEN DATEADD(minute,15,GETDATE()) ELSE LockoutUntil END WHERE UserId=@UserId", user, tx, cancellationToken: ct));
            await c.ExecuteAsync(new CommandDefinition("INSERT LoginAudit(UserId,Username,Success,FailureReason,DeviceName) VALUES(@id,@Username,0,'Invalid credentials or account unavailable',@DeviceId)", new { id = user?.UserId, request.Username, request.DeviceId }, tx, cancellationToken: ct));
            await tx.CommitAsync(ct); throw new MobileHttpException(401, "Unable to sign in. Check your details or contact the office.");
        }
        if (user!.ForcePasswordChange) throw new MobileHttpException(403, "Change your password in ServoERP Desktop before using the engineer app.");
        var identity = await Identity(c, tx, user.UserId, request.CompanyId, ct);
        var tokens = await Issue(c, tx, identity, Guid.NewGuid(), request.DeviceId, ct);
        await c.ExecuteAsync(new CommandDefinition("UPDATE AppUsers SET FailedAttempts=0,LockoutUntil=NULL,LastLoginDate=GETDATE() WHERE UserId=@UserId", user, tx, cancellationToken: ct));
        await c.ExecuteAsync(new CommandDefinition("INSERT LoginAudit(UserId,Username,Success,DeviceName) VALUES(@UserId,@Username,1,@DeviceId)", new { user.UserId, request.Username, request.DeviceId }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct); return tokens;
    }
    public async Task<SessionTokens> Refresh(RefreshRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.RefreshToken) || request.RefreshToken.Length > 200) throw new MobileHttpException(401, "Sign in again to synchronize your saved work.");
        await using var c = new SqlConnection(connectionString); await c.OpenAsync(ct); await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        var session = await c.QuerySingleOrDefaultAsync<Session>(new CommandDefinition("SELECT * FROM EngineerSessions WITH(UPDLOCK,HOLDLOCK) WHERE RefreshHash=@hash", new { hash = Hash(request.RefreshToken) }, tx, cancellationToken: ct));
        if (session is null) throw new MobileHttpException(401, "Sign in again to synchronize your saved work.");
        if (session.Revoked || session.RefreshExpiresUtc <= DateTime.UtcNow)
        {
            await c.ExecuteAsync(new CommandDefinition("UPDATE EngineerSessions SET Revoked=1 WHERE FamilyId=@FamilyId", session, tx, cancellationToken: ct)); await tx.CommitAsync(ct);
            throw new MobileHttpException(401, "This session has expired. Sign in again.");
        }
        var identity = await Identity(c, tx, session.UserId, session.CompanyId, ct);
        await c.ExecuteAsync(new CommandDefinition("UPDATE EngineerSessions SET Revoked=1 WHERE SessionId=@SessionId", session, tx, cancellationToken: ct));
        var tokens = await Issue(c, tx, identity, session.FamilyId, session.DeviceId, ct); await tx.CommitAsync(ct); return tokens;
    }
    public async Task<EngineerIdentity> Authenticate(string access, CancellationToken ct)
    {
        if (access.Length is < 40 or > 200) throw new MobileHttpException(401, "Sign in to synchronize your saved work.");
        await using var c = new SqlConnection(connectionString); await c.OpenAsync(ct);
        var session = await c.QuerySingleOrDefaultAsync<Session>(new CommandDefinition("SELECT * FROM EngineerSessions WHERE AccessHash=@hash AND Revoked=0 AND AccessExpiresUtc>SYSUTCDATETIME()", new { hash = Hash(access) }, cancellationToken: ct));
        if (session is null) throw new MobileHttpException(401, "Your session has expired. Sign in again.");
        return await Identity(c, null, session.UserId, session.CompanyId, ct);
    }
    public async Task Logout(string access, CancellationToken ct)
    {
        await using var c = new SqlConnection(connectionString);
        await c.ExecuteAsync(new CommandDefinition("UPDATE EngineerSessions SET Revoked=1 WHERE FamilyId=(SELECT FamilyId FROM EngineerSessions WHERE AccessHash=@hash)", new { hash = Hash(access) }, cancellationToken: ct));
    }
    private static async Task<EngineerIdentity> Identity(SqlConnection c, SqlTransaction? tx, int userId, int companyId, CancellationToken ct)
    {
        var user = await c.QuerySingleOrDefaultAsync<User>(new CommandDefinition(@"SELECT u.UserId,@companyId CompanyId,u.EmployeeID EmployeeId,ISNULL(uc.RoleId,u.RoleId) RoleId,u.DisplayName
FROM AppUsers u JOIN UserCompanies uc ON uc.UserId=u.UserId AND uc.CompanyId=@companyId AND uc.IsActive=1
JOIN Companies co ON co.CompanyId=uc.CompanyId AND co.IsActive=1
JOIN Employees e ON e.EmployeeID=u.EmployeeID AND e.CompanyId=@companyId
WHERE u.UserId=@userId AND u.IsActive=1 AND u.ForcePasswordChange=0", new { userId, companyId }, tx, cancellationToken: ct));
        if (user is null) throw new MobileHttpException(403, "Your account is not enabled as an engineer for this company. Contact the office.");
        if (!await c.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT CAST(CASE WHEN EXISTS(SELECT 1 FROM RolePermissions WHERE RoleId=@RoleId AND ModuleKey='WorkOrders' AND CanView=1) THEN 1 ELSE 0 END AS bit)", user, tx, cancellationToken: ct)))
            throw new MobileHttpException(403, "Your account does not have service-job access.");
        return new(user.UserId, companyId, user.EmployeeId, user.RoleId, user.DisplayName);
    }
    private static async Task<SessionTokens> Issue(SqlConnection c, SqlTransaction tx, EngineerIdentity identity, Guid family, string device, CancellationToken ct)
    {
        var access = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)); var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)); var expires = DateTimeOffset.UtcNow.AddMinutes(15);
        await c.ExecuteAsync(new CommandDefinition(@"INSERT EngineerSessions(SessionId,FamilyId,UserId,CompanyId,AccessHash,RefreshHash,AccessExpiresUtc,RefreshExpiresUtc,DeviceId)
VALUES(@id,@family,@UserId,@CompanyId,@access,@refresh,@expires,@refreshExpires,@device)", new { id = Guid.NewGuid(), family, identity.UserId, identity.CompanyId, access = Hash(access), refresh = Hash(refresh), expires = expires.UtcDateTime, refreshExpires = DateTime.UtcNow.AddDays(30), device }, tx, cancellationToken: ct));
        return new(access, refresh, expires, identity.UserId, identity.CompanyId, identity.DisplayName);
    }
}

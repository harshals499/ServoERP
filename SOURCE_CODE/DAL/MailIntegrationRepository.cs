using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.DAL
{
    public class MailIntegrationRepository
    {
        private readonly DatabaseManager _db = new DatabaseManager();

        public List<ConnectedMailAccount> GetAccountsForUser(int userId)
        {
            var list = new List<ConnectedMailAccount>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT AccountId, UserId, Provider, EmailAddress, DisplayName, AccessTokenEncrypted,
                           RefreshTokenEncrypted, TokenExpiresAtUtc, LastSyncUtc, LastSyncStatus,
                           IsActive, CreatedAt, UpdatedAt
                    FROM ConnectedMailAccounts
                    WHERE UserId = @userId AND IsActive = 1
                    ORDER BY Provider, EmailAddress;", conn))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        while (r.Read())
                            list.Add(MapAccount(r));
                }
            }
            return list;
        }

        public ConnectedMailAccount GetAccount(int accountId)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT AccountId, UserId, Provider, EmailAddress, DisplayName, AccessTokenEncrypted,
                           RefreshTokenEncrypted, TokenExpiresAtUtc, LastSyncUtc, LastSyncStatus,
                           IsActive, CreatedAt, UpdatedAt
                    FROM ConnectedMailAccounts
                    WHERE AccountId = @id;", conn))
                {
                    cmd.Parameters.AddWithValue("@id", accountId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        return r.Read() ? MapAccount(r) : null;
                }
            }
        }

        public int UpsertAccount(ConnectedMailAccount account)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    DECLARE @existing INT;
                    SELECT @existing = AccountId
                    FROM ConnectedMailAccounts
                    WHERE UserId = @userId AND Provider = @provider AND EmailAddress = @email;

                    IF @existing IS NULL
                    BEGIN
                        INSERT INTO ConnectedMailAccounts
                            (UserId, Provider, EmailAddress, DisplayName, AccessTokenEncrypted, RefreshTokenEncrypted,
                             TokenExpiresAtUtc, LastSyncStatus, IsActive, CreatedAt, UpdatedAt)
                        VALUES
                            (@userId, @provider, @email, @displayName, @accessToken, @refreshToken,
                             @expires, @status, 1, GETUTCDATE(), GETUTCDATE());
                        SELECT SCOPE_IDENTITY();
                    END
                    ELSE
                    BEGIN
                        UPDATE ConnectedMailAccounts SET
                            DisplayName = @displayName,
                            AccessTokenEncrypted = @accessToken,
                            RefreshTokenEncrypted = COALESCE(NULLIF(@refreshToken, ''), RefreshTokenEncrypted),
                            TokenExpiresAtUtc = @expires,
                            LastSyncStatus = @status,
                            IsActive = 1,
                            UpdatedAt = GETUTCDATE()
                        WHERE AccountId = @existing;
                        SELECT @existing;
                    END", conn))
                {
                    cmd.Parameters.AddWithValue("@userId", account.UserId);
                    cmd.Parameters.AddWithValue("@provider", account.Provider ?? string.Empty);
                    cmd.Parameters.AddWithValue("@email", account.EmailAddress ?? string.Empty);
                    cmd.Parameters.AddWithValue("@displayName", (object)account.DisplayName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@accessToken", (object)account.AccessTokenEncrypted ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@refreshToken", (object)account.RefreshTokenEncrypted ?? string.Empty);
                    cmd.Parameters.AddWithValue("@expires", (object)account.TokenExpiresAtUtc ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@status", (object)account.LastSyncStatus ?? DBNull.Value);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public void UpdateTokens(ConnectedMailAccount account)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE ConnectedMailAccounts SET
                        AccessTokenEncrypted = @accessToken,
                        RefreshTokenEncrypted = COALESCE(NULLIF(@refreshToken, ''), RefreshTokenEncrypted),
                        TokenExpiresAtUtc = @expires,
                        UpdatedAt = GETUTCDATE()
                    WHERE AccountId = @id;", conn))
                {
                    cmd.Parameters.AddWithValue("@id", account.AccountId);
                    cmd.Parameters.AddWithValue("@accessToken", (object)account.AccessTokenEncrypted ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@refreshToken", (object)account.RefreshTokenEncrypted ?? string.Empty);
                    cmd.Parameters.AddWithValue("@expires", (object)account.TokenExpiresAtUtc ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdateSyncStatus(int accountId, DateTime? lastSyncUtc, string status)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE ConnectedMailAccounts SET
                        LastSyncUtc = @lastSync,
                        LastSyncStatus = @status,
                        UpdatedAt = GETUTCDATE()
                    WHERE AccountId = @id;", conn))
                {
                    cmd.Parameters.AddWithValue("@id", accountId);
                    cmd.Parameters.AddWithValue("@lastSync", (object)lastSyncUtc ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@status", (object)status ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void Disconnect(int accountId)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("UPDATE ConnectedMailAccounts SET IsActive = 0, UpdatedAt = GETUTCDATE() WHERE AccountId = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", accountId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public bool EmailAlreadySynced(int accountId, string providerMessageId)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM ServiceDeskEmails WHERE AccountId = @accountId AND ProviderMessageId = @messageId", conn))
                {
                    cmd.Parameters.AddWithValue("@accountId", accountId);
                    cmd.Parameters.AddWithValue("@messageId", providerMessageId ?? string.Empty);
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
            }
        }

        public int SaveSyncedEmail(SyncedServiceDeskEmail email)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO ServiceDeskEmails
                        (AccountId, IncidentId, ProviderMessageId, ThreadId, FromAddress, FromName,
                         Subject, BodyPreview, ReceivedAtUtc, SyncedAtUtc)
                    VALUES
                        (@accountId, @incidentId, @messageId, @threadId, @fromAddress, @fromName,
                         @subject, @preview, @received, GETUTCDATE());
                    SELECT SCOPE_IDENTITY();", conn))
                {
                    cmd.Parameters.AddWithValue("@accountId", email.AccountId);
                    cmd.Parameters.AddWithValue("@incidentId", (object)email.IncidentId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@messageId", email.ProviderMessageId ?? string.Empty);
                    cmd.Parameters.AddWithValue("@threadId", (object)email.ThreadId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fromAddress", (object)email.FromAddress ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fromName", (object)email.FromName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@subject", (object)email.Subject ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@preview", (object)email.BodyPreview ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@received", (object)email.ReceivedAtUtc ?? DBNull.Value);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public List<SyncedServiceDeskEmail> GetEmailsForIncident(int incidentId)
        {
            var list = new List<SyncedServiceDeskEmail>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT EmailId, AccountId, IncidentId, ProviderMessageId, ThreadId, FromAddress, FromName,
                           Subject, BodyPreview, ReceivedAtUtc, SyncedAtUtc
                    FROM ServiceDeskEmails
                    WHERE IncidentId = @incidentId
                    ORDER BY ReceivedAtUtc DESC, SyncedAtUtc DESC;", conn))
                {
                    cmd.Parameters.AddWithValue("@incidentId", incidentId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        while (r.Read())
                            list.Add(MapEmail(r));
                }
            }
            return list;
        }

        private static ConnectedMailAccount MapAccount(SqlDataReader r)
        {
            return new ConnectedMailAccount
            {
                AccountId = Convert.ToInt32(r["AccountId"]),
                UserId = Convert.ToInt32(r["UserId"]),
                Provider = Convert.ToString(r["Provider"]),
                EmailAddress = Convert.ToString(r["EmailAddress"]),
                DisplayName = r["DisplayName"] == DBNull.Value ? null : Convert.ToString(r["DisplayName"]),
                AccessTokenEncrypted = r["AccessTokenEncrypted"] == DBNull.Value ? null : Convert.ToString(r["AccessTokenEncrypted"]),
                RefreshTokenEncrypted = r["RefreshTokenEncrypted"] == DBNull.Value ? null : Convert.ToString(r["RefreshTokenEncrypted"]),
                TokenExpiresAtUtc = r["TokenExpiresAtUtc"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["TokenExpiresAtUtc"]),
                LastSyncUtc = r["LastSyncUtc"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["LastSyncUtc"]),
                LastSyncStatus = r["LastSyncStatus"] == DBNull.Value ? null : Convert.ToString(r["LastSyncStatus"]),
                IsActive = r["IsActive"] == DBNull.Value || Convert.ToBoolean(r["IsActive"]),
                CreatedAt = r["CreatedAt"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(r["CreatedAt"]),
                UpdatedAt = r["UpdatedAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["UpdatedAt"])
            };
        }

        private static SyncedServiceDeskEmail MapEmail(SqlDataReader r)
        {
            return new SyncedServiceDeskEmail
            {
                EmailId = Convert.ToInt32(r["EmailId"]),
                AccountId = Convert.ToInt32(r["AccountId"]),
                IncidentId = r["IncidentId"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["IncidentId"]),
                ProviderMessageId = Convert.ToString(r["ProviderMessageId"]),
                ThreadId = r["ThreadId"] == DBNull.Value ? null : Convert.ToString(r["ThreadId"]),
                FromAddress = r["FromAddress"] == DBNull.Value ? null : Convert.ToString(r["FromAddress"]),
                FromName = r["FromName"] == DBNull.Value ? null : Convert.ToString(r["FromName"]),
                Subject = r["Subject"] == DBNull.Value ? null : Convert.ToString(r["Subject"]),
                BodyPreview = r["BodyPreview"] == DBNull.Value ? null : Convert.ToString(r["BodyPreview"]),
                ReceivedAtUtc = r["ReceivedAtUtc"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["ReceivedAtUtc"]),
                SyncedAtUtc = r["SyncedAtUtc"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(r["SyncedAtUtc"])
            };
        }
    }
}

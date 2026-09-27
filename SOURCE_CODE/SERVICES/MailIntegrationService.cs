using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public class MailIntegrationService
    {
        private const int OAuthCallbackTimeoutMinutes = 5;
        private const int GmailPageSize = 500;
        private const int GmailMaximumPagesPerSync = 10;

        private readonly MailIntegrationRepository _repo = new MailIntegrationRepository();
        private readonly ServiceDeskService _serviceDesk = new ServiceDeskService();
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public List<ConnectedMailAccount> GetAccountsForCurrentUser()
        {
            if (!SessionManager.IsLoggedIn)
                return new List<ConnectedMailAccount>();
            return _repo.GetAccountsForUser(SessionManager.CurrentUser.UserId);
        }

        public async Task<ConnectedMailAccount> ConnectAsync(string provider)
        {
            if (!SessionManager.IsLoggedIn)
                throw new InvalidOperationException("Login is required.");

            provider = NormalizeProvider(provider);
            OAuthSettings settings = LoadSettings(provider);
            string redirectUri = "http://127.0.0.1:" + GetFreePort() + "/";
            string state = Guid.NewGuid().ToString("N");
            string codeVerifier = provider == "Gmail" ? CreateCodeVerifier() : null;
            string codeChallenge = string.IsNullOrWhiteSpace(codeVerifier) ? null : CreateCodeChallenge(codeVerifier);
            string authUrl = BuildAuthorizeUrl(provider, settings, redirectUri, state, codeChallenge);

            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(redirectUri);
                listener.Start();
                Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

                Task<HttpListenerContext> callbackTask = listener.GetContextAsync();
                Task completedTask = await Task.WhenAny(callbackTask, Task.Delay(TimeSpan.FromMinutes(OAuthCallbackTimeoutMinutes)));
                if (completedTask != callbackTask)
                {
                    listener.Stop();
                    try { await callbackTask; } catch { }
                    throw new TimeoutException(provider + " sign-in timed out. Start the connection again and complete sign-in within five minutes.");
                }

                HttpListenerContext context = await callbackTask;
                string code = context.Request.QueryString["code"];
                string returnedState = context.Request.QueryString["state"];
                string error = context.Request.QueryString["error"];
                string errorDescription = context.Request.QueryString["error_description"];
                byte[] response = Encoding.UTF8.GetBytes("<html><body style='font-family:Segoe UI,Arial;padding:32px'><h2>Authorization received</h2><p>You can close this browser window and return to ServoERP.</p></body></html>");
                context.Response.ContentType = "text/html";
                context.Response.ContentEncoding = Encoding.UTF8;
                context.Response.OutputStream.Write(response, 0, response.Length);
                context.Response.Close();

                if (!string.IsNullOrWhiteSpace(error))
                    throw new Exception("Sign-in was not completed: " + FirstNonEmpty(errorDescription, error));
                if (string.IsNullOrWhiteSpace(code) || !string.Equals(returnedState, state, StringComparison.Ordinal))
                    throw new Exception("OAuth response was invalid.");

                TokenResponse token = await ExchangeCodeAsync(provider, settings, redirectUri, code, codeVerifier);
                if (string.IsNullOrWhiteSpace(token.AccessToken))
                    throw new Exception("The provider did not return an access token. Please reconnect the mailbox.");
                MailProfile profile = await GetProfileAsync(provider, token.AccessToken);
                if (string.IsNullOrWhiteSpace(profile.EmailAddress))
                    throw new Exception("The connected Gmail account could not be identified. Please reconnect and allow Gmail read access.");

                var account = new ConnectedMailAccount
                {
                    UserId = SessionManager.CurrentUser.UserId,
                    Provider = provider,
                    EmailAddress = profile.EmailAddress,
                    DisplayName = profile.DisplayName,
                    AccessTokenEncrypted = Protect(token.AccessToken),
                    RefreshTokenEncrypted = string.IsNullOrWhiteSpace(token.RefreshToken) ? null : Protect(token.RefreshToken),
                    TokenExpiresAtUtc = token.ExpiresInSeconds > 0 ? DateTime.UtcNow.AddSeconds(token.ExpiresInSeconds - 120) : (DateTime?)null,
                    LastSyncStatus = "Connected"
                };
                account.AccountId = _repo.UpsertAccount(account);
                SessionManager.LogAction("CONNECT", "ServiceDesk", account.AccountId, provider + " mail connected");
                return account;
            }
        }

        public void Disconnect(int accountId)
        {
            _repo.Disconnect(accountId);
            SessionManager.LogAction("DISCONNECT", "ServiceDesk", accountId, "Mail account disconnected");
        }

        public async Task<MailSyncResult> SyncAccountAsync(int accountId)
        {
            ConnectedMailAccount account = _repo.GetAccount(accountId);
            if (account == null || !account.IsActive)
                throw new Exception("Mail account is not connected.");

            try
            {
                string accessToken = await EnsureAccessTokenAsync(account);
                List<MailMessageSyncItem> messages = string.Equals(account.Provider, "Outlook", StringComparison.OrdinalIgnoreCase)
                    ? await FetchOutlookMessagesAsync(accessToken, account.LastSyncUtc)
                    : await FetchGmailMessagesAsync(accessToken, account.LastSyncUtc);

                MailSyncResult result = ProcessMessages(account, messages);
                result.Message = "Scanned " + result.Scanned + ", created " + result.CreatedIncidents + ", updated " + result.UpdatedIncidents + ".";
                _repo.UpdateSyncStatus(account.AccountId, DateTime.UtcNow, result.Message);
                SessionManager.LogAction("SYNC", "ServiceDesk", account.AccountId, "Mail sync: " + result.Message);
                return result;
            }
            catch (Exception ex)
            {
                _repo.UpdateSyncStatus(account.AccountId, account.LastSyncUtc, "Sync failed: " + ex.Message);
                throw;
            }
        }

        public List<SyncedServiceDeskEmail> GetEmailsForIncident(int incidentId)
        {
            return _repo.GetEmailsForIncident(incidentId);
        }

        private MailSyncResult ProcessMessages(ConnectedMailAccount account, List<MailMessageSyncItem> messages)
        {
            var result = new MailSyncResult();
            foreach (MailMessageSyncItem message in messages ?? new List<MailMessageSyncItem>())
            {
                result.Scanned++;
                if (string.IsNullOrWhiteSpace(message.ProviderMessageId) || _repo.EmailAlreadySynced(account.AccountId, message.ProviderMessageId))
                {
                    result.Skipped++;
                    continue;
                }

                int? incidentId = FindIncidentIdFromSubject(message.Subject);
                if (incidentId.HasValue)
                {
                    _serviceDesk.AddNote(new ServiceDeskNote
                    {
                        IncidentId = incidentId.Value,
                        NoteType = "Customer update",
                        NoteText = BuildEmailNote(account, message)
                    });
                    result.UpdatedIncidents++;
                }
                else
                {
                    var incident = new ServiceDeskIncident
                    {
                        IncidentNumber = _serviceDesk.GenerateIncidentNumber(),
                        CallerName = FirstNonEmpty(message.FromName, message.FromAddress),
                        CallerPhone = message.FromAddress,
                        Category = "Customer Complaint",
                        EquipmentType = "Other",
                        Priority = "Medium",
                        Status = "New",
                        ShortDescription = FirstNonEmpty(message.Subject, "Email request from " + message.FromAddress),
                        Description = BuildEmailDescription(account, message),
                        OpenedAt = message.ReceivedAtUtc.HasValue ? message.ReceivedAtUtc.Value.ToLocalTime() : DateTime.Now
                    };
                    incident.SlaDueAt = ServiceDeskService.ComputeSlaDue(incident.Priority, incident.OpenedAt);
                    incidentId = _serviceDesk.Save(incident);
                    _serviceDesk.AddNote(new ServiceDeskNote
                    {
                        IncidentId = incidentId.Value,
                        NoteType = "Email",
                        NoteText = BuildEmailNote(account, message)
                    });
                    result.CreatedIncidents++;
                }

                _repo.SaveSyncedEmail(new SyncedServiceDeskEmail
                {
                    AccountId = account.AccountId,
                    IncidentId = incidentId,
                    ProviderMessageId = message.ProviderMessageId,
                    ThreadId = message.ThreadId,
                    FromAddress = message.FromAddress,
                    FromName = message.FromName,
                    Subject = message.Subject,
                    BodyPreview = message.BodyPreview,
                    ReceivedAtUtc = message.ReceivedAtUtc
                });
            }
            return result;
        }

        private int? FindIncidentIdFromSubject(string subject)
        {
            Match match = Regex.Match(subject ?? string.Empty, @"INC\d{6}", RegexOptions.IgnoreCase);
            if (!match.Success)
                return null;

            ServiceDeskIncident incident = _serviceDesk.GetAll()
                .FirstOrDefault(i => string.Equals(i.IncidentNumber, match.Value, StringComparison.OrdinalIgnoreCase));
            return incident?.IncidentId;
        }

        private async Task<string> EnsureAccessTokenAsync(ConnectedMailAccount account)
        {
            if (account.TokenExpiresAtUtc.HasValue && account.TokenExpiresAtUtc.Value > DateTime.UtcNow.AddMinutes(2))
                return Unprotect(account.AccessTokenEncrypted);

            string refreshToken = Unprotect(account.RefreshTokenEncrypted);
            if (string.IsNullOrWhiteSpace(refreshToken))
                return Unprotect(account.AccessTokenEncrypted);

            OAuthSettings settings = LoadSettings(account.Provider);
            TokenResponse token = await RefreshTokenAsync(account.Provider, settings, refreshToken);
            account.AccessTokenEncrypted = Protect(token.AccessToken);
            if (!string.IsNullOrWhiteSpace(token.RefreshToken))
                account.RefreshTokenEncrypted = Protect(token.RefreshToken);
            account.TokenExpiresAtUtc = token.ExpiresInSeconds > 0 ? DateTime.UtcNow.AddSeconds(token.ExpiresInSeconds - 120) : (DateTime?)null;
            _repo.UpdateTokens(account);
            return token.AccessToken;
        }

        private async Task<List<MailMessageSyncItem>> FetchOutlookMessagesAsync(string accessToken, DateTime? sinceUtc)
        {
            string filter = sinceUtc.HasValue ? "&$filter=receivedDateTime ge " + Uri.EscapeDataString(sinceUtc.Value.AddMinutes(-5).ToString("o")) : string.Empty;
            string url = "https://graph.microsoft.com/v1.0/me/mailFolders/inbox/messages?$top=25&$orderby=receivedDateTime desc&$select=id,conversationId,subject,from,receivedDateTime,bodyPreview,body" + filter;
            Dictionary<string, object> root = await GetJsonAsync(url, accessToken);
            var list = new List<MailMessageSyncItem>();
            foreach (object item in GetArray(root, "value"))
            {
                var row = item as Dictionary<string, object>;
                if (row == null)
                    continue;
                Dictionary<string, object> from = GetObject(GetObject(row, "from"), "emailAddress");
                Dictionary<string, object> body = GetObject(row, "body");
                list.Add(new MailMessageSyncItem
                {
                    ProviderMessageId = GetString(row, "id"),
                    ThreadId = GetString(row, "conversationId"),
                    Subject = GetString(row, "subject"),
                    FromAddress = GetString(from, "address"),
                    FromName = GetString(from, "name"),
                    BodyPreview = GetString(row, "bodyPreview"),
                    BodyText = StripHtml(GetString(body, "content")),
                    ReceivedAtUtc = ParseDate(GetString(row, "receivedDateTime"))
                });
            }
            return list;
        }

        private async Task<List<MailMessageSyncItem>> FetchGmailMessagesAsync(string accessToken, DateTime? sinceUtc)
        {
            string query = "in:inbox newer_than:7d";
            if (sinceUtc.HasValue)
                query = "in:inbox after:" + ((DateTimeOffset)sinceUtc.Value.AddDays(-1)).ToUnixTimeSeconds();

            var messageIds = new List<string>();
            string pageToken = null;
            for (int page = 0; page < GmailMaximumPagesPerSync; page++)
            {
                string listUrl = "https://gmail.googleapis.com/gmail/v1/users/me/messages?maxResults=" + GmailPageSize
                    + "&includeSpamTrash=false&q=" + Uri.EscapeDataString(query);
                if (!string.IsNullOrWhiteSpace(pageToken))
                    listUrl += "&pageToken=" + Uri.EscapeDataString(pageToken);

                Dictionary<string, object> root = await GetJsonAsync(listUrl, accessToken);
                foreach (object item in GetArray(root, "messages"))
                {
                    var messageReference = item as Dictionary<string, object>;
                    string messageId = GetString(messageReference, "id");
                    if (!string.IsNullOrWhiteSpace(messageId))
                        messageIds.Add(messageId);
                }

                pageToken = GetString(root, "nextPageToken");
                if (string.IsNullOrWhiteSpace(pageToken))
                    break;

                if (page == GmailMaximumPagesPerSync - 1)
                    throw new Exception("Gmail returned more than 5,000 inbox messages for this sync window. Sync again with a more recent mailbox window.");
            }

            var output = new List<MailMessageSyncItem>();
            foreach (string id in messageIds)
            {
                Dictionary<string, object> msg = await GetJsonAsync("https://gmail.googleapis.com/gmail/v1/users/me/messages/" + Uri.EscapeDataString(id) + "?format=metadata&metadataHeaders=From&metadataHeaders=Subject&metadataHeaders=Date", accessToken);
                Dictionary<string, object> payload = GetObject(msg, "payload");
                string fromRaw = Header(payload, "From");
                output.Add(new MailMessageSyncItem
                {
                    ProviderMessageId = id,
                    ThreadId = GetString(msg, "threadId"),
                    Subject = Header(payload, "Subject"),
                    FromAddress = ExtractEmail(fromRaw),
                    FromName = ExtractName(fromRaw),
                    BodyPreview = GetString(msg, "snippet"),
                    BodyText = GetString(msg, "snippet"),
                    ReceivedAtUtc = ParseUnixMillis(GetString(msg, "internalDate"))
                });
            }
            return output.OrderBy(message => message.ReceivedAtUtc ?? DateTime.MinValue).ToList();
        }

        private async Task<MailProfile> GetProfileAsync(string provider, string accessToken)
        {
            if (string.Equals(provider, "Outlook", StringComparison.OrdinalIgnoreCase))
            {
                Dictionary<string, object> me = await GetJsonAsync("https://graph.microsoft.com/v1.0/me?$select=displayName,mail,userPrincipalName", accessToken);
                string email = FirstNonEmpty(GetString(me, "mail"), GetString(me, "userPrincipalName"));
                return new MailProfile { EmailAddress = email, DisplayName = GetString(me, "displayName") };
            }
            Dictionary<string, object> gmail = await GetJsonAsync("https://gmail.googleapis.com/gmail/v1/users/me/profile", accessToken);
            return new MailProfile { EmailAddress = GetString(gmail, "emailAddress"), DisplayName = GetString(gmail, "emailAddress") };
        }

        private async Task<Dictionary<string, object>> GetJsonAsync(string url, string accessToken)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(45);
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                HttpResponseMessage response = await client.GetAsync(url);
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    throw new Exception("Mail provider request failed (" + (int)response.StatusCode + "): " + ReadProviderError(body, response.ReasonPhrase));
                return _json.Deserialize<Dictionary<string, object>>(body);
            }
        }

        private async Task<TokenResponse> ExchangeCodeAsync(string provider, OAuthSettings settings, string redirectUri, string code, string codeVerifier)
        {
            var values = new Dictionary<string, string>
            {
                { "client_id", settings.ClientId },
                { "code", code },
                { "redirect_uri", redirectUri },
                { "grant_type", "authorization_code" }
            };
            if (!string.IsNullOrWhiteSpace(codeVerifier))
                values["code_verifier"] = codeVerifier;
            if (provider == "Gmail" && !string.IsNullOrWhiteSpace(settings.ClientSecret))
                values["client_secret"] = settings.ClientSecret;
            if (provider == "Outlook")
                values["scope"] = settings.Scope;
            return await PostTokenAsync(settings.TokenUrl, values);
        }

        private async Task<TokenResponse> RefreshTokenAsync(string provider, OAuthSettings settings, string refreshToken)
        {
            var values = new Dictionary<string, string>
            {
                { "client_id", settings.ClientId },
                { "refresh_token", refreshToken },
                { "grant_type", "refresh_token" }
            };
            if (provider == "Gmail" && !string.IsNullOrWhiteSpace(settings.ClientSecret))
                values["client_secret"] = settings.ClientSecret;
            if (provider == "Outlook")
                values["scope"] = settings.Scope;
            return await PostTokenAsync(settings.TokenUrl, values);
        }

        private async Task<TokenResponse> PostTokenAsync(string url, Dictionary<string, string> values)
        {
            using (var client = new HttpClient())
            using (var content = new FormUrlEncodedContent(values))
            {
                HttpResponseMessage response = await client.PostAsync(url, content);
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    throw new Exception("Mailbox authorization failed: " + ReadProviderError(body, response.ReasonPhrase));
                Dictionary<string, object> json = _json.Deserialize<Dictionary<string, object>>(body);
                return new TokenResponse
                {
                    AccessToken = GetString(json, "access_token"),
                    RefreshToken = GetString(json, "refresh_token"),
                    ExpiresInSeconds = GetInt(json, "expires_in")
                };
            }
        }

        private string BuildAuthorizeUrl(string provider, OAuthSettings settings, string redirectUri, string state, string codeChallenge)
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            query["client_id"] = settings.ClientId;
            query["redirect_uri"] = redirectUri;
            query["response_type"] = "code";
            query["scope"] = settings.Scope;
            query["state"] = state;
            if (provider == "Gmail")
            {
                query["access_type"] = "offline";
                query["prompt"] = "consent";
                query["include_granted_scopes"] = "true";
                query["code_challenge"] = codeChallenge;
                query["code_challenge_method"] = "S256";
            }
            else
            {
                query["response_mode"] = "query";
            }
            return settings.AuthorizeUrl + "?" + query;
        }

        private OAuthSettings LoadSettings(string provider)
        {
            if (provider == "Outlook")
            {
                string tenant = ConfigService.Get("Mail", "OutlookTenant", "common");
                string clientId = ConfigService.Get("Mail", "OutlookClientId", string.Empty);
                if (string.IsNullOrWhiteSpace(clientId))
                    throw new Exception("Outlook browser login is not ready. Add the one-time Outlook ClientId in Admin Keys first.");
                return new OAuthSettings
                {
                    ClientId = clientId,
                    Scope = "offline_access User.Read Mail.Read",
                    AuthorizeUrl = "https://login.microsoftonline.com/" + tenant + "/oauth2/v2.0/authorize",
                    TokenUrl = "https://login.microsoftonline.com/" + tenant + "/oauth2/v2.0/token"
                };
            }

            string gmailClientId = ConfigService.Get("Mail", "GmailClientId", string.Empty);
            if (string.IsNullOrWhiteSpace(gmailClientId))
                throw new Exception("Gmail browser login is not ready. Add the one-time Gmail ClientId in Admin Keys first.");
            return new OAuthSettings
            {
                ClientId = gmailClientId,
                ClientSecret = ConfigService.Get("Mail", "GmailClientSecret", string.Empty),
                Scope = "https://www.googleapis.com/auth/gmail.readonly",
                AuthorizeUrl = "https://accounts.google.com/o/oauth2/v2/auth",
                TokenUrl = "https://oauth2.googleapis.com/token"
            };
        }

        private static string NormalizeProvider(string provider)
        {
            string p = (provider ?? string.Empty).Trim();
            if (p.Equals("Outlook", StringComparison.OrdinalIgnoreCase) || p.Equals("Microsoft", StringComparison.OrdinalIgnoreCase))
                return "Outlook";
            if (p.Equals("Gmail", StringComparison.OrdinalIgnoreCase) || p.Equals("Google", StringComparison.OrdinalIgnoreCase))
                return "Gmail";
            throw new Exception("Choose Outlook or Gmail.");
        }

        private static string Protect(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;
            byte[] raw = Encoding.UTF8.GetBytes(value);
            byte[] protectedBytes = ProtectedData.Protect(raw, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }

        private static string Unprotect(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;
            byte[] raw = Convert.FromBase64String(value);
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(raw, null, DataProtectionScope.CurrentUser));
        }

        private static int GetFreePort()
        {
            var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static string CreateCodeVerifier()
        {
            byte[] bytes = new byte[64];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
                random.GetBytes(bytes);
            return Base64UrlEncode(bytes);
        }

        private static string CreateCodeChallenge(string codeVerifier)
        {
            using (SHA256 sha256 = SHA256.Create())
                return Base64UrlEncode(sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier)));
        }

        private static string Base64UrlEncode(byte[] value)
        {
            return Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private string ReadProviderError(string body, string fallback)
        {
            try
            {
                Dictionary<string, object> json = _json.Deserialize<Dictionary<string, object>>(body ?? string.Empty);
                string description = GetString(json, "error_description");
                if (!string.IsNullOrWhiteSpace(description))
                    return description;

                object errorValue;
                if (json != null && json.TryGetValue("error", out errorValue))
                {
                    var errorObject = errorValue as Dictionary<string, object>;
                    return FirstNonEmpty(GetString(errorObject, "message"), Convert.ToString(errorValue), fallback);
                }
            }
            catch
            {
                // Provider errors are best-effort parsed; do not expose the full response body.
            }
            return FirstNonEmpty(fallback, "Unknown provider error");
        }

        private static string BuildEmailNote(ConnectedMailAccount account, MailMessageSyncItem message)
        {
            return "Synced from " + account.Provider + " (" + account.EmailAddress + ")\r\n"
                + "From: " + FirstNonEmpty(message.FromName, message.FromAddress) + "\r\n"
                + "Subject: " + message.Subject + "\r\n\r\n"
                + FirstNonEmpty(message.BodyText, message.BodyPreview);
        }

        private static string BuildEmailDescription(ConnectedMailAccount account, MailMessageSyncItem message)
        {
            return "Email received in " + account.EmailAddress + "\r\n"
                + "From: " + FirstNonEmpty(message.FromName, message.FromAddress) + "\r\n"
                + "Subject: " + message.Subject + "\r\n\r\n"
                + FirstNonEmpty(message.BodyText, message.BodyPreview);
        }

        private static string Header(Dictionary<string, object> payload, string name)
        {
            foreach (object item in GetArray(payload, "headers"))
            {
                var header = item as Dictionary<string, object>;
                if (string.Equals(GetString(header, "name"), name, StringComparison.OrdinalIgnoreCase))
                    return GetString(header, "value");
            }
            return string.Empty;
        }

        private static IEnumerable<object> GetArray(Dictionary<string, object> obj, string key)
        {
            if (obj == null || !obj.ContainsKey(key) || obj[key] == null)
                return new object[0];
            return obj[key] as object[] ?? new object[0];
        }

        private static Dictionary<string, object> GetObject(Dictionary<string, object> obj, string key)
        {
            if (obj == null || !obj.ContainsKey(key))
                return new Dictionary<string, object>();
            return obj[key] as Dictionary<string, object> ?? new Dictionary<string, object>();
        }

        private static string GetString(Dictionary<string, object> obj, string key)
        {
            if (obj == null || !obj.ContainsKey(key) || obj[key] == null)
                return string.Empty;
            return Convert.ToString(obj[key]);
        }

        private static int GetInt(Dictionary<string, object> obj, string key)
        {
            int value;
            return int.TryParse(GetString(obj, key), out value) ? value : 0;
        }

        private static DateTime? ParseDate(string value)
        {
            DateTimeOffset dto;
            return DateTimeOffset.TryParse(value, out dto) ? dto.UtcDateTime : (DateTime?)null;
        }

        private static DateTime? ParseUnixMillis(string value)
        {
            long millis;
            return long.TryParse(value, out millis) ? DateTimeOffset.FromUnixTimeMilliseconds(millis).UtcDateTime : (DateTime?)null;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            return string.Empty;
        }

        private static string StripHtml(string value)
        {
            return Regex.Replace(value ?? string.Empty, "<.*?>", " ").Trim();
        }

        private static string ExtractEmail(string value)
        {
            Match match = Regex.Match(value ?? string.Empty, @"<([^>]+)>");
            return match.Success ? match.Groups[1].Value : (value ?? string.Empty);
        }

        private static string ExtractName(string value)
        {
            int index = (value ?? string.Empty).IndexOf('<');
            return index > 0 ? value.Substring(0, index).Trim().Trim('"') : string.Empty;
        }

        private sealed class OAuthSettings
        {
            public string ClientId;
            public string ClientSecret;
            public string Scope;
            public string AuthorizeUrl;
            public string TokenUrl;
        }

        private sealed class TokenResponse
        {
            public string AccessToken;
            public string RefreshToken;
            public int ExpiresInSeconds;
        }

        private sealed class MailProfile
        {
            public string EmailAddress;
            public string DisplayName;
        }
    }
}

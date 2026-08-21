using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Collections.Generic;
using HVAC_Pro_Desktop.Helpers;
using HVAC_Pro_Desktop.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HVAC_Pro_Desktop.Services
{
    /// <summary>Private-office API client. When enabled, guarded writes must not fall back to a different SQL target.</summary>
    public static class OfficeApiClient
    {
        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        private static readonly string SecretPath = System.IO.Path.Combine(SecureStorageHelper.StoreDirectory, "office-api-key.dat");
        private static readonly string UserTokenPath = System.IO.Path.Combine(SecureStorageHelper.StoreDirectory, "office-api-user-token.dat");

        public static bool IsEnabled => string.Equals(ConfigService.Get("OfficeApi", "Enabled", "false"), "true", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(ConfigService.Get("OfficeApi", "BaseUrl", string.Empty))
            && SecureStorageHelper.TryReadProtectedText(SecretPath, out _)
            && SecureStorageHelper.TryReadProtectedText(UserTokenPath, out _)
            && SessionManager.CurrentUser != null;

        public static string BaseUrl => ConfigService.Get("OfficeApi", "BaseUrl", string.Empty).Trim().TrimEnd('/');

        public static void SaveSettings(string baseUrl, string apiKey, bool enabled, string userToken = null, int? companyId = null)
        {
            if (enabled && (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey)))
                throw new InvalidOperationException("Enter the office API address and API key before enabling API mode.");
            if (!string.IsNullOrWhiteSpace(baseUrl) && !Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri uri))
                throw new InvalidOperationException("Enter a valid API address, for example https://OFFICE-SERVER:7443.");

            ConfigService.Set("OfficeApi", "BaseUrl", (baseUrl ?? string.Empty).Trim().TrimEnd('/'));
            ConfigService.Set("OfficeApi", "Enabled", enabled ? "true" : "false");
            if (!string.IsNullOrWhiteSpace(apiKey))
                SecureStorageHelper.SaveProtectedText(SecretPath, apiKey.Trim());
            if (!string.IsNullOrWhiteSpace(userToken))
                SecureStorageHelper.SaveProtectedText(UserTokenPath, userToken.Trim());
            if (companyId.HasValue && companyId.Value > 0)
                ConfigService.Set("OfficeApi", "ActiveCompanyId", companyId.Value.ToString());
        }

        public static void Disable() => ConfigService.Set("OfficeApi", "Enabled", "false");

        public static ApiHealthResult CheckHealth()
        {
            if (!IsEnabled) return ApiHealthResult.NotConfigured();
            JObject response = Send("GET", "/api/v1/health", null);
            return ApiHealthResult.Online(response.Value<string>("server"), response.Value<string>("databaseName"), response.Value<string>("version"));
        }

        public static IReadOnlyList<OfficeApiCompany> GetAuthorizedCompanies()
        {
            JArray response = SendArray("/api/v1/companies");
            var companies = new List<OfficeApiCompany>();
            foreach (JToken item in response)
                companies.Add(new OfficeApiCompany(item.Value<int>("companyId"), item.Value<string>("companyCode") ?? string.Empty, item.Value<string>("companyName") ?? string.Empty, item.Value<string>("role") ?? "User"));
            return companies;
        }

        public static int RecordPayment(Payment payment)
        {
            JObject response = Send("POST", "/api/v1/payments", new
            {
                invoiceId = payment.InvoiceID,
                amountPaid = payment.AmountPaid,
                paymentDate = payment.PaymentDate,
                paymentMode = payment.PaymentMode,
                referenceNumber = payment.ReferenceNumber,
                notes = payment.Notes,
                requestedBy = ResolveActor()
            });
            return response.Value<int>("paymentId");
        }

        public static int RecordStockMovement(StockMovement movement)
        {
            JObject response = Send("POST", "/api/v1/inventory/" + movement.ItemID + "/movements", new
            {
                quantity = movement.Quantity,
                movementType = movement.MovementType,
                fromLocation = movement.FromLocation,
                toLocation = movement.ToLocation,
                referenceNumber = movement.ReferenceNo,
                notes = movement.Notes,
                requestedBy = ResolveActor()
            });
            return response.Value<int>("movementId");
        }

        public static string ReceivePurchaseOrder(int purchaseOrderId)
        {
            JObject response = Send("POST", "/api/v1/purchase-orders/" + purchaseOrderId + "/receive", new { });
            return response.Value<string>("status") ?? string.Empty;
        }

        private static JObject Send(string method, string path, object payload)
        {
            string key;
            if (!SecureStorageHelper.TryReadProtectedText(SecretPath, out key))
                throw new InvalidOperationException("Office API key is not configured on this PC.");
            var request = new HttpRequestMessage(new HttpMethod(method), BaseUrl + path);
            request.Headers.Add("X-ServoERP-Api-Key", key);
            string userToken;
            if (!SecureStorageHelper.TryReadProtectedText(UserTokenPath, out userToken) || SessionManager.CurrentUser == null)
                throw new InvalidOperationException("Office API user identity is not configured for this signed-in user.");
            request.Headers.Add("X-ServoERP-User-Id", SessionManager.CurrentUser.UserId.ToString());
            request.Headers.Add("X-ServoERP-Company-Id", ConfigService.Get("OfficeApi", "ActiveCompanyId", "1"));
            request.Headers.Add("X-ServoERP-User-Token", userToken);
            if (!string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                request.Headers.Add("X-ServoERP-Operation-Id", Guid.NewGuid().ToString("N"));
            if (payload != null)
                request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            try
            {
                HttpResponseMessage response = Client.SendAsync(request).GetAwaiter().GetResult();
                string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    string message;
                    try { message = JObject.Parse(body).Value<string>("error"); } catch { message = body; }
                    throw new InvalidOperationException("Office API rejected the request (HTTP " + (int)response.StatusCode + "): " + (string.IsNullOrWhiteSpace(message) ? response.ReasonPhrase : message));
                }
                return JObject.Parse(body);
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException("Office API is unavailable. No data was written locally; restore the office server connection and try again.", ex);
            }
        }

        private static JArray SendArray(string path)
        {
            string key;
            if (!SecureStorageHelper.TryReadProtectedText(SecretPath, out key)) throw new InvalidOperationException("Office API key is not configured on this PC.");
            string userToken;
            if (!SecureStorageHelper.TryReadProtectedText(UserTokenPath, out userToken) || SessionManager.CurrentUser == null) throw new InvalidOperationException("Office API user identity is not configured for this signed-in user.");
            var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + path);
            request.Headers.Add("X-ServoERP-Api-Key", key);
            request.Headers.Add("X-ServoERP-User-Id", SessionManager.CurrentUser.UserId.ToString());
            request.Headers.Add("X-ServoERP-Company-Id", ConfigService.Get("OfficeApi", "ActiveCompanyId", "1"));
            request.Headers.Add("X-ServoERP-User-Token", userToken);
            try
            {
                HttpResponseMessage response = Client.SendAsync(request).GetAwaiter().GetResult();
                string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Office API rejected the company lookup (HTTP " + (int)response.StatusCode + ").");
                return JArray.Parse(body);
            }
            catch (HttpRequestException ex) { throw new InvalidOperationException("Office API is unavailable. No data was written locally; restore the office server connection and try again.", ex); }
        }

        private static string ResolveActor()
        {
            return SessionManager.CurrentUser == null ? Environment.UserName : (SessionManager.CurrentUser.DisplayName ?? SessionManager.CurrentUser.Username ?? Environment.UserName);
        }
    }

    public sealed class OfficeApiCompany
    {
        public OfficeApiCompany(int companyId, string companyCode, string companyName, string role) { CompanyId = companyId; CompanyCode = companyCode; CompanyName = companyName; Role = role; }
        public int CompanyId { get; }
        public string CompanyCode { get; }
        public string CompanyName { get; }
        public string Role { get; }
        public override string ToString() => CompanyName + (string.IsNullOrWhiteSpace(CompanyCode) ? string.Empty : " (" + CompanyCode + ")") + " - " + Role;
    }

    public sealed class ApiHealthResult
    {
        public bool IsConfigured { get; private set; }
        public bool IsOnline { get; private set; }
        public string Server { get; private set; }
        public string Database { get; private set; }
        public string Version { get; private set; }
        public string Message { get; private set; }
        public static ApiHealthResult NotConfigured() => new ApiHealthResult { Message = "Office API mode is not configured on this PC." };
        public static ApiHealthResult Online(string server, string database, string version) => new ApiHealthResult { IsConfigured = true, IsOnline = true, Server = server, Database = database, Version = version, Message = "Office API is connected." };
    }
}

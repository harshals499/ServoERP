using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services.Integrations
{
    public sealed class WhatsAppCloudIntegrationService
    {
        private const string Provider = "WhatsApp Cloud API";
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public bool IsConfigured
        {
            get
            {
                return !string.IsNullOrWhiteSpace(AccessToken) &&
                       !string.IsNullOrWhiteSpace(PhoneNumberId);
            }
        }

        private string GraphVersion => IntegrationConfig.Get("WhatsAppCloud", "GraphVersion", "v20.0");

        private string PhoneNumberId => IntegrationConfig.Get("WhatsAppCloud", "PhoneNumberId", string.Empty);

        private string AccessToken => IntegrationConfig.Get("WhatsAppCloud", "AccessToken", string.Empty);

        public Task<IntegrationOperationResult> SendTextMessageAsync(string toPhoneNumber, string message, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(message))
                return Task.FromResult(IntegrationOperationResult.Fail(Provider, "SendTextMessage", "Message text is required."));

            var payload = new Dictionary<string, object>
            {
                { "messaging_product", "whatsapp" },
                { "to", NormalizePhone(toPhoneNumber) },
                { "type", "text" },
                { "text", new Dictionary<string, object> { { "preview_url", false }, { "body", message } } }
            };

            return PostMessageAsync("SendTextMessage", toPhoneNumber, payload, cancellationToken);
        }

        public Task<IntegrationOperationResult> SendTemplateMessageAsync(
            string toPhoneNumber,
            string templateName,
            string languageCode,
            IList<WhatsAppTemplateComponent> components,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(templateName))
                return Task.FromResult(IntegrationOperationResult.Fail(Provider, "SendTemplateMessage", "Template name is required."));

            var template = new Dictionary<string, object>
            {
                { "name", templateName.Trim() },
                { "language", new Dictionary<string, object> { { "code", string.IsNullOrWhiteSpace(languageCode) ? "en" : languageCode.Trim() } } }
            };

            List<object> componentPayload = BuildTemplateComponents(components);
            if (componentPayload.Count > 0)
                template["components"] = componentPayload;

            var payload = new Dictionary<string, object>
            {
                { "messaging_product", "whatsapp" },
                { "to", NormalizePhone(toPhoneNumber) },
                { "type", "template" },
                { "template", template }
            };

            return PostMessageAsync("SendTemplateMessage", toPhoneNumber, payload, cancellationToken);
        }

        private async Task<IntegrationOperationResult> PostMessageAsync(string operation, string toPhoneNumber, Dictionary<string, object> payload, CancellationToken cancellationToken)
        {
            if (!IsConfigured)
                return IntegrationOperationResult.Fail(Provider, operation, "WhatsApp Cloud API credentials are not configured.");

            if (string.IsNullOrWhiteSpace(toPhoneNumber))
                return IntegrationOperationResult.Fail(Provider, operation, "Recipient phone number is required.");

            try
            {
                string endpoint = "https://graph.facebook.com/" + GraphVersion.Trim('/') + "/" + Uri.EscapeDataString(PhoneNumberId.Trim()) + "/messages";
                string body = _json.Serialize(payload);

                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
                using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken.Trim());
                    request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                    using (HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false))
                    {
                        string raw = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var result = response.IsSuccessStatusCode
                            ? IntegrationOperationResult.Ok(Provider, operation, "WhatsApp message accepted.")
                            : IntegrationOperationResult.Fail(Provider, operation, "WhatsApp API returned HTTP " + (int)response.StatusCode + ".");
                        result.ReferenceId = NormalizePhone(toPhoneNumber);
                        result.RawResponse = raw;
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("WhatsAppCloudIntegrationService." + operation, ex);
                return IntegrationOperationResult.Fail(Provider, operation, ex.Message);
            }
        }

        private static List<object> BuildTemplateComponents(IList<WhatsAppTemplateComponent> components)
        {
            var output = new List<object>();
            if (components == null)
                return output;

            foreach (WhatsAppTemplateComponent component in components)
            {
                if (component == null || string.IsNullOrWhiteSpace(component.Type))
                    continue;

                var parameters = new List<object>();
                foreach (string value in component.TextParameters ?? new List<string>())
                {
                    parameters.Add(new Dictionary<string, object>
                    {
                        { "type", "text" },
                        { "text", value ?? string.Empty }
                    });
                }

                output.Add(new Dictionary<string, object>
                {
                    { "type", component.Type.Trim().ToLowerInvariant() },
                    { "parameters", parameters }
                });
            }

            return output;
        }

        private static string NormalizePhone(string value)
        {
            string digits = string.Empty;
            foreach (char c in value ?? string.Empty)
            {
                if (char.IsDigit(c))
                    digits += c;
            }

            if (digits.Length == 10)
                return "91" + digits;

            return digits;
        }
    }
}

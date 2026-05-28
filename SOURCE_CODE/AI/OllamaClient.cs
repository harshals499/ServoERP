using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace HVAC_Pro_Desktop.AI
{
    /// <summary>
    /// Minimal Ollama HTTP client. No API key is used or logged.
    /// </summary>
    public class OllamaClient
    {
        private static readonly HttpClient Http = new HttpClient();
        private readonly AiProviderConfig _config;
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 };

        public OllamaClient(AiProviderConfig config)
        {
            _config = config ?? AiProviderConfig.Load();
        }

        public async Task<bool> IsReachableAsync(CancellationToken cancellationToken)
        {
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                linked.CancelAfter(TimeSpan.FromSeconds(4));
                try
                {
                    using (HttpResponseMessage response = await Http.GetAsync(BuildUrl("/api/tags"), linked.Token).ConfigureAwait(false))
                        return response.IsSuccessStatusCode;
                }
                catch
                {
                    return false;
                }
            }
        }

        public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return string.Empty;

            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                linked.CancelAfter(TimeSpan.FromSeconds(Math.Max(10, _config.TimeoutSeconds)));

                var payload = new
                {
                    model = _config.ModelName,
                    prompt = prompt,
                    stream = false,
                    options = new
                    {
                        num_predict = _config.MaxTokens,
                        temperature = (double)_config.Temperature
                    }
                };

                string json = _json.Serialize(payload);
                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                using (HttpResponseMessage response = await Http.PostAsync(BuildUrl("/api/generate"), content, linked.Token).ConfigureAwait(false))
                {
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException("Ollama returned " + (int)response.StatusCode + ": " + response.ReasonPhrase);

                    var parsed = _json.Deserialize<Dictionary<string, object>>(body);
                    if (parsed != null && parsed.ContainsKey("response") && parsed["response"] != null)
                        return Convert.ToString(parsed["response"]);

                    return body;
                }
            }
        }

        private string BuildUrl(string path)
        {
            string endpoint = string.IsNullOrWhiteSpace(_config.EndpointUrl) ? "http://localhost:11434" : _config.EndpointUrl.Trim();
            return endpoint.TrimEnd('/') + path;
        }
    }
}

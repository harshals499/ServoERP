using System;
using System.Threading;
using System.Threading.Tasks;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.AI
{
    /// <summary>
    /// Main ServoERP Copilot orchestration service. Read-only by design.
    /// </summary>
    public class AiAssistantService
    {
        private readonly AiContextProvider _contextProvider = new AiContextProvider();
        private readonly AiIntentRouter _intentRouter = new AiIntentRouter();
        private readonly AiPromptBuilder _promptBuilder = new AiPromptBuilder();

        public async Task<bool> IsLocalAiReachableAsync(CancellationToken cancellationToken)
        {
            AiProviderConfig config = AiProviderConfig.Load();
            if (!config.Enabled)
                return false;
            if (!string.Equals(config.Provider, "Ollama", StringComparison.OrdinalIgnoreCase))
                return false;
            return await new OllamaClient(config).IsReachableAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<AiAssistantResponse> AskAsync(AiAssistantRequest request, CancellationToken cancellationToken)
        {
            AiProviderConfig config = AiProviderConfig.Load();
            if (!config.Enabled)
            {
                return Error("Local AI Assistant is disabled in Settings.", config);
            }

            if (!string.Equals(config.Provider, "Ollama", StringComparison.OrdinalIgnoreCase))
            {
                return Error("Only Ollama is enabled in this build. OpenAI-compatible endpoints are reserved for future support.", config);
            }

            try
            {
                var context = _contextProvider.BuildContext(request?.Mode, request?.CurrentModule, request?.UserMessage);
                var plugin = _intentRouter.Route(request);
                string prompt = _promptBuilder.BuildPrompt(request, context, plugin);

                var client = new OllamaClient(config);
                if (!await client.IsReachableAsync(cancellationToken).ConfigureAwait(false))
                    return Error("Local AI is not running. Please install/start Ollama and pull a model like llama3.1 or qwen2.5.", config);

                string answer = await client.GenerateAsync(prompt, cancellationToken).ConfigureAwait(false);
                var response = new AiAssistantResponse
                {
                    Answer = string.IsNullOrWhiteSpace(answer) ? "The local model returned an empty response." : answer.Trim(),
                    Provider = config.Provider,
                    Model = config.ModelName,
                    RequiresConfirmation = plugin != null && plugin.SuggestedActions.Exists(a => a.IsWriteAction)
                };
                if (plugin != null && plugin.SuggestedActions != null)
                    response.SuggestedActions.AddRange(plugin.SuggestedActions);
                return response;
            }
            catch (OperationCanceledException)
            {
                return Error("AI request cancelled.", config);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("AI.Copilot.Error", ex);
                return Error("AI request failed: " + ex.Message, config);
            }
        }

        private static AiAssistantResponse Error(string message, AiProviderConfig config)
        {
            return new AiAssistantResponse
            {
                IsError = true,
                Answer = message,
                Provider = config?.Provider ?? "Ollama",
                Model = config?.ModelName ?? "llama3.1"
            };
        }
    }
}

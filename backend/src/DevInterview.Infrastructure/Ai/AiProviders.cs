using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using DevInterview.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevInterview.Infrastructure.Ai;

/// <summary>Per-provider settings under "AI:Providers:{Name}". ApiKey must come from environment variables / secrets.</summary>
public sealed class AiProviderOptions
{
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; }
    public string Model { get; set; } = string.Empty;
    public decimal InputCostPerMillionTokens { get; set; }
    public decimal OutputCostPerMillionTokens { get; set; }
    /// <summary>Anthropic only: effort level (low, medium, high, xhigh, max).</summary>
    public string? Effort { get; set; }
}

/// <summary>Claude via the official Anthropic C# SDK.</summary>
public sealed class AnthropicAiProvider : IAiProvider
{
    public const string ProviderName = "Anthropic";
    private const string DefaultModel = "claude-opus-5-5";
    private readonly AiProviderOptions _options;
    private readonly ILogger<AnthropicAiProvider> _logger;
    private readonly Lazy<AnthropicClient> _client;

    public AnthropicAiProvider(IOptionsMonitor<AiProviderOptions> options, ILogger<AnthropicAiProvider> logger)
    {
        _options = options.Get(ProviderName);
        _logger = logger;
        _client = new Lazy<AnthropicClient>(() =>
        {
            var client = new AnthropicClient { ApiKey = _options.ApiKey!, MaxRetries = 0 }; // retries are handled by the orchestrator
            if (!string.IsNullOrWhiteSpace(_options.BaseUrl)) client = new AnthropicClient { ApiKey = _options.ApiKey!, BaseUrl = _options.BaseUrl, MaxRetries = 0 };
            return client;
        });
    }

    public string Name => ProviderName;
    public string Model => string.IsNullOrWhiteSpace(_options.Model) ? DefaultModel : _options.Model;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);
    public decimal InputCostPerMillionTokens => _options.InputCostPerMillionTokens;
    public decimal OutputCostPerMillionTokens => _options.OutputCostPerMillionTokens;

    public async Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken ct)
    {
        if (!IsConfigured) throw new AiProviderException(AiFailureKind.NotConfigured, "Anthropic API key is not configured.");
        var messages = request.Messages.Select(m => m.Role == AiRole.User
            ? (BetaMessageParam)new BetaUserMessageParam { Content = m.Content }
            : new BetaAssistantMessageParam { Content = m.Content }).ToList();
        var parameters = new MessageCreateParams
        {
            Model = Model,
            MaxTokens = Math.Max(request.MaxOutputTokens, 1024),
            System = request.SystemPrompt + (request.JsonOutput ? "\n\nRespond with a single JSON object only." : string.Empty),
            Messages = messages,
            OutputConfig = new BetaOutputConfig { Effort = ParseEffort(_options.Effort) },
            // Server-side refusal fallback: a policy decline is re-served by a fallback model in the same call.
            Betas = ["server-side-fallback-2026-07-01"],
            Fallbacks = new Default(),
        };

        try
        {
            var response = await _client.Value.Beta.Messages.Create(parameters, ct);
            var text = string.Concat(response.Content.Select(b => b.Value).OfType<BetaTextBlock>().Select(t => t.Text));
            return new AiCompletionResult(text, response.Model.ToString() ?? Model, (int)response.Usage.InputTokens, (int)response.Usage.OutputTokens,
                response.StopReason?.ToString());
        }
        catch (AnthropicRateLimitException ex)
        {
            throw new AiProviderException(AiFailureKind.RateLimited, "Anthropic rate limit reached.", ex);
        }
        catch (Anthropic5xxException ex)
        {
            throw new AiProviderException(AiFailureKind.ProviderError, "Anthropic service error.", ex);
        }
        catch (AnthropicIOException ex)
        {
            throw new AiProviderException(AiFailureKind.ProviderError, "Could not reach Anthropic.", ex);
        }
        catch (AnthropicApiException ex)
        {
            _logger.LogError("Anthropic request rejected: {Message}", ex.Message);
            throw new AiProviderException(AiFailureKind.InvalidRequest, "Anthropic rejected the request.", ex);
        }
    }

    private static Effort ParseEffort(string? value) => value?.ToLowerInvariant() switch
    {
        "low" => Effort.Low,
        "high" => Effort.High,
        "max" => Effort.Max,
        _ => Effort.Medium
    };
}

/// <summary>
/// Any OpenAI-compatible Chat Completions endpoint (OpenAI, Azure OpenAI-compatible gateways, local LLM servers).
/// Uses raw HTTP so that any compatible provider works with only a BaseUrl/Model change.
/// </summary>
public sealed class OpenAiCompatibleProvider : IAiProvider
{
    public const string ProviderName = "OpenAI";
    private readonly HttpClient _http;
    private readonly AiProviderOptions _options;

    public OpenAiCompatibleProvider(HttpClient http, IOptionsMonitor<AiProviderOptions> options)
    {
        _http = http;
        _options = options.Get(ProviderName);
    }

    public string Name => ProviderName;
    public string Model => _options.Model;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey) && !string.IsNullOrWhiteSpace(_options.Model);
    public decimal InputCostPerMillionTokens => _options.InputCostPerMillionTokens;
    public decimal OutputCostPerMillionTokens => _options.OutputCostPerMillionTokens;

    public async Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken ct)
    {
        if (!IsConfigured) throw new AiProviderException(AiFailureKind.NotConfigured, "OpenAI-compatible provider is not configured.");
        var baseUrl = (string.IsNullOrWhiteSpace(_options.BaseUrl) ? "https://api.openai.com/v1" : _options.BaseUrl).TrimEnd('/');
        var messages = new JsonArray { new JsonObject { ["role"] = "system", ["content"] = request.SystemPrompt } };
        foreach (var m in request.Messages)
            messages.Add(new JsonObject { ["role"] = m.Role == AiRole.User ? "user" : "assistant", ["content"] = m.Content });
        var body = new JsonObject { ["model"] = _options.Model, ["messages"] = messages, ["max_tokens"] = request.MaxOutputTokens };
        if (request.JsonOutput) body["response_format"] = new JsonObject { ["type"] = "json_object" };

        using var message = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions") { Content = JsonContent.Create(body) };
        message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(message, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new AiProviderException(AiFailureKind.ProviderError, "Could not reach the AI provider.", ex);
        }
        using (response)
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests) throw new AiProviderException(AiFailureKind.RateLimited, "AI provider rate limit reached.");
            if ((int)response.StatusCode >= 500) throw new AiProviderException(AiFailureKind.ProviderError, $"AI provider returned {(int)response.StatusCode}.");
            if (!response.IsSuccessStatusCode) throw new AiProviderException(AiFailureKind.InvalidRequest, $"AI provider rejected the request ({(int)response.StatusCode}).");

            JsonNode? json;
            try { json = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: ct); }
            catch (JsonException ex) { throw new AiProviderException(AiFailureKind.ProviderError, "AI provider returned malformed JSON.", ex); }
            var choice = json?["choices"]?[0];
            var content = choice?["message"]?["content"]?.GetValue<string>() ?? string.Empty;
            var finish = choice?["finish_reason"]?.GetValue<string>();
            var usage = json?["usage"];
            return new AiCompletionResult(content, json?["model"]?.GetValue<string>() ?? _options.Model,
                usage?["prompt_tokens"]?.GetValue<int>() ?? 0, usage?["completion_tokens"]?.GetValue<int>() ?? 0,
                finish == "content_filter" ? "refusal" : finish);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TrayVisionPrompt.Configuration;
using TrayVisionPrompt.Models;

namespace TrayVisionPrompt.Services;

public abstract class OpenAiCompatibleClient : IOllmClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly AppConfiguration _configuration;
    private readonly ILogger _logger;

    protected OpenAiCompatibleClient(AppConfiguration configuration, ILogger logger)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = CreateHttpClient(configuration);
    }

    public async Task<LlmResponse> GetResponseAsync(LlmRequest request)
    {
        var payload = BuildPayload(request);
        var endpoint = NormalizeEndpoint(_configuration.Endpoint);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(payload)
        };

        httpRequest.Headers.Add("Accept", "application/json");

        _logger.LogDebug("Sending payload: {Payload}", JsonSerializer.Serialize(payload));

        using var cts = new System.Threading.CancellationTokenSource(_configuration.RequestTimeoutMs);
        var start = DateTime.UtcNow;
        var response = await _httpClient.SendAsync(httpRequest, cts.Token);
        if (!response.IsSuccessStatusCode)
        {
            string errorBody = string.Empty;
            try
            {
                errorBody = await response.Content.ReadAsStringAsync(cts.Token);
            }
            catch { /* ignore */ }
            throw new HttpRequestException($"Request failed {(int)response.StatusCode} {response.ReasonPhrase}: {errorBody}");
        }
        var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cts.Token), cancellationToken: cts.Token);

        var text = ParseResponse(document);
        return new LlmResponse
        {
            Text = text,
            Model = _configuration.Model,
            Duration = DateTime.UtcNow - start
        };
    }

    protected virtual HttpClient CreateHttpClient(AppConfiguration configuration)
    {
        var handler = new HttpClientHandler();
        if (!string.IsNullOrWhiteSpace(configuration.Proxy))
        {
            handler.Proxy = new System.Net.WebProxy(configuration.Proxy);
            handler.UseProxy = true;
        }

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMilliseconds(configuration.RequestTimeoutMs)
        };

        return client;
    }

    protected abstract object BuildPayload(LlmRequest request);

    protected virtual string ParseResponse(JsonDocument document)
    {
        if (document.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var choice = choices[0];
            if (choice.TryGetProperty("message", out var message))
            {
                if (message.TryGetProperty("content", out var content))
                {
                    if (content.ValueKind == JsonValueKind.String)
                    {
                        return content.GetString() ?? string.Empty;
                    }

                    if (content.ValueKind == JsonValueKind.Array)
                    {
                        var text = string.Empty;
                        foreach (var part in content.EnumerateArray())
                        {
                            if (part.TryGetProperty("text", out var textElement))
                            {
                                text += textElement.GetString();
                            }
                        }

                        return text;
                    }
                }
            }
        }

        return document.RootElement.ToString();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private static string NormalizeEndpoint(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "http://127.0.0.1:11434/v1/chat/completions";
        }
        if (raw.Contains("/v1/", StringComparison.OrdinalIgnoreCase))
        {
            return raw;
        }
        if (raw.EndsWith("/", StringComparison.Ordinal))
        {
            raw = raw.TrimEnd('/');
        }
        return raw + "/v1/chat/completions";
    }
}

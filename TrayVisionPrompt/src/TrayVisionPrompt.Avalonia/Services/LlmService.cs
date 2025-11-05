using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TrayVisionPrompt.Avalonia.Configuration;
using TrayVisionPrompt.Configuration; // AppConfiguration

namespace TrayVisionPrompt.Avalonia.Services;

public class LlmService : IDisposable
{
    private readonly ConfigurationStore _store = new();
    private readonly HttpClient _httpClient;
    private const int MaxRequestTimeoutMs = 180_000;

    public LlmService()
    {
        _store.Load();
        _httpClient = CreateHttpClient(_store.Current);
    }

    public async Task<string> SendAsync(string prompt, string? imageBase64 = null, CancellationToken cancellationToken = default, bool forceVision = false, string? systemPrompt = null)
    {
        var cfg = _store.Current;
        var endpoint = NormalizeEndpoint(cfg.Endpoint);

        var content = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["type"] = "text",
                ["text"] = prompt
            }
        };

        if ((cfg.UseVision || forceVision) && !string.IsNullOrWhiteSpace(imageBase64))
        {
            content.Add(new Dictionary<string, object?>
            {
                ["type"] = "image_url",
                ["image_url"] = new Dictionary<string, object?>
                {
                    ["url"] = $"data:image/png;base64,{imageBase64}"
                }
            });
        }

        var messages = new List<Dictionary<string, object?>>();

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new Dictionary<string, object?>
            {
                ["role"] = "system",
                ["content"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["type"] = "text",
                        ["text"] = systemPrompt
                    }
                }
            });
        }

        messages.Add(new Dictionary<string, object?>
        {
            ["role"] = "user",
            ["content"] = content
        });

        var payload = new
        {
            model = cfg.Model,
            temperature = cfg.Temperature,
            max_tokens = cfg.MaxTokens,
            messages
        };

        var timeout = CalculateTimeout(prompt, cfg.RequestTimeoutMs);
        using var cts = CreateLinkedCts(timeout, cancellationToken);

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("Accept", "application/json");

        var response = await _httpClient.SendAsync(request, cts.Token);
        if (!response.IsSuccessStatusCode)
        {
            var body = await SafeReadAsync(response, cts.Token);
            throw new HttpRequestException($"Request failed {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);
        return ParseResponse(document);
    }

    private static HttpClient CreateHttpClient(AppConfiguration cfg)
    {
        var handler = new HttpClientHandler();
        if (!string.IsNullOrWhiteSpace(cfg.Proxy))
        {
            handler.Proxy = new System.Net.WebProxy(cfg.Proxy);
            handler.UseProxy = true;
        }
        var effectiveTimeout = Math.Max(60_000, Math.Min(MaxRequestTimeoutMs, Math.Max(1_000, cfg.RequestTimeoutMs)));
        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMilliseconds(effectiveTimeout)
        };
    }

    private static string NormalizeEndpoint(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "http://127.0.0.1:11434/v1/chat/completions";
        }

        // If it already points to a /v1 route, keep as-is
        if (raw.Contains("/v1/", StringComparison.OrdinalIgnoreCase))
        {
            return raw;
        }

        // If it's just host:port, append standard OpenAI-compatible path
        if (raw.EndsWith("/", StringComparison.Ordinal))
        {
            raw = raw.TrimEnd('/');
        }
        return raw + "/v1/chat/completions";
    }

    private static string ParseResponse(JsonDocument document)
    {
        if (document.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var choice = choices[0];
            if (choice.TryGetProperty("message", out var message))
            {
                if (message.TryGetProperty("content", out var content))
                {
                    if (content.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        return content.GetString() ?? string.Empty;
                    }

                    if (content.ValueKind == System.Text.Json.JsonValueKind.Array)
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

    private static async Task<string> SafeReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try { return await response.Content.ReadAsStringAsync(ct); }
        catch { return string.Empty; }
    }

    private static CancellationTokenSource CreateLinkedCts(int timeoutMs, CancellationToken external)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(external);
        if (timeoutMs > 0)
        {
            cts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));
        }
        return cts;
    }

    private static int CalculateTimeout(string prompt, int baseTimeout)
    {
        var effectiveBase = Math.Max(45_000, baseTimeout);
        var length = prompt?.Length ?? 0;
        var extra = Math.Min(120_000, length * 4);
        return Math.Min(effectiveBase + extra, MaxRequestTimeoutMs);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

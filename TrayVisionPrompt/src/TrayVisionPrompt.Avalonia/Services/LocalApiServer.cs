using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TrayVisionPrompt.Avalonia.Services;

public sealed class LocalApiServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();

    public int Port { get; }

    public LocalApiServer(int port = 27124)
    {
        Port = port;
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
    }

    public void Start()
    {
        try
        {
            _listener.Start();
            _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
        }
        catch (HttpListenerException ex)
        {
            // Most likely URL ACL missing. Surface via debug but do not crash app.
            System.Diagnostics.Debug.WriteLine($"Local API failed to start: {ex.Message}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Local API start error: {ex}");
        }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext? ctx = null;
            try
            {
                ctx = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception)
            {
                if (ct.IsCancellationRequested) break;
                continue;
            }

            _ = Task.Run(() => HandleAsync(ctx), ct);
        }
    }

    private static void AddCors(HttpListenerResponse resp)
    {
        resp.Headers["Access-Control-Allow-Origin"] = "*";
        resp.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
        resp.Headers["Access-Control-Allow-Headers"] = "Content-Type";
        resp.Headers["Access-Control-Max-Age"] = "600";
    }

    private static async Task HandleAsync(HttpListenerContext ctx)
    {
        try
        {
            AddCors(ctx.Response);

            if (ctx.Request.HttpMethod == "OPTIONS")
            {
                ctx.Response.StatusCode = 204;
                ctx.Response.Close();
                return;
            }

            if (ctx.Request.HttpMethod == "POST" && ctx.Request.Url?.AbsolutePath == "/v1/process")
            {
                using var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding);
                var json = await reader.ReadToEndAsync().ConfigureAwait(false);
                var req = JsonSerializer.Deserialize<ProcessRequest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (req == null || string.IsNullOrWhiteSpace(req.Text))
                {
                    await WriteJsonAsync(ctx.Response, 400, new { error = "Missing text" });
                    return;
                }

                string? extra = req.Action switch
                {
                    "proofread" => TrayVisionPrompt.Configuration.PromptShortcutConfiguration.DefaultProofreadPrompt,
                    "translate" => TrayVisionPrompt.Configuration.PromptShortcutConfiguration.DefaultTranslatePrompt,
                    _ => req.ExtraPrompt
                };

                try
                {
                    using var llm = new LlmService();
                    var system = SystemPromptBuilder.Build(new Configuration.ConfigurationStore().Current.Language ?? "English", extra);
                    var resp = await llm.SendAsync(req.Text, systemPrompt: system).ConfigureAwait(false);
                    await WriteJsonAsync(ctx.Response, 200, new { response = resp });
                }
                catch (Exception ex)
                {
                    await WriteJsonAsync(ctx.Response, 500, new { error = ex.Message });
                }
                return;
            }

            await WriteJsonAsync(ctx.Response, 404, new { error = "Not found" });
        }
        catch
        {
            try { ctx.Response.Abort(); } catch { }
        }
    }

    private static async Task WriteJsonAsync(HttpListenerResponse resp, int status, object payload)
    {
        resp.ContentType = "application/json";
        resp.StatusCode = status;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        await resp.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
        resp.Close();
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        try { _listener.Close(); } catch { }
        _cts.Dispose();
    }

    private sealed class ProcessRequest
    {
        public string? Action { get; set; }
        public string? Text { get; set; }
        public string? ExtraPrompt { get; set; }
    }
}

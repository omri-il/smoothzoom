using System.IO;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SmoothZoom.Services;

/// <summary>
/// Tells SmoothZoom when OBS starts and stops recording, over obs-websocket v5.
/// The port and password are read from OBS's own obs-websocket config file on
/// every connect, so there is no second copy of the secret. When OBS is closed
/// it quietly retries every few seconds.
/// </summary>
public sealed class ObsRecordingWatcher : IDisposable
{
    private static readonly string ConfigFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "obs-studio", "plugin_config", "obs-websocket", "config.json");

    private const int OutputsEventMask = 1 << 6; // EventSubscription.Outputs
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly CancellationTokenSource _cts = new();
    private volatile bool _recording;
    private volatile bool _connected;

    /// <summary>Raised on a thread-pool thread. true = recording.</summary>
    public event Action<bool>? RecordingChanged;

    public bool Connected => _connected;
    public bool Recording => _recording;

    public void Start() => _ = Task.Run(() => RunAsync(_cts.Token));

    private async Task RunAsync(CancellationToken ct)
    {
        string? lastError = null;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ConnectAndListenAsync(ct);
                lastError = null;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Log each distinct failure once, not every 5 s while OBS is closed
                if (ex.Message != lastError) Log($"not connected: {ex.Message}");
                lastError = ex.Message;
            }

            // OBS gone (or never there): a recording can't still be running
            _connected = false;
            SetRecording(false);

            try { await Task.Delay(RetryDelay, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ConnectAndListenAsync(CancellationToken ct)
    {
        var config = JsonNode.Parse(File.ReadAllText(ConfigFile))
                     ?? throw new InvalidDataException("empty obs-websocket config");
        if (config["server_enabled"]?.GetValue<bool>() == false)
            throw new InvalidOperationException("obs-websocket server is disabled in OBS");
        int port = config["server_port"]?.GetValue<int>() ?? 4455;
        string password = config["server_password"]?.GetValue<string>() ?? "";

        using var ws = new ClientWebSocket();
        ws.Options.AddSubProtocol("obswebsocket.json");
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{port}"), ct);

        // Hello (op 0) → Identify (op 1)
        var hello = await ReceiveAsync(ws, ct) ?? throw new IOException("closed before Hello");
        var identify = new JsonObject
        {
            ["rpcVersion"] = 1,
            ["eventSubscriptions"] = OutputsEventMask,
        };
        var auth = hello["d"]?["authentication"];
        if (auth != null)
        {
            string secret = Sha256Base64(password + auth["salt"]!.GetValue<string>());
            identify["authentication"] = Sha256Base64(secret + auth["challenge"]!.GetValue<string>());
        }
        await SendAsync(ws, 1, identify, ct);

        var identified = await ReceiveAsync(ws, ct);
        if (identified?["op"]?.GetValue<int>() != 2)
            throw new IOException("OBS refused Identify (wrong password?)");
        _connected = true;
        Log("connected to OBS");

        // Current state first, then follow events
        await SendAsync(ws, 6, new JsonObject
        {
            ["requestType"] = "GetRecordStatus",
            ["requestId"] = "rec",
        }, ct);

        while (!ct.IsCancellationRequested)
        {
            var msg = await ReceiveAsync(ws, ct);
            if (msg == null) throw new IOException("OBS closed the connection");

            int op = msg["op"]?.GetValue<int>() ?? -1;
            var d = msg["d"];
            if (op == 7 && d?["requestId"]?.GetValue<string>() == "rec")
            {
                SetRecording(d["responseData"]?["outputActive"]?.GetValue<bool>() == true);
            }
            else if (op == 5 && d?["eventType"]?.GetValue<string>() == "RecordStateChanged")
            {
                // STARTING/STOPPING arrive too; only the settled states count
                string state = d["eventData"]?["outputState"]?.GetValue<string>() ?? "";
                if (state == "OBS_WEBSOCKET_OUTPUT_STARTED") SetRecording(true);
                else if (state == "OBS_WEBSOCKET_OUTPUT_STOPPED") SetRecording(false);
            }
        }
    }

    private void SetRecording(bool recording)
    {
        if (recording == _recording) return;
        _recording = recording;
        Log(recording ? "recording started" : "recording stopped");
        RecordingChanged?.Invoke(recording);
    }

    private static string Sha256Base64(string text) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static Task SendAsync(ClientWebSocket ws, int op, JsonObject d, CancellationToken ct)
    {
        var json = new JsonObject { ["op"] = op, ["d"] = d }.ToJsonString();
        return ws.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, ct);
    }

    private static async Task<JsonNode?> ReceiveAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[8192];
        using var ms = new MemoryStream();
        while (true)
        {
            var result = await ws.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            ms.Write(buffer, 0, result.Count);
            if (result.EndOfMessage) break;
        }
        return JsonNode.Parse(ms.ToArray());
    }

    private static readonly string LogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SmoothZoom", "obs.log");

    private static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!);
            File.AppendAllText(LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
        }
        catch { }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}

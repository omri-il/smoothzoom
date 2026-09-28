using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace SmoothShared;

/// <summary>
/// A tiny control line for other local programs (a second
/// launch of this exe with --toggle, from the Start menu or the pen): one text command in, one JSON line out, over
/// the named pipe \\.\pipe\&lt;name&gt;. Only the same Windows user can connect.
/// Compiled into both apps (linked from src/Shared in each csproj).
/// </summary>
public sealed class ControlPipeServer : IDisposable
{
    private const int Instances = 4;          // a few callers at once never see "pipe busy"

    private readonly string _name;
    private readonly Func<string, object> _handle;
    private readonly CancellationTokenSource _cts = new();

    /// <param name="handle">Command → reply object (serialised to JSON). Called on a
    /// thread-pool thread; marshal to the UI thread with <see cref="OnUi"/>.</param>
    public ControlPipeServer(string name, Func<string, object> handle)
    {
        _name = name;
        _handle = handle;
    }

    /// <summary>Run a command on the app's UI thread, giving up (TimeoutException →
    /// reply "busy") rather than holding the caller when that thread is stuck.</summary>
    public static object OnUi(System.Windows.Threading.Dispatcher ui, Func<object> run) =>
        ui.Invoke(run, System.Windows.Threading.DispatcherPriority.Send,
            CancellationToken.None, TimeSpan.FromSeconds(1.5));

    public void Start()
    {
        for (int i = 0; i < Instances; i++)
            _ = Task.Run(() => ServeAsync(_cts.Token));
    }

    private async Task ServeAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(_name, PipeDirection.InOut, Instances,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(ct);

                string command;
                using (var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    readTimeout.CancelAfter(TimeSpan.FromSeconds(2));
                    command = await ReadLineAsync(pipe, readTimeout.Token);
                }

                // The reply always gets written, even when handling was slow — its own
                // timer, so a busy UI thread can never turn into an empty answer
                object reply;
                try { reply = _handle(command.Trim()); }
                catch (TimeoutException) { reply = new { ok = false, error = "busy" }; }
                catch (Exception ex) { reply = new { ok = false, error = ex.Message }; }

                using var writeTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                writeTimeout.CancelAfter(TimeSpan.FromSeconds(2));
                await WriteLineAsync(pipe, JsonSerializer.Serialize(reply), writeTimeout.Token);

                // Keep the pipe open until the caller has read the reply and hung up (a
                // read that ends at 0 bytes), for at most 2 s. Never WaitForPipeDrain():
                // it blocks a thread with no timeout, and a caller that never reads
                // (the stuck --toggle copy before its fix, 2026-09-27) held it for good.
                using var hangUp = CancellationTokenSource.CreateLinkedTokenSource(ct);
                hangUp.CancelAfter(TimeSpan.FromSeconds(2));
                try { await WaitForHangUpAsync(pipe, hangUp.Token); }
                catch (Exception) when (!ct.IsCancellationRequested) { /* timed out or gone */ }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                // A caller that timed out or vanished must never stop the loop
                try { await Task.Delay(100, ct); } catch (OperationCanceledException) { return; }
            }
        }
    }

    /// <summary>Send one command to a running instance; null when none answers.
    /// Safe to call from a UI thread (the second launch's OnStartup): the work runs on
    /// the thread pool. Waiting on async pipe calls from the UI thread directly
    /// deadlocked — their continuations queue for the very thread that is waiting.</summary>
    public static string? Send(string name, string command, int timeoutMs = 1500)
    {
        var work = Task.Run(async () =>
        {
            using var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            using var timeout = new CancellationTokenSource(timeoutMs);
            await pipe.ConnectAsync(timeout.Token);
            await WriteLineAsync(pipe, command, timeout.Token);
            return await ReadLineAsync(pipe, timeout.Token);
        });
        try
        {
            return work.Wait(timeoutMs + 500) ? work.Result : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task WaitForHangUpAsync(Stream pipe, CancellationToken ct)
    {
        var one = new byte[1];
        while (await pipe.ReadAsync(one, ct) > 0) { /* anything more is ignored */ }
    }

    private static async Task<string> ReadLineAsync(Stream pipe, CancellationToken ct)
    {
        var bytes = new List<byte>();
        var one = new byte[1];
        while (bytes.Count < 64 * 1024)
        {
            int n = await pipe.ReadAsync(one, ct);
            if (n == 0 || one[0] == (byte)'\n') break;
            bytes.Add(one[0]);
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    private static async Task WriteLineAsync(Stream pipe, string line, CancellationToken ct)
    {
        var data = Encoding.UTF8.GetBytes(line + "\n");
        await pipe.WriteAsync(data, ct);
        await pipe.FlushAsync(ct);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}

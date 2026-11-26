using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TomasAI.IFM.Application.ServerManager;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1515",Justification="Preserve the existing public host, configuration and reflection contracts used by external test assemblies.")]

public sealed class DevelopmentControlPipe : IAsyncDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Action _requestShutdown;
    private readonly Task _monitor;
    private readonly Func<string, CancellationToken, Task<string>>? _capture;

    public DevelopmentControlPipe(Action requestShutdown) : this(requestShutdown, null) { }

    public DevelopmentControlPipe(Action requestShutdown, Func<string, CancellationToken, Task<string>>? capture)
    {
        _requestShutdown = requestShutdown ?? throw new ArgumentNullException(nameof(requestShutdown));
        _capture = capture;
        _monitor = MonitorAsync(_cancellation.Token);
    }

    public async ValueTask DisposeAsync()
    {
        await _cancellation.CancelAsync().ConfigureAwait(false);
        try
        {
            await _monitor.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected while the Development manager is stopping.
        }
        finally
        {
            _cancellation.Dispose();
        }
    }

    private async Task MonitorAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
var pipe = new NamedPipeServerStream(
                DevelopmentProcessSession.ControlPipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
await using var pipeDisposal = pipe.ConfigureAwait(true);
            await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(pipe, new UTF8Encoding(false), leaveOpen: true);
            var command = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (command is { Length: <= 128 } && command.StartsWith("capture-", StringComparison.Ordinal))
            {
                string reply;
                try
                {
                    if (Environment.GetEnvironmentVariable("IFM_SESSION_CAPTURE_BOUNDARY_ENABLED") != "1" || _capture is null)
                        throw new InvalidOperationException("Capture lifecycle is explicitly disabled.");
                    using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    budget.CancelAfter(TimeSpan.FromMinutes(5));
                    reply = await _capture(command, budget.Token).ConfigureAwait(false);
                }
                catch (Exception error) when (error is InvalidOperationException or ArgumentException or TimeoutException or OperationCanceledException)
                { reply = System.Text.Json.JsonSerializer.Serialize(new { State = "Blocked", Error = error.GetType().Name, ForcedStop = false }); }
                using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
                await writer.WriteLineAsync(reply).ConfigureAwait(false);
                continue;
            }
            if (string.Equals(command, "shutdown", StringComparison.OrdinalIgnoreCase))
            {
                _requestShutdown();
                return;
            }
        }
    }
}

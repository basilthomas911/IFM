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

    public DevelopmentControlPipe(Action requestShutdown)
    {
        _requestShutdown = requestShutdown ?? throw new ArgumentNullException(nameof(requestShutdown));
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
                PipeDirection.In,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
await using var pipeDisposal = pipe.ConfigureAwait(true);
            await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(pipe, new UTF8Encoding(false), leaveOpen: true);
            var command = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (string.Equals(command, "shutdown", StringComparison.OrdinalIgnoreCase))
            {
                _requestShutdown();
                return;
            }
        }
    }
}

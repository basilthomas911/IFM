using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TomasAI.IFM.Application.ServerManager;

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
        _cancellation.Cancel();
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
            await using var pipe = new NamedPipeServerStream(
                DevelopmentProcessSession.ControlPipeName,
                PipeDirection.In,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
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

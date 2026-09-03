using System.Threading.Channels;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

/// <summary>
/// Coalesces native/managed terminal notifications so the watchdog can evaluate immediately
/// without allowing a feed worker to execute lifecycle operations itself.
/// </summary>
public sealed class DatabentoTerminalFaultSignal
{
    readonly Channel<string> _signals = Channel.CreateBounded<string>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
        SingleWriter = false
    });

    public void Notify(string detail)
        => _signals.Writer.TryWrite(string.IsNullOrWhiteSpace(detail) ? "Databento worker completed." : detail);

    internal ValueTask<string> ReadAsync(CancellationToken cancellationToken)
        => _signals.Reader.ReadAsync(cancellationToken);
}

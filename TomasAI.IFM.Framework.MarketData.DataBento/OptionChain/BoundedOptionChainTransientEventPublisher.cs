using System.Threading.Channels;

namespace TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;

public sealed class BoundedOptionChainTransientEventPublisher :
    IOptionChainTransientEventPublisher,
    IAsyncDisposable
{
    private readonly IOptionChainTransientEventSink _sink;
    private readonly Channel<Publication> _channel;
    private readonly Task _worker;
    private int _disposed;

    public BoundedOptionChainTransientEventPublisher(
        IOptionChainTransientEventSink sink,
        int capacity = 4096)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _channel = Channel.CreateBounded<Publication>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });
        _worker = Task.Run(ProcessAsync);
    }

    public ValueTask PublishAsync(FuturesOptionChainQuoteChangedServiceEvent @event)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _channel.Writer.WriteAsync(new Publication(@event, null));
    }

    public ValueTask PublishAsync(FuturesOptionChainTradeChangedServiceEvent @event)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _channel.Writer.WriteAsync(new Publication(null, @event));
    }

    private async Task ProcessAsync()
    {
        await foreach (var publication in _channel.Reader.ReadAllAsync()
            .ConfigureAwait(false))
        {
            if (publication.Quote is { } quote)
                await _sink.OnQuoteAsync(quote).ConfigureAwait(false);
            else if (publication.Trade is { } trade)
                await _sink.OnTradeAsync(trade).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _channel.Writer.TryComplete();
        await _worker.ConfigureAwait(false);
    }

    private readonly record struct Publication(
        FuturesOptionChainQuoteChangedServiceEvent? Quote,
        FuturesOptionChainTradeChangedServiceEvent? Trade);
}

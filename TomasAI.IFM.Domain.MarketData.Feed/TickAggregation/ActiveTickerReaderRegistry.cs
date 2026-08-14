using System.Collections.Concurrent;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Domain.MarketData.Feed.TickAggregation;

/// <summary>
/// Owns the transient ticker readers acquired by one event actor instance.
/// </summary>
internal sealed class ActiveTickerReaderRegistry
{
    private readonly ConcurrentDictionary<ReaderKey, ITickerDataReader> _readers = [];

    /// <summary>
    /// Acquires or returns the reader owned by the specified workflow and retains it for actor event handling.
    /// </summary>
    public async ValueTask<ITickerDataReader> AcquireAsync(
        IMarketDataApi marketDataApi,
        TickerReaderOwner owner,
        string contractId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(marketDataApi);
        var reader = await marketDataApi.CreateTickerDataReaderAsync(
            owner,
            contractId,
            cancellationToken).ConfigureAwait(false);
        var key = new ReaderKey(contractId, owner);
        var active = _readers.GetOrAdd(key, reader);
        if (!ReferenceEquals(active, reader))
            await reader.DisposeAsync().ConfigureAwait(false);
        return active;
    }

    /// <summary>
    /// Releases the reader owned by the specified workflow, if it is currently retained.
    /// </summary>
    public async ValueTask<bool> ReleaseAsync(
        string contractId,
        TickerReaderOwner owner)
    {
        if (!_readers.TryRemove(new ReaderKey(contractId, owner), out var reader))
            return false;
        await reader.DisposeAsync().ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Finds an active actor-owned reader for the supplied contract.
    /// </summary>
    public bool TryGetReader(
        string contractId,
        out ITickerDataReader reader)
    {
        foreach (var pair in _readers)
        {
            if (StringComparer.Ordinal.Equals(pair.Key.ContractId, contractId))
            {
                reader = pair.Value;
                return true;
            }
        }
        reader = default!;
        return false;
    }

    /// <summary>
    /// Releases every reader retained by the actor and reports any combined disposal failures.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        var readers = _readers.ToArray();
        _readers.Clear();
        List<Exception>? failures = null;
        foreach (var pair in readers)
        {
            try { await pair.Value.DisposeAsync().ConfigureAwait(false); }
            catch (Exception exception) { (failures ??= []).Add(exception); }
        }
        if (failures is not null)
            throw new AggregateException("One or more ticker readers could not be released.", failures);
    }

    private readonly record struct ReaderKey(
        string ContractId,
        TickerReaderOwner Owner);
}

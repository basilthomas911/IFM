using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.MarketData.TradeStrategySymbols;
/// <summary>Validates the complete discovery result before persisting or returning any products.</summary>
public sealed class TradeStrategySymbolCatalog(ITradeStrategySymbolSource source, ITradeStrategySymbolStore store, TimeProvider timeProvider) : ITradeStrategySymbolCatalog, IDisposable
{
    readonly SemaphoreSlim _gate = new(1, 1);
    readonly Dictionary<TradeStrategyFamilyType, (DateTimeOffset Expires, TradeStrategySymbolReadModel[] Rows)> _cache = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "The service boundary returns a classified unavailable or failed result and records diagnostics instead of aborting its supervisor.")]
    public async Task<ServiceResult<TradeStrategySymbolReadModel[]>> GetAsync(TradeStrategyFamilyType family, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (family is not (TradeStrategyFamilyType.Futures or TradeStrategyFamilyType.FuturesOption))
            return new ServiceFailed<TradeStrategySymbolReadModel[]>(400, $"Trade strategy symbols are not supported for {family}.");
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cache.TryGetValue(family, out var cached) && cached.Expires > timeProvider.GetUtcNow())
                return new ServiceOk<TradeStrategySymbolReadModel[]>([..cached.Rows]);
            var products = (await source.DiscoverAsync(family, cancellationToken).ConfigureAwait(false)).ToArray();
            foreach (var product in products)
            {
                product.Validate();
                if (product.Family != family)
                    throw new InvalidOperationException("Provider returned a product from the wrong family.");
            }

            var distinct = products.Distinct().OrderBy(x => x.Symbol, StringComparer.Ordinal).ThenBy(x => x.Exchange, StringComparer.Ordinal).ThenBy(x => x.Currency, StringComparer.Ordinal).ToArray();
            List<TradeStrategySymbolReadModel> result = [];
            foreach (var product in distinct)
            {
                var row = await store.GetOrCreateAsync(product, cancellationToken).ConfigureAwait(false);
                if (row.Validate().Count != 0 || row != product.WithId(row.Id))
                    throw new InvalidOperationException($"Persisted product identity for '{product.Symbol}' does not match the provider metadata.");
                result.Add(row);
            }

            if (result.Select(x => x.Id).Distinct().Count() != result.Count)
                throw new InvalidOperationException("Product catalog returned duplicate identities.");
            cancellationToken.ThrowIfCancellationRequested();
            var rows = result.ToArray();
            _cache[family] = (timeProvider.GetUtcNow().AddMinutes(5), rows);
            return new ServiceOk<TradeStrategySymbolReadModel[]>([..rows]);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ServiceFailed<TradeStrategySymbolReadModel[]>(503, $"Trade strategy symbol lookup failed: {ex.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    private int disposedGateOwner;
    /// <summary>Releases the owned synchronization gate after the host has stopped its operations.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposedGateOwner, 1) != 0)
            return;
        _gate.Dispose();
        GC.SuppressFinalize(this);
    }
}
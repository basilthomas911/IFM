using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.TradeStrategySymbols;
/// <summary>Reusable on-demand refresh. No hosted service or scheduled task starts it implicitly.</summary>
public sealed class InstrumentDefinitionRefresh(IInstrumentDefinitionProvider provider, IInstrumentDefinitionStore store, DatabentoMarketDataRuntimeOptions options, TimeProvider clock, ILogger<InstrumentDefinitionRefresh>? logger = null) : IDisposable
{
    readonly SemaphoreSlim _gate = new(1, 1);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Preserve native shutdown and containment ownership/failure ordering in this compatibility pass.")]
    public async Task<InstrumentDefinitionSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var datasets = (options.TradeStrategySymbolDatasets.Count == 0 ? [options.FeedOptions.Dataset] : options.TradeStrategySymbolDatasets).Select(x => x.Trim().ToUpperInvariant()).Distinct(StringComparer.Ordinal).ToArray();
            if (datasets.Length == 0 || datasets.Any(string.IsNullOrWhiteSpace))
                throw new InvalidOperationException("At least one definition dataset is required.");
            var snapshot = Guid.NewGuid();
            long total = 0;
            HashSet<TradeStrategyProduct> products = [];
            foreach (var dataset in datasets)
            {
                var latest = new Dictionary<InstrumentKey, (ulong Received, ulong Event, bool Deleted, ContractDetail Detail, TomasAI.IFM.Domain.MarketData.Shared.ViewModels.InstrumentDefinitionSelection Selection)>();
                List<Task> pending = [];
                long count = 0;
                try
                {
                    await foreach (var row in provider.ReadLatestAsync(dataset, cancellationToken).ConfigureAwait(false))
                    {
                        var index = count++;
                        pending.Add(store.InsertAsync(snapshot, index, row, cancellationToken));
                        var key = row.Summary.Instrument;
                        if ((row.Summary.ContractKind is ContractKind.Future or ContractKind.CallOption or ContractKind.PutOption || row.Deleted || latest.ContainsKey(key)) && (!latest.TryGetValue(key, out var old) || row.ReceivedNanoseconds > old.Received || (row.ReceivedNanoseconds == old.Received && row.EventNanoseconds >= old.Event)))
                        {
                            latest[key] = (row.ReceivedNanoseconds, row.EventNanoseconds, row.Deleted, row.Summary, InstrumentDefinitionSelectionMapper.Map(snapshot, index, row));
                        }

                        if (pending.Count >= 32)
                        {
                            await Task.WhenAll(pending).ConfigureAwait(false);
                            pending.Clear();
                        }

                        if ((count % 100000 == 0) && (logger?.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Information) == true))
                        {
                            OptionalApplicationDiagnosticLog.Write11(logger, null, count, dataset);
                        }
                    }

                    await Task.WhenAll(pending).ConfigureAwait(false);
                }
                catch
                {
                    await Task.WhenAll(pending).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                    throw;
                }

                if (count == 0)
                    throw new InvalidOperationException($"No instrument definitions returned for {dataset}.");
                total += count;
                await Parallel.ForEachAsync(latest.Values.Where(x => x.Selection.InstrumentClass is "F" or "C" or "P"), new ParallelOptions { MaxDegreeOfParallelism = 16, CancellationToken = cancellationToken }, async (entry, token) => await store.IndexSelectionAsync(entry.Selection, token).ConfigureAwait(false)).ConfigureAwait(false);
                products.UnionWith(Products(latest.Values.Where(x => !x.Deleted).Select(x => x.Detail), clock.GetUtcNow()));
            }

            if (products.Count == 0)
                throw new InvalidOperationException("No eligible futures or futures-option products were found; the previous snapshot remains active.");
            var completed = new InstrumentDefinitionSnapshot(snapshot, clock.GetUtcNow().UtcDateTime, total, datasets);
            await store.PublishAsync(completed, products, cancellationToken).ConfigureAwait(false);
            if (logger?.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Information) == true)
            {
                OptionalApplicationDiagnosticLog.Write12(logger, null, snapshot, total, products.Count);
            }

            return completed;
        }
        finally
        {
            _gate.Release();
        }
    }

    internal static IReadOnlyCollection<TradeStrategyProduct> Products(IEnumerable<ContractDetail> definitions, DateTimeOffset now)
    {
        bool Current(ContractDetail x) => !(x.ActivationTimestampNanoseconds is { } start && start / 1_000_000_000UL > (ulong)now.ToUnixTimeSeconds()) && (x.ExpirationTimestampNanoseconds is { } end ? end / 1_000_000_000UL > (ulong)now.ToUnixTimeSeconds() : x.MaturityDate >= DateOnly.FromDateTime(now.UtcDateTime));
        var rows = definitions.Where(Current).ToArray();
        var futures = rows.Where(x => x.ContractKind == ContractKind.Future).ToArray();
        var byId = futures.ToLookup(x => x.Instrument.InstrumentId);
        var byName = futures.ToLookup(x => x.RawSymbol, StringComparer.Ordinal);
        HashSet<TradeStrategyProduct> result = [];
        void Add(TradeStrategyFamilyType family, ContractDetail underlying, ContractDetail priced)
        {
            var product = new TradeStrategyProduct(family, underlying.Ticker.Trim(), priced.Currency.Trim().ToUpperInvariant(), priced.Exchange.Trim().ToUpperInvariant());
            if (product.WithId(1).Validate().Count == 0)
                result.Add(product);
        }

        foreach (var row in futures)
            Add(TradeStrategyFamilyType.Futures, row, row);
        foreach (var row in rows.Where(x => x.ContractKind is ContractKind.CallOption or ContractKind.PutOption))
        {
            var matches = row.UnderlyingInstrumentId != 0 ? byId[row.UnderlyingInstrumentId].ToArray() : byName[row.Underlying].ToArray();
            if (matches.Length == 1)
                Add(TradeStrategyFamilyType.FuturesOption, matches[0], row);
        }

        return result;
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

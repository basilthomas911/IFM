

using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
namespace TomasAI.IFM.Domain.Portfolio.Financial.Repository;

/// <summary>Offline reconstruction of financial read tables from complete persisted source facts. No broker or workflow dispatch.</summary>
public sealed class FinancialReadProjectionRebuilder(LedgerConfigurationProjection configuration,LedgerFinancialProjection ledger,
    CapacityFinancialProjection capacity,EmulatorExecutionStore emulator,FinancialAuthorityProjection authority,IFinancialHistoryProjection history,AccountingExportStore exports,BrokerAccountingIntentStore intents,FinancialProjectionBaseline baseline,PortfolioOrderCompositionProjection? composition=null)
{
    PortfolioOrderCompositionProjection RequireComposition()=>composition ?? throw new InvalidOperationException("Composition reconstruction service is required.");

    /// <summary>Restores a migration baseline, then only events committed after it. Earlier receipt-only events are not replayed.</summary>
    public async Task<long> RebuildFromBaselineAsync(FinancialProjectionBaselineEvent source,IAsyncEnumerable<IEvent> laterSources,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(source);
        await (baseline.ReplayAsync(source,token)).ConfigureAwait(false);
        return await (RebuildAsync(AfterBaseline(laterSources,source.EventId,token),token)).ConfigureAwait(false);
    }

    static async IAsyncEnumerable<IEvent> AfterBaseline(IAsyncEnumerable<IEvent> sources,long cut,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    {
        await foreach(var source in (sources.WithCancellation(token)).ConfigureAwait(false))
        {
            if(source.EventId<=cut) throw new InvalidDataException("Baseline rebuild requires only post-baseline source events.");
            yield return source;
        }
    }

    /// <summary>The supplied log must be from quiesced, selected financial streams; sequence allocation is not an online commit cursor.</summary>
    public async Task<long> RebuildAsync(IAsyncEnumerable<IEvent> sources,CancellationToken token=default)
    {
        long previous=0,count=0;
        await foreach(var source in sources.WithCancellation(token).ConfigureAwait(false))
        {
            if(source.EventId<=previous) throw new InvalidDataException("Persisted financial sources must be in ascending event-log order.");
            switch(source)
            {
                case PortfolioOrderCompositionCompletedEvent value: await (RequireComposition().ReplayAsync(value,token)).ConfigureAwait(false); break;
                case PortfolioCloseOrderCompositionCompletedEvent value: await (RequireComposition().ReplayAsync(value,token)).ConfigureAwait(false); break;
                case LedgerConfigurationCompletedEvent value: await (configuration.ReplayAsync(value,token)).ConfigureAwait(false); break;
                case LedgerPostingCompletedEvent value: await (ledger.ReplayAsync(value,token)).ConfigureAwait(false); break;
                case LedgerPostingBatchCompletedEvent value: await (ledger.ReplayAsync(value,token)).ConfigureAwait(false); break;
                case CapacityReservationCompletedEvent value: await (capacity.ReplayAsync(value,token)).ConfigureAwait(false); break;
                case CapacityConsumptionCompletedEvent value: await (capacity.ReplayAsync(value,token)).ConfigureAwait(false); break;
                case CapacityLifecycleCompletedEvent value: await (capacity.ReplayAsync(value,token)).ConfigureAwait(false); break;
                case EmulatorOrderSubmittedEvent value: await (emulator.ReplayAsync(value,token)).ConfigureAwait(false); break;
                case FinancialAuthoritySnapshotEvent value: await (authority.ReplayAsync(value,token)).ConfigureAwait(false); break;
                case AccountingExportSnapshotEvent value: await (exports.ReplayAsync(value,token)).ConfigureAwait(false); break;
                case BrokerAccountingIntentCapturedEvent value: await (intents.ReplayAsync(value,token)).ConfigureAwait(false); break;
                default: throw new InvalidDataException($"Unsupported financial source: {source.EventName}.");
            }
            if(source is IFinancialCompletedEvent completed) await (history.ApplyAsync(completed,token)).ConfigureAwait(false);
            previous=source.EventId;count++;
        }
        return count;
    }
}

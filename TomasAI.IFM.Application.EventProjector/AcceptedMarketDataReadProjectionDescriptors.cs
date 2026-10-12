using System.Collections.Immutable;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.EventProjector;

/// <summary>Audited reconstruction actions for accepted reference imports and download outcomes.</summary>
/// <remarks>Acquisition request events are deliberately absent. These actions use only persisted accepted facts and
/// never call a provider, publish notifications or dispatch another command.</remarks>
public static class AcceptedMarketDataReadProjectionDescriptors
{
    /// <summary>Creates explicit materialization contracts for a controlled, quiesced reconstruction.</summary>
    public static IReadOnlyCollection<EventProjectionDescriptor> Create(IDbContextFactory databases, IPostgresEventTransaction transactions)
    {
        ArgumentNullException.ThrowIfNull(databases);
        ArgumentNullException.ThrowIfNull(transactions);
        return ImmutableArray.Create(
            Describe<EconomicCalendarsImportedCompleteEvent>((source,token) => transactions.ExecuteOnceAsync(async (transaction,cancellation) =>
            {
                await MarketDataDbContextExtensions.ImportEconomicCalendarsAsync(transaction,source.EconomicCalendars,source.DuplicatePolicy,source.CommandId,cancellation).ConfigureAwait(false);
                return true;
            },token)),
            Describe<YieldCurveRatesImportedCompleteEvent>((source,token) => transactions.ExecuteOnceAsync(async (transaction,cancellation) =>
            {
                await MarketDataDbContextExtensions.ImportYieldCurvesAsync(transaction,source.YieldCurveRates,source.DuplicatePolicy,source.CommandId,source.ReceivedOn,cancellation).ConfigureAwait(false);
                return true;
            },token)),
            Describe<MarketDataDownloadLogInsertedEvent>((source,token) => transactions.ExecuteOnceAsync(async (transaction,cancellation) =>
            {
                await databases.MarketDataDb.ProjectDownloadLogAsync(transaction,source.Outcome,source.CommandId,source.PayloadSha256,cancellation).ConfigureAwait(false);
                return true;
            },token)));
    }

    static EventProjectionDescriptor Describe<TEvent>(Func<TEvent,CancellationToken,Task> materialize) where TEvent : IEvent =>
        new EventProjectionDescriptor(typeof(TEvent),EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            (_,_) => throw new InvalidOperationException("Use the materialization-only action for accepted market-data reconstruction."),
            _ => null,(_,_) => null,publishProcessingEvent:false,useDurableReplay:false,publishTerminalEvent:false)
            .WithReadModelRebuild((source,token) => materialize((TEvent)source,token));
}

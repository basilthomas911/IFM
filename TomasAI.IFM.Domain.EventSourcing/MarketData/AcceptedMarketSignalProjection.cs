using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.EventSourcing.MarketData;

/// <summary>Routes accepted market-data source events to transaction-bound read-model mutations.</summary>
/// <remarks>Owns event dispatch and daily observation mapping; never requests provider data or recalculates signals.</remarks>
public static class AcceptedMarketSignalProjection
{
    /// <summary>Projects accepted signal facts, returning false for control events without a read model.</summary>
    /// <param name="transaction">The source-event transaction.</param>
    /// <param name="source">The accepted source event.</param>
    /// <param name="token">Cancels the operation.</param>
    /// <returns>Whether the event has a supported read projection.</returns>
    public static async Task<bool> TryProjectAsync(IEnlistedPostgresTransaction transaction, IEvent source, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(source);
        var projection = source switch
        {
            TradeLiveFeedTurnedOnEvent accepted => MarketDataDbContextExtensions.PersistTradeLiveFeedAsync(transaction,accepted,token),
            TradeLiveFeedTurnedOffEvent accepted => MarketDataDbContextExtensions.DeleteTradeLiveFeedAsync(transaction,accepted,token),
            FuturesEodDataInsertedEvent accepted => MarketDataDbContextExtensions.PersistAcceptedRawEodAsync(transaction,accepted,token),
            VixFuturesEodDataInsertedEvent accepted => MarketDataDbContextExtensions.PersistAcceptedVixEodAsync(transaction,accepted,token),
            FuturesBarDataInsertedEvent accepted => MarketDataDbContextExtensions.PersistAcceptedFuturesBarAsync(transaction,accepted,token),
            FuturesBarDataDeletedEvent accepted => MarketDataDbContextExtensions.DeleteAcceptedFuturesBarAsync(transaction,accepted,token),
            FuturesTradeSessionBarPublishedEvent accepted => ProjectSessionBarAsync(transaction,accepted.Bar,token),
            FuturesClosingPriceInsertedEvent accepted => MarketDataDbContextExtensions.PersistAcceptedClosingPriceAsync(transaction,accepted,token),
            MarketOutlookSnapshotInsertedEvent accepted => MarketDataDbContextExtensions.PersistAcceptedMarketOutlookAsync(transaction,accepted,source.EventId,token),
            FuturesAdxSignalGeneratedEvent accepted => MarketDataDbContextExtensions.PersistAcceptedFuturesAdxSignalAsync(transaction,accepted.FuturesAdxSignal,token),
            FuturesAdxDailySignalGeneratedEvent accepted => MarketDataDbContextExtensions.PersistAcceptedFuturesAdxSignalAsync(transaction,accepted.FuturesAdxSignal,token),
            FuturesAtrSignalGeneratedEvent accepted => MarketDataDbContextExtensions.PersistAcceptedFuturesAtrSignalAsync(transaction,accepted.FuturesAtrSignal,token),
            FuturesAtrDailySignalGeneratedEvent accepted => MarketDataDbContextExtensions.PersistAcceptedFuturesAtrSignalAsync(transaction,accepted.FuturesAtrSignal,token),
            FuturesMacdSignalGeneratedEvent accepted => MarketDataDbContextExtensions.PersistAcceptedFuturesMacdSignalAsync(transaction,accepted.FuturesMacdSignal,token),
            FuturesMacdDailySignalGeneratedEvent accepted => MarketDataDbContextExtensions.PersistAcceptedFuturesMacdSignalAsync(transaction,accepted.FuturesMacdSignal,token),
            FuturesRsiSignalGeneratedEvent accepted => MarketDataDbContextExtensions.PersistAcceptedFuturesRsiSignalAsync(transaction,accepted.FuturesRsiSignal,token),
            FuturesRsiDailySignalGeneratedEvent accepted => MarketDataDbContextExtensions.PersistAcceptedFuturesRsiSignalAsync(transaction,accepted.FuturesRsiSignal,token),
            FuturesTdiSignalGeneratedEvent accepted => MarketDataDbContextExtensions.PersistAcceptedFuturesTdiSignalAsync(transaction,accepted.FuturesTdiSignal,token),
            FuturesEmaSignalGeneratedEvent accepted => MarketDataDbContextExtensions.PersistAcceptedFuturesEmaSignalAsync(transaction,accepted.FuturesEmaSignal,token),
            FuturesBbSignalGeneratedEvent accepted => MarketDataDbContextExtensions.PersistAcceptedFuturesBbSignalAsync(transaction,accepted.FuturesBbSignal,token),
            FuturesVwapSignalUpdatedEvent accepted => MarketDataDbContextExtensions.PersistAcceptedFuturesVwapSignalAsync(transaction,accepted.FuturesVwapSignal,token),
            FuturesVxTermStructureSignalUpdatedEvent accepted => MarketDataDbContextExtensions.PersistAcceptedFuturesVxTermStructureSignalAsync(transaction,accepted.FuturesVxTermStructureSignal,token),
            FuturesTradeSignalUpdatedEvent accepted => MarketDataDbContextExtensions.PersistAcceptedFuturesTradeSignalAsync(transaction,accepted.FuturesTradeSignal,source.EventId,token),
            FuturesItiSignalGeneratedEvent accepted => MarketDataDbContextExtensions.PersistAcceptedFuturesItiSignalAsync(transaction,accepted.FuturesItiSignal,source.EventId,token),
            FuturesItiSignalHoldTradeSetEvent accepted => MarketDataDbContextExtensions.PersistAcceptedFuturesItiSignalAsync(transaction,accepted.FuturesItiSignal,source.EventId,token),
            FuturesItiSignalHoldTradeClearedEvent accepted => MarketDataDbContextExtensions.PersistAcceptedFuturesItiSignalAsync(transaction,accepted.FuturesItiSignal,source.EventId,token),
            _ => null
        };
        if (projection is null) return false;
        await projection.ConfigureAwait(false);
        return true;
    }

    /// <summary>Resolves the accepted VX replacement snapshot before its source event is persisted.</summary>
    /// <param name="transaction">The source-event transaction.</param>
    /// <param name="source">The VX source event.</param>
    /// <param name="token">Cancels the operation.</param>
    /// <returns>The event carrying the accepted replacement facts.</returns>
    public static async Task<VixFuturesEodDataInsertedEvent> AcceptVixAsync(IEnlistedPostgresTransaction transaction,
        VixFuturesEodDataInsertedEvent source,CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(source);
        return source with { AcceptedSnapshot = await MarketDataDbContextExtensions.ResolveSnapshotAsync(
            transaction,source.VixFuturesTickData,source.SessionStatistics,token)
            .ConfigureAwait(false) };
    }

    static async Task ProjectSessionBarAsync(IEnlistedPostgresTransaction transaction, FuturesTradeSessionBarReadModel bar, CancellationToken token)
    {
        FuturesEodObservationReadModel? eod = null;
        if (bar.TimeFrame == TimeFrameType.Daily)
        {
            eod = new FuturesEodObservationReadModel
            {
                MarketSeriesIdentity=bar.MarketSeriesIdentity,ContractId=bar.ContractId,ValueDate=bar.ValueDate,
                SessionStartUtc=bar.IntervalStartUtc,SessionEndUtc=bar.IntervalEndUtc,
                Open=bar.Open,High=bar.High,Low=bar.Low,Close=bar.Close,Volume=bar.Volume,
                TradeCount=bar.TradeCount,PriceVolumeSum=bar.PriceVolumeSum,ObservationId=bar.ObservationId,
                FirstSourceSequence=bar.FirstSourceSequence,LastSourceSequence=bar.LastSourceSequence,
                FirstMarketEventUtc=bar.FirstMarketEventUtc,LastMarketEventUtc=bar.LastMarketEventUtc,
                SchemaVersion=bar.SchemaVersion,IsComplete=bar.IsComplete,IsValid=bar.IsValid
            };
        }
        await MarketDataDbContextExtensions.PersistSessionBarFactsAsync(transaction,bar,eod,token).ConfigureAwait(false);
    }
}

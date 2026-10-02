using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Model;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Model;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime;

/// <summary>
/// Converts one live futures trade into the rolling EOD projection input. All
/// reads are current-state queries; the resulting write uses the realtime
/// source/complete/fail lifecycle and is never replayed.
/// </summary>
public static class FuturesTickTradeDataInserted
{
    static readonly string ServiceId = $"{LogSourceType.FuturesTickTradeDataInserted}";

    public static async ValueTask<bool> ExecuteAsync(
        this FuturesTickTradeDataInsertedEvent source,
        IEventActorContext context,
        IMarketDataApi marketDataApi,
        IBlackboardService blackboardService,
        IStatusConsoleWriter statusConsoleWriter,
        IRealtimeProjector<FuturesEodDataRealtimeActor> projector,
        ILogger logger)
        => await ExecuteCoreAsync(source, context, marketDataApi, blackboardService,
            statusConsoleWriter, projector, logger, null).ConfigureAwait(false);

    internal static ValueTask<bool> ExecuteWithCacheAsync(
        this FuturesTickTradeDataInsertedEvent source,
        IEventActorContext context,
        IMarketDataApi marketDataApi,
        IBlackboardService blackboardService,
        IStatusConsoleWriter statusConsoleWriter,
        IRealtimeProjector<FuturesEodDataRealtimeActor> projector,
        ILogger logger,
        FuturesEodTradeReadCache cache)
        => ExecuteCoreAsync(source, context, marketDataApi, blackboardService,
            statusConsoleWriter, projector, logger, cache);

    static async ValueTask<bool> ExecuteCoreAsync(
        FuturesTickTradeDataInsertedEvent source,
        IEventActorContext context,
        IMarketDataApi marketDataApi,
        IBlackboardService blackboardService,
        IStatusConsoleWriter statusConsoleWriter,
        IRealtimeProjector<FuturesEodDataRealtimeActor> projector,
        ILogger logger,
        FuturesEodTradeReadCache? cache)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(marketDataApi);
        ArgumentNullException.ThrowIfNull(blackboardService);
        ArgumentNullException.ThrowIfNull(statusConsoleWriter);
        ArgumentNullException.ThrowIfNull(projector);
        ArgumentNullException.ThrowIfNull(logger);

        if (source.AssetTypeId != AssetTypeId.Futures
            || !marketDataApi.IsTickDataStreamActive(source.EntityId.ContractId))
            return true;

        try
        {
            var stageStarted = Stopwatch.GetTimestamp();
            var contract = await marketDataApi.GetFuturesContractAsync(
                    source.EntityId.ContractId)
                .ConfigureAwait(false);
            LogSlowStage(logger, source, "contract_lookup", stageStarted);
            if (contract is null)
                return true;

            var tickData = ToFuturesTickData(source);
            if (contract.Id.IsVxContract)
            {
                FuturesSessionStatisticsSnapshot? statistics =
                    marketDataApi.TryGetFuturesSessionStatistics(
                        source.EntityId.ContractId,
                        out var currentStatistics)
                    && currentStatistics.ValueDate == source.EntityId.ValueDate
                        ? currentStatistics
                        : null;
                stageStarted = Stopwatch.GetTimestamp();
                var projected = await projector.ProcessRealtimeEventAsync(
                        VxFuturesEodDataEventFactory.Create(source, tickData, statistics))
                    .ConfigureAwait(false);
                LogSlowStage(logger, source, "vx_projection", stageStarted);
                return projected;
            }

            var eodId = new FuturesEodDataId(contract.ContractId, tickData.ValueDate);
            FuturesEodDataV2ReadModel? cached = null;
            if (cache?.TryGet(eodId, source.TickDataId.SequenceId, out cached,
                    out var stale) == true && stale)
                return true;
            stageStarted = Stopwatch.GetTimestamp();
            var insertedEvent = await CreateFuturesInsertedEventAsync(
                    source,
                    context,
                    marketDataApi,
                    blackboardService,
                    contract,
                    tickData,
                    logger,
                    cached)
                .ConfigureAwait(false);
            LogSlowStage(logger, source, "es_prepare", stageStarted);
            if (insertedEvent is null)
                return true;
            stageStarted = Stopwatch.GetTimestamp();
            var result = await projector.ProcessRealtimeEventAsync(insertedEvent).ConfigureAwait(false);
            LogSlowStage(logger, source, "es_projection", stageStarted);
            if (result)
                cache?.Set(eodId, source.TickDataId.SequenceId, insertedEvent.FuturesEodData);
            else
                cache?.Invalidate(eodId);
            return result;
        }
        catch (Exception exception)
        {
            cache?.Invalidate(new FuturesEodDataId(
                source.EntityId.ContractId, source.EntityId.ValueDate));
            await statusConsoleWriter.WriteConsoleAsync(
                LogSourceType.MarketDataFeedEvent,
                6009,
                exception.GetErrorMessage()).ConfigureAwait(false);
            logger.LogErrorEvent(
                ServiceId,
                exception,
                "{EventName} for {ContractId}: realtime futures EOD workflow failed",
                nameof(FuturesTickTradeDataInsertedEvent),
                source.EntityId.ContractId);
            throw;
        }
    }

    static async ValueTask<FuturesEodDataInsertedEvent?> CreateFuturesInsertedEventAsync(
        FuturesTickTradeDataInsertedEvent source,
        IEventActorContext context,
        IMarketDataApi marketDataApi,
        IBlackboardService blackboardService,
        FuturesContractV3ReadModel contract,
        FuturesTickDataV2ReadModel tickData,
        ILogger logger,
        FuturesEodDataV2ReadModel? cachedToday)
    {
        var valueDate = tickData.ValueDate;
        var eodDataToday = cachedToday;
        if (eodDataToday is null)
        {
            var stageStarted = Stopwatch.GetTimestamp();
            eodDataToday = await context.GetFuturesEodDataAsync(
                contract.ContractId, valueDate).ConfigureAwait(false);
            LogSlowStage(logger, source, "eod_today_query", stageStarted);
        }
        var hasCurrentSessionRow = eodDataToday is not null;
        var persistedVolume = eodDataToday?.Volume;
        if (eodDataToday is null)
        {
            var stageStarted = Stopwatch.GetTimestamp();
            eodDataToday = await context.GetLastFuturesEodDataAsync(
                contract.ContractId,
                valueDate).ConfigureAwait(false);
            LogSlowStage(logger, source, "eod_last_query", stageStarted);
        }

        var hasStatistics = marketDataApi.TryGetFuturesSessionStatistics(
                contract.ContractId,
                out var statistics)
            && statistics.IsComplete
            && statistics.ValueDate == valueDate
            && tickData.Price >= statistics.LowPrice
            && tickData.Price <= statistics.HighPrice;
        eodDataToday ??= CreateSessionBaseline(
            contract,
            tickData,
            hasStatistics,
            statistics);
        if (eodDataToday is null)
            return null;
        if (!hasCurrentSessionRow && !hasStatistics)
            return null;
        if (hasStatistics)
        {
            eodDataToday = eodDataToday with
            {
                OpenPrice = statistics.OpenPrice,
                HighPrice = statistics.HighPrice,
                LowPrice = statistics.LowPrice,
                Volume = statistics.HasVolume ? statistics.Volume : eodDataToday.Volume
            };
        }
        else if (marketDataApi.TryGetFuturesSessionStatistics(
                     contract.ContractId,
                     out statistics)
                 && statistics.ValueDate == valueDate
                 && statistics.HasVolume)
        {
            eodDataToday = eodDataToday with { Volume = statistics.Volume };
        }
        if (hasCurrentSessionRow
            && eodDataToday.ClosePrice == tickData.Price
            && (!statistics.HasVolume || persistedVolume == statistics.Volume))
            return null;

        // Rolling OHLC uses only the ES trade and official session statistics.
        // VX, historical ranges and normal-curve availability must not gate it.
        var eodData = FuturesEodDataModel.CreateFuturesEodData(
            valueDate,
            tickData,
            contract,
            eodDataToday);
        var entityId = new FuturesEodDataId(contract.ContractId, valueDate);
        return new FuturesEodDataInsertedEvent
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                FuturesEodDataRealtimeActor.ActorName,
                FuturesEodDataInsertedEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = entityId,
            CommandId = source.CommandId,
            AggregateId = source.AggregateId,
            EventSource = nameof(FuturesTickTradeDataInsertedEvent),
            ReceivedOn = DateTime.UtcNow,
            FuturesEodData = eodData,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = source.UserName
        };
    }

    static void LogSlowStage(ILogger logger, FuturesTickTradeDataInsertedEvent source,
        string stage, long started)
    {
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        if (elapsed >= 250)
            logger.LogInformation(
                "Futures EOD trade stage: {ContractId}; SourceId={SourceId}; stage={Stage}; elapsed {ElapsedMilliseconds:F3} ms.",
                source.EntityId.ContractId, source.Id, stage, elapsed);
    }

    internal static FuturesEodDataV2ReadModel? CreateSessionBaseline(
        FuturesContractV3ReadModel contract,
        FuturesTickDataV2ReadModel tickData,
        bool hasStatistics,
        FuturesSessionStatisticsSnapshot statistics)
    {
        if (!hasStatistics)
            return null;

        return new FuturesEodDataV2ReadModel(
            contract.ContractId,
            tickData.ValueDate,
            contract.Symbol,
            statistics.OpenPrice,
            statistics.HighPrice,
            statistics.LowPrice,
            tickData.Price,
            statistics.HasVolume ? statistics.Volume : tickData.Size,
            FuturesSessionPriceCalculator.CalculateDailyPercentChange(
                tickData.Price,
                statistics.OpenPrice),
            priceDirection: FuturesSessionPriceCalculator.CalculatePriceDirection(
                tickData.Price,
                statistics.OpenPrice));
    }

    internal static FuturesTickDataV2ReadModel ToFuturesTickData(
        FuturesTickTradeDataInsertedEvent source) => new(
        source.EntityId.ContractId,
        source.EntityId.ValueDate,
        source.TickDataId.SequenceId,
        ToTimeOnly(source.TradeData.EventTimestampNanoseconds),
        source.TradeData.Price,
        checked((int)source.TradeData.Size));

    static TimeOnly ToTimeOnly(long unixNanoseconds)
    {
        try
        {
            return TimeOnly.FromDateTime(
                DateTimeOffset.UnixEpoch.AddTicks(unixNanoseconds / 100L).UtcDateTime);
        }
        catch (ArgumentOutOfRangeException)
        {
            return TimeOnly.MinValue;
        }
    }
}

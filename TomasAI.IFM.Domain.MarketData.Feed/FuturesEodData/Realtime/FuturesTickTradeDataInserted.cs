using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Model;
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
internal static class FuturesTickTradeDataInserted
{
    static readonly string ServiceId = $"{LogSourceType.FuturesTickTradeDataInserted}";

    internal static async ValueTask<bool> ExecuteAsync(
        this FuturesTickTradeDataInsertedEvent source,
        IEventActorContext context,
        IMarketDataApi marketDataApi,
        IBlackboardService blackboardService,
        IStatusConsoleWriter statusConsoleWriter,
        IRealtimeProjector<FuturesEodDataRealtimeActor> projector,
        ILogger logger)
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
            var contract = await marketDataApi.GetFuturesContractAsync(
                    source.EntityId.ContractId)
                .ConfigureAwait(false);
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
                return await projector.ProcessRealtimeEventAsync(
                        VxFuturesEodDataEventFactory.Create(source, tickData, statistics))
                    .ConfigureAwait(false);
            }

            var insertedEvent = await CreateFuturesInsertedEventAsync(
                    source,
                    context,
                    marketDataApi,
                    blackboardService,
                    contract,
                    tickData)
                .ConfigureAwait(false);
            return insertedEvent is null
                || await projector.ProcessRealtimeEventAsync(insertedEvent)
                    .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
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
        FuturesContractV2ReadModel contract,
        FuturesTickDataV2ReadModel tickData)
    {
        var valueDate = tickData.ValueDate;
        var eodDataToday = await context.GetFuturesEodDataAsync(
            contract.ContractId,
            valueDate).ConfigureAwait(false);
        var hasCurrentSessionRow = eodDataToday is not null;
        var persistedVolume = eodDataToday?.Volume;
        eodDataToday ??= await context.GetLastFuturesEodDataAsync(
            contract.ContractId,
            valueDate).ConfigureAwait(false);

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

        var vixContractId = blackboardService.MarketDataFeed.VixFuturesContractId.Get(valueDate);
        if (string.IsNullOrWhiteSpace(vixContractId))
            return null;

        var vixData = blackboardService.MarketDataFeed.VixFuturesEodData.Get(
            vixContractId,
            valueDate);
        if (vixData.Count == 0)
        {
            vixData = await context.GetVixFuturesEodDataAsync(
                vixContractId,
                valueDate).ConfigureAwait(false);
            if (vixData.Count == 0)
                return null;
            blackboardService.MarketDataFeed.VixFuturesEodData.Set(
                vixContractId,
                valueDate,
                vixData);
        }

        var eodDataRange = await blackboardService.MarketDataFeed.FuturesEodDataRange.GetAsync(
                contract.ContractId,
                valueDate,
                (contractId, startDate, endDate) =>
                    context.GetFuturesEodDataByDateRangeAsync(contractId, startDate, endDate))
            .ConfigureAwait(false);
        var normalCurve = await blackboardService.MarketDataFeed.NormalCurveTable.GetAsync(
                valueDate,
                () => context.GetNormalCurveTableAsync()!)
            .ConfigureAwait(false);
        if (normalCurve is null)
            return null;

        var eodData = FuturesEodDataModel.CreateFuturesEodData(
            valueDate,
            tickData,
            contract,
            eodDataToday,
            eodDataRange,
            normalCurve,
            20,
            vixData);
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

    internal static FuturesEodDataV2ReadModel? CreateSessionBaseline(
        FuturesContractV2ReadModel contract,
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
            FuturesSessionStatisticsUpdated.CalculateDailyPercentChange(
                tickData.Price,
                statistics.OpenPrice),
            priceDirection: FuturesSessionStatisticsUpdated.CalculatePriceDirection(
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

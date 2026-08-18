using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime;

internal static class FuturesSessionStatisticsUpdated
{
    internal static async ValueTask<bool> ExecuteAsync(
        this FuturesSessionStatisticsUpdatedRealtimeEvent source,
        IEventActorContext context,
        IRealtimeProjector<FuturesEodDataRealtimeActor> projector,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(projector);
        ArgumentNullException.ThrowIfNull(logger);

        var statistics = source.Statistics;
        if (!statistics.IsComplete
            || !StringComparer.Ordinal.Equals(
                statistics.ContractId,
                source.EntityId.ContractId)
            || statistics.ValueDate != source.EntityId.ValueDate)
            return true;

        var current = await context.GetFuturesEodDataAsync(
                source.EntityId.ContractId,
                source.EntityId.ValueDate)
            .ConfigureAwait(false);
        if (current is null)
            return true;

        // Mixed-schema delivery can momentarily place a trade ahead of its matching
        // official high/low update. Wait for the next statistics observation rather
        // than persisting an internally inconsistent EOD row.
        if (current.ClosePrice < statistics.LowPrice
            || current.ClosePrice > statistics.HighPrice)
            return true;

        var dailyPercentChange = CalculateDailyPercentChange(
            current.ClosePrice,
            statistics.OpenPrice);
        var priceDirection = CalculatePriceDirection(
            current.ClosePrice,
            statistics.OpenPrice);
        if (current.OpenPrice == statistics.OpenPrice
            && current.HighPrice == statistics.HighPrice
            && current.LowPrice == statistics.LowPrice
            && current.DailyPercentChange == dailyPercentChange
            && current.PriceDirection == priceDirection)
            return true;

        var updated = current with
        {
            OpenPrice = statistics.OpenPrice,
            HighPrice = statistics.HighPrice,
            LowPrice = statistics.LowPrice,
            DailyPercentChange = dailyPercentChange,
            PriceDirection = priceDirection
        };
        var entityId = new FuturesEodDataId(current.ContractId, current.ValueDate);
        var projected = new FuturesEodSessionStatisticsUpdatedEvent
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                FuturesEodDataRealtimeActor.ActorName,
                FuturesEodSessionStatisticsUpdatedEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = entityId,
            CommandId = source.CommandId,
            AggregateId = source.AggregateId,
            EventSource = nameof(FuturesSessionStatisticsUpdatedRealtimeEvent),
            ReceivedOn = DateTime.UtcNow,
            FuturesEodData = updated,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = source.UserName
        };
        return await projector.ProcessRealtimeEventAsync(projected)
            .ConfigureAwait(false);
    }

    internal static double CalculateDailyPercentChange(
        decimal closePrice,
        decimal openPrice) => openPrice <= 0m
            ? 0d
            : Convert.ToDouble(Math.Round((closePrice - openPrice) / openPrice, 4));

    internal static PriceDirectionType CalculatePriceDirection(
        decimal closePrice,
        decimal openPrice) => closePrice switch
        {
            _ when closePrice > openPrice => PriceDirectionType.Rising,
            _ when closePrice < openPrice => PriceDirectionType.Falling,
            _ => PriceDirectionType.Rising
        };
}

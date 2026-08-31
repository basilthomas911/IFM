using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Model;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
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
        if (!statistics.HasAnyData
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
        {
            var vxRows = await context.GetVixFuturesEodDataAsync(
                    source.EntityId.ContractId,
                    source.EntityId.ValueDate)
                .ConfigureAwait(false);
            var vxCurrent = vxRows.SingleOrDefault(row =>
                StringComparer.Ordinal.Equals(row.ContractId, source.EntityId.ContractId)
                && row.ValueDate == source.EntityId.ValueDate);
            if (vxCurrent is null)
                return true;
            var tick = new FuturesTickDataV2ReadModel(
                vxCurrent.ContractId,
                vxCurrent.ValueDate,
                statistics.SourceSequence,
                TimeOnly.FromDateTime(DateTime.UtcNow),
                vxCurrent.ClosePrice,
                0);
            return await projector.ProcessRealtimeEventAsync(
                    VxFuturesEodDataEventFactory.Create(source, tick, statistics))
                .ConfigureAwait(false);
        }

        // Mixed-schema delivery can momentarily place a trade ahead of its matching
        // official high/low update. Wait for the next statistics observation rather
        // than persisting an internally inconsistent EOD row.
        var updatePrices = statistics.HasPriceStatistics
            && current.ClosePrice >= statistics.LowPrice
            && current.ClosePrice <= statistics.HighPrice;
        var dailyPercentChange = updatePrices
            ? FuturesSessionPriceCalculator.CalculateDailyPercentChange(
                current.ClosePrice,
                statistics.OpenPrice)
            : current.DailyPercentChange;
        var priceDirection = updatePrices
            ? FuturesSessionPriceCalculator.CalculatePriceDirection(
                current.ClosePrice,
                statistics.OpenPrice)
            : current.PriceDirection;
        var volume = statistics.HasVolume ? statistics.Volume : current.Volume;
        if ((!updatePrices
             || (current.OpenPrice == statistics.OpenPrice
                 && current.HighPrice == statistics.HighPrice
                 && current.LowPrice == statistics.LowPrice
                 && current.DailyPercentChange == dailyPercentChange
                 && current.PriceDirection == priceDirection))
            && current.Volume == volume)
            return true;

        var updated = current with
        {
            OpenPrice = updatePrices ? statistics.OpenPrice : current.OpenPrice,
            HighPrice = updatePrices ? statistics.HighPrice : current.HighPrice,
            LowPrice = updatePrices ? statistics.LowPrice : current.LowPrice,
            Volume = volume,
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

}

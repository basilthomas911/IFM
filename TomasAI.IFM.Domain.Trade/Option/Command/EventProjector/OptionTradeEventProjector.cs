using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Option.Command.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Option.Command.EventProjector;

public sealed class OptionTradeEventProjector(
    IDbContextFactory dbFactory, IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext dbEventSource, IBlackboardService blackboardService,
    ILogger<OptionTradeEventProjector> logger, EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<OptionTradeCommandActor>(durableReplayQueue, dbEventSource, blackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        DescribeNotification<OptionTradeOrderPlacedEvent, OptionTradeEntityId>(e => InsertOptionTradeAsync(dbFactory.TradeDb, e)),
        DescribeNotification<OptionTradeToOpenEvent, OptionTradeEntityId>(e => ReplaceOptionTradeAsync(dbFactory.TradeDb, e.OptionTrade)),
        DescribeNotification<OptionTradeToCloseEvent, OptionTradeEntityId>(e => ReplaceOptionTradeAsync(dbFactory.TradeDb, e.OptionTrade)),
        DescribeNotification<OptionTradeSnapshotEvent, OptionTradeEntityId>(),
        DescribeNotification<OptionTradePositionOpenedEvent, OptionTradeEntityId>(),
        DescribeNotification<OptionTradePositionClosedEvent, OptionTradeEntityId>(),
        DescribeNotification<OptionTradeEndOfDayProcessedEvent, OptionTradeEntityId>(),
        DescribeNotification<OptionTradeSpreadDistributionStatisticsUpdatedEvent, OptionTradeEntityId>(),
        DescribeNotification<OptionTradeSpreadDataInsertedEvent, OptionTradeEntityId>(
            (e, context) => dbFactory.TradeDb.InsertOptionTradeSpreadDataAsync(
                e.OptionTradeSpreadData with
                {
                    SequenceId = e.OptionTradeSpreadData.SequenceId > 0
                        ? e.OptionTradeSpreadData.SequenceId
                        : context.EventId
                })),
        DescribeNotification<OptionTradeSpreadBarDataInsertedEvent, OptionTradeEntityId>(e => dbFactory.TradeDb.InsertOptionTradeSpreadBarDataAsync(e.OptionTradeSpreadBarData)),
        DescribeNotification<OptionTradeSpreadBarDataDeletedEvent, OptionTradeEntityId>(e => dbFactory.TradeDb.DeleteOptionTradeSpreadBarDataAsync(e.OrderId, e.TradeId, e.ValueDate, e.TradeType)),
        DescribeLocal<TradePositionAddedEvent>(e => dbFactory.TradeDb.InsertTradePositionAsync(e.TradePosition)),
        DescribeNotification<TradePositionUpdatedEvent, OptionTradeEntityId>(e => UpdateTradePositionAsync(dbFactory.TradeDb, e)),
        DescribeNotification<TradePositionStatusUpdatedEvent, OptionTradeEntityId>(e => dbFactory.TradeDb.UpdateTradePositionStatusAsync(
            e.OrderId, e.TradeId, e.TradeType, e.ValueDate, e.DaysToExpiry,
            e.OldTradeStatus, e.NewTradeStatus, e.UpdatedOn, e.UpdatedBy)),
        DescribeNotification<OptionTradeDeletedEvent, OptionTradeEntityId>(e => dbFactory.TradeDb.DeleteOptionTradeAsync(e.OrderId, e.TradeId)),
        DescribeNotification<OptionTradeDailyProfitTargetUpdatedEvent, OptionTradeEntityId>(e => dbFactory.TradeDb.UpdateTradeLimitDailyProfitTarget(
            e.TradeId, e.TradeType, e.DailyProfitTarget, e.UpdatedOn, e.UpdatedBy))
    ];

    static async Task ReplaceOptionTradeAsync(ITradeDbContext db, OptionTradeReadModel trade)
    {
        await db.DeleteOptionTradeAsync(trade.OrderId, trade.TradeId).ConfigureAwait(false);
        await db.InsertOptionTradeAsync(trade).ConfigureAwait(false);
    }

    static Task InsertOptionTradeAsync(ITradeDbContext db, OptionTradeOrderPlacedEvent e)
    {
        var optionTrade = new OptionTradeReadModel(
                e.OptionTrade.OrderId, e.OptionTrade.TradeId, e.OptionTrade.TradeStrategy,
                e.OptionTrade.TradeDate, e.OptionTrade.MaturityDate, e.OptionTrade.TradeType,
                e.OptionTrade.TradeState, e.OptionTrade.TradeAction, e.OptionTrade.UnderlyingContractId,
                e.OptionTrade.UnderlyingAssetType, e.OptionTrade.IsPrimaryTrade, e.OptionTrade.IsHedgeTrade,
                e.CreatedOn, e.CreatedBy, e.CreatedOn, e.CreatedBy)
            .AddOptionLegs(e.OptionTrade.OptionLegs ?? [])
            .AddTradePosition(e.OptionTrade.TradePositions ?? [])
            .SetTradeLimit(e.OptionTrade.TradeLimit!)
            .AddTradeTypeLimits(e.OptionTrade.TradeTypeLimits ?? [])
            .AddTradeFills(e.OptionTrade.TradeFills ?? []);
        return db.InsertOptionTradeAsync(optionTrade);
    }

    static Task UpdateTradePositionAsync(ITradeDbContext db, TradePositionUpdatedEvent e)
        => e.TradePositionChangeSource switch
        {
            TradePositionChangeSourceType.PutCreditSpreadLeg => db.InsertTradePositionAsync(e.PutTradePosition!),
            TradePositionChangeSourceType.CallCreditSpreadLeg => db.InsertTradePositionAsync(e.CallTradePosition!),
            TradePositionChangeSourceType.SpreadDistributionStatistics => db.InsertTradePositionAsync([e.PutTradePosition!, e.CallTradePosition!]),
            _ => Task.CompletedTask
        };

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes => _descriptors.Select(static x => x.SourceEventType).ToArray();
}

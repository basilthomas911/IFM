using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.Extensions;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.EventProjector;

/// <summary>Projects committed YieldCurveRate CRUD facts and delivers their correlated outcomes.</summary>
public sealed class YieldCurveRateEventProjector(ICommandActorContext<YieldCurveRateCommandActor> context,
    IDurableReplayQueue queue, IBlackboardService blackboard, EventProjectorReliabilityOptions? options = null)
    : ConventionalEventProjector<YieldCurveRateCommandActor>(queue, context.DbEventSource,
        blackboard, context.Logger, options)
{
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors { get; } =
    [
        Describe<YieldCurveRateAddedEvent, YieldCurveRateAddedCompleteEvent, YieldCurveRateAddedFailEvent, YieldCurveRateEntityId>(e => Apply(context.DbFactory.MarketDataDb, e, static (db, e) => db.InsertYieldCurveRateAsync(e.YieldCurveRate)))
            .WithReadModelRebuild((source, token) => Apply(context.DbFactory.MarketDataDb, (YieldCurveRateAddedEvent)source, static (db, e) => db.InsertYieldCurveRateAsync(e.YieldCurveRate), token)),
        Describe<YieldCurveRateChangedEvent, YieldCurveRateChangedCompleteEvent, YieldCurveRateChangedFailEvent, YieldCurveRateEntityId>(e => Apply(context.DbFactory.MarketDataDb, e, static (db, e) => db.InsertYieldCurveRateAsync(e.YieldCurveRate)))
            .WithReadModelRebuild((source, token) => Apply(context.DbFactory.MarketDataDb, (YieldCurveRateChangedEvent)source, static (db, e) => db.InsertYieldCurveRateAsync(e.YieldCurveRate), token)),
        Describe<YieldCurveRateRemovedEvent, YieldCurveRateRemovedCompleteEvent, YieldCurveRateRemovedFailEvent, YieldCurveRateEntityId>(e => Apply(context.DbFactory.MarketDataDb, e, static (db, e) => db.DeleteYieldCurveRateAsync(e.ValueDate)))
            .WithReadModelRebuild((source, token) => Apply(context.DbFactory.MarketDataDb, (YieldCurveRateRemovedEvent)source, static (db, e) => db.DeleteYieldCurveRateAsync(e.ValueDate), token))
    ];

    protected override bool ShouldDiscardProjection(IEvent domainEvent) =>
        domainEvent is IRequireDurableProjection { RequiresDurableProjection: false };

    private static Task Apply<T>(IMarketDataDbContext db, T source,
        Func<IMarketDataDbContext, T, Task> apply, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        return apply(db, source);
    }
}

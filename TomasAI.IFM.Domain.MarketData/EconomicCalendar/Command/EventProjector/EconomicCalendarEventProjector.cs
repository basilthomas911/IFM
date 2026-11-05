using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.Extensions;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.EventProjector;

/// <summary>Projects committed EconomicCalendar CRUD facts and delivers their correlated outcomes.</summary>
public sealed class EconomicCalendarEventProjector(ICommandActorContext<EconomicCalendarCommandActor> context,
    IDurableReplayQueue queue, IBlackboardService blackboard, EventProjectorReliabilityOptions? options = null)
    : ConventionalEventProjector<EconomicCalendarCommandActor>(queue, context.DbEventSource,
        blackboard, context.Logger, options)
{
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors { get; } =
    [
        Describe<EconomicCalendarAddedEvent, EconomicCalendarAddedCompleteEvent, EconomicCalendarAddedFailEvent, EconomicCalendarId>(e => Apply(context.DbFactory.MarketDataDb, e, static (db, e) => db.InsertEconomicCalendarAsync(e.EconomicCalendar!)))
            .WithReadModelRebuild((source, token) => Apply(context.DbFactory.MarketDataDb, (EconomicCalendarAddedEvent)source, static (db, e) => db.InsertEconomicCalendarAsync(e.EconomicCalendar!), token)),
        Describe<EconomicCalendarChangedEvent, EconomicCalendarChangedCompleteEvent, EconomicCalendarChangedFailEvent, EconomicCalendarId>(e => Apply(context.DbFactory.MarketDataDb, e, static (db, e) => db.UpdateEconomicCalendarAsync(e.EntityId, e.EconomicCalendar!)))
            .WithReadModelRebuild((source, token) => Apply(context.DbFactory.MarketDataDb, (EconomicCalendarChangedEvent)source, static (db, e) => db.UpdateEconomicCalendarAsync(e.EntityId, e.EconomicCalendar!), token)),
        Describe<EconomicCalendarRemovedEvent, EconomicCalendarRemovedCompleteEvent, EconomicCalendarRemovedFailEvent, EconomicCalendarId>(e => Apply(context.DbFactory.MarketDataDb, e, static (db, e) => db.DeleteEconomicCalendarAsync(e.EntityId)))
            .WithReadModelRebuild((source, token) => Apply(context.DbFactory.MarketDataDb, (EconomicCalendarRemovedEvent)source, static (db, e) => db.DeleteEconomicCalendarAsync(e.EntityId), token))
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

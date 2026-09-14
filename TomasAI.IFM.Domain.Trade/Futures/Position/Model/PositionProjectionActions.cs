using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Model;

internal static class PositionProjectionActions
{
    public static async Task ProjectAsync(
        ICommandActorContext context,
        IDbContextFactory dbFactory,
        PositionChangedEvent changed,
        string positionActor,
        string realtimeActor,
        string tradePlanRealtimeActor)
    {
        await dbFactory.TradeDb.UpsertStrategyPositionAsync(changed.State).ConfigureAwait(false);
        await PublishTradePlanUpdateAsync(context, changed, tradePlanRealtimeActor).ConfigureAwait(false);

        if (changed.State.Phase is StrategyPositionPhase.Open or
            StrategyPositionPhase.Close or StrategyPositionPhase.Correction)
        {
            await dbFactory.TradeDb
                .ReplaceOpenPositionRoutesAsync(changed.State)
                .ConfigureAwait(false);
            await context.SendAsync<OpenPositionRoutesChangedEvent, StrategyPositionId>(new OpenPositionRoutesChangedEvent
            {
                Subject = new ActorSubject(
                    ActorType.Realtime,
                    realtimeActor,
                    OpenPositionRoutesChangedEvent.Verb,
                    changed.EntityId.Format()),
                EntityId = changed.EntityId,
                Id = changed.Id,
                EventId = changed.EventId,
                CommandId = changed.CommandId,
                AggregateId = changed.AggregateId,
                EventSource = changed.EventSource,
                ReceivedOn = changed.ReceivedOn,
                Position = changed.State
            }).ConfigureAwait(false);
        }

        switch (changed.State.Phase)
        {
            case StrategyPositionPhase.Open:
                await context.SendAsync<StrategyPositionOpenedEvent, StrategyPositionId>(
                    CreateBoundary<StrategyPositionOpenedEvent>(changed, "StrategyPositionOpened"))
                    .ConfigureAwait(false);
                break;
            case StrategyPositionPhase.Close:
                await context.SendAsync<StrategyPositionClosedEvent, StrategyPositionId>(
                    CreateBoundary<StrategyPositionClosedEvent>(changed, "StrategyPositionClosed"))
                    .ConfigureAwait(false);
                break;
            case StrategyPositionPhase.Correction:
                await context.SendAsync<StrategyPositionCorrectedEvent, StrategyPositionId>(
                    CreateBoundary<StrategyPositionCorrectedEvent>(changed, "StrategyPositionCorrected"))
                    .ConfigureAwait(false);
                break;
        }
    }

    static ValueTask PublishTradePlanUpdateAsync(
        ICommandActorContext context,
        PositionChangedEvent changed,
        string realtimeActor)
    {
        var subject = new ActorSubject(
            ActorType.Realtime,
            realtimeActor,
            PositionChangedEvent.Verb,
            changed.EntityId.Format());
        return changed switch
        {
            Shared.Futures.Option.Position.IronCondorPositionChangedEvent ironCondor =>
                context.SendAsync<Shared.Futures.Option.Position.IronCondorPositionChangedEvent, StrategyPositionId>(
                    ironCondor with { Subject = subject }),
            Shared.Futures.Option.Position.VerticalSpreadPositionChangedEvent verticalSpread =>
                context.SendAsync<Shared.Futures.Option.Position.VerticalSpreadPositionChangedEvent, StrategyPositionId>(
                    verticalSpread with { Subject = subject }),
            Shared.Futures.Position.FuturesPositionChangedEvent futures =>
                context.SendAsync<Shared.Futures.Position.FuturesPositionChangedEvent, StrategyPositionId>(
                    futures with { Subject = subject }),
            _ => throw new InvalidOperationException($"Unsupported position event {changed.GetType().Name}.")
        };
    }

    static TBoundary CreateBoundary<TBoundary>(PositionChangedEvent changed, string verb)
        where TBoundary : PositionBoundaryEvent, new() => new()
        {
            Subject = new ActorSubject(
                ActorType.Notify,
                PositionBoundaryEvent.Actor,
                verb,
                changed.EntityId.Format()),
            EntityId = changed.EntityId,
            Id = changed.Id,
            EventId = changed.EventId,
            CommandId = changed.CommandId,
            AggregateId = changed.AggregateId,
            EventSource = changed.EventSource,
            ReceivedOn = changed.ReceivedOn,
            Position = changed.State
        };
}

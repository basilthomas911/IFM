using System.Collections.Immutable;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Futures.Position.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Command.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared.Futures;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Command.EventProjector;

public sealed class FuturesTradeEventProjector : ConventionalEventProjector<FuturesTradeCommandActor>
{
    readonly IFuturesTradeCommandContext context;
    readonly ImmutableArray<EventProjectionDescriptor> descriptors;

    public FuturesTradeEventProjector(
        ICommandActorContext<FuturesTradeCommandActor> actorContext,
        EventProjectorReliabilityOptions? options = null)
        : base(Typed(actorContext).DurableReplayQueue, Typed(actorContext).DbEventSource,
            Typed(actorContext).BlackboardService, Typed(actorContext).Logger, options)
    {
        context = Typed(actorContext);
        descriptors =
        [
            DescribeNotification<FuturesTradeChangedEvent, TradeEntityId>(ProjectAsync)
        ];
    }

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes => [typeof(FuturesTradeChangedEvent)];

    async Task ProjectAsync(FuturesTradeChangedEvent changed)
    {
        await context.DbFactory.TradeDb.UpsertEstablishedTradeAsync(changed.State).ConfigureAwait(false);
        if (!changed.IsInitialEstablishment) return;

        var positionId = new StrategyPositionId(
            changed.State.Id,
            TradeHandoffIdentity.Create("futures-position", changed.State.Id.Format()));
        var command = new OpenFuturesPositionCommand
        {
            CommandId = TradeHandoffIdentity.Create("open-futures-position", changed.State.Id.Format()),
            Subject = new ActorSubject(
                ActorType.Command,
                FuturesTradePositionCommandActor.ActorName,
                OpenFuturesPositionCommand.Verb,
                positionId.Format()),
            EntityId = positionId,
            Trade = changed.State,
            EffectiveAtUtc = changed.State.EstablishedAtUtc
        };
        var result = await context.ActorService
            .SendAsync<OpenFuturesPositionCommand, StrategyPositionId>(command, positionId)
            .ConfigureAwait(false);
        if (!result.Success)
            throw new InvalidOperationException(
                $"FUTURES_TRADE.HANDOFF_FAILED;{result.ErrorCode};{result.ErrorMessage}");
    }

    static IFuturesTradeCommandContext Typed(ICommandActorContext<FuturesTradeCommandActor> context) =>
        context as IFuturesTradeCommandContext ??
        throw new ArgumentException("Typed Futures Trade context required.");
}

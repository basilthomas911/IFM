using System.Collections.Immutable;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Command.Actor;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Command.EventProjector;

public sealed class FuturesOptionTradeEventProjector :
    ConventionalEventProjector<FuturesOptionTradeCommandActor>
{
    readonly IFuturesOptionTradeCommandContext _context;
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors;

    public FuturesOptionTradeEventProjector(
        ICommandActorContext<FuturesOptionTradeCommandActor> actorContext,
        EventProjectorReliabilityOptions? options = null)
        : base(
            Typed(actorContext).DurableReplayQueue,
            Typed(actorContext).DbEventSource,
            Typed(actorContext).BlackboardService,
            Typed(actorContext).Logger,
            options)
    {
        _context = Typed(actorContext);
        _descriptors =
        [
            DescribeNotification<OptionTradeChangedEvent, TradeEntityId>(ProjectAsync)
        ];
    }

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors =>
        _descriptors;

    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        [typeof(OptionTradeChangedEvent)];

    async Task ProjectAsync(OptionTradeChangedEvent changed)
    {
        await _context.DbFactory.TradeDb.UpsertEstablishedTradeAsync(changed.State);
        if (!changed.IsInitialEstablishment)
            return;

        var positionId = new StrategyPositionId(
            changed.State.Id,
            TradeHandoffIdentity.Create(
                "strategy-position",
                changed.State.Id.Format()));

        if (changed.State.StrategyKind == TradeStrategyKind.IronCondor)
        {
            var command = new OpenIronCondorPositionCommand
            {
                CommandId = TradeHandoffIdentity.Create(
                    "open-iron-condor",
                    changed.State.Id.Format()),
                Subject = new ActorSubject(
                    ActorType.Command,
                    FuturesIronCondorTradePositionCommandActor.ActorName,
                    OpenIronCondorPositionCommand.Verb,
                    positionId.Format()),
                EntityId = positionId,
                Trade = changed.State,
                EffectiveAtUtc = changed.State.EstablishedAtUtc
            };
            EnsureSuccessful(
                await _context.ActorService.SendAsync<
                    OpenIronCondorPositionCommand,
                    StrategyPositionId>(command, positionId));
        }
        else if (changed.State.StrategyKind == TradeStrategyKind.VerticalSpread)
        {
            var command = new OpenVerticalSpreadPositionCommand
            {
                CommandId = TradeHandoffIdentity.Create(
                    "open-vertical-spread",
                    changed.State.Id.Format()),
                Subject = new ActorSubject(
                    ActorType.Command,
                    FuturesVerticalSpreadTradePositionCommandActor.ActorName,
                    OpenVerticalSpreadPositionCommand.Verb,
                    positionId.Format()),
                EntityId = positionId,
                Trade = changed.State,
                EffectiveAtUtc = changed.State.EstablishedAtUtc
            };
            EnsureSuccessful(
                await _context.ActorService.SendAsync<
                    OpenVerticalSpreadPositionCommand,
                    StrategyPositionId>(command, positionId));
        }
    }

    static void EnsureSuccessful(ServiceResult<Guid> result)
    {
        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"OPTION_TRADE.HANDOFF_FAILED;{result.ErrorCode};{result.ErrorMessage}");
        }
    }

    static IFuturesOptionTradeCommandContext Typed(
        ICommandActorContext<FuturesOptionTradeCommandActor> context) =>
        context as IFuturesOptionTradeCommandContext
        ?? throw new ArgumentException("Typed Futures Option Trade context required.");
}

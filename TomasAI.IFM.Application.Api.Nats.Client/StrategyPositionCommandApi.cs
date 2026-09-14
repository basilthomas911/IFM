using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Nats.Client;

/// <summary>Sends typed lifecycle commands to futures and futures-option strategy-position actors.</summary>
/// <param name="producer">The actor-message producer used to issue request/reply commands.</param>
public sealed class StrategyPositionCommandApi(IActorProducer producer)
    : NatsClientApi(producer), IStrategyPositionCommandApi
{
    /// <inheritdoc />
    public Task<ServiceResult<Guid>> EndOfDayAsync(
        StrategyPositionId positionId,
        TradeStrategyKind strategyKind,
        DateTime effectiveAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (!positionId.IsValid)
            throw new ArgumentException("A valid strategy-position identity is required.", nameof(positionId));
        if (effectiveAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The effective time must be UTC.", nameof(effectiveAtUtc));

        return strategyKind switch
        {
            TradeStrategyKind.IronCondor => SendAsync(new EndOfDayIronCondorPositionCommand
            {
                CommandId = Guid.NewGuid(),
                Subject = Subject(PositionActorNames.IronCondorCommand,
                    EndOfDayIronCondorPositionCommand.Verb, positionId),
                EntityId = positionId,
                EffectiveAtUtc = effectiveAtUtc
            }, cancellationToken),
            TradeStrategyKind.VerticalSpread => SendAsync(new EndOfDayVerticalSpreadPositionCommand
            {
                CommandId = Guid.NewGuid(),
                Subject = Subject(PositionActorNames.VerticalSpreadCommand,
                    EndOfDayVerticalSpreadPositionCommand.Verb, positionId),
                EntityId = positionId,
                EffectiveAtUtc = effectiveAtUtc
            }, cancellationToken),
            TradeStrategyKind.FuturesOutright => SendAsync(new EndOfDayFuturesPositionCommand
            {
                CommandId = Guid.NewGuid(),
                Subject = Subject(FuturesPositionActorNames.Command,
                    EndOfDayFuturesPositionCommand.Verb, positionId),
                EntityId = positionId,
                EffectiveAtUtc = effectiveAtUtc
            }, cancellationToken),
            _ => throw new ArgumentException(
                $"Strategy {strategyKind} does not support end-of-day processing.", nameof(strategyKind))
        };
    }

    Task<ServiceResult<Guid>> SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken)
        where TCommand : class, ICommand<StrategyPositionId> =>
        RequestCommandAsync(command, command.EntityId, cancellationToken).AsTask();

    static ActorSubject Subject(string actor, string verb, StrategyPositionId id) =>
        new(ActorType.Command, actor, verb, id.Format());
}

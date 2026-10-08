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
        => EndOfDayAsync(positionId, strategyKind,
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(effectiveAtUtc, TomasAI.IFM.Domain.MarketData.Shared.FuturesTradingValueDate.MarketTimeZone)), effectiveAtUtc, cancellationToken);

    /// <inheritdoc />
    public Task<ServiceResult<Guid>> EndOfDayAsync(StrategyPositionId positionId, TradeStrategyKind strategyKind,
        DateOnly valueDate, DateTime effectiveAtUtc, CancellationToken cancellationToken = default)
    {
        if (!positionId.IsValid)
            throw new ArgumentException("A valid strategy-position identity is required.", nameof(positionId));
        if (effectiveAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The effective time must be UTC.", nameof(effectiveAtUtc));

        if (valueDate == default) throw new ArgumentOutOfRangeException(nameof(valueDate));
        var identity = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"PositionEndOfDay:{positionId.Format()}:{valueDate:yyyy-MM-dd}"));
        var commandId = new Guid(identity.AsSpan(0,16));
        return strategyKind switch
        {
            TradeStrategyKind.IronCondor => SendAsync(new EndOfDayIronCondorPositionCommand
            {
                CommandId = commandId,
                Subject = Subject(PositionActorNames.IronCondorCommand,
                    EndOfDayIronCondorPositionCommand.Verb, positionId),
                EntityId = positionId,
                ValueDate = valueDate,
                EffectiveAtUtc = effectiveAtUtc
            }, cancellationToken),
            TradeStrategyKind.VerticalSpread => SendAsync(new EndOfDayVerticalSpreadPositionCommand
            {
                CommandId = commandId,
                Subject = Subject(PositionActorNames.VerticalSpreadCommand,
                    EndOfDayVerticalSpreadPositionCommand.Verb, positionId),
                EntityId = positionId,
                ValueDate = valueDate,
                EffectiveAtUtc = effectiveAtUtc
            }, cancellationToken),
            TradeStrategyKind.FuturesOutright => SendAsync(new EndOfDayFuturesPositionCommand
            {
                CommandId = commandId,
                Subject = Subject(FuturesPositionActorNames.Command,
                    EndOfDayFuturesPositionCommand.Verb, positionId),
                EntityId = positionId,
                ValueDate = valueDate,
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

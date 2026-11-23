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
        DateOnly valueDate, DateTime closeBoundaryUtc, CancellationToken cancellationToken = default)
    {
        if (!positionId.IsValid)
            throw new ArgumentException("A valid strategy-position identity is required.", nameof(positionId));
        if (closeBoundaryUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The effective time must be UTC.", nameof(closeBoundaryUtc));

        ArgumentOutOfRangeException.ThrowIfEqual(valueDate, default);
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
                EffectiveAtUtc = closeBoundaryUtc
            }, cancellationToken),
            TradeStrategyKind.VerticalSpread => SendAsync(new EndOfDayVerticalSpreadPositionCommand
            {
                CommandId = commandId,
                Subject = Subject(PositionActorNames.VerticalSpreadCommand,
                    EndOfDayVerticalSpreadPositionCommand.Verb, positionId),
                EntityId = positionId,
                ValueDate = valueDate,
                EffectiveAtUtc = closeBoundaryUtc
            }, cancellationToken),
            TradeStrategyKind.FuturesOutright => SendAsync(new EndOfDayFuturesPositionCommand
            {
                CommandId = commandId,
                Subject = Subject(FuturesPositionActorNames.Command,
                    EndOfDayFuturesPositionCommand.Verb, positionId),
                EntityId = positionId,
                ValueDate = valueDate,
                EffectiveAtUtc = closeBoundaryUtc
            }, cancellationToken),
            _ => throw new ArgumentException(
                $"Strategy {strategyKind} does not support end-of-day processing.", nameof(strategyKind))
        };
    }

    /// <inheritdoc />
    public Task<ServiceResult<Guid>> RepairEndOfDayAsync(StrategyPositionSnapshot source, long sourceEventId,
        DateOnly valueDate, DateTime closeBoundaryUtc, StoredPositionClosingQuote[] quotes, CancellationToken cancellationToken = default)
        => RepairEndOfDayWithBaselineAsync(source,sourceEventId,valueDate,closeBoundaryUtc,quotes,null,cancellationToken);

    public Task<ServiceResult<Guid>> RepairEndOfDayWithBaselineAsync(StrategyPositionSnapshot source, long sourceEventId,
        DateOnly valueDate, DateTime closeBoundaryUtc, StoredPositionClosingQuote[] quotes, StrategyPositionSnapshot? previousEndOfDay, CancellationToken cancellationToken = default)
    {
        if (!source.Id.IsValid || sourceEventId <= 0 || quotes.Length == 0 || closeBoundaryUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("An exact position source, UTC close and closing quotes are required.");
        var identity = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            $"PositionEodRepair:{source.Id.Format()}:{valueDate:yyyy-MM-dd}:{sourceEventId}:{previousEndOfDay?.PositionSequence}:{string.Join(',',quotes.OrderBy(q=>q.ContractId,StringComparer.Ordinal).Select(q=>q.EvidenceId))}"));
        var commandId = new Guid(identity.AsSpan(0,16));
        return source.StrategyKind switch
        {
            TradeStrategyKind.IronCondor => SendAsync(new EndOfDayIronCondorPositionCommand {
                CommandId=commandId,EntityId=source.Id,Subject=Subject(PositionActorNames.IronCondorCommand,EndOfDayIronCondorPositionCommand.Verb,source.Id),
                ValueDate=valueDate,EffectiveAtUtc=closeBoundaryUtc,HistoricalSnapshot=source,HistoricalSourceEventId=sourceEventId,HistoricalClosingQuotes=quotes,PreviousEndOfDay=previousEndOfDay },cancellationToken),
            TradeStrategyKind.VerticalSpread => SendAsync(new EndOfDayVerticalSpreadPositionCommand {
                CommandId=commandId,EntityId=source.Id,Subject=Subject(PositionActorNames.VerticalSpreadCommand,EndOfDayVerticalSpreadPositionCommand.Verb,source.Id),
                ValueDate=valueDate,EffectiveAtUtc=closeBoundaryUtc,HistoricalSnapshot=source,HistoricalSourceEventId=sourceEventId,HistoricalClosingQuotes=quotes,PreviousEndOfDay=previousEndOfDay },cancellationToken),
            TradeStrategyKind.FuturesOutright => SendAsync(new EndOfDayFuturesPositionCommand {
                CommandId=commandId,EntityId=source.Id,Subject=Subject(FuturesPositionActorNames.Command,EndOfDayFuturesPositionCommand.Verb,source.Id),
                ValueDate=valueDate,EffectiveAtUtc=closeBoundaryUtc,HistoricalSnapshot=source,HistoricalSourceEventId=sourceEventId,HistoricalClosingQuotes=quotes,PreviousEndOfDay=previousEndOfDay },cancellationToken),
            _ => throw new ArgumentException("Unsupported EOD strategy.")
        };
    }

    Task<ServiceResult<Guid>> SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken)
        where TCommand : class, ICommand<StrategyPositionId> =>
        RequestCommandAsync(command, command.EntityId, cancellationToken).AsTask();

    static ActorSubject Subject(string actor, string verb, StrategyPositionId id) =>
        new(ActorType.Command, actor, verb, id.Format());
}

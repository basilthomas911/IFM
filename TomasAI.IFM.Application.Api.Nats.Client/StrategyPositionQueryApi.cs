using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Nats.Client;

/// <summary>Reads current futures and futures-option strategy-position snapshots through NATS.</summary>
/// <param name="producer">The actor-message producer used to issue request/reply queries.</param>
public sealed class StrategyPositionQueryApi(IActorProducer producer)
    : NatsClientApi(producer), IStrategyPositionQueryApi
{
    /// <inheritdoc />
    public Task<ServiceResult<StrategyPositionSnapshot>> GetCurrentAsync(
        StrategyPositionId positionId,
        TradeStrategyKind strategyKind,
        CancellationToken cancellationToken = default)
    {
        if (!positionId.IsValid)
            throw new ArgumentException("A valid strategy-position identity is required.", nameof(positionId));

        return strategyKind switch
        {
            TradeStrategyKind.IronCondor => SendAsync<GetIronCondorOptionTradePositionQuery>(
                new GetIronCondorOptionTradePositionQuery
                {
                    PositionId = positionId,
                    Subject = Subject(PositionActorNames.Query,
                        GetIronCondorOptionTradePositionQuery.Verb, positionId)
                }, cancellationToken),
            TradeStrategyKind.VerticalSpread => SendAsync<GetVerticalSpreadOptionTradePositionQuery>(
                new GetVerticalSpreadOptionTradePositionQuery
                {
                    PositionId = positionId,
                    Subject = Subject(PositionActorNames.Query,
                        GetVerticalSpreadOptionTradePositionQuery.Verb, positionId)
                }, cancellationToken),
            TradeStrategyKind.FuturesOutright => SendAsync<GetFuturesTradePositionQuery>(
                new GetFuturesTradePositionQuery
                {
                    PositionId = positionId,
                    Subject = Subject(FuturesPositionActorNames.Query,
                        GetFuturesTradePositionQuery.Verb, positionId)
                }, cancellationToken),
            _ => throw new ArgumentException(
                $"Strategy {strategyKind} does not expose a supported position query.", nameof(strategyKind))
        };
    }

    /// <inheritdoc />
    public Task<ServiceResult<StrategyPositionSnapshot[]>> GetHistoryAsync(
        StrategyPositionId positionId, TradeStrategyKind strategyKind,
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        if (!positionId.IsValid) throw new ArgumentException("A valid position identity is required.", nameof(positionId));
        return strategyKind switch
        {
            TradeStrategyKind.IronCondor => RequestAsync<GetIronCondorOptionTradePositionHistoryQuery, StrategyPositionSnapshot[]>(
                Subject(PositionActorNames.Query, GetIronCondorOptionTradePositionHistoryQuery.Verb, positionId),
                new GetIronCondorOptionTradePositionHistoryQuery {
                    Subject = Subject(PositionActorNames.Query, GetIronCondorOptionTradePositionHistoryQuery.Verb, positionId),
                    PositionId = positionId, FromUtc = fromUtc, ToUtc = toUtc, PageSize = 1000, LoadAll = true
                }, cancellationToken).AsTask(),
            TradeStrategyKind.VerticalSpread => RequestAsync<GetVerticalSpreadOptionTradePositionHistoryQuery, StrategyPositionSnapshot[]>(
                Subject(PositionActorNames.Query, GetVerticalSpreadOptionTradePositionHistoryQuery.Verb, positionId),
                new GetVerticalSpreadOptionTradePositionHistoryQuery {
                    Subject = Subject(PositionActorNames.Query, GetVerticalSpreadOptionTradePositionHistoryQuery.Verb, positionId),
                    PositionId = positionId, FromUtc = fromUtc, ToUtc = toUtc, PageSize = 1000, LoadAll = true
                }, cancellationToken).AsTask(),
            _ => throw new ArgumentOutOfRangeException(nameof(strategyKind), "Complete option position history requires an option strategy.")
        };
    }

    Task<ServiceResult<StrategyPositionSnapshot>> SendAsync<TQuery>(
        TQuery query,
        CancellationToken cancellationToken)
        where TQuery : class, IQuery<StrategyPositionSnapshot> =>
        RequestAsync<TQuery, StrategyPositionSnapshot>(query.Subject, query, cancellationToken).AsTask();

    static ActorSubject Subject(string actor, string verb, StrategyPositionId id) =>
        new(ActorType.Query, actor, verb, id.Format());
}

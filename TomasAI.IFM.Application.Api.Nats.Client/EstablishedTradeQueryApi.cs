using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.Futures;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Application.Api.Nats.Client;
/// <summary>Routes exact established-trade lookups to the existing strategy query actors.</summary>
public sealed class EstablishedTradeQueryApi(TomasAI.IFM.Shared.EventModelActor.Contracts.IActorProducer producer)
    : NatsClientApi(producer), IEstablishedTradeQueryApi
{
    public Task<ServiceResult<TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime.TradeRealtimeSnapshot>> GetRealtimeAsync(
        TradeEntityId tradeId, DateOnly valueDate, CancellationToken cancellationToken = default)
    {
        var query = new TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime.GetTradeRealtimeSnapshotQuery
        {
            TradeId = tradeId, ValueDate = valueDate,
            Subject = Subject(TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime.GetTradeRealtimeSnapshotQuery.Actor,
                TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime.GetTradeRealtimeSnapshotQuery.Verb, tradeId.Format())
        };
        return RequestAsync<TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime.GetTradeRealtimeSnapshotQuery,
            TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime.TradeRealtimeSnapshot>(query.Subject, query, cancellationToken).AsTask();
    }
    /// <inheritdoc />
    public Task<ServiceResult<EstablishedTradeDefinition>> GetAsync(TradeEntityId tradeId, TradeStrategyKind strategyKind,
        CancellationToken cancellationToken = default) => strategyKind switch
    {
        TradeStrategyKind.IronCondor => IronCondorAsync(tradeId, cancellationToken),
        TradeStrategyKind.VerticalSpread => VerticalAsync(tradeId, cancellationToken),
        TradeStrategyKind.FuturesOutright => FuturesAsync(tradeId, cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(strategyKind))
    };
    /// <summary>Reads the iron condor projection for the supplied identity.</summary>
    private Task<ServiceResult<EstablishedTradeDefinition>> IronCondorAsync(TradeEntityId id, CancellationToken token)
    {
        var query = new GetIronCondorOptionTradeQuery { TradeId = id,
            Subject = Subject(GetIronCondorOptionTradeQuery.Actor, GetIronCondorOptionTradeQuery.Verb, id.Format()) };
        return RequestAsync<GetIronCondorOptionTradeQuery, EstablishedTradeDefinition>(query.Subject, query, token).AsTask();
    }
    /// <summary>Reads the vertical spread projection for the supplied identity.</summary>
    private Task<ServiceResult<EstablishedTradeDefinition>> VerticalAsync(TradeEntityId id, CancellationToken token)
    {
        var query = new GetVerticalSpreadOptionTradeQuery { TradeId = id,
            Subject = Subject(GetVerticalSpreadOptionTradeQuery.Actor, GetVerticalSpreadOptionTradeQuery.Verb, id.Format()) };
        return RequestAsync<GetVerticalSpreadOptionTradeQuery, EstablishedTradeDefinition>(query.Subject, query, token).AsTask();
    }
    /// <summary>Reads the futures projection for the supplied identity.</summary>
    private Task<ServiceResult<EstablishedTradeDefinition>> FuturesAsync(TradeEntityId id, CancellationToken token)
    {
        var query = new GetFuturesTradeQuery { TradeId = id,
            Subject = Subject(GetFuturesTradeQuery.Actor, GetFuturesTradeQuery.Verb, id.Format()) };
        return RequestAsync<GetFuturesTradeQuery, EstablishedTradeDefinition>(query.Subject, query, token).AsTask();
    }
    /// <summary>Builds the subject for an exact strategy query.</summary>
    private static ActorSubject Subject(string actor, string verb, string entityId) =>
        new(ActorType.Query, actor, verb, entityId);
}

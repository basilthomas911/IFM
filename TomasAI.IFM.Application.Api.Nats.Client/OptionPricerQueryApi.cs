using TomasAI.IFM.Shared.Application;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.OptionPricer.Queries;
using TomasAI.IFM.Shared.OptionPricer.QueryParameters;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Application.Api.Nats.Client;

/// <summary>
/// REST client for option pricer queries. Delegates to an <see cref="IQueryServiceApi"/> and uses
/// the URI paths defined in <see cref="OptionPricerQueryUriPath"/>.
/// </summary>
public class OptionPricerQueryApi(IActorProducer actorProducer)
    : NatsCommandApi(actorProducer), IOptionPricerQueryApi
{
    /// <summary>
    /// Retrieves configured option pricer devices.
    /// </summary>
    public async Task<ServiceResult<OptionPricerDevicesReadModel>> GetOptionPricerDevicesAsync()
    {
        var entityId = new GetOptionPricerDevicesParameter();
        GetOptionPricerDevicesQuery query = new()
        {
            Subject = new ActorSubject(ActorType.Query, GetOptionPricerDevicesQuery.Actor, GetOptionPricerDevicesQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetOptionPricerDevicesQuery, OptionPricerDevicesReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Retrieves spread distribution for a given trade.
    /// </summary>
    /// <param name="tradeId">Trade identifier.</param>
    /// <param name="tradeType">Trade type.</param>
    /// <param name="tradeStatus">Trade status.</param>
    /// <param name="valueDate">Value date for the distribution.</param>
    /// <param name="daysToExpiry">Days to expiry.</param>
    public async Task<ServiceResult<SpreadDistributionReadModel>> GetSpreadDistributionAsync(int tradeId, TradeType tradeType, TradeStatus tradeStatus, DateOnly valueDate, int daysToExpiry)
    {
        var entityId = new GetSpreadDistributionParameter(tradeId, tradeType, tradeStatus, valueDate, daysToExpiry);
        GetSpreadDistributionQuery query = new(tradeId, tradeType, tradeStatus, valueDate, daysToExpiry)
        {
            Subject = new ActorSubject(ActorType.Query, GetSpreadDistributionQuery.Actor, GetSpreadDistributionQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetSpreadDistributionQuery, SpreadDistributionReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Determines whether a spread distribution job is currently in progress for the specified order and trade.
    /// </summary>
    /// <param name="orderId">Order identifier.</param>
    /// <param name="tradeId">Trade identifier.</param>
    public async Task<ServiceResult<ScalarReadModel<bool>>> IsSpreadDistributionJobInProgressAsync(int orderId, int tradeId)
    {
        var entityId = new GetSpreadDistributionJobInProgressParameter(orderId, tradeId);
        GetSpreadDistributionJobInProgressQuery query = new(orderId, tradeId)
        {
            Subject = new ActorSubject(ActorType.Query, GetSpreadDistributionJobInProgressQuery.Actor, GetSpreadDistributionJobInProgressQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetSpreadDistributionJobInProgressQuery, ScalarReadModel<bool>>(query.Subject, query);
    }
}

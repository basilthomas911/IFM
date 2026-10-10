using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Application.Api.Nats.Client;
public partial class MarketDataQueryApi
{
    /// <inheritdoc />
    public Task<ServiceResult<StrategyOptionChainParameterSet>> GetStrategyOptionChainParametersAsync(Guid setId, int version,
        CancellationToken cancellationToken = default)
    {
        var query = new GetStrategyOptionChainParametersQuery { ParameterSetId = setId, Version = version,
            Subject = new(ActorType.Query, GetStrategyOptionChainParametersQuery.Actor, GetStrategyOptionChainParametersQuery.Verb, ActorEntityId.Default.Format()) };
        return RequestAsync<GetStrategyOptionChainParametersQuery, StrategyOptionChainParameterSet>(query.Subject, query, cancellationToken).AsTask();
    }
}

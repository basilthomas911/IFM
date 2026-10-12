using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Order.Broker.Query.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Query.Actor;
/// <summary>Typed context for broker-order queries.</summary>
public sealed class BrokerOrderQueryContext(IActorSupervisor supervisor, IBrokerOrderReadStore store, ILogger<BrokerOrderQueryActor> logger) : QueryActorContext(supervisor, new(ActorType.Query, BrokerOrderQueryActor.ActorName)), IBrokerOrderQueryContext
{
    /// <inheritdoc/>
    public IBrokerOrderReadStore Store { get; } = store;
    /// <inheritdoc/>
    public ILogger<BrokerOrderQueryActor> Logger { get; } = logger;
}

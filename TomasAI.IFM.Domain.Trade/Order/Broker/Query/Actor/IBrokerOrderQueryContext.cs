using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Order.Broker.Query.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Query.Actor;
/// <summary>Services exposed to broker-order query handlers.</summary>
public interface IBrokerOrderQueryContext : IQueryActorContext<BrokerOrderQueryActor>
{
    /// <summary>Gets the committed broker-order projection.</summary>
    IBrokerOrderReadStore Store { get; }

    /// <summary>Gets the query logger.</summary>
    ILogger<BrokerOrderQueryActor> Logger { get; }
}

using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Event.Actor;
/// <summary>Exposes services used by BrokerOrder event handlers.</summary>
public interface IBrokerOrderEventContext : IEventActorContext<BrokerOrderEventActor>
{
    IActorService ActorService { get; }

    ILogger<BrokerOrderEventActor> Logger { get; }
}

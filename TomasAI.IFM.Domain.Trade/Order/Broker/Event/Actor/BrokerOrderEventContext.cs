using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Event.Actor;
/// <summary>Provides runtime services for the BrokerOrder event actor.</summary>
public sealed class BrokerOrderEventContext(IActorSupervisor supervisor, IActorService actorService, ILogger<BrokerOrderEventActor> logger) : EventActorContext(supervisor, new ActorMailboxId(ActorType.Event, BrokerOrderEventActor.ActorName)), IBrokerOrderEventContext
{
    public IActorService ActorService { get; } = actorService;
    public ILogger<BrokerOrderEventActor> Logger { get; } = logger;
}

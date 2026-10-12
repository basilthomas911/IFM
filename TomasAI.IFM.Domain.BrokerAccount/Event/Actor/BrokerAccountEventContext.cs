using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.BrokerAccount.Event.Actor;

/// <summary>Provides runtime services for the BrokerAccount event actor.</summary>
public sealed class BrokerAccountEventContext(IActorSupervisor supervisor, IActorService actorService,
    ILogger<BrokerAccountEventActor> logger)
    : EventActorContext(supervisor, new ActorMailboxId(ActorType.Event, BrokerAccountEventActor.ActorName)),
IBrokerAccountEventContext
{
    public IActorService ActorService { get; } = actorService;
    public ILogger<BrokerAccountEventActor> Logger { get; } = logger;
}

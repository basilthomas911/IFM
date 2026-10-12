using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.BrokerAccount.Event.Actor;

/// <summary>Exposes services used by BrokerAccount event handlers.</summary>
public interface IBrokerAccountEventContext : IEventActorContext<BrokerAccountEventActor>
{
    IActorService ActorService { get; }
    ILogger<BrokerAccountEventActor> Logger { get; }
}

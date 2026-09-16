using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.BrokerAccount.Query.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.BrokerAccount.Query.Actor;

/// <summary>Services used by BrokerAccount query handlers.</summary>
public interface IBrokerAccountQueryContext : IQueryActorContext<BrokerAccountQueryActor>
{
    IBrokerAccountReadStore Store { get; }
    ILogger<BrokerAccountQueryActor> Logger { get; }
}

/// <summary>Typed context for BrokerAccount queries.</summary>
public sealed class BrokerAccountQueryContext(
    IActorSupervisor supervisor,
    IBrokerAccountReadStore store,
    ILogger<BrokerAccountQueryActor> logger)
    : QueryActorContext(supervisor, new(ActorType.Query, BrokerAccountQueryActor.ActorName)),
      IQueryActorContext<BrokerAccountQueryActor>, IBrokerAccountQueryContext
{
    public IBrokerAccountReadStore Store { get; } = store;
    public ILogger<BrokerAccountQueryActor> Logger { get; } = logger;
}

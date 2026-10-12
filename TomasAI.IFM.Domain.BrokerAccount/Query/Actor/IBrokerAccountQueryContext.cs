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

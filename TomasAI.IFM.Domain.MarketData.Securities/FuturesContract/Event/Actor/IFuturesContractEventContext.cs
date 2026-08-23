using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Event.Actor;

/// <summary>Defines the runtime services required by <see cref="FuturesContractEventActor"/>.</summary>
public interface IFuturesContractEventContext : IEventActorContext<FuturesContractEventActor>
{
    /// <summary>Gets the actor supervisor.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesContractEventActor> Logger { get; }
}

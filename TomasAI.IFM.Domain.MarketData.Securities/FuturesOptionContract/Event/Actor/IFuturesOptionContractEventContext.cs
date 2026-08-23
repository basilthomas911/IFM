using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Event.Actor;

/// <summary>Defines the runtime services required by <see cref="FuturesOptionContractEventActor"/>.</summary>
public interface IFuturesOptionContractEventContext : IEventActorContext<FuturesOptionContractEventActor>
{
    /// <summary>Gets the actor supervisor.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesOptionContractEventActor> Logger { get; }
}

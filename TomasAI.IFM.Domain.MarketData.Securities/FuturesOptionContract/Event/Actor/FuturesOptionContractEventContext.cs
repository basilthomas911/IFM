using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Event.Actor;

/// <summary>Provides the typed runtime context used by <see cref="FuturesOptionContractEventActor"/>.</summary>
public sealed class FuturesOptionContractEventContext : EventActorContext,
    IEventActorContext<FuturesOptionContractEventActor>, IFuturesOptionContractEventContext
{
    /// <summary>Initializes a futures-option-contract event context.</summary>
    public FuturesOptionContractEventContext(IActorSupervisor supervisor, ILogger<FuturesOptionContractEventActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Event, FuturesOptionContractEventActor.Actor))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Logger = IsArgumentNull.Set(logger);
    }
    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public ILogger<FuturesOptionContractEventActor> Logger { get; }
}

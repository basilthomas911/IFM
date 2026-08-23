using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Event.Actor;

/// <summary>Provides the typed runtime context used by <see cref="FuturesContractEventActor"/>.</summary>
public sealed class FuturesContractEventContext : EventActorContext,
    IEventActorContext<FuturesContractEventActor>, IFuturesContractEventContext
{
    /// <summary>Initializes a futures-contract event context.</summary>
    public FuturesContractEventContext(IActorSupervisor supervisor, ILogger<FuturesContractEventActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Event, FuturesContractEventActor.Actor))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Logger = IsArgumentNull.Set(logger);
    }
    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public ILogger<FuturesContractEventActor> Logger { get; }
}

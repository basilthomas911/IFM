using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Reference.LookupType.Event.Actor;

/// <summary>Provides the typed runtime context used by <see cref="LookupTypeEventActor"/>.</summary>
public sealed class LookupTypeEventContext :
    EventActorContext,
    IEventActorContext<LookupTypeEventActor>,
    ILookupTypeEventContext
{
    /// <summary>Initializes a lookup-type event context.</summary>
    public LookupTypeEventContext(IActorSupervisor supervisor, ILogger<LookupTypeEventActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Event, LookupTypeEventActor.Actor))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public ILogger<LookupTypeEventActor> Logger { get; }
}

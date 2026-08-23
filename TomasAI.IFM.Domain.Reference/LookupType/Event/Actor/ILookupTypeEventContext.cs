using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Reference.LookupType.Event.Actor;

/// <summary>Defines the runtime services required by <see cref="LookupTypeEventActor"/>.</summary>
public interface ILookupTypeEventContext : IEventActorContext<LookupTypeEventActor>
{
    /// <summary>Gets the actor supervisor.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the event actor logger.</summary>
    ILogger<LookupTypeEventActor> Logger { get; }
}

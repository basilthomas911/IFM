using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.LookupType.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Reference.LookupType.Event.Extensions;

/// <summary>Provides readonly lookup-type services on the typed event context.</summary>
public static class LookupTypeEventExtensions
{
    extension(IEventActorContext<LookupTypeEventActor> context)
    {
        /// <summary>Gets the actor supervisor.</summary>
        public IActorSupervisor Supervisor => Typed(context).Supervisor;
        /// <summary>Gets the event actor logger.</summary>
        public ILogger<LookupTypeEventActor> Logger => Typed(context).Logger;
    }

    static ILookupTypeEventContext Typed(IEventActorContext<LookupTypeEventActor> context)
        => IsArgumentNull.Set(context as ILookupTypeEventContext, nameof(context))!;
}

using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Domain.Application.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.Application.Actor.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Application.Actor.Event.Extensions;

/// <summary>Exposes readonly ApplicationEvent Event context properties.</summary>
public static class ApplicationEventContextExtensions
{
    extension(IEventActorContext<ApplicationEventActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public IApplicationEventContext DomainContext =>
            IsArgumentNull.Set(context as IApplicationEventContext, nameof(context))!;
        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;
        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<ApplicationEventActor> Logger => context.DomainContext.Logger;
    }
}

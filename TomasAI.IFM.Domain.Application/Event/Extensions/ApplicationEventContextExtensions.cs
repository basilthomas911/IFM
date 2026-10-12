using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Domain.Application.Shared.Events;
using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.Application.Event.Actor;

namespace TomasAI.IFM.Domain.Application.Event.Extensions;

/// <summary>Exposes readonly ApplicationEvent Event context properties.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# extension members use their declared receiver; static conversion would remove the established actor context extension contract.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034", Justification = "C# 14 extension blocks generate nested implementation types; the public API consists of actor context extension members, not nested domain types.")]
public static class ApplicationEventContextExtensions
{
    extension(IEventActorContext<ApplicationEventActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public IApplicationEventContext DomainContext =>
            IsArgumentNull.Named(context as IApplicationEventContext, nameof(context))!;
        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;
        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<ApplicationEventActor> Logger => context.DomainContext.Logger;
        /// <summary>Gets the ordered operational startup activities.</summary>
        public IApplicationStartupActivities StartupActivities => context.DomainContext.StartupActivities;
        /// <summary>Gets the latest lifecycle status store.</summary>
        public IApplicationStartupStatusStore StartupStatusStore => context.DomainContext.StartupStatusStore;
        /// <summary>Gets the unattended status-console writer.</summary>
        public IStatusConsoleWriter StatusConsoleWriter => context.DomainContext.StatusConsoleWriter;
        /// <summary>Gets the lifecycle clock.</summary>
        public TimeProvider TimeProvider => context.DomainContext.TimeProvider;
    }
}

using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Domain.Application.Shared.Events;
using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.Application.Event.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="ApplicationEventActor"/>.</summary>
public interface IApplicationEventContext : IEventActorContext<ApplicationEventActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<ApplicationEventActor> Logger { get; }
    /// <summary>Gets the ordered startup activity adapter.</summary>
    IApplicationStartupActivities StartupActivities { get; }
    /// <summary>Gets the latest lifecycle status store.</summary>
    IApplicationStartupStatusStore StartupStatusStore { get; }
    /// <summary>Gets the unattended system-console writer.</summary>
    IStatusConsoleWriter StatusConsoleWriter { get; }
    /// <summary>Gets the clock used for deterministic lifecycle timestamps.</summary>
    TimeProvider TimeProvider { get; }
}

using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Event.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="FuturesTdiSignalEventActor"/>.</summary>
public interface IFuturesTdiSignalEventContext : IEventActorContext<FuturesTdiSignalEventActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the StatusConsoleWriter service supplied to the actor context.</summary>
    IStatusConsoleWriter StatusConsoleWriter { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<FuturesTdiSignalEventActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="FuturesTdiSignalEventActor"/>.</summary>
public sealed class FuturesTdiSignalEventContext : EventActorContext, IEventActorContext<FuturesTdiSignalEventActor>, IFuturesTdiSignalEventContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public FuturesTdiSignalEventContext(
        IActorSupervisor supervisor,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger<FuturesTdiSignalEventActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Event, FuturesTdiSignalEventActor.Actor))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        StatusConsoleWriter = IsArgumentNull.Set(statusConsoleWriter);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IStatusConsoleWriter StatusConsoleWriter { get; }
    /// <inheritdoc/>
    public ILogger<FuturesTdiSignalEventActor> Logger { get; }
}

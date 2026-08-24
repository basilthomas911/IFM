using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.Validation;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="FuturesTdiSignalCommandActor"/>.</summary>
public interface IFuturesTdiSignalCommandContext : ICommandActorContext<FuturesTdiSignalCommandActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the DbEventSource service supplied to the actor context.</summary>
    IEventSourceActorDbContext DbEventSource { get; }
    /// <summary>Gets the EventProjector service supplied to the actor context.</summary>
    IEventProjector<FuturesTdiSignalCommandActor> EventProjector { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<FuturesTdiSignalCommandActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="FuturesTdiSignalCommandActor"/>.</summary>
public sealed class FuturesTdiSignalCommandContext : CommandActorContext, ICommandActorContext<FuturesTdiSignalCommandActor>, IFuturesTdiSignalCommandContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public FuturesTdiSignalCommandContext(
        IActorSupervisor supervisor,
        IEventSourceActorDbContext dbEventSource,
        IEventProjector<FuturesTdiSignalCommandActor> eventProjector,
        ILogger<FuturesTdiSignalCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, FuturesTdiSignalCommandActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        DbEventSource = IsArgumentNull.Set(dbEventSource);
        EventProjector = IsArgumentNull.Set(eventProjector);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IEventSourceActorDbContext DbEventSource { get; }
    /// <inheritdoc/>
    public IEventProjector<FuturesTdiSignalCommandActor> EventProjector { get; }
    /// <inheritdoc/>
    public ILogger<FuturesTdiSignalCommandActor> Logger { get; }
}

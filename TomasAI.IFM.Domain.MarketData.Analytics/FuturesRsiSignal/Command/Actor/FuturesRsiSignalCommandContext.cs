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
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Validation;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="FuturesRsiSignalCommandActor"/>.</summary>
public interface IFuturesRsiSignalCommandContext : ICommandActorContext<FuturesRsiSignalCommandActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the DbEventSource service supplied to the actor context.</summary>
    IEventSourceActorDbContext DbEventSource { get; }
    /// <summary>Gets the EventProjector service supplied to the actor context.</summary>
    IEventProjector<FuturesRsiSignalCommandActor> EventProjector { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<FuturesRsiSignalCommandActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="FuturesRsiSignalCommandActor"/>.</summary>
public sealed class FuturesRsiSignalCommandContext : CommandActorContext, ICommandActorContext<FuturesRsiSignalCommandActor>, IFuturesRsiSignalCommandContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public FuturesRsiSignalCommandContext(
        IActorSupervisor supervisor,
        IEventSourceActorDbContext dbEventSource,
        IEventProjector<FuturesRsiSignalCommandActor> eventProjector,
        ILogger<FuturesRsiSignalCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, FuturesRsiSignalCommandActor.ActorName))
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
    public IEventProjector<FuturesRsiSignalCommandActor> EventProjector { get; }
    /// <inheritdoc/>
    public ILogger<FuturesRsiSignalCommandActor> Logger { get; }
}

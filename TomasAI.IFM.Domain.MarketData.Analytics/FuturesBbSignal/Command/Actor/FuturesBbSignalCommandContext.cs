using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command.Actor;

/// <summary>Defines readonly services required by the Bollinger command actor.</summary>
public interface IFuturesBbSignalCommandContext : ICommandActorContext<FuturesBbSignalCommandActor>
{
    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesBbSignalCommandActor> Logger { get; }
    /// <summary>Gets the event-source database.</summary>
    IEventSourceActorDbContext DbEventSource { get; }
    /// <summary>Gets the event projector.</summary>
    IEventProjector<FuturesBbSignalCommandActor> EventProjector { get; }
}

/// <summary>Provides the typed Bollinger command context.</summary>
public sealed class FuturesBbSignalCommandContext : CommandActorContext,
    ICommandActorContext<FuturesBbSignalCommandActor>, IFuturesBbSignalCommandContext
{
    /// <summary>Initializes the context.</summary>
    public FuturesBbSignalCommandContext(IActorSupervisor supervisor,
        IEventSourceActorDbContext dbEventSource,
        IEventProjector<FuturesBbSignalCommandActor> eventProjector,
        ILogger<FuturesBbSignalCommandActor> logger)
        : base(supervisor, new(ActorType.Command, FuturesBbSignalCommandActor.ActorName))
    {
        DbEventSource = IsArgumentNull.Set(dbEventSource);
        EventProjector = IsArgumentNull.Set(eventProjector);
        Logger = IsArgumentNull.Set(logger);
    }
    /// <inheritdoc />
    public IEventSourceActorDbContext DbEventSource { get; }
    /// <inheritdoc />
    public IEventProjector<FuturesBbSignalCommandActor> EventProjector { get; }
    /// <summary>Gets the actor logger.</summary>
    public ILogger<FuturesBbSignalCommandActor> Logger { get; }
}

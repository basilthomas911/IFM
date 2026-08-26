using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.Actor;

/// <summary>Defines readonly services required by the EMA command actor.</summary>
public interface IFuturesEmaSignalCommandContext : ICommandActorContext<FuturesEmaSignalCommandActor>
{
    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesEmaSignalCommandActor> Logger { get; }
    /// <summary>Gets the event-source database.</summary>
    IEventSourceActorDbContext DbEventSource { get; }
    /// <summary>Gets the event projector.</summary>
    IEventProjector<FuturesEmaSignalCommandActor> EventProjector { get; }
}

/// <summary>Provides the typed EMA command context.</summary>
public sealed class FuturesEmaSignalCommandContext : CommandActorContext,
    ICommandActorContext<FuturesEmaSignalCommandActor>, IFuturesEmaSignalCommandContext
{
    /// <summary>Initializes the context.</summary>
    public FuturesEmaSignalCommandContext(IActorSupervisor supervisor,
        IEventSourceActorDbContext dbEventSource,
        IEventProjector<FuturesEmaSignalCommandActor> eventProjector,
        ILogger<FuturesEmaSignalCommandActor> logger)
        : base(supervisor, new(ActorType.Command, FuturesEmaSignalCommandActor.ActorName))
    {
        DbEventSource = IsArgumentNull.Set(dbEventSource);
        EventProjector = IsArgumentNull.Set(eventProjector);
        Logger = IsArgumentNull.Set(logger);
    }
    /// <inheritdoc />
    public IEventSourceActorDbContext DbEventSource { get; }
    /// <inheritdoc />
    public IEventProjector<FuturesEmaSignalCommandActor> EventProjector { get; }
    /// <summary>Gets the actor logger.</summary>
    public ILogger<FuturesEmaSignalCommandActor> Logger { get; }
}

using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Event.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="FuturesTradeSignalEventActor"/>.</summary>
public interface IFuturesTradeSignalEventContext : IEventActorContext<FuturesTradeSignalEventActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the StatusConsoleWriter service supplied to the actor context.</summary>
    IStatusConsoleWriter StatusConsoleWriter { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<FuturesTradeSignalEventActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="FuturesTradeSignalEventActor"/>.</summary>
public sealed class FuturesTradeSignalEventContext : EventActorContext, IEventActorContext<FuturesTradeSignalEventActor>, IFuturesTradeSignalEventContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public FuturesTradeSignalEventContext(
        IActorSupervisor supervisor,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger<FuturesTradeSignalEventActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Event, FuturesTradeSignalEventActor.Actor))
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
    public ILogger<FuturesTradeSignalEventActor> Logger { get; }
}

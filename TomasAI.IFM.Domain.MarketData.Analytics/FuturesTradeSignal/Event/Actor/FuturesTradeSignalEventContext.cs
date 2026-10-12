using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Event.Actor;

/// <summary>Provides the typed runtime context used by <see cref="FuturesTradeSignalEventActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesTradeSignalEventContext : EventActorContext, IFuturesTradeSignalEventContext
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

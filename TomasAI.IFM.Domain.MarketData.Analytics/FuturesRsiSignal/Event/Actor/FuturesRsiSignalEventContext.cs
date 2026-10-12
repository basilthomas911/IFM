using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Model;
using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Actor;

/// <summary>Provides the typed runtime context used by <see cref="FuturesRsiSignalEventActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesRsiSignalEventContext : EventActorContext, IFuturesRsiSignalEventContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public FuturesRsiSignalEventContext(
        IActorSupervisor supervisor,
        IMarketDataApi marketDataApi,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger<FuturesRsiSignalEventActor> logger,
        IBlackboardService blackboardService)
        : base(supervisor, new ActorMailboxId(ActorType.Event, FuturesRsiSignalEventActor.Actor))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        MarketDataApi = IsArgumentNull.Set(marketDataApi);
        StatusConsoleWriter = IsArgumentNull.Set(statusConsoleWriter);
        Logger = IsArgumentNull.Set(logger);
        BlackboardService = IsArgumentNull.Set(blackboardService);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IMarketDataApi MarketDataApi { get; }
    /// <inheritdoc/>
    public IStatusConsoleWriter StatusConsoleWriter { get; }
    /// <inheritdoc/>
    public ILogger<FuturesRsiSignalEventActor> Logger { get; }
    /// <inheritdoc/>
    public IBlackboardService BlackboardService { get; }
}

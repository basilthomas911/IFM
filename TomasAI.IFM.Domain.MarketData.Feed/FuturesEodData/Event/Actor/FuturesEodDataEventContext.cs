using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Model;
using TomasAI.IFM.Domain.Trade.Shared.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using ApplicationMarketDataApi = TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event.Actor;

/// <summary>Provides the typed runtime context used by <see cref="FuturesEodDataEventActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesEodDataEventContext : EventActorContext, IFuturesEodDataEventContext
{
    /// <summary>Initializes the typed event context.</summary>
    public FuturesEodDataEventContext(
        IActorSupervisor supervisor,
        ILogger<FuturesEodDataEventActor> logger,
        IBlackboardService blackboardService,
        IStatusConsoleWriter statusConsoleWriter)
        : base(supervisor, new ActorMailboxId(ActorType.Event, FuturesEodDataEventActor.Actor))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Logger = IsArgumentNull.Set(logger);
        BlackboardService = IsArgumentNull.Set(blackboardService);
        StatusConsoleWriter = IsArgumentNull.Set(statusConsoleWriter);
    }
    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public ILogger<FuturesEodDataEventActor> Logger { get; }
    /// <inheritdoc/>
    public IBlackboardService BlackboardService { get; }
    /// <inheritdoc/>
    public IStatusConsoleWriter StatusConsoleWriter { get; }
}


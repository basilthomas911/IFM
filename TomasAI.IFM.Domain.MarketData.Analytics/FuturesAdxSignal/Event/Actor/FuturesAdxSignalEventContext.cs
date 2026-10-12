using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event.Model;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event.Actor;

/// <summary>Provides the typed runtime context used by <see cref="FuturesAdxSignalEventActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesAdxSignalEventContext : EventActorContext, IFuturesAdxSignalEventContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public FuturesAdxSignalEventContext(
        IActorSupervisor supervisor,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger<FuturesAdxSignalEventActor> logger,
        IMarketDataApi marketDataApi)
        : base(supervisor, new ActorMailboxId(ActorType.Event, FuturesAdxSignalEventActor.Actor))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        StatusConsoleWriter = IsArgumentNull.Set(statusConsoleWriter);
        Logger = IsArgumentNull.Set(logger);
        MarketDataApi = IsArgumentNull.Set(marketDataApi);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IStatusConsoleWriter StatusConsoleWriter { get; }
    /// <inheritdoc/>
    public ILogger<FuturesAdxSignalEventActor> Logger { get; }
    /// <inheritdoc/>
    public IMarketDataApi MarketDataApi { get; }
}

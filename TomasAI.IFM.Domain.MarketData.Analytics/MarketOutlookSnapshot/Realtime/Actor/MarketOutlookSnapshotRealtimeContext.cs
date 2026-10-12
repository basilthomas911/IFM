using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Realtime.Actor;

/// <summary>Provides the typed runtime context used by <see cref="MarketOutlookSnapshotRealtimeActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class MarketOutlookSnapshotRealtimeContext : EventActorContext, IMarketOutlookSnapshotRealtimeContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public MarketOutlookSnapshotRealtimeContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        IMarketDataApi marketDataApi,
        IMarketOutlookUpdateWriter updateWriter,
        ILogger<MarketOutlookSnapshotRealtimeActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Realtime, MarketOutlookSnapshotRealtimeActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        DbFactory = IsArgumentNull.Set(dbFactory);
        MarketDataApi = IsArgumentNull.Set(marketDataApi);
        UpdateWriter = IsArgumentNull.Set(updateWriter);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public IMarketDataApi MarketDataApi { get; }
    /// <inheritdoc/>
    public IMarketOutlookUpdateWriter UpdateWriter { get; }
    /// <inheritdoc/>
    public ILogger<MarketOutlookSnapshotRealtimeActor> Logger { get; }
}

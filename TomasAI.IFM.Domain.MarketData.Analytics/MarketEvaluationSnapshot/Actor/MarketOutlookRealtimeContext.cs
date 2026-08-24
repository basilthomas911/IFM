using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
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
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketEvaluationSnapshot.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="MarketOutlookRealtimeActor"/>.</summary>
public interface IMarketOutlookRealtimeContext : IRealtimeActorContext<MarketOutlookRealtimeActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the DbFactory service supplied to the actor context.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<MarketOutlookRealtimeActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="MarketOutlookRealtimeActor"/>.</summary>
public sealed class MarketOutlookRealtimeContext : EventActorContext, IRealtimeActorContext<MarketOutlookRealtimeActor>, IMarketOutlookRealtimeContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public MarketOutlookRealtimeContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        ILogger<MarketOutlookRealtimeActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Realtime, MarketOutlookRealtimeActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        DbFactory = IsArgumentNull.Set(dbFactory);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public ILogger<MarketOutlookRealtimeActor> Logger { get; }
}

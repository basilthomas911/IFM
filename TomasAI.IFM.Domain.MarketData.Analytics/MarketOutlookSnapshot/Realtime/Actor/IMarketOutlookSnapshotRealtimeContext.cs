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

/// <summary>Defines the readonly runtime services required by <see cref="MarketOutlookSnapshotRealtimeActor"/>.</summary>
public interface IMarketOutlookSnapshotRealtimeContext : IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the DbFactory service supplied to the actor context.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<MarketOutlookSnapshotRealtimeActor> Logger { get; }
    /// <summary>Gets the local Market Outlook update writer.</summary>
    IMarketOutlookUpdateWriter UpdateWriter { get; }
    /// <summary>Gets the provider-neutral live market-data state.</summary>
    IMarketDataApi MarketDataApi { get; }
}

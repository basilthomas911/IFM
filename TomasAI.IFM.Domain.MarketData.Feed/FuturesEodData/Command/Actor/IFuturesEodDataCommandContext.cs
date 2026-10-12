using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Actor;

/// <summary>Defines the runtime services required by <see cref="FuturesEodDataCommandActor"/>.</summary>
public interface IFuturesEodDataCommandContext : ICommandActorContext<FuturesEodDataCommandActor>
{
    /// <summary>Gets the database factory.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the Market Data database.</summary>
    IMarketDataDbContext MarketDataDb { get; }
    /// <summary>Gets the blackboard service.</summary>
    IBlackboardService BlackboardService { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesEodDataCommandActor> Logger { get; }
    /// <summary>Gets the event-source database.</summary>
    IEventSourceActorDbContext DbEventSource { get; }
    /// <summary>Gets the durable replay queue.</summary>
    IDurableReplayQueue DurableReplayQueue { get; }
    /// <summary>Gets the state factory.</summary>
    IEventSourceActorStateFactory StateFactory { get; }
    /// <summary>Gets the actor service.</summary>
    IActorService ActorService { get; }
    /// <summary>Gets the event projector.</summary>
    IEventProjector<FuturesEodDataCommandActor> EventProjector { get; }
}


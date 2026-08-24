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
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="FuturesRsiSignalEventActor"/>.</summary>
public interface IFuturesRsiSignalEventContext : IEventActorContext<FuturesRsiSignalEventActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the MarketDataApi service supplied to the actor context.</summary>
    IMarketDataApi MarketDataApi { get; }
    /// <summary>Gets the StatusConsoleWriter service supplied to the actor context.</summary>
    IStatusConsoleWriter StatusConsoleWriter { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<FuturesRsiSignalEventActor> Logger { get; }
    /// <summary>Gets the BlackboardService service supplied to the actor context.</summary>
    IBlackboardService BlackboardService { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="FuturesRsiSignalEventActor"/>.</summary>
public sealed class FuturesRsiSignalEventContext : EventActorContext, IEventActorContext<FuturesRsiSignalEventActor>, IFuturesRsiSignalEventContext
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

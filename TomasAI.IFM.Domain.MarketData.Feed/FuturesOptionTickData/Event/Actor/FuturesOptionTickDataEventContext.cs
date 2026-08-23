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

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Actor;

/// <summary>Defines the runtime services required by <see cref="FuturesOptionTickDataEventActor"/>.</summary>
public interface IFuturesOptionTickDataEventContext : IEventActorContext<FuturesOptionTickDataEventActor>
{
    /// <summary>Gets the actor supervisor.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesOptionTickDataEventActor> Logger { get; }
    /// <summary>Gets the MarketDataApi service.</summary>
    ApplicationMarketDataApi MarketDataApi { get; }
    /// <summary>Gets the StatusConsoleWriter service.</summary>
    IStatusConsoleWriter StatusConsoleWriter { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="FuturesOptionTickDataEventActor"/>.</summary>
public sealed class FuturesOptionTickDataEventContext : EventActorContext, IEventActorContext<FuturesOptionTickDataEventActor>, IFuturesOptionTickDataEventContext
{
    /// <summary>Initializes the typed event context.</summary>
    public FuturesOptionTickDataEventContext(
        IActorSupervisor supervisor,
        ILogger<FuturesOptionTickDataEventActor> logger,
        ApplicationMarketDataApi marketDataApi,
        IStatusConsoleWriter statusConsoleWriter)
        : base(supervisor, new ActorMailboxId(ActorType.Event, FuturesOptionTickDataEventActor.Actor))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Logger = IsArgumentNull.Set(logger);
        MarketDataApi = IsArgumentNull.Set(marketDataApi);
        StatusConsoleWriter = IsArgumentNull.Set(statusConsoleWriter);
    }
    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public ILogger<FuturesOptionTickDataEventActor> Logger { get; }
    /// <inheritdoc/>
    public ApplicationMarketDataApi MarketDataApi { get; }
    /// <inheritdoc/>
    public IStatusConsoleWriter StatusConsoleWriter { get; }
}


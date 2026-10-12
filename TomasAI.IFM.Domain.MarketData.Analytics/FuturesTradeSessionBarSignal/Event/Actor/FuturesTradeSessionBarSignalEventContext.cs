using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Event.Actor;

/// <summary>Provides the closed generic context for the bar signal Event actor.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesTradeSessionBarSignalEventContext
    : EventActorContext,
      IFuturesTradeSessionBarSignalEventContext
{
    /// <summary>Initializes the immutable Event context.</summary>
    public FuturesTradeSessionBarSignalEventContext(
        IActorSupervisor supervisor,
        TimeProvider timeProvider,
        ILogger<FuturesTradeSessionBarSignalEventActor> logger)
        : base(supervisor, new(ActorType.Event, FuturesTradeSessionBarSignalEventActor.ActorName))
    {
        TimeProvider = IsArgumentNull.Set(timeProvider);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc />
    public TimeProvider TimeProvider { get; }
    /// <inheritdoc />
    public ILogger<FuturesTradeSessionBarSignalEventActor> Logger { get; }
}

using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Event.Actor;

/// <summary>Defines readonly services required by the stateless VWAP Event actor.</summary>
public interface IFuturesVwapSignalEventContext : IEventActorContext<FuturesVwapSignalEventActor>
{
    ILogger<FuturesVwapSignalEventActor> Logger { get; }
}

/// <summary>Provides the closed generic VWAP Event context.</summary>
public sealed class FuturesVwapSignalEventContext : EventActorContext,
    IEventActorContext<FuturesVwapSignalEventActor>, IFuturesVwapSignalEventContext
{
    /// <summary>Initializes the readonly Event context.</summary>
    public FuturesVwapSignalEventContext(
        IActorSupervisor supervisor, ILogger<FuturesVwapSignalEventActor> logger)
        : base(supervisor, new(ActorType.Event, FuturesVwapSignalEventActor.ActorName)) =>
        Logger = IsArgumentNull.Set(logger);
    /// <inheritdoc />
    public ILogger<FuturesVwapSignalEventActor> Logger { get; }
}

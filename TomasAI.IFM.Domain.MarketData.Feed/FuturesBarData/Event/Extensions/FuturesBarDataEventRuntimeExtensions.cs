using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event.Extensions;

/// <summary>Provides typed runtime properties for <see cref="FuturesBarDataEventActor"/> contexts.</summary>
public static class FuturesBarDataEventRuntimeExtensions
{
    extension(IEventActorContext<FuturesBarDataEventActor> context)
    {
        /// <summary>Gets the actor supervisor.</summary>
        public IActorSupervisor Supervisor => IsArgumentNull.Set((context as IFuturesBarDataEventContext)?.Supervisor, nameof(context))!;
        /// <summary>Gets the typed actor logger.</summary>
        public ILogger<FuturesBarDataEventActor> Logger => IsArgumentNull.Set((context as IFuturesBarDataEventContext)?.Logger, nameof(context))!;
    }
}


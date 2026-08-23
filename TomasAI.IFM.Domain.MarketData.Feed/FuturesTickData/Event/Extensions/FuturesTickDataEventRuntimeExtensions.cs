using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event.Extensions;

/// <summary>Provides typed runtime properties for <see cref="FuturesTickDataEventActor"/> contexts.</summary>
public static class FuturesTickDataEventRuntimeExtensions
{
    extension(IEventActorContext<FuturesTickDataEventActor> context)
    {
        /// <summary>Gets the actor supervisor.</summary>
        public IActorSupervisor Supervisor => IsArgumentNull.Set((context as IFuturesTickDataEventContext)?.Supervisor, nameof(context))!;
        /// <summary>Gets the typed actor logger.</summary>
        public ILogger<FuturesTickDataEventActor> Logger => IsArgumentNull.Set((context as IFuturesTickDataEventContext)?.Logger, nameof(context))!;
    }
}


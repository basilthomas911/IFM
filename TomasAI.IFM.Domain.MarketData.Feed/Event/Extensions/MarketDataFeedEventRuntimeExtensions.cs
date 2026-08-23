using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;

/// <summary>Provides typed runtime properties for <see cref="MarketDataFeedEventActor"/> contexts.</summary>
public static class MarketDataFeedEventRuntimeExtensions
{
    extension(IEventActorContext<MarketDataFeedEventActor> context)
    {
        /// <summary>Gets the actor supervisor.</summary>
        public IActorSupervisor Supervisor => IsArgumentNull.Set((context as IMarketDataFeedEventContext)?.Supervisor, nameof(context))!;
        /// <summary>Gets the typed actor logger.</summary>
        public ILogger<MarketDataFeedEventActor> Logger => IsArgumentNull.Set((context as IMarketDataFeedEventContext)?.Logger, nameof(context))!;
    }
}


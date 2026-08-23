using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Extensions;

/// <summary>Provides typed runtime properties for <see cref="FuturesOptionTickDataEventActor"/> contexts.</summary>
public static class FuturesOptionTickDataEventRuntimeExtensions
{
    extension(IEventActorContext<FuturesOptionTickDataEventActor> context)
    {
        /// <summary>Gets the actor supervisor.</summary>
        public IActorSupervisor Supervisor => IsArgumentNull.Set((context as IFuturesOptionTickDataEventContext)?.Supervisor, nameof(context))!;
        /// <summary>Gets the typed actor logger.</summary>
        public ILogger<FuturesOptionTickDataEventActor> Logger => IsArgumentNull.Set((context as IFuturesOptionTickDataEventContext)?.Logger, nameof(context))!;
    }
}


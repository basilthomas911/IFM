using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event.Extensions;

/// <summary>Provides typed runtime properties for <see cref="FuturesEodDataEventActor"/> contexts.</summary>
public static class FuturesEodDataEventRuntimeExtensions
{
    extension(IEventActorContext<FuturesEodDataEventActor> context)
    {
        /// <summary>Gets the actor supervisor.</summary>
        public IActorSupervisor Supervisor => IsArgumentNull.Set((context as IFuturesEodDataEventContext)?.Supervisor, nameof(context))!;
        /// <summary>Gets the typed actor logger.</summary>
        public ILogger<FuturesEodDataEventActor> Logger => IsArgumentNull.Set((context as IFuturesEodDataEventContext)?.Logger, nameof(context))!;
    }
}


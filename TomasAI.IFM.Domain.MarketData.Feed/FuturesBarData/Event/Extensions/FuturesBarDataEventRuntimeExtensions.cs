using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event.Extensions;

/// <summary>Provides typed runtime properties for <see cref="FuturesBarDataEventActor"/> contexts.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# extension members use their declared receiver; static conversion would remove the established actor context extension contract.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034", Justification = "C# 14 extension blocks generate nested implementation types; the public API consists of actor context extension members, not nested domain types.")]
public static class FuturesBarDataEventRuntimeExtensions
{
    extension(IEventActorContext<FuturesBarDataEventActor> context)
    {
        /// <summary>Gets the actor supervisor.</summary>
        public IActorSupervisor Supervisor => IsArgumentNull.Named((context as IFuturesBarDataEventContext)?.Supervisor, nameof(context))!;
        /// <summary>Gets the typed actor logger.</summary>
        public ILogger<FuturesBarDataEventActor> Logger => IsArgumentNull.Named((context as IFuturesBarDataEventContext)?.Logger, nameof(context))!;
    }
}


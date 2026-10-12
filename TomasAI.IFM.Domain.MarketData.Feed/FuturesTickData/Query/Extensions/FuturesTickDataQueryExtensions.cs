using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Query.Extensions;

/// <summary>Provides typed runtime properties for <see cref="FuturesTickDataQueryActor"/> contexts.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# extension members use their declared receiver; static conversion would remove the established actor context extension contract.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034", Justification = "C# 14 extension blocks generate nested implementation types; the public API consists of actor context extension members, not nested domain types.")]
public static class FuturesTickDataQueryExtensions
{
    extension(IQueryActorContext<FuturesTickDataQueryActor> context)
    {
        /// <summary>Gets the database-context factory.</summary>
        public IDbContextFactory DbFactory =>
            IsArgumentNull.Named((context as IFuturesTickDataQueryContext)?.DbFactory, nameof(context))!;

        /// <summary>Gets the typed actor logger.</summary>
        public ILogger<FuturesTickDataQueryActor> Logger =>
            IsArgumentNull.Named((context as IFuturesTickDataQueryContext)?.Logger, nameof(context))!;
    }
}


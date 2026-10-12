#pragma warning disable CA1034 // C# 14 extension blocks generate nested metadata types; no public nested type is declared here.
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Query.Extensions;

/// <summary>Exposes market-data query services and operations.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# extension members use their declared receiver; static conversion would remove the established actor context extension contract.")]
public static partial class MarketDataQueryExtensions
{
    extension(IQueryActorContext<MarketDataQueryActor> context)
    {
        /// <summary>Gets the domain context.</summary>
        public IMarketDataQueryContext MarketDataContext => IsArgumentNull.Set(context as IMarketDataQueryContext)!;
        /// <summary>Gets the database factory.</summary>
        public IDbContextFactory DbFactory => context.MarketDataContext.DbFactory;
        /// <summary>Gets the actor logger.</summary>
        public ILogger<MarketDataQueryActor> Logger => context.MarketDataContext.Logger;
    }
}

#pragma warning restore CA1034

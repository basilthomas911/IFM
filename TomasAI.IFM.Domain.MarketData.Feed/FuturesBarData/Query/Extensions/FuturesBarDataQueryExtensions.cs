using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Query.Extensions;

/// <summary>Provides typed runtime properties for <see cref="FuturesBarDataQueryActor"/> contexts.</summary>
public static class FuturesBarDataQueryExtensions
{
    extension(IQueryActorContext<FuturesBarDataQueryActor> context)
    {
        /// <summary>Gets the database-context factory.</summary>
        public IDbContextFactory DbFactory =>
            IsArgumentNull.Set((context as IFuturesBarDataQueryContext)?.DbFactory, nameof(context))!;

        /// <summary>Gets the typed actor logger.</summary>
        public ILogger<FuturesBarDataQueryActor> Logger =>
            IsArgumentNull.Set((context as IFuturesBarDataQueryContext)?.Logger, nameof(context))!;
    }
}


using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query.Extensions;

/// <summary>Provides typed runtime properties for <see cref="MarketDataFeedQueryActor"/> contexts.</summary>
public static class MarketDataFeedQueryExtensions
{
    extension(IQueryActorContext<MarketDataFeedQueryActor> context)
    {
        /// <summary>Gets the database-context factory.</summary>
        public IDbContextFactory DbFactory =>
            IsArgumentNull.Set((context as IMarketDataFeedQueryContext)?.DbFactory, nameof(context))!;

        /// <summary>Gets the typed actor logger.</summary>
        public ILogger<MarketDataFeedQueryActor> Logger =>
            IsArgumentNull.Set((context as IMarketDataFeedQueryContext)?.Logger, nameof(context))!;
    }
}


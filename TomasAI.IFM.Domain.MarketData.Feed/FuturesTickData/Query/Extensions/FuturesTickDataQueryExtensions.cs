using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Query.Extensions;

/// <summary>Provides typed runtime properties for <see cref="FuturesTickDataQueryActor"/> contexts.</summary>
public static class FuturesTickDataQueryExtensions
{
    extension(IQueryActorContext<FuturesTickDataQueryActor> context)
    {
        /// <summary>Gets the database-context factory.</summary>
        public IDbContextFactory DbFactory =>
            IsArgumentNull.Set((context as IFuturesTickDataQueryContext)?.DbFactory, nameof(context))!;

        /// <summary>Gets the typed actor logger.</summary>
        public ILogger<FuturesTickDataQueryActor> Logger =>
            IsArgumentNull.Set((context as IFuturesTickDataQueryContext)?.Logger, nameof(context))!;
    }
}


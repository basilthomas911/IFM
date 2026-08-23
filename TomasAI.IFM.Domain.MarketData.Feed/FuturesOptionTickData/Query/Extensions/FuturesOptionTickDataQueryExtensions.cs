using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Query.Extensions;

/// <summary>Provides typed runtime properties for <see cref="FuturesOptionTickDataQueryActor"/> contexts.</summary>
public static class FuturesOptionTickDataQueryExtensions
{
    extension(IQueryActorContext<FuturesOptionTickDataQueryActor> context)
    {
        /// <summary>Gets the database-context factory.</summary>
        public IDbContextFactory DbFactory =>
            IsArgumentNull.Set((context as IFuturesOptionTickDataQueryContext)?.DbFactory, nameof(context))!;

        /// <summary>Gets the typed actor logger.</summary>
        public ILogger<FuturesOptionTickDataQueryActor> Logger =>
            IsArgumentNull.Set((context as IFuturesOptionTickDataQueryContext)?.Logger, nameof(context))!;
    }
}


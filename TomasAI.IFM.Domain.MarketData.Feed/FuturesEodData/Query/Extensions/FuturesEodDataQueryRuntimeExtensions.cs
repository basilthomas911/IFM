using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query.Extensions;

/// <summary>Provides typed runtime properties for Futures EOD query actor contexts.</summary>
public static class FuturesEodDataQueryRuntimeExtensions
{
    extension(IQueryActorContext<FuturesEodDataQueryActor> context)
    {
        /// <summary>Gets the database-context factory.</summary>
        public IDbContextFactory DbFactory => IsArgumentNull.Set((context as IFuturesEodDataQueryContext)?.DbFactory, nameof(context))!;
        /// <summary>Gets the typed actor logger.</summary>
        public ILogger<FuturesEodDataQueryActor> Logger => IsArgumentNull.Set((context as IFuturesEodDataQueryContext)?.Logger, nameof(context))!;
    }
}


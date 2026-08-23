using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Query.Extensions;

/// <summary>Exposes market-data query services and operations.</summary>
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

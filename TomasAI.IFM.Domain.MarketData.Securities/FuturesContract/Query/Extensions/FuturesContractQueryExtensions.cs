using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Query.Extensions;

/// <summary>Exposes futures-contract query services as readonly extension properties.</summary>
public static class FuturesContractQueryExtensions
{
    extension(IQueryActorContext<FuturesContractQueryActor> context)
    {
        /// <summary>Gets the domain query context.</summary>
        public IFuturesContractQueryContext FuturesContractContext => IsArgumentNull.Set(context as IFuturesContractQueryContext)!;
        /// <summary>Gets the database-context factory.</summary>
        public IDbContextFactory DbFactory => context.FuturesContractContext.DbFactory;
        /// <summary>Gets the actor logger.</summary>
        public ILogger<FuturesContractQueryActor> Logger => context.FuturesContractContext.Logger;
    }
}

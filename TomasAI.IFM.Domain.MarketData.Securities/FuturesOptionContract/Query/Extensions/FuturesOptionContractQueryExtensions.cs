using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Query.Extensions;

/// <summary>Exposes futures-option-contract query services as readonly extension properties.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# extension members use their declared receiver; static conversion would remove the established actor context extension contract.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034", Justification = "C# 14 extension blocks generate nested implementation types; the public API consists of actor context extension members, not nested domain types.")]
public static class FuturesOptionContractQueryExtensions
{
    extension(IQueryActorContext<FuturesOptionContractQueryActor> context)
    {
        /// <summary>Gets the domain query context.</summary>
        public IFuturesOptionContractQueryContext FuturesOptionContractContext => IsArgumentNull.Set(context as IFuturesOptionContractQueryContext)!;
        /// <summary>Gets the database-context factory.</summary>
        public IDbContextFactory DbFactory => context.FuturesOptionContractContext.DbFactory;
        /// <summary>Gets the actor logger.</summary>
        public ILogger<FuturesOptionContractQueryActor> Logger => context.FuturesOptionContractContext.Logger;
    }
}

using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Query;

public static class GetFuturesOptionContracts
{
    /// <summary>
    /// Handles a request to retrieve all futures option contracts by symbol.
    /// </summary>
    public static async ValueTask<FuturesOptionContractReadModel[]> GetFuturesOptionContractsAsync(
        this GetFuturesOptionContractsQuery q, IDbContextFactory dbFactory)
        => [.. await dbFactory.SecuritiesDb.GetFuturesOptionContractsAsync(q.Symbol)];
}

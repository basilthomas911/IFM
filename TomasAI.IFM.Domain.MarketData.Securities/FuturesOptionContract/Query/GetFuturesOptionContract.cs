using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Query;

public static class GetFuturesOptionContract
{
    /// <summary>
    /// Handles a request to retrieve a specific futures option contract by contract ID.
    /// </summary>
    public static async ValueTask<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(
        this GetFuturesOptionContractQuery q, IDbContextFactory dbFactory)
        => await dbFactory.SecuritiesDb.GetFuturesOptionContractAsync(q.ContractId);
}

using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Query;

public static class GetFuturesContracts
{
    public static async ValueTask<FuturesContractV2ReadModel[]> GetFuturesContractsAsync(
        this GetFuturesContractsQuery q, IDbContextFactory dbFactory)
        => [.. await dbFactory.SecuritiesDb.GetFuturesContractsAsync()];
}

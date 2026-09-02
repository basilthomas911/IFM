using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Query;

public static class GetFuturesContracts
{
    public static async ValueTask<FuturesContractV3ReadModel[]> GetFuturesContractsAsync(
        this GetFuturesContractsQuery q,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
        => [.. await (cancellationToken.CanBeCanceled
            ? dbFactory.SecuritiesDb.GetFuturesContractsAsync(cancellationToken)
            : dbFactory.SecuritiesDb.GetFuturesContractsAsync())];
}

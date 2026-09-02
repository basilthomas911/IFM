using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Query;

public static class GetRolloverFuturesContracts
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="q"></param>
    /// <param name="context"></param>
    /// <param name="dbFactory"></param>
    /// <returns></returns>
    public static async ValueTask<FuturesContractV3ReadModel[]> GetRolloverFuturesContractsAsync(
        this GetRolloverFuturesContractsQuery q,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
        => [.. await (cancellationToken.CanBeCanceled
            ? dbFactory.SecuritiesDb.GetRolloverFuturesContractsAsync(q.Symbol, cancellationToken)
            : dbFactory.SecuritiesDb.GetRolloverFuturesContractsAsync(q.Symbol))];
}

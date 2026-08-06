using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Query;

public static class GetFuturesContract
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="q"></param>
    /// <param name="context"></param>
    /// <param name="dbFactory"></param>
    /// <returns></returns>
    public static ValueTask<FuturesContractV2ReadModel?> GetFuturesContractAsync(
        this GetFuturesContractQuery q,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
        => new(cancellationToken.CanBeCanceled
            ? dbFactory.SecuritiesDb.GetFuturesContractAsync(q.ContractId, cancellationToken)
            : dbFactory.SecuritiesDb.GetFuturesContractAsync(q.ContractId));
}

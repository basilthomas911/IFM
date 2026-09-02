using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Query;

public static class GetOnTheRunFuturesContract
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="q"></param>
    /// <param name="context"></param>
    /// <param name="dbFactory"></param>
    /// <returns></returns>
    public static ValueTask<FuturesContractV3ReadModel?> GetOnTheRunFuturesContractAsync(
        this GetOnTheRunFuturesContractQuery q,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
        => new(cancellationToken.CanBeCanceled
            ? dbFactory.SecuritiesDb.GetOnTheRunFuturesContractAsync(q.Symbol, cancellationToken)
            : dbFactory.SecuritiesDb.GetOnTheRunFuturesContractAsync(q.Symbol));
}

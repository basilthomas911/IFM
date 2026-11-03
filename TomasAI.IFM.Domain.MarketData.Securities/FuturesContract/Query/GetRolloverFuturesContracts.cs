using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Query;

public static class GetRolloverFuturesContracts
{

    /// <param name="q">
    ///
    /// </param>
    extension(GetRolloverFuturesContractsQuery q)
    {
        /// <summary>
        ///
        /// </summary>
        ///
        /// <param name="context"></param>
        /// <param name="dbFactory"></param>
        /// <returns></returns>
        public async ValueTask<FuturesContractV3ReadModel[]> ExecuteAsync(
            IDbContextFactory dbFactory,
            CancellationToken cancellationToken = default)

        {
            ArgumentNullException.ThrowIfNull(dbFactory);
            ArgumentNullException.ThrowIfNull(q);
            return [.. await (dbFactory.SecuritiesDb.GetRolloverFuturesContractsAsync(q.Symbol, cancellationToken)).ConfigureAwait(false)];
        }
    }
}

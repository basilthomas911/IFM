using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Query.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Query;

/// <summary>Handles one latest-VWAP query and its typed reply.</summary>
public static class GetLatestFuturesVwapSignal
{

    extension(GetLatestFuturesVwapSignalQuery query)
    {
        /// <summary>Reads the current VWAP projection and replies to the caller.</summary>
        public async ValueTask<ServiceResult<FuturesVwapSignalReadModel?>> ExecuteAsync(
            IQueryActorContext<FuturesVwapSignalQueryActor> context,
            IFuturesVwapSignalQueryContext typedContext,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(typedContext);
            var current = await FuturesVwapSignalQueryModel.ExecuteAsync(query,
                typedContext.DbFactory, cancellationToken).ConfigureAwait(false);
            return new ServiceResult<FuturesVwapSignalReadModel?>(current);
        }
    }
}

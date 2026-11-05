using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Query.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Query.Actor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Query;

/// <summary>Handles the GetFuturesAdxSignalQuery query.</summary>
public static class GetFuturesAdxSignal
{

    extension(GetFuturesAdxSignalQuery q)
    {
        /// <summary>Reads the GetFuturesAdxSignalQuery message.</summary>
        public async ValueTask<ServiceResult<FuturesAdxSignalReadModel?>> ExecuteAsync(
            IQueryActorContext<FuturesAdxSignalQueryActor> ctx,
            IDbContextFactory dbFactory,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(ctx);
            ArgumentNullException.ThrowIfNull(q);
            var query = q;
            var queryResult = await query.GetLastFuturesAdxSignalAsync(dbFactory, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var serviceResult = new ServiceResult<FuturesAdxSignalReadModel?>(queryResult);
            return serviceResult;
        }
    }
}

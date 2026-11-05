using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Domain.MarketData.Feed.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Query.Model;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;
/// <summary>Handles <see cref="GetDatabentoReadinessQuery"/>.</summary>
public static class GetDatabentoReadiness
{

    extension(GetDatabentoReadinessQuery query)
    {
        /// <summary>Returns the mapped current Databento readiness snapshot.</summary>
        public async ValueTask<ServiceResult<DatabentoReadinessReadModel>> ExecuteAsync(IMarketDataFeedQueryContext context, MarketDataFeedQueryParameters parameters)

        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(parameters);
            ArgumentNullException.ThrowIfNull(query);
            var backend = await parameters.MarketDataApi.GetBackendStatusAsync().ConfigureAwait(false);
            var model = DatabentoQueryModel.MapReadiness(backend);
            return new ServiceResult<DatabentoReadinessReadModel>(model);
        }
    }
}

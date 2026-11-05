using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Domain.MarketData.Feed.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Query.Model;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;
/// <summary>Handles <see cref="GetMarketDataFeedRuntimeStatusQuery"/>.</summary>
public static class GetMarketDataFeedRuntimeStatus
{

    extension(GetMarketDataFeedRuntimeStatusQuery query)
    {
        /// <summary>Returns the current feed runtime status.</summary>
        public ValueTask<ServiceResult<MarketDataFeedRuntimeStatusReadModel>> ExecuteAsync(IMarketDataFeedQueryContext context, MarketDataFeedQueryParameters parameters)

        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(parameters);
            ArgumentNullException.ThrowIfNull(query);
            return ValueTask.FromResult(new ServiceResult<MarketDataFeedRuntimeStatusReadModel>(parameters.MarketDataApi.GetRuntimeStatus()));
        }
    }
}
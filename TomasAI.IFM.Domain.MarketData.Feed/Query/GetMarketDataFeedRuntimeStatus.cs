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
    /// <summary>Returns the current feed runtime status.</summary>
    public static ValueTask ExecuteAsync(this GetMarketDataFeedRuntimeStatusQuery query, IMarketDataFeedQueryContext context, MarketDataFeedQueryParameters parameters)
        => context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceResult<MarketDataFeedRuntimeStatusReadModel>(parameters.MarketDataApi.GetRuntimeStatus()));
}
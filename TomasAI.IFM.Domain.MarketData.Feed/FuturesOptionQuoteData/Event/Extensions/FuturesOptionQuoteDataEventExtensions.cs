using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Event.Extensions;

internal static class FuturesOptionQuoteDataEventExtensions
{
	internal static async ValueTask DeleteStreamingRequestIdAsync(this IActorMarketDataFeedCommandApi commandApi, FeedId feedId)
	{
		_ = await commandApi.DeleteStreamingRequestIdAsync(feedId);
	}

	internal static async ValueTask InsertFuturesOptionQuoteDataAsync(this IActorMarketDataFeedCommandApi commandApi, int quoteId, string contractId, QuoteData quoteData)
	{
		_ = await commandApi.InsertFuturesOptionQuoteDataAsync(quoteId, contractId, quoteData);
	}
}

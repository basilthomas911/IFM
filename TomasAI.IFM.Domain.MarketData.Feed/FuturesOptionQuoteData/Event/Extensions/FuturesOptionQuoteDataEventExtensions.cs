using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Event.Extensions;

internal static class FuturesOptionQuoteDataEventExtensions
{
	internal static async ValueTask<bool> SendFuturesOptionQuoteDataUpdatedEventAsync(
		this IEventActorContext context, FuturesOptionQuoteDataInsertedCompleteEvent e)
	{
		FuturesOptionQuoteDataUpdatedEvent updatedEvent = new(e.OptionQuoteData)
		{
			Subject = new ActorSubject(ActorType.Event, FuturesOptionQuoteDataUpdatedEvent.Actor, FuturesOptionQuoteDataUpdatedEvent.Verb, e.EntityId.Format()),
			EntityId = e.EntityId,
			Id = Guid.NewGuid(),
			CommandId = e.CommandId,
			AggregateId = e.AggregateId,
			EventSource = e.EventSource,
			ReceivedOn = DateTime.UtcNow
		};
		await context.SendAsync<FuturesOptionQuoteDataUpdatedEvent, QuoteId>(updatedEvent);
		return true;
	}

	internal static async ValueTask DeleteStreamingRequestIdAsync(this IEventActorContext context, FeedId feedId)
	{
		DeleteStreamingRequestIdCommand cmd = new(feedId)
		{
			Subject = new ActorSubject(ActorType.Command, StopFuturesBarDataStreamingCommand.Actor, StopFuturesBarDataStreamingCommand.Verb, feedId.Format()),
			EntityId = feedId,
			ErrorCode = StopFuturesBarDataStreamingCommand.ErrorId
		};
		var serviceResult = await context.RequestAsync<DeleteStreamingRequestIdCommand, FeedId>(cmd);
		if (serviceResult?.Success != true)
			throw new InvalidOperationException(serviceResult?.ErrorMessage);
	}

	internal static async ValueTask InsertFuturesOptionQuoteDataAsync(this IEventActorContext context, int quoteId, string contractId, QuoteData quoteData)
	{
		var entityId = new QuoteId(quoteId);
		InsertFuturesOptionQuoteDataCommand cmd = new(quoteId, contractId, quoteData)
		{
			Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionQuoteDataCommand.Actor, InsertFuturesOptionQuoteDataCommand.Verb, entityId.Format()),
			EntityId = entityId,
			ErrorCode = InsertFuturesOptionQuoteDataCommand.ErrorId
		};
		var serviceResult = await context.RequestAsync<InsertFuturesOptionQuoteDataCommand, QuoteId>(cmd);
		if (serviceResult?.Success != true)
			throw new InvalidOperationException(serviceResult?.ErrorMessage);
	}
}

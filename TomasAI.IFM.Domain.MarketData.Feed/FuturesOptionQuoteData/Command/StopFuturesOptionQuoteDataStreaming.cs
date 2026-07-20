using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Command;

public static class StopFuturesOptionQuoteDataStreaming
{
    /// <summary>
    /// Handle a <see cref="StopFuturesOptionQuoteDataStreamingCommand"/> by building the corresponding
    /// <see cref="FuturesOptionQuoteDataStreamingStoppedEvent"/> and updating the actor state.
    /// </summary>
    public static bool Execute(this StopFuturesOptionQuoteDataStreamingCommand e, FuturesOptionQuoteDataCommandState state)
        => e switch
        {
            _ when !state.QuoteExists(e.QuoteId) => throw new StopFuturesOptionQuoteDataStreamingException(e.StopFuturesOptionQuoteDataStreamingErrorMsg()),
            _ => state.Update(e.CreateFuturesOptionQuoteDataStreamingStoppedEvent(state.QuoteMap?.Values is not null ? [.. state.QuoteMap.Values] : []), e)
        };

    internal static FuturesOptionQuoteDataStreamingStoppedEvent CreateFuturesOptionQuoteDataStreamingStoppedEvent(this StopFuturesOptionQuoteDataStreamingCommand e, FuturesOptionQuoteReadModel[] futuresOptionQuotes)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, FuturesOptionQuoteDataStreamingStoppedEvent.Actor, FuturesOptionQuoteDataStreamingStoppedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            QuoteId = e.QuoteId,
            FuturesOptionQuotes = futuresOptionQuotes,
            StoppedOn = e.OriginatedOn,
            StoppedBy = e.OriginatedBy
        };

    internal static string StopFuturesOptionQuoteDataStreamingErrorMsg(this StopFuturesOptionQuoteDataStreamingCommand e)
        => $"{e.CommandName}: futures option quote data stream {e.QuoteId} does not exist";
}

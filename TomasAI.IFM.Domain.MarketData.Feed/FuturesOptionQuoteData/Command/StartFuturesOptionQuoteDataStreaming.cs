using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Command;

public static class StartFuturesOptionQuoteDataStreaming
{
    /// <summary>
    /// Handle a <see cref="StartFuturesOptionQuoteDataStreamingCommand"/> by building the corresponding
    /// <see cref="FuturesOptionQuoteDataStreamingStartedEvent"/> and updating the actor state.
    /// </summary>
    public static bool Execute(this StartFuturesOptionQuoteDataStreamingCommand e, FuturesOptionQuoteDataCommandState state)
        => e switch
        {
            _ when state.QuoteExists(e.QuoteId) => throw new StartFuturesOptionQuoteDataStreamingException(e.StartFuturesOptionQuoteDataStreamingErrorMsg()),
            _ => state.Update(e.CreateFuturesOptionQuoteDataStreamingStartedEvent(), e)
        };

    internal static FuturesOptionQuoteDataStreamingStartedEvent CreateFuturesOptionQuoteDataStreamingStartedEvent(this StartFuturesOptionQuoteDataStreamingCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, FuturesOptionQuoteDataStreamingStartedEvent.Actor, FuturesOptionQuoteDataStreamingStartedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            QuoteId = e.QuoteId,
            FuturesOptionQuotes = e.FuturesOptionQuotes,
            FuturesOptionContracts = e.FuturesOptionContracts,
            FuturesOptionQuoteData = [.. e.FuturesOptionQuotes.Select(o => new FuturesOptionQuoteDataReadModel(o.QuoteId, o.ContractId, o.RequestId, -1, -1, -1, -1))],
            StartedOn = e.OriginatedOn,
            StartedBy = e.OriginatedBy
        };

    internal static string StartFuturesOptionQuoteDataStreamingErrorMsg(this StartFuturesOptionQuoteDataStreamingCommand e)
        => $"{e.CommandName}: futures option quote data stream {e.QuoteId} already exists";

}

using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Command.State;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Command;

public static class InsertFuturesOptionQuoteData
{
    /// <summary>
    /// Handle an <see cref="InsertFuturesOptionQuoteDataCommand"/> by updating the option quote data
    /// in the actor state and producing a <see cref="FuturesOptionQuoteDataInsertedEvent"/> when valid.
    /// </summary>
    public static bool Execute(this InsertFuturesOptionQuoteDataCommand e, FuturesOptionQuoteDataCommandState state)
        => e switch
        {
            _ when !state.QuoteExists(e.QuoteId) => throw new InsertFuturesOptionQuoteDataException(e.InsertFuturesOptionQuoteDataErrorMsg()),
            _ => InsertFuturesOptionQuote(e, state)
        };

    static bool InsertFuturesOptionQuote(InsertFuturesOptionQuoteDataCommand e, FuturesOptionQuoteDataCommandState state)
    {
        var optionQuote = state.QuoteMap![e.ContractId];
        var optionQuoteData = state.GetFuturesOptionQuoteData(optionQuote.Id);
        if (optionQuoteData is null)
            return false;

        optionQuoteData = e switch
        {
            _ when e.QuoteData.QuoteType == QuoteType.Price && e.QuoteData.Side == QuoteSide.Ask => optionQuoteData with { AskPrice = Convert.ToDecimal(e.QuoteData.Price) },
            _ when e.QuoteData.QuoteType == QuoteType.Size && e.QuoteData.Side == QuoteSide.Ask => optionQuoteData with { AskPrice = e.QuoteData.Size },
            _ when e.QuoteData.QuoteType == QuoteType.Price && e.QuoteData.Side == QuoteSide.Bid => optionQuoteData with { BidPrice = Convert.ToDecimal(e.QuoteData.Price) },
            _ when e.QuoteData.QuoteType == QuoteType.Size && e.QuoteData.Side == QuoteSide.Bid => optionQuoteData with { BidPrice = e.QuoteData.Size },
            _ => optionQuoteData
        };
        state.SetFuturesOptionQuoteData(optionQuote.Id, optionQuoteData);
        var futuresOptionQuoteDataInsertedEvent = !optionQuoteData.IsValid
            ? default(IEvent)
            : e.CreateFuturesOptionQuoteDataInsertedEvent(optionQuoteData);

        return state.Update(futuresOptionQuoteDataInsertedEvent!, e);
    }
 
    internal static FuturesOptionQuoteDataInsertedEvent CreateFuturesOptionQuoteDataInsertedEvent(this InsertFuturesOptionQuoteDataCommand e, FuturesOptionQuoteDataReadModel optionQuoteData)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, FuturesOptionQuoteDataInsertedEvent.Actor, FuturesOptionQuoteDataInsertedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            QuoteId = e.QuoteId,
            ContractId = e.ContractId,
            OptionQuoteData = optionQuoteData,
            UpdatedOn = e.OriginatedOn,
            UpdatedBy = e.OriginatedBy
        };

    internal static string InsertFuturesOptionQuoteDataErrorMsg(this InsertFuturesOptionQuoteDataCommand e)
        => $"{e.CommandName}: futures option quote data stream {e.QuoteId} does not exist";

}

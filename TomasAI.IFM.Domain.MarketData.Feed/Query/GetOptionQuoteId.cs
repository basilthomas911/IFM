using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;

public static class GetOptionQuoteId
{
    internal static ValueTask<ScalarValue<int>> GetOptionQuoteIdAsync(
        this GetOptionQuoteIdQuery q, SequenceCounterModel sequenceCounter)
        => ValueTask.FromResult(new ScalarValue<int>(
            Convert.ToInt32(sequenceCounter.Increment(SequenceName.OptionQuote_QuoteId))));
}

using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;

public static class GetOptionQuoteId
{
    internal static async ValueTask GetOptionQuoteIdAsync(
        this GetOptionQuoteIdQuery q, IQueryActorContext context, SequenceCounterModel sequenceCounter)
    {
        var result = new ScalarValue<int>(Convert.ToInt32(sequenceCounter.Increment(SequenceName.OptionQuote_QuoteId)));
        await context.ReplyAsync(q.Subject.ThreadId, GetOptionQuoteIdQuery.Verb, new ServiceResult<ScalarValue<int>>(result));
    }
}

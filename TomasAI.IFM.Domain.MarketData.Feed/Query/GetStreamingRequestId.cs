using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;

public static class GetStreamingRequestId
{
    internal static async ValueTask GetStreamingRequestIdAsync(
        this GetStreamingRequestIdQuery q, IQueryActorContext context, SequenceCounterModel sequenceCounter)
    {
        var result = new ScalarValue<int>(Convert.ToInt32(sequenceCounter.Increment(SequenceName.StreamingRequest_RequestId)));
        await context.ReplyAsync(q.Subject.ThreadId, GetStreamingRequestIdQuery.Verb, new ServiceResult<ScalarValue<int>>(result));
    }
}

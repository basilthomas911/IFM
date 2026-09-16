using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;

public static class GetStreamingRequestId
{
    internal static async ValueTask<ScalarValue<int>> ExecuteAsync(
        this GetStreamingRequestIdQuery q,
        ISequenceIdGenerator sequenceIdGenerator,
        CancellationToken cancellationToken = default)
        => new(checked((int)await sequenceIdGenerator
            .GetSequenceIdAsync(SequenceName.StreamingRequest_RequestId, cancellationToken)
            .ConfigureAwait(false)));

    /// <summary>Reads and replies with the requested market-data feed result.</summary>
    public static async ValueTask ExecuteAsync(
        this GetStreamingRequestIdQuery query,
        TomasAI.IFM.Domain.MarketData.Feed.Query.Actor.IMarketDataFeedQueryContext context,
        MarketDataFeedQueryParameters parameters)
    {
        var result = await query.ExecuteAsync(parameters.SequenceIdGenerator).ConfigureAwait(false);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new TomasAI.IFM.Shared.EventSourcing.ServiceResult<ScalarValue<int>>(result)).ConfigureAwait(false);
    }
}

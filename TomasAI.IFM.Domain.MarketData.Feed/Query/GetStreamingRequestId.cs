using TomasAI.IFM.Application.Identity;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;

public static class GetStreamingRequestId
{

    extension(GetStreamingRequestIdQuery q)
    {
        internal async ValueTask<ScalarValue<int>> ExecuteAsync(
            ISequenceIdApi sequenceIdGenerator,
            CancellationToken cancellationToken = default)
            => new(checked((int)await sequenceIdGenerator
                .GetSequenceIdAsync(SequenceName.StreamingRequest_RequestId, cancellationToken)
                .ConfigureAwait(false)));
    }

    extension(GetStreamingRequestIdQuery query)
    {
        /// <summary>Reads and replies with the requested market-data feed result.</summary>
        public async ValueTask ExecuteAsync(
            TomasAI.IFM.Domain.MarketData.Feed.Query.Actor.IMarketDataFeedQueryContext context,
            MarketDataFeedQueryParameters parameters)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(parameters);
            ArgumentNullException.ThrowIfNull(query);
            var result = await query.ExecuteAsync(parameters.SequenceIdGenerator).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
                new TomasAI.IFM.Shared.EventSourcing.ServiceResult<ScalarValue<int>>(result)).ConfigureAwait(false);
        }
    }
}

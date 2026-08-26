using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketSignals.Query.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Indicators;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketSignals.Query.Actor;

/// <summary>Serves bounded latest-snapshot queries from successfully projected realtime state.</summary>
public sealed class FuturesRegimeIndicatorQueryActor(
    IQueryActorContext<FuturesRegimeIndicatorQueryActor> actorContext)
    : BaseQueryActor<FuturesRegimeIndicatorQueryActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the query actor mailbox name.</summary>
    public const string ActorName = GetLatestFuturesRegimeIndicatorSnapshotQuery.Actor;

    /// <summary>Parses the exact latest-snapshot query.</summary>
    protected override IQuery ParseMessage(
        IQueryActorContext<FuturesRegimeIndicatorQueryActor> context,
        IActorMessage message)
    {
        if (message.Subject is not
            {
                ActorType: ActorType.Query,
                Name: ActorName,
                Verb: GetLatestFuturesRegimeIndicatorSnapshotQuery.Verb
            })
            throw new InvalidOperationException($"Unsupported regime-indicator query {message.Subject}.");
        var query = message.AsQuery<
            GetLatestFuturesRegimeIndicatorSnapshotQuery,
            FuturesRegimeIndicatorSnapshot>()!;
        context.SetMessageInfo(message.Subject.ThreadId, message.Subject.Verb,
            new ActorMessageInfo(message, query));
        return query;
    }

    /// <summary>Replies with the latest successfully persisted snapshot, or a missing result.</summary>
    protected override ValueTask ReceiveAsync(
        IQueryActorContext<FuturesRegimeIndicatorQueryActor> context,
        IQuery query) => ReceiveAsync(context, query, CancellationToken.None);

    /// <summary>Replies with the latest successfully persisted snapshot, honoring cancellation.</summary>
    protected override async ValueTask ReceiveAsync(
        IQueryActorContext<FuturesRegimeIndicatorQueryActor> context,
        IQuery query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var getLatest = query as GetLatestFuturesRegimeIndicatorSnapshotQuery
            ?? throw new InvalidOperationException($"Unsupported regime-indicator query {query.GetType().Name}.");
        var entityId = new FuturesTradeSessionBarEntityId(
            getLatest.MarketSeriesIdentity,
            getLatest.TimeFrame);
        FuturesRegimeIndicatorSnapshotCache.TryGet(entityId, out var snapshot);
        await context.ReplyAsync(query.Subject.ThreadId, GetLatestFuturesRegimeIndicatorSnapshotQuery.Verb,
            new ServiceResult<FuturesRegimeIndicatorSnapshot?>(snapshot)).ConfigureAwait(false);
    }

    /// <summary>Returns a typed failure reply for a rejected query.</summary>
    protected override async ValueTask OnExceptionAsync(
        IQueryActorContext<FuturesRegimeIndicatorQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception) =>
        await context.ReplyAsync(threadId, verb,
            new ServiceResult<FuturesRegimeIndicatorSnapshot?>(query.ErrorCode, exception.Message))
            .ConfigureAwait(false);
}

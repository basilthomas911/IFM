using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Query.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Historical;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Query.Actor;

/// <summary>Returns durable, provider-neutral diagnostics for a historical data-load attempt.</summary>
public sealed class FuturesAnalyticsHistoricalDataLoaderQueryActor(
    IQueryActorContext<FuturesAnalyticsHistoricalDataLoaderQueryActor> actorContext)
    : BaseQueryActor<FuturesAnalyticsHistoricalDataLoaderQueryActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the Query actor mailbox name.</summary>
    public const string ActorName = GetFuturesAnalyticsHistoricalDataLoaderQuery.Actor;

    /// <inheritdoc />
    protected override IQuery ParseMessage(
        IQueryActorContext<FuturesAnalyticsHistoricalDataLoaderQueryActor> context,
        IActorMessage message)
    {
        if (message.Subject is not { ActorType: ActorType.Query, Name: ActorName,
                Verb: GetFuturesAnalyticsHistoricalDataLoaderQuery.Verb })
            throw new InvalidOperationException($"Unable to resolve {ActorName} query from {message.Subject}.");
        var query = message.AsQuery<
            GetFuturesAnalyticsHistoricalDataLoaderQuery,
            FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel>()
            ?? throw new InvalidOperationException("Unable to deserialize the data load query.");
        context.SetMessageInfo(message.Subject.ThreadId,
            message.Subject.Verb,
            new ActorMessageInfo(message, query));
        return query;
    }

    /// <inheritdoc />
    protected override ValueTask ReceiveAsync(
        IQueryActorContext<FuturesAnalyticsHistoricalDataLoaderQueryActor> context,
        IQuery query) => ReceiveAsync(context, query, CancellationToken.None);

    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(
        IQueryActorContext<FuturesAnalyticsHistoricalDataLoaderQueryActor> context,
        IQuery query,
        CancellationToken cancellationToken)
    {
        var request = (GetFuturesAnalyticsHistoricalDataLoaderQuery)query;
        var state = await context.DataLoaderStore.GetAsync(
            request.DataLoadAttemptId, cancellationToken).ConfigureAwait(false);
        var result = state is null ? null : new FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel
        {
            DataLoadAttemptId = state.DataLoadAttemptId,
            RequestSha256 = state.RequestSha256,
            Status = state.Status.ToString(),
            ManifestId = state.Manifest?.ManifestId,
            LastCompletedBatchOrdinal = checked((int)state.Checkpoint.BatchOrdinal),
            LastCompletedRecordOrdinal = long.TryParse(state.Checkpoint.SourcePosition, out var ordinal) ? ordinal : -1,
            ValidSessionCount = state.Audit?.ValidSessionCount ?? 0,
            GapCount = state.Audit?.Gaps.Count ?? 0,
            RollCount = state.Audit?.Rolls.Count ?? 0,
            ErrorMessage = state.ErrorMessage,
            UpdatedAtUtc = state.UpdatedAtUtc
        };
        cancellationToken.ThrowIfCancellationRequested();
        await context.ReplyAsync(query.Subject.ThreadId,
            GetFuturesAnalyticsHistoricalDataLoaderQuery.Verb,
            new ServiceResult<FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel?>(result)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask OnExceptionAsync(
        IQueryActorContext<FuturesAnalyticsHistoricalDataLoaderQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception) => await context.ReplyAsync(
            threadId, verb,
            new ServiceResult<FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel?>(
                query.ErrorCode, exception.Message)).ConfigureAwait(false);
}

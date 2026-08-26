using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalBootstrap.Query.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Historical;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalBootstrap.Query.Actor;

/// <summary>Returns durable, provider-neutral diagnostics for a history-bootstrap attempt.</summary>
public sealed class FuturesAnalyticsHistoryBootstrapQueryActor(
    IQueryActorContext<FuturesAnalyticsHistoryBootstrapQueryActor> actorContext)
    : BaseQueryActor<FuturesAnalyticsHistoryBootstrapQueryActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the Query actor mailbox name.</summary>
    public const string ActorName = GetFuturesAnalyticsHistoryBootstrapQuery.Actor;

    /// <inheritdoc />
    protected override IQuery ParseMessage(
        IQueryActorContext<FuturesAnalyticsHistoryBootstrapQueryActor> context,
        IActorMessage message)
    {
        if (message.Subject is not { ActorType: ActorType.Query, Name: ActorName,
                Verb: GetFuturesAnalyticsHistoryBootstrapQuery.Verb })
            throw new InvalidOperationException($"Unable to resolve {ActorName} query from {message.Subject}.");
        var query = message.AsQuery<
            GetFuturesAnalyticsHistoryBootstrapQuery,
            FuturesAnalyticsHistoryBootstrapDiagnosticsReadModel>()
            ?? throw new InvalidOperationException("Unable to deserialize the bootstrap query.");
        context.SetMessageInfo(message.Subject.ThreadId,
            message.Subject.Verb,
            new ActorMessageInfo(message, query));
        return query;
    }

    /// <inheritdoc />
    protected override ValueTask ReceiveAsync(
        IQueryActorContext<FuturesAnalyticsHistoryBootstrapQueryActor> context,
        IQuery query) => ReceiveAsync(context, query, CancellationToken.None);

    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(
        IQueryActorContext<FuturesAnalyticsHistoryBootstrapQueryActor> context,
        IQuery query,
        CancellationToken cancellationToken)
    {
        var request = (GetFuturesAnalyticsHistoryBootstrapQuery)query;
        var state = await context.BootstrapStore.GetAsync(
            request.BootstrapAttemptId, cancellationToken).ConfigureAwait(false);
        var result = state is null ? null : new FuturesAnalyticsHistoryBootstrapDiagnosticsReadModel
        {
            BootstrapAttemptId = state.BootstrapAttemptId,
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
            GetFuturesAnalyticsHistoryBootstrapQuery.Verb,
            new ServiceResult<FuturesAnalyticsHistoryBootstrapDiagnosticsReadModel?>(result)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask OnExceptionAsync(
        IQueryActorContext<FuturesAnalyticsHistoryBootstrapQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception) => await context.ReplyAsync(
            threadId, verb,
            new ServiceResult<FuturesAnalyticsHistoryBootstrapDiagnosticsReadModel?>(
                query.ErrorCode, exception.Message)).ConfigureAwait(false);
}

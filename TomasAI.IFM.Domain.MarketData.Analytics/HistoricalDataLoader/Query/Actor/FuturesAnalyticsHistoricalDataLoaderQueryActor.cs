using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Query.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;
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
        => ParseMappedQuery(context, message, _parseMap);

    static readonly Dictionary<string, Func<IActorMessage, IQuery>> _parseMap = new()
    {
        [GetFuturesAnalyticsHistoricalDataLoaderQuery.Verb] = message =>
            message.AsQuery<GetFuturesAnalyticsHistoricalDataLoaderQuery,
                FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel>()!
    };

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
        var receive = ResolveMappedQueryHandler(query, _receiveMap);
        await receive(context, query, cancellationToken).ConfigureAwait(false);
    }

    static readonly Dictionary<Type, Func<IQueryActorContext<FuturesAnalyticsHistoricalDataLoaderQueryActor>,
        IQuery, CancellationToken, ValueTask>> _receiveMap = new()
    {
        [typeof(GetFuturesAnalyticsHistoricalDataLoaderQuery)] = static async (context, query, cancellationToken) =>
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
    };

    /// <inheritdoc />
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap =
        CreateQueryExceptionMap(_receiveMap.Keys);

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<FuturesAnalyticsHistoricalDataLoaderQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);
}

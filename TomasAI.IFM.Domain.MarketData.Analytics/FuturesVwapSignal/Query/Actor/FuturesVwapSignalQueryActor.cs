using NATS.Client.Core;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Query.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Query.Actor;

/// <summary>Processes latest and history VWAP read-model queries.</summary>
public sealed class FuturesVwapSignalQueryActor(
    IQueryActorContext<FuturesVwapSignalQueryActor> actorContext)
    : BaseQueryActor<FuturesVwapSignalQueryActor>(actorContext,
        ((IFuturesVwapSignalQueryContext)actorContext).Logger)
{
    /// <summary>Identifies the VWAP Query mailbox.</summary>
    public const string ActorName = GetLatestFuturesVwapSignalQuery.Actor;
    IFuturesVwapSignalQueryContext TypedContext { get; } = IsArgumentNull.Set(
        actorContext as IFuturesVwapSignalQueryContext, nameof(actorContext))!;

    /// <inheritdoc />
    protected override IQuery ParseMessage(IQueryActorContext<FuturesVwapSignalQueryActor> context,
        IActorMessage message)
        => ParseMappedQuery(context, message, _parseMap);

    static readonly Dictionary<string, Func<IActorMessage, IQuery>> _parseMap = new()
    {
        [GetLatestFuturesVwapSignalQuery.Verb] = message =>
            message.AsQuery<GetLatestFuturesVwapSignalQuery, FuturesVwapSignalReadModel?>()!,
        [GetFuturesVwapSignalHistoryQuery.Verb] = message =>
            message.AsQuery<GetFuturesVwapSignalHistoryQuery, FuturesVwapSignalReadModel[]>()!
    };

    /// <inheritdoc />
    protected override ValueTask ReceiveAsync(
        IQueryActorContext<FuturesVwapSignalQueryActor> context, IQuery query) =>
        ReceiveAsync(context, query, CancellationToken.None);

    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(
        IQueryActorContext<FuturesVwapSignalQueryActor> context,
        IQuery query, CancellationToken cancellationToken)
    {
        var receive = ResolveMappedQueryHandler(query, _receiveMap);
        await receive(this, context, query, cancellationToken).ConfigureAwait(false);
    }

    static readonly Dictionary<Type, Func<FuturesVwapSignalQueryActor,
        IQueryActorContext<FuturesVwapSignalQueryActor>, IQuery, CancellationToken, ValueTask>> _receiveMap = new()
    {
        [typeof(GetLatestFuturesVwapSignalQuery)] = static async (actor, context, query, cancellationToken) =>
        {
            var latest = (GetLatestFuturesVwapSignalQuery)query;
            var current = await latest.ExecuteAsync(
                actor.TypedContext.DbFactory, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, latest.Subject.Verb,
                new ServiceResult<FuturesVwapSignalReadModel?>(current)).ConfigureAwait(false);
        },
        [typeof(GetFuturesVwapSignalHistoryQuery)] = static async (actor, context, query, cancellationToken) =>
        {
            var history = (GetFuturesVwapSignalHistoryQuery)query;
            var values = await history.ExecuteAsync(
                actor.TypedContext.DbFactory, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, history.Subject.Verb,
                new ServiceResult<FuturesVwapSignalReadModel[]>(values)).ConfigureAwait(false);
        }
    };

    /// <inheritdoc />
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap =
        CreateQueryExceptionMap(_receiveMap.Keys);

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<FuturesVwapSignalQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);
}

using NATS.Client.Core;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Query;
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

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap = new Dictionary<string, Func<IActorMessage, IQuery>>()
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
        await receive(context, TypedContext, query, cancellationToken).ConfigureAwait(false);
    }

    static readonly IReadOnlyDictionary<Type, Func<IQueryActorContext<FuturesVwapSignalQueryActor>,
        IFuturesVwapSignalQueryContext, IQuery, CancellationToken, ValueTask>> _receiveMap = new Dictionary<Type, Func<IQueryActorContext<FuturesVwapSignalQueryActor>,
        IFuturesVwapSignalQueryContext, IQuery, CancellationToken, ValueTask>>()
    {
        [typeof(GetLatestFuturesVwapSignalQuery)] = static (context, typedContext, query, cancellationToken) =>
            ((GetLatestFuturesVwapSignalQuery)query).ExecuteAsync(context, typedContext, cancellationToken),
        [typeof(GetFuturesVwapSignalHistoryQuery)] = static (context, typedContext, query, cancellationToken) =>
            ((GetFuturesVwapSignalHistoryQuery)query).ExecuteAsync(context, typedContext, cancellationToken)
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

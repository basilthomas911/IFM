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

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap = new Dictionary<string, Func<IActorMessage, IQuery>>()
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

    static readonly IReadOnlyDictionary<Type, Func<IQueryActorContext<FuturesAnalyticsHistoricalDataLoaderQueryActor>,
        IQuery, CancellationToken, ValueTask>> _receiveMap = new Dictionary<Type, Func<IQueryActorContext<FuturesAnalyticsHistoricalDataLoaderQueryActor>,
        IQuery, CancellationToken, ValueTask>>()
    {
        [typeof(GetFuturesAnalyticsHistoricalDataLoaderQuery)] = static (context, query, cancellationToken) =>
            ((GetFuturesAnalyticsHistoricalDataLoaderQuery)query).ExecuteAsync(context, cancellationToken)
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

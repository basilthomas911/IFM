using NATS.Client.Core;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Query.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Query.Actor;

/// <summary>Processes latest VX term-structure read-model queries.</summary>
public sealed class FuturesVxTermStructureSignalQueryActor(
    IQueryActorContext<FuturesVxTermStructureSignalQueryActor> actorContext)
    : BaseQueryActor<FuturesVxTermStructureSignalQueryActor>(actorContext, actorContext.Logger)
{
    public const string ActorName = GetLatestFuturesVxTermStructureSignalQuery.Actor;
    /// <inheritdoc />
    protected override IQuery ParseMessage(IQueryActorContext<FuturesVxTermStructureSignalQueryActor> context,
        IActorMessage message)
        => ParseMappedQuery(context, message, _parseMap);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap = new Dictionary<string, Func<IActorMessage, IQuery>>()
    {
        [GetLatestFuturesVxTermStructureSignalQuery.Verb] = message =>
            message.AsQuery<GetLatestFuturesVxTermStructureSignalQuery,
                FuturesVxTermStructureSignalReadModel?>()!
    };
    /// <inheritdoc />
    protected override ValueTask ReceiveAsync(IQueryActorContext<FuturesVxTermStructureSignalQueryActor> context,
        IQuery query) => ReceiveAsync(context, query, CancellationToken.None);
    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(
        IQueryActorContext<FuturesVxTermStructureSignalQueryActor> context,
        IQuery query,
        CancellationToken cancellationToken)
    {
        var receive = ResolveMappedQueryHandler(query, _receiveMap);
        await receive(context, query, cancellationToken).ConfigureAwait(false);
    }

    static readonly IReadOnlyDictionary<Type, Func<IQueryActorContext<FuturesVxTermStructureSignalQueryActor>,
        IQuery, CancellationToken, ValueTask>> _receiveMap = new Dictionary<Type, Func<IQueryActorContext<FuturesVxTermStructureSignalQueryActor>,
        IQuery, CancellationToken, ValueTask>>()
    {
        [typeof(GetLatestFuturesVxTermStructureSignalQuery)] = static async (context, query, cancellationToken) =>
        {
            var latest = (GetLatestFuturesVxTermStructureSignalQuery)query;
            var result = await latest.ExecuteAsync(context.DbFactory, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId,
                GetLatestFuturesVxTermStructureSignalQuery.Verb,
                new ServiceResult<FuturesVxTermStructureSignalReadModel?>(result)).ConfigureAwait(false);
        }
    };
    /// <inheritdoc />
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap =
        CreateQueryExceptionMap(_receiveMap.Keys);

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<FuturesVxTermStructureSignalQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);
}

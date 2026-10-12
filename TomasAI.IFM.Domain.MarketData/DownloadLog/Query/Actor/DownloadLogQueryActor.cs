using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.MarketData.DownloadLog.Query.Actor;

public sealed class DownloadLogQueryActor(IQueryActorContext<DownloadLogQueryActor> context)
    : BaseQueryActor<DownloadLogQueryActor>(context, ((IDownloadLogQueryContext)context).Logger)
{
    public const string ActorName = "DownloadLogQuery";
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap = new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
    {
        [GetMarketDataDownloadLogQuery.Verb] = m => m.AsQuery<GetMarketDataDownloadLogQuery, MarketDataDownloadLogResult>()!,
        [GetMarketDataDownloadHistoryQuery.Verb] = m => m.AsQuery<GetMarketDataDownloadHistoryQuery, MarketDataDownloadHistoryResult>()!,
        [GetMarketDataDownloadStatusQuery.Verb] = m => m.AsQuery<GetMarketDataDownloadStatusQuery, MarketDataDownloadStatusResult>()!,
    };
    static readonly IReadOnlyDictionary<Type, Func<IDownloadLogQueryContext, IQuery, CancellationToken, ValueTask>> _receiveMap = new Dictionary<Type, Func<IDownloadLogQueryContext, IQuery, CancellationToken, ValueTask>>
    {
        [typeof(GetMarketDataDownloadLogQuery)] = async (ctx, q, ct) =>
        {
            var result = await (((GetMarketDataDownloadLogQuery)q).ExecuteAsync(ctx.DbFactory, ct)).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            await (ctx.ReplyAsync(q.Subject.ThreadId, GetMarketDataDownloadLogQuery.Verb, new ServiceResult<MarketDataDownloadLogResult>(result))).ConfigureAwait(false);
        },
        [typeof(GetMarketDataDownloadHistoryQuery)] = async (ctx, q, ct) =>
        {
            var result = await (((GetMarketDataDownloadHistoryQuery)q).ExecuteAsync(ctx.DbFactory, ct)).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            await (ctx.ReplyAsync(q.Subject.ThreadId, GetMarketDataDownloadHistoryQuery.Verb, new ServiceResult<MarketDataDownloadHistoryResult>(result))).ConfigureAwait(false);
        },
        [typeof(GetMarketDataDownloadStatusQuery)] = async (ctx, q, ct) =>
        {
            var result = await (((GetMarketDataDownloadStatusQuery)q).ExecuteAsync(ctx.DbFactory, ct)).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            await (ctx.ReplyAsync(q.Subject.ThreadId, GetMarketDataDownloadStatusQuery.Verb, new ServiceResult<MarketDataDownloadStatusResult>(result))).ConfigureAwait(false);
        },
    };
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap = CreateQueryExceptionMap(_receiveMap.Keys);
    protected override IQuery ParseMessage(IQueryActorContext<DownloadLogQueryActor> context, IActorMessage message) => ParseMappedQuery(context, message, _parseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<DownloadLogQueryActor> context, IQuery query) => ReceiveAsync(context, query, CancellationToken.None);
    protected override ValueTask ReceiveAsync(IQueryActorContext<DownloadLogQueryActor> context, IQuery query, CancellationToken cancellationToken)
        => ResolveMappedQueryHandler(query, _receiveMap)((IDownloadLogQueryContext)context, query, cancellationToken);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<DownloadLogQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, ex, _exceptionMap);
}

using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Domain.MarketData.DownloadLog.Query.Extensions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.MarketData.DownloadLog.Query.Actor;
public sealed class DownloadLogQueryActor(IQueryActorContext<DownloadLogQueryActor> context)
    : BaseQueryActor<DownloadLogQueryActor>(context, ((IDownloadLogQueryContext)context).Logger)
{
    public const string ActorName = "DownloadLogQuery";
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap = new Dictionary<string, Func<IActorMessage, IQuery>>
    {
        [GetMarketDataDownloadLogQuery.Verb] = m => m.AsQuery<GetMarketDataDownloadLogQuery, MarketDataDownloadLogResult>()!,
        [GetMarketDataDownloadHistoryQuery.Verb] = m => m.AsQuery<GetMarketDataDownloadHistoryQuery, MarketDataDownloadHistoryResult>()!,
        [GetMarketDataDownloadStatusQuery.Verb] = m => m.AsQuery<GetMarketDataDownloadStatusQuery, MarketDataDownloadStatusResult>()!,
    };
    static readonly IReadOnlyDictionary<Type, Func<IDownloadLogQueryContext, IQuery, CancellationToken, ValueTask>> _receiveMap = new Dictionary<Type, Func<IDownloadLogQueryContext, IQuery, CancellationToken, ValueTask>>
    {
        [typeof(GetMarketDataDownloadLogQuery)] = async (ctx, q, ct) =>
        {
            var result = await ((GetMarketDataDownloadLogQuery)q).ExecuteAsync(ctx.DbFactory, ct);
            ct.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetMarketDataDownloadLogQuery.Verb, new ServiceResult<MarketDataDownloadLogResult>(result));
        },
        [typeof(GetMarketDataDownloadHistoryQuery)] = async (ctx, q, ct) =>
        {
            var result = await ((GetMarketDataDownloadHistoryQuery)q).ExecuteAsync(ctx.DbFactory, ct);
            ct.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetMarketDataDownloadHistoryQuery.Verb, new ServiceResult<MarketDataDownloadHistoryResult>(result));
        },
        [typeof(GetMarketDataDownloadStatusQuery)] = async (ctx, q, ct) =>
        {
            var result = await ((GetMarketDataDownloadStatusQuery)q).ExecuteAsync(ctx.DbFactory, ct);
            ct.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetMarketDataDownloadStatusQuery.Verb, new ServiceResult<MarketDataDownloadStatusResult>(result));
        },
    };
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap = CreateQueryExceptionMap(_receiveMap.Keys);
    protected override IQuery ParseMessage(IQueryActorContext<DownloadLogQueryActor> ctx, IActorMessage message) => ParseMappedQuery(ctx, message, _parseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<DownloadLogQueryActor> ctx, IQuery query) => ReceiveAsync(ctx, query, CancellationToken.None);
    protected override ValueTask ReceiveAsync(IQueryActorContext<DownloadLogQueryActor> ctx, IQuery query, CancellationToken ct)
        => ResolveMappedQueryHandler(query, _receiveMap)((IDownloadLogQueryContext)ctx, query, ct);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<DownloadLogQueryActor> ctx, ActorThreadId threadId, IQuery query, string verb, Exception ex)
        => ExceptionMappedQueryAsync(ctx, threadId, query, verb, ex, _exceptionMap);
}

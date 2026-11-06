using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Query;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Query;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Queries;
using System.Collections.Frozen;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Queries;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Query.Actor;
/// <summary>Routes scheduled task queries exclusively through persisted read models and timing preview.</summary>
public sealed class ScheduledTaskQueryActor(IQueryActorContext<ScheduledTaskQueryActor> context)
    : BaseQueryActor<ScheduledTaskQueryActor>(context, ((ScheduledTaskQueryContext)context).Logger)
{
    public const string Actor = "ScheduledTaskQuery";
    private readonly ScheduledTaskQueryContext _owner = (ScheduledTaskQueryContext)context;
    /// <summary>Gets all supported query types.</summary>
    public static IReadOnlyCollection<Type> SupportedQueryTypes =>CachedSupportedQueryTypes.Value;
    private static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap = new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
    {
        [GetScheduledTaskRunHistoryQuery.Verb] = message => message.AsQuery<GetScheduledTaskRunHistoryQuery, ScheduledTaskRunPage>()!,
        [GetScheduledTaskOutputQuery.Verb] = message => message.AsQuery<GetScheduledTaskOutputQuery, ScheduledTaskOutputPage>()!,
        [GetScheduledMarketPositionsQuery.Verb] = message => message.AsQuery<GetScheduledMarketPositionsQuery, ScheduledMarketPositionPage>()!,
        [GetScheduledTaskCatalogQuery.Verb] = message => message.AsQuery<GetScheduledTaskCatalogQuery, ScheduledTaskCatalog>()!,
        [GetScheduledTasksDashboardQuery.Verb] = message => message.AsQuery<GetScheduledTasksDashboardQuery, ScheduledTasksDashboard>()!,
        [GetScheduledTaskQuery.Verb] = message => message.AsQuery<GetScheduledTaskQuery, ScheduledTaskDefinition>()!,
        [PreviewScheduledTaskScheduleQuery.Verb] = message => message.AsQuery<PreviewScheduledTaskScheduleQuery, ScheduledTaskSchedulePreview>()!,
        [GetScheduledTaskRunQuery.Verb] = message => message.AsQuery<GetScheduledTaskRunQuery, ScheduledTaskRun>()!,
        [ListScheduledTaskRunsQuery.Verb] = message => message.AsQuery<ListScheduledTaskRunsQuery, ScheduledTaskRun[]>()!,
        [GetScheduledTaskHostHealthQuery.Verb] = message => message.AsQuery<GetScheduledTaskHostHealthQuery, ScheduledTaskHostCapability>()!,
    }.ToFrozenDictionary();
    private static readonly IReadOnlyDictionary<Type, Func<IQuery, ScheduledTaskQueryContext, IQueryActorContext<ScheduledTaskQueryActor>, CancellationToken, ValueTask>> _receiveMap = new Dictionary<Type, Func<IQuery, ScheduledTaskQueryContext, IQueryActorContext<ScheduledTaskQueryActor>, CancellationToken, ValueTask>>
    {
        [typeof(GetScheduledTaskRunHistoryQuery)] = static async (query, owner, context, cancellationToken) =>
        {
            var result = await ((GetScheduledTaskRunHistoryQuery)query).ExecuteAsync(owner.ReadStore, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetScheduledTaskOutputQuery)] = static async (query, owner, context, cancellationToken) =>
        {
            var result = await ((GetScheduledTaskOutputQuery)query).ExecuteAsync(owner.ReadStore, owner.OutputReader ?? throw new InvalidOperationException("ScheduledTask output reader is unavailable."), cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetScheduledMarketPositionsQuery)] = static async (query, owner, context, cancellationToken) =>
        {
            var result = await ((GetScheduledMarketPositionsQuery)query).ExecuteAsync(owner.TradeReads ?? throw new InvalidOperationException("Persisted position queries are unavailable."), cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetScheduledTaskCatalogQuery)] = static async (query, owner, context, cancellationToken) =>
        {
            var result = await ((GetScheduledTaskCatalogQuery)query).ExecuteAsync(owner.ReadStore, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetScheduledTasksDashboardQuery)] = static async (query, owner, context, cancellationToken) =>
        {
            var result = await ((GetScheduledTasksDashboardQuery)query).ExecuteAsync(owner.ReadStore, owner.Clock, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetScheduledTaskQuery)] = static async (query, owner, context, cancellationToken) =>
        {
            var result = await ((GetScheduledTaskQuery)query).ExecuteAsync(owner.ReadStore, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(PreviewScheduledTaskScheduleQuery)] = static async (query, owner, context, cancellationToken) =>
        {
            var result = await ((PreviewScheduledTaskScheduleQuery)query).ExecuteAsync(owner.Clock, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetScheduledTaskRunQuery)] = static async (query, owner, context, cancellationToken) =>
        {
            var result = await ((GetScheduledTaskRunQuery)query).ExecuteAsync(owner.ReadStore, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(ListScheduledTaskRunsQuery)] = static async (query, owner, context, cancellationToken) =>
        {
            var result = await ((ListScheduledTaskRunsQuery)query).ExecuteAsync(owner.ReadStore, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetScheduledTaskHostHealthQuery)] = static async (query, owner, context, cancellationToken) =>
        {
            var result = await ((GetScheduledTaskHostHealthQuery)query).ExecuteAsync(owner.ReadStore, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
    }.ToFrozenDictionary();
    private static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap = CreateQueryExceptionMap(_receiveMap.Keys);
    /// <inheritdoc />
    protected override IQuery ParseMessage(IQueryActorContext<ScheduledTaskQueryActor> context, IActorMessage message) => ParseMappedQuery(context, message, _parseMap);
    /// <inheritdoc />
    protected override ValueTask ReceiveAsync(IQueryActorContext<ScheduledTaskQueryActor> context, IQuery query) => ReceiveAsync(context, query, CancellationToken.None);
    /// <inheritdoc />
    protected override ValueTask ReceiveAsync(IQueryActorContext<ScheduledTaskQueryActor> context, IQuery query, CancellationToken cancellationToken) => ResolveMappedQueryHandler(query, _receiveMap)(query, _owner, context, cancellationToken);
    /// <inheritdoc />
    protected override ValueTask OnExceptionAsync(IQueryActorContext<ScheduledTaskQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex) => ExceptionMappedQueryAsync(context, threadId, query, verb, ex, _exceptionMap);
private static readonly System.Lazy<IReadOnlyCollection<Type>> CachedSupportedQueryTypes = new(() => System.Array.AsReadOnly(_receiveMap.Keys.ToArray()), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
}

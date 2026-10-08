using System.Collections.Frozen;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Queries;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Query.Actor;
/// <summary>Routes seven concrete queries exclusively through persisted read models and timing preview.</summary>
public sealed class ScheduledTaskQueryActor(IQueryActorContext<ScheduledTaskQueryActor> context)
    : BaseQueryActor<ScheduledTaskQueryActor>(context, ((ScheduledTaskQueryContext)context).Logger)
{
    public const string Actor = "ScheduledTaskQuery";
    private readonly ScheduledTaskQueryContext _owner = (ScheduledTaskQueryContext)context;
    /// <summary>Gets all supported query types.</summary>
    public static IReadOnlyCollection<Type> SupportedQueryTypes => _receiveMap.Keys.ToArray();
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
        [typeof(GetScheduledTaskRunHistoryQuery)] = (query, owner, context, cancellationToken) => ((GetScheduledTaskRunHistoryQuery)query).ExecuteAsync(owner.ReadStore, context, cancellationToken),
        [typeof(GetScheduledTaskOutputQuery)] = (query, owner, context, cancellationToken) => ((GetScheduledTaskOutputQuery)query).ExecuteAsync(owner.ReadStore, context, owner.OutputReader ?? throw new InvalidOperationException("ScheduledTask output reader is unavailable."), cancellationToken),
        [typeof(GetScheduledMarketPositionsQuery)] = (query, owner, context, cancellationToken) => ((GetScheduledMarketPositionsQuery)query).ExecuteAsync(owner.TradeReads ?? throw new InvalidOperationException("Persisted position queries are unavailable."), context, cancellationToken),
        [typeof(GetScheduledTaskCatalogQuery)] = (query, owner, context, cancellationToken) => ((GetScheduledTaskCatalogQuery)query).ExecuteAsync(owner.ReadStore, context, owner.Clock, cancellationToken),
        [typeof(GetScheduledTasksDashboardQuery)] = (query, owner, context, cancellationToken) => ((GetScheduledTasksDashboardQuery)query).ExecuteAsync(owner.ReadStore, context, owner.Clock, cancellationToken),
        [typeof(GetScheduledTaskQuery)] = (query, owner, context, cancellationToken) => ((GetScheduledTaskQuery)query).ExecuteAsync(owner.ReadStore, context, owner.Clock, cancellationToken),
        [typeof(PreviewScheduledTaskScheduleQuery)] = (query, owner, context, cancellationToken) => ((PreviewScheduledTaskScheduleQuery)query).ExecuteAsync(owner.ReadStore, context, owner.Clock, cancellationToken),
        [typeof(GetScheduledTaskRunQuery)] = (query, owner, context, cancellationToken) => ((GetScheduledTaskRunQuery)query).ExecuteAsync(owner.ReadStore, context, owner.Clock, cancellationToken),
        [typeof(ListScheduledTaskRunsQuery)] = (query, owner, context, cancellationToken) => ((ListScheduledTaskRunsQuery)query).ExecuteAsync(owner.ReadStore, context, owner.Clock, cancellationToken),
        [typeof(GetScheduledTaskHostHealthQuery)] = (query, owner, context, cancellationToken) => ((GetScheduledTaskHostHealthQuery)query).ExecuteAsync(owner.ReadStore, context, owner.Clock, cancellationToken),
    }.ToFrozenDictionary();
    private static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap = CreateQueryExceptionMap(_receiveMap.Keys);
    /// <inheritdoc />
    protected override IQuery ParseMessage(IQueryActorContext<ScheduledTaskQueryActor> context, IActorMessage message) => ParseMappedQuery(context, message, _parseMap);
    /// <inheritdoc />
    protected override ValueTask ReceiveAsync(IQueryActorContext<ScheduledTaskQueryActor> context, IQuery query) => ReceiveAsync(context, query, CancellationToken.None);
    /// <inheritdoc />
    protected override ValueTask ReceiveAsync(IQueryActorContext<ScheduledTaskQueryActor> context, IQuery query, CancellationToken cancellationToken) => ResolveMappedQueryHandler(query, _receiveMap)(query, _owner, context, cancellationToken);
    /// <inheritdoc />
    protected override ValueTask OnExceptionAsync(IQueryActorContext<ScheduledTaskQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception exception) => ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);
}

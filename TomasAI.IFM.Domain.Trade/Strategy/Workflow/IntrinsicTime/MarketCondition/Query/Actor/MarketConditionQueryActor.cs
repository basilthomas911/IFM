using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Queries;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Reference;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Query.Actor;

public sealed class MarketConditionQueryActor(IQueryActorContext<MarketConditionQueryActor> context)
    : BaseQueryActor<MarketConditionQueryActor>(context, Typed(context).Logger)
{
    public const string ActorName = GetMarketConditionQuery.Actor;
    readonly IMarketConditionQueryContext _context = Typed(context);
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
        {
            [GetMarketConditionAssessmentQuery.Verb] = x => x.AsQuery<GetMarketConditionAssessmentQuery, MarketConditionAssessmentCompletedEvent>()!,
            [GetMarketConditionAssessmentReferenceQuery.Verb] = x => x.AsQuery<GetMarketConditionAssessmentReferenceQuery, MarketConditionAssessmentReferenceRow[]>()!,
            [GetMarketConditionAssessmentHistoryQuery.Verb] = x => x.AsQuery<GetMarketConditionAssessmentHistoryQuery, MarketConditionAssessmentCompletedEvent[]>()!,
            [GetMarketConditionQuery.Verb] = x => x.AsQuery<GetMarketConditionQuery,
                MarketConditionReadModel>()!,
            [GetLatestMarketConditionQuery.Verb] = x => x.AsQuery<GetLatestMarketConditionQuery,
                MarketConditionReadModel>()!,
            [GetMarketConditionHistoryQuery.Verb] = x => x.AsQuery<GetMarketConditionHistoryQuery,
                ICollection<MarketConditionReadModel>>()!,

        };
    static readonly IReadOnlyDictionary<Type, Func<MarketConditionQueryActor, IQueryActorContext<MarketConditionQueryActor>,
        IQuery, CancellationToken, ValueTask>> _receiveMap = new Dictionary<Type, Func<MarketConditionQueryActor, IQueryActorContext<MarketConditionQueryActor>,
        IQuery, CancellationToken, ValueTask>>()
    {
        [typeof(GetMarketConditionAssessmentQuery)] = static async (actor, c, query, token) =>
        {
            var get = (GetMarketConditionAssessmentQuery)query;
            var value = await actor._context.DbFactory.TradeDb.GetMarketConditionAssessmentAsync(get.WorkflowId,token).ConfigureAwait(false)
                ?? throw new KeyNotFoundException("Assessment result was not found.");
            await c.ReplyAsync(query.Subject.ThreadId,get.Subject.Verb,new ServiceResult<MarketConditionAssessmentCompletedEvent>(value)).ConfigureAwait(false);
        },
        [typeof(GetMarketConditionAssessmentReferenceQuery)] = static async (_, c, query, _) =>
        {
            await c.ReplyAsync(query.Subject.ThreadId,query.Subject.Verb,new ServiceResult<MarketConditionAssessmentReferenceRow[]>(new MarketConditionAssessmentReferenceGenerator().Generate())).ConfigureAwait(false);
        },
        [typeof(GetMarketConditionAssessmentHistoryQuery)] = static async (actor, c, query, token) =>
        {
            var get = (GetMarketConditionAssessmentHistoryQuery)query;
            var values = await actor._context.DbFactory.TradeDb.GetMarketConditionAssessmentHistoryAsync(get.MarketProfileId,get.InstrumentRoot,get.TargetHorizon,get.BeforeUtc,get.PageSize,token).ConfigureAwait(false);
            await c.ReplyAsync(query.Subject.ThreadId,get.Subject.Verb,new ServiceResult<MarketConditionAssessmentCompletedEvent[]>(values.ToArray())).ConfigureAwait(false);
        },
        [typeof(GetMarketConditionQuery)] = static async (actor, c, query, token) =>
        {
            var get = (GetMarketConditionQuery)query;
            var value = await actor._context.DbFactory.TradeDb.GetMarketConditionAsync(get.WorkflowId, token)
                .ConfigureAwait(false) ?? throw new KeyNotFoundException(
                    $"Market Condition result for workflow {get.WorkflowId} was not found.");
            await c.ReplyAsync(query.Subject.ThreadId, get.Subject.Verb,
                new ServiceResult<MarketConditionReadModel>(value)).ConfigureAwait(false);
        },
        [typeof(GetLatestMarketConditionQuery)] = static async (actor, c, query, token) =>
        {
            var get = (GetLatestMarketConditionQuery)query;
            var values = await actor._context.DbFactory.TradeDb.GetMarketConditionHistoryAsync(
                get.FundId, get.InstrumentRoot, get.TargetHorizon, DateTime.MaxValue, 1, token)
                .ConfigureAwait(false);
            var value = values.FirstOrDefault() ?? throw new KeyNotFoundException(
                $"Latest Market Condition result for fund {get.FundId}/{get.InstrumentRoot}/{get.TargetHorizon} was not found.");
            await c.ReplyAsync(query.Subject.ThreadId, get.Subject.Verb,
                new ServiceResult<MarketConditionReadModel>(value)).ConfigureAwait(false);
        },
        [typeof(GetMarketConditionHistoryQuery)] = static async (actor, c, query, token) =>
        {
            var get = (GetMarketConditionHistoryQuery)query;
            var values = await actor._context.DbFactory.TradeDb.GetMarketConditionHistoryAsync(
                get.FundId, get.InstrumentRoot, get.TargetHorizon, get.BeforeUtc, get.PageSize, token)
                .ConfigureAwait(false);
            await c.ReplyAsync(query.Subject.ThreadId, get.Subject.Verb,
                new ServiceResult<ICollection<MarketConditionReadModel>>(values)).ConfigureAwait(false);
        },

    };
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap = CreateQueryExceptionMap(_receiveMap.Keys);
    protected override IQuery ParseMessage(
        IQueryActorContext<MarketConditionQueryActor> context,
        IActorMessage message)
        => ParseMappedQuery(context, message, _parseMap);
    protected override ValueTask ReceiveAsync(
        IQueryActorContext<MarketConditionQueryActor> context,
        IQuery query)
        => ReceiveAsync(context, query, CancellationToken.None);
    protected override async ValueTask ReceiveAsync(
        IQueryActorContext<MarketConditionQueryActor> context,
        IQuery query,
        CancellationToken cancellationToken)
        => await ResolveMappedQueryHandler(query, _receiveMap)(
            this, context, query, cancellationToken).ConfigureAwait(false);
    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<MarketConditionQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);
    static IMarketConditionQueryContext Typed(IQueryActorContext<MarketConditionQueryActor> c)
        => c as IMarketConditionQueryContext ?? throw new ArgumentException(
            $"{nameof(c)} must implement {nameof(IMarketConditionQueryContext)}.", nameof(c));
}

using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Query;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Queries;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Reference;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Query.Actor;
public sealed class MarketConditionQueryActor(IQueryActorContext<MarketConditionQueryActor> context) : BaseQueryActor<MarketConditionQueryActor>(context, Typed(context).Logger)
{
    public const string ActorName = GetMarketConditionQuery.Actor;
    readonly IMarketConditionQueryContext _context = Typed(context);
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap = new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
    {
        [GetMarketConditionAssessmentQuery.Verb] = x => x.AsQuery<GetMarketConditionAssessmentQuery, MarketConditionAssessmentCompletedEvent>()!,
        [GetMarketConditionAssessmentReferenceQuery.Verb] = x => x.AsQuery<GetMarketConditionAssessmentReferenceQuery, MarketConditionAssessmentReferenceRow[]>()!,
        [GetMarketConditionAssessmentHistoryQuery.Verb] = x => x.AsQuery<GetMarketConditionAssessmentHistoryQuery, MarketConditionAssessmentCompletedEvent[]>()!,
        [GetMarketConditionQuery.Verb] = x => x.AsQuery<GetMarketConditionQuery, MarketConditionReadModel>()!,
        [GetLatestMarketConditionQuery.Verb] = x => x.AsQuery<GetLatestMarketConditionQuery, MarketConditionReadModel>()!,
        [GetMarketConditionHistoryQuery.Verb] = x => x.AsQuery<GetMarketConditionHistoryQuery, ICollection<MarketConditionReadModel>>()!,
    };
    static readonly IReadOnlyDictionary<Type, Func<IMarketConditionQueryContext, IQueryActorContext<MarketConditionQueryActor>, IQuery, CancellationToken, ValueTask>> _receiveMap = new Dictionary<Type, Func<IMarketConditionQueryContext, IQueryActorContext<MarketConditionQueryActor>, IQuery, CancellationToken, ValueTask>>
    {
        [typeof(GetMarketConditionAssessmentQuery)] = static async (services, context, query, cancellationToken) =>
        {
            var result = await ((GetMarketConditionAssessmentQuery)query).ExecuteAsync(services, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetMarketConditionAssessmentReferenceQuery)] = static async (services, context, query, cancellationToken) =>
        {
            var result = await ((GetMarketConditionAssessmentReferenceQuery)query).ExecuteAsync(services, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetMarketConditionAssessmentHistoryQuery)] = static async (services, context, query, cancellationToken) =>
        {
            var result = await ((GetMarketConditionAssessmentHistoryQuery)query).ExecuteAsync(services, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetMarketConditionQuery)] = static async (services, context, query, cancellationToken) =>
        {
            var result = await ((GetMarketConditionQuery)query).ExecuteAsync(services, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetLatestMarketConditionQuery)] = static async (services, context, query, cancellationToken) =>
        {
            var result = await ((GetLatestMarketConditionQuery)query).ExecuteAsync(services, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetMarketConditionHistoryQuery)] = static async (services, context, query, cancellationToken) =>
        {
            var result = await ((GetMarketConditionHistoryQuery)query).ExecuteAsync(services, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        }
    };
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap = CreateQueryExceptionMap(_receiveMap.Keys);
    protected override IQuery ParseMessage(IQueryActorContext<MarketConditionQueryActor> context, IActorMessage message) => ParseMappedQuery(context, message, _parseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<MarketConditionQueryActor> context, IQuery query) => ReceiveAsync(context, query, CancellationToken.None);
    protected override async ValueTask ReceiveAsync(IQueryActorContext<MarketConditionQueryActor> context, IQuery query, CancellationToken cancellationToken) => await ResolveMappedQueryHandler(query, _receiveMap)(_context, context, query, cancellationToken).ConfigureAwait(false);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<MarketConditionQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex) => ExceptionMappedQueryAsync(context, threadId, query, verb, ex, _exceptionMap);
    static IMarketConditionQueryContext Typed(IQueryActorContext<MarketConditionQueryActor> c) => c as IMarketConditionQueryContext ?? throw new ArgumentException($"{nameof(c)} must implement {nameof(IMarketConditionQueryContext)}.", nameof(c));
}

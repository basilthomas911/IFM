using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Query.Actor;
public sealed class StrategyTradePlanActivityQueryActor(IQueryActorContext<StrategyTradePlanActivityQueryActor> context) : BaseQueryActor<StrategyTradePlanActivityQueryActor>(context, Typed(context).Logger)
{
    public const string ActorName = GetStrategyTradePlanActivityQuery.Actor;
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap = new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
    {
        [GetStrategyTradePlanActivityQuery.Verb] = message => message.AsQuery<GetStrategyTradePlanActivityQuery, StrategyTradePlanActivityPage>()!
    }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type, Func<IStrategyTradePlanActivityQueryContext, IQuery, CancellationToken, ValueTask>> _receiveMap = new Dictionary<Type, Func<IStrategyTradePlanActivityQueryContext, IQuery, CancellationToken, ValueTask>>
    {
        [typeof(GetStrategyTradePlanActivityQuery)] = static (owner, query, token) => ((GetStrategyTradePlanActivityQuery)query).ExecuteAsync(owner, token)
    }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap = CreateQueryExceptionMap(_receiveMap.Keys);
    protected override IQuery ParseMessage(IQueryActorContext<StrategyTradePlanActivityQueryActor> context, IActorMessage message) => ParseMappedQuery(context, message, _parseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<StrategyTradePlanActivityQueryActor> context, IQuery query) => ReceiveAsync(context, query, CancellationToken.None);
    protected override ValueTask ReceiveAsync(IQueryActorContext<StrategyTradePlanActivityQueryActor> context, IQuery query, CancellationToken cancellationToken) => ResolveMappedQueryHandler(query, _receiveMap)(Typed(context), query, cancellationToken);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<StrategyTradePlanActivityQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex) => ExceptionMappedQueryAsync(context, threadId, query, verb, ex, _exceptionMap);
    static IStrategyTradePlanActivityQueryContext Typed(IQueryActorContext<StrategyTradePlanActivityQueryActor> context) => context as IStrategyTradePlanActivityQueryContext ?? throw new ArgumentException("Typed strategy Trade Plan activity query context required.", nameof(context));
}

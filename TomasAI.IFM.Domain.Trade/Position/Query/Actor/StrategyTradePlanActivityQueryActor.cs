using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Position.Query.Actor;

public sealed class StrategyTradePlanActivityQueryActor(
    IQueryActorContext<StrategyTradePlanActivityQueryActor> context)
    : BaseQueryActor<StrategyTradePlanActivityQueryActor>(context, Typed(context).Logger)
{
    public const string ActorName = GetStrategyTradePlanActivityQuery.Actor;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> ParseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
        {
            [GetStrategyTradePlanActivityQuery.Verb] = message =>
                message.AsQuery<GetStrategyTradePlanActivityQuery,
                    StrategyTradePlanActivityPage>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type,
        Func<IStrategyTradePlanActivityQueryContext, IQuery, CancellationToken, ValueTask>>
        ReceiveMap =
        new Dictionary<Type,
            Func<IStrategyTradePlanActivityQueryContext, IQuery, CancellationToken, ValueTask>>
        {
            [typeof(GetStrategyTradePlanActivityQuery)] = static (owner, query, token) =>
                ((GetStrategyTradePlanActivityQuery)query).ExecuteAsync(owner, token)
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> ExceptionMap =
        CreateQueryExceptionMap(ReceiveMap.Keys);

    protected override IQuery ParseMessage(
        IQueryActorContext<StrategyTradePlanActivityQueryActor> actorContext,
        IActorMessage message) => ParseMappedQuery(actorContext, message, ParseMap);

    protected override ValueTask ReceiveAsync(
        IQueryActorContext<StrategyTradePlanActivityQueryActor> actorContext,
        IQuery query) => ReceiveAsync(actorContext, query, CancellationToken.None);

    protected override ValueTask ReceiveAsync(
        IQueryActorContext<StrategyTradePlanActivityQueryActor> actorContext,
        IQuery query,
        CancellationToken cancellationToken) =>
        ResolveMappedQueryHandler(query, ReceiveMap)(Typed(actorContext), query, cancellationToken);

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<StrategyTradePlanActivityQueryActor> actorContext,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception) =>
        ExceptionMappedQueryAsync(actorContext, threadId, query, verb, exception, ExceptionMap);

    static IStrategyTradePlanActivityQueryContext Typed(
        IQueryActorContext<StrategyTradePlanActivityQueryActor> context) =>
        context as IStrategyTradePlanActivityQueryContext ??
        throw new ArgumentException("Typed strategy Trade Plan activity query context required.");
}

public interface IStrategyTradePlanActivityQueryContext :
    IQueryActorContext<StrategyTradePlanActivityQueryActor>
{
    IDbContextFactory DbFactory { get; }
    ILogger<StrategyTradePlanActivityQueryActor> Logger { get; }
}

public sealed class StrategyTradePlanActivityQueryContext(
    IActorSupervisor supervisor,
    IDbContextFactory dbFactory,
    ILogger<StrategyTradePlanActivityQueryActor> logger)
    : QueryActorContext(supervisor,
        new(ActorType.Query, StrategyTradePlanActivityQueryActor.ActorName)),
      IStrategyTradePlanActivityQueryContext
{
    public IDbContextFactory DbFactory { get; } = dbFactory;
    public ILogger<StrategyTradePlanActivityQueryActor> Logger { get; } = logger;
}

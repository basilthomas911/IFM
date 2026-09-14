using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Query.Actor;

/// <summary>Serves current and historical outright Futures Trade Plans.</summary>
public sealed class FuturesTradePlanQueryActor(IQueryActorContext<FuturesTradePlanQueryActor> context)
    : BaseQueryActor<FuturesTradePlanQueryActor>(context, Typed(context).Logger)
{
    public const string ActorName = GetCurrentFuturesTradePlanQuery.Actor;
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> ParseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
        {
            [GetCurrentFuturesTradePlanQuery.Verb] = message =>
                message.AsQuery<GetCurrentFuturesTradePlanQuery, StrategyTradePlanSnapshot?>()!,
            [GetFuturesTradePlanHistoryQuery.Verb] = message =>
                message.AsQuery<GetFuturesTradePlanHistoryQuery, StrategyTradePlanHistoryPage>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type,
        Func<IFuturesTradePlanQueryContext, IQuery, CancellationToken, ValueTask>> ReceiveMap =
        new Dictionary<Type, Func<IFuturesTradePlanQueryContext, IQuery, CancellationToken, ValueTask>>
        {
            [typeof(GetCurrentFuturesTradePlanQuery)] = static (c, q, t) =>
                ((GetCurrentFuturesTradePlanQuery)q).ExecuteAsync(c, t),
            [typeof(GetFuturesTradePlanHistoryQuery)] = static (c, q, t) =>
                ((GetFuturesTradePlanHistoryQuery)q).ExecuteAsync(c, t)
        }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> ExceptionMap =
        CreateQueryExceptionMap(ReceiveMap.Keys);

    protected override IQuery ParseMessage(IQueryActorContext<FuturesTradePlanQueryActor> c, IActorMessage m) =>
        ParseMappedQuery(c, m, ParseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<FuturesTradePlanQueryActor> c, IQuery q) =>
        ReceiveAsync(c, q, CancellationToken.None);
    protected override ValueTask ReceiveAsync(IQueryActorContext<FuturesTradePlanQueryActor> c, IQuery q,
        CancellationToken t) => ResolveMappedQueryHandler(q, ReceiveMap)(Typed(c), q, t);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<FuturesTradePlanQueryActor> c,
        ActorThreadId id, IQuery q, string verb, Exception exception) =>
        ExceptionMappedQueryAsync(c, id, q, verb, exception, ExceptionMap);
    static IFuturesTradePlanQueryContext Typed(IQueryActorContext<FuturesTradePlanQueryActor> context) =>
        context as IFuturesTradePlanQueryContext
        ?? throw new ArgumentException("Typed Futures Trade Plan query context required.");
}

public interface IFuturesTradePlanQueryContext : IQueryActorContext<FuturesTradePlanQueryActor>
{
    IDbContextFactory DbFactory { get; }
    ILogger<FuturesTradePlanQueryActor> Logger { get; }
}

public sealed class FuturesTradePlanQueryContext(IActorSupervisor supervisor, IDbContextFactory dbFactory,
    ILogger<FuturesTradePlanQueryActor> logger)
    : QueryActorContext(supervisor, new(ActorType.Query, FuturesTradePlanQueryActor.ActorName)),
        IQueryActorContext<FuturesTradePlanQueryActor>, IFuturesTradePlanQueryContext
{
    public IDbContextFactory DbFactory { get; } = dbFactory;
    public ILogger<FuturesTradePlanQueryActor> Logger { get; } = logger;
}

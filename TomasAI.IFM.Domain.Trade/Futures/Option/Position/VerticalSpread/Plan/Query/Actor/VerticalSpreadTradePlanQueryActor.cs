using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Query.Actor;

/// <summary>Serves current and historical Vertical Spread Trade Plans.</summary>
public sealed class VerticalSpreadTradePlanQueryActor(
    IQueryActorContext<VerticalSpreadTradePlanQueryActor> context)
    : BaseQueryActor<VerticalSpreadTradePlanQueryActor>(context, Typed(context).Logger)
{
    public const string ActorName = GetCurrentVerticalSpreadTradePlanQuery.Actor;
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> ParseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
        {
            [GetCurrentVerticalSpreadTradePlanQuery.Verb] = message =>
                message.AsQuery<GetCurrentVerticalSpreadTradePlanQuery, StrategyTradePlanSnapshot?>()!,
            [GetVerticalSpreadTradePlanHistoryQuery.Verb] = message =>
                message.AsQuery<GetVerticalSpreadTradePlanHistoryQuery, StrategyTradePlanHistoryPage>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type,
        Func<IVerticalSpreadTradePlanQueryContext, IQuery, CancellationToken, ValueTask>> ReceiveMap =
        new Dictionary<Type, Func<IVerticalSpreadTradePlanQueryContext, IQuery, CancellationToken, ValueTask>>
        {
            [typeof(GetCurrentVerticalSpreadTradePlanQuery)] = static (c, q, t) =>
                ((GetCurrentVerticalSpreadTradePlanQuery)q).ExecuteAsync(c, t),
            [typeof(GetVerticalSpreadTradePlanHistoryQuery)] = static (c, q, t) =>
                ((GetVerticalSpreadTradePlanHistoryQuery)q).ExecuteAsync(c, t)
        }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> ExceptionMap =
        CreateQueryExceptionMap(ReceiveMap.Keys);

    protected override IQuery ParseMessage(IQueryActorContext<VerticalSpreadTradePlanQueryActor> c, IActorMessage m) =>
        ParseMappedQuery(c, m, ParseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<VerticalSpreadTradePlanQueryActor> c, IQuery q) =>
        ReceiveAsync(c, q, CancellationToken.None);
    protected override ValueTask ReceiveAsync(IQueryActorContext<VerticalSpreadTradePlanQueryActor> c, IQuery q,
        CancellationToken t) => ResolveMappedQueryHandler(q, ReceiveMap)(Typed(c), q, t);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<VerticalSpreadTradePlanQueryActor> c,
        ActorThreadId id, IQuery q, string verb, Exception exception) =>
        ExceptionMappedQueryAsync(c, id, q, verb, exception, ExceptionMap);
    static IVerticalSpreadTradePlanQueryContext Typed(IQueryActorContext<VerticalSpreadTradePlanQueryActor> context) =>
        context as IVerticalSpreadTradePlanQueryContext
        ?? throw new ArgumentException("Typed Vertical Spread Trade Plan query context required.");
}

public interface IVerticalSpreadTradePlanQueryContext : IQueryActorContext<VerticalSpreadTradePlanQueryActor>
{
    IDbContextFactory DbFactory { get; }
    ILogger<VerticalSpreadTradePlanQueryActor> Logger { get; }
}

public sealed class VerticalSpreadTradePlanQueryContext(IActorSupervisor supervisor, IDbContextFactory dbFactory,
    ILogger<VerticalSpreadTradePlanQueryActor> logger)
    : QueryActorContext(supervisor, new(ActorType.Query, VerticalSpreadTradePlanQueryActor.ActorName)),
        IQueryActorContext<VerticalSpreadTradePlanQueryActor>, IVerticalSpreadTradePlanQueryContext
{
    public IDbContextFactory DbFactory { get; } = dbFactory;
    public ILogger<VerticalSpreadTradePlanQueryActor> Logger { get; } = logger;
}

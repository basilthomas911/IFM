using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Query.Actor;

/// <summary>Serves current and historical Iron Condor Trade Plans.</summary>
public sealed class IronCondorTradePlanQueryActor(
    IQueryActorContext<IronCondorTradePlanQueryActor> context)
    : BaseQueryActor<IronCondorTradePlanQueryActor>(context, Typed(context).Logger)
{
    public const string ActorName = GetCurrentIronCondorTradePlanQuery.Actor;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
        {
            [GetCurrentIronCondorTradePlanQuery.Verb] = message =>
                message.AsQuery<GetCurrentIronCondorTradePlanQuery, StrategyTradePlanSnapshot?>()!,
            [GetIronCondorTradePlanHistoryQuery.Verb] = message =>
                message.AsQuery<GetIronCondorTradePlanHistoryQuery, StrategyTradePlanHistoryPage>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type,
        Func<IIronCondorTradePlanQueryContext, IQuery, CancellationToken, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<IIronCondorTradePlanQueryContext, IQuery, CancellationToken, ValueTask>>
        {
            [typeof(GetCurrentIronCondorTradePlanQuery)] = static (c, q, t) =>
                ((GetCurrentIronCondorTradePlanQuery)q).ExecuteAsync(c, t),
            [typeof(GetIronCondorTradePlanHistoryQuery)] = static (c, q, t) =>
                ((GetIronCondorTradePlanHistoryQuery)q).ExecuteAsync(c, t)
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap =
        CreateQueryExceptionMap(_receiveMap.Keys);

    protected override IQuery ParseMessage(IQueryActorContext<IronCondorTradePlanQueryActor> c, IActorMessage m) =>
        ParseMappedQuery(c, m, _parseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<IronCondorTradePlanQueryActor> c, IQuery q) =>
        ReceiveAsync(c, q, CancellationToken.None);
    protected override ValueTask ReceiveAsync(IQueryActorContext<IronCondorTradePlanQueryActor> c, IQuery q,
        CancellationToken t) => ResolveMappedQueryHandler(q, _receiveMap)(Typed(c), q, t);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<IronCondorTradePlanQueryActor> c,
        ActorThreadId id, IQuery q, string verb, Exception exception) =>
        ExceptionMappedQueryAsync(c, id, q, verb, exception, _exceptionMap);

    static IIronCondorTradePlanQueryContext Typed(IQueryActorContext<IronCondorTradePlanQueryActor> context) =>
        context as IIronCondorTradePlanQueryContext
        ?? throw new ArgumentException("Typed Iron Condor Trade Plan query context required.");
}

public interface IIronCondorTradePlanQueryContext : IQueryActorContext<IronCondorTradePlanQueryActor>
{
    IDbContextFactory DbFactory { get; }
    ILogger<IronCondorTradePlanQueryActor> Logger { get; }
}

public sealed class IronCondorTradePlanQueryContext(IActorSupervisor supervisor, IDbContextFactory dbFactory,
    ILogger<IronCondorTradePlanQueryActor> logger)
    : QueryActorContext(supervisor, new(ActorType.Query, IronCondorTradePlanQueryActor.ActorName)),
        IQueryActorContext<IronCondorTradePlanQueryActor>, IIronCondorTradePlanQueryContext
{
    public IDbContextFactory DbFactory { get; } = dbFactory;
    public ILogger<IronCondorTradePlanQueryActor> Logger { get; } = logger;
}

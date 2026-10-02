using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.Queries;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Health.Query.Actor;

/// <summary>Serves immutable Supervisor operational-health queries through the standard NATS actor runtime.</summary>
public sealed class SupervisorQueryActor(IQueryActorContext<SupervisorQueryActor> context)
    : BaseQueryActor<SupervisorQueryActor>(context, Require(context).Logger)
{
    public const string ActorName = GetSupervisorHealthSummaryQuery.Actor;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
        {
            [GetSupervisorHealthSummaryQuery.QueryVerb] = static message =>
                message.AsQuery<GetSupervisorHealthSummaryQuery, SupervisorHealthSummaryReadModel>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type,
        Func<ISupervisorQueryActorContext, IQuery, CancellationToken, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<ISupervisorQueryActorContext, IQuery, CancellationToken, ValueTask>>
        {
            [typeof(GetSupervisorHealthSummaryQuery)] = static (owner, query, token) =>
                ((GetSupervisorHealthSummaryQuery)query).ExecuteAsync(owner, token)
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap =
        CreateQueryExceptionMap(_receiveMap.Keys);

    protected override IQuery ParseMessage(
        IQueryActorContext<SupervisorQueryActor> context,
        IActorMessage message) => ParseMappedQuery(context, message, _parseMap);

    protected override ValueTask ReceiveAsync(
        IQueryActorContext<SupervisorQueryActor> context,
        IQuery query) => ReceiveAsync(context, query, CancellationToken.None);

    protected override ValueTask ReceiveAsync(
        IQueryActorContext<SupervisorQueryActor> context,
        IQuery query,
        CancellationToken cancellationToken) =>
        ResolveMappedQueryHandler(query, _receiveMap)(Require(context), query, cancellationToken);

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<SupervisorQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception) => ExceptionMappedQueryAsync(
            context, threadId, query, verb, exception, _exceptionMap);

    static ISupervisorQueryActorContext Require(IQueryActorContext<SupervisorQueryActor> context) =>
        context as ISupervisorQueryActorContext
        ?? throw new ArgumentException("A privileged Supervisor query context is required.", nameof(context));
}

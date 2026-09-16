using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.Configuration.Strategy;
using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Query;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Configuration.Strategy.Query.Actor;

/// <summary>Serves exact and effective immutable Regime Discovery parameter-set queries.</summary>
public sealed class RegimeDiscoveryConfigurationQueryActor(
    IQueryActorContext<RegimeDiscoveryConfigurationQueryActor> actorContext)
    : BaseQueryActor<RegimeDiscoveryConfigurationQueryActor>(actorContext, Typed(actorContext).Logger)
{
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
        {
            [GetRegimeDiscoveryParameterSetQuery.Verb] = message =>
                message.AsQuery<GetRegimeDiscoveryParameterSetQuery, RegimeDiscoveryParameterSet>()!,
            [ResolveRegimeDiscoveryParameterSetQuery.Verb] = message =>
                message.AsQuery<ResolveRegimeDiscoveryParameterSetQuery, RegimeDiscoveryParameterSet>()!
        };

    /// <summary>Gets the Query actor name.</summary>
    public const string ActorName = GetRegimeDiscoveryParameterSetQuery.Actor;
    IRegimeDiscoveryConfigurationQueryContext ActorContext { get; } = Typed(actorContext);

    /// <inheritdoc />
    protected override IQuery ParseMessage(IQueryActorContext<RegimeDiscoveryConfigurationQueryActor> context,
        IActorMessage message)
        => ParseMappedQuery(context, message, _parseMap);

    /// <inheritdoc />
    protected override ValueTask ReceiveAsync(
        IQueryActorContext<RegimeDiscoveryConfigurationQueryActor> context, IQuery query)
        => ReceiveAsync(context, query, CancellationToken.None);

    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(
        IQueryActorContext<RegimeDiscoveryConfigurationQueryActor> context,
        IQuery query,
        CancellationToken cancellationToken)
    {
        var receive = ResolveMappedQueryHandler(query, _receiveMap);
        await receive(ActorContext, context, query, cancellationToken).ConfigureAwait(false);
    }

    static readonly IReadOnlyDictionary<Type, Func<IRegimeDiscoveryConfigurationQueryContext,
        IQueryActorContext<RegimeDiscoveryConfigurationQueryActor>, IQuery, CancellationToken, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<IRegimeDiscoveryConfigurationQueryContext,
            IQueryActorContext<RegimeDiscoveryConfigurationQueryActor>, IQuery, CancellationToken, ValueTask>>
    {
        [typeof(GetRegimeDiscoveryParameterSetQuery)] = static (services, context, query, cancellationToken) =>
            ((GetRegimeDiscoveryParameterSetQuery)query).ExecuteAsync(services, context, cancellationToken),
        [typeof(ResolveRegimeDiscoveryParameterSetQuery)] = static (services, context, query, cancellationToken) =>
            ((ResolveRegimeDiscoveryParameterSetQuery)query).ExecuteAsync(services, context, cancellationToken)
    };
    /// <inheritdoc />
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap =
        CreateQueryExceptionMap(_receiveMap.Keys);

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<RegimeDiscoveryConfigurationQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);

    static IRegimeDiscoveryConfigurationQueryContext Typed(
        IQueryActorContext<RegimeDiscoveryConfigurationQueryActor> context)
        => context as IRegimeDiscoveryConfigurationQueryContext
           ?? throw new ArgumentException("A typed configuration Query context is required.", nameof(context));
}

using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.Configuration.Strategy;
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
    static readonly Dictionary<string, Func<IActorMessage, IQuery>> _parseMap =
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
        await receive(this, context, query, cancellationToken).ConfigureAwait(false);
    }

    static readonly Dictionary<Type, Func<RegimeDiscoveryConfigurationQueryActor,
        IQueryActorContext<RegimeDiscoveryConfigurationQueryActor>, IQuery, CancellationToken, ValueTask>> _receiveMap = new()
    {
        [typeof(GetRegimeDiscoveryParameterSetQuery)] = static async (actor, context, query, cancellationToken) =>
        {
            var exact = (GetRegimeDiscoveryParameterSetQuery)query;
            var result = (await actor.ActorContext.ConfigurationDb.GetRegimeDiscoveryAsync(
                exact.ParameterSetId, exact.Version, cancellationToken).ConfigureAwait(false))?.ParameterSet;
            await ReplyAsync(context, query, result).ConfigureAwait(false);
        },
        [typeof(ResolveRegimeDiscoveryParameterSetQuery)] = static async (actor, context, query, cancellationToken) =>
        {
            var effective = (ResolveRegimeDiscoveryParameterSetQuery)query;
            var result = (await actor.ActorContext.ConfigurationDb.ResolveEffectiveRegimeDiscoveryAsync(
                effective.EffectiveAtUtc, effective.TargetHorizon, cancellationToken).ConfigureAwait(false))?.ParameterSet;
            await ReplyAsync(context, query, result).ConfigureAwait(false);
        }
    };

    static ValueTask ReplyAsync(
        IQueryActorContext<RegimeDiscoveryConfigurationQueryActor> context,
        IQuery query,
        RegimeDiscoveryParameterSet? result)
    {
        if (result is null)
            throw new KeyNotFoundException("The requested Regime Discovery parameter set was not found.");
        return context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<RegimeDiscoveryParameterSet>(result));
    }

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

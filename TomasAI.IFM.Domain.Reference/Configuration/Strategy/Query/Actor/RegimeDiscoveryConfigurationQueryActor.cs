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
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> Parsers =
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
    {
        if (message.Subject is not { ActorType: ActorType.Query, Name: ActorName } ||
            !Parsers.TryGetValue(message.Subject.Verb, out var parser))
            throw new InvalidOperationException($"Unable to resolve {ActorName} query from {message.Subject}.");
        var query = parser(message);
        context.SetMessageInfo(message.Subject.ThreadId, message.Subject.Verb, new ActorMessageInfo(message, query));
        return query;
    }

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
        RegimeDiscoveryParameterSet? result = query switch
        {
            GetRegimeDiscoveryParameterSetQuery exact =>
                (await ActorContext.ConfigurationDb.GetRegimeDiscoveryAsync(
                    exact.ParameterSetId, exact.Version, cancellationToken).ConfigureAwait(false))?.ParameterSet,
            ResolveRegimeDiscoveryParameterSetQuery effective =>
                (await ActorContext.ConfigurationDb.ResolveEffectiveRegimeDiscoveryAsync(
                    effective.EffectiveAtUtc, effective.TargetHorizon, cancellationToken).ConfigureAwait(false))?.ParameterSet,
            _ => throw new InvalidOperationException($"Unsupported configuration query {query.GetType().Name}.")
        };
        if (result is null)
            throw new KeyNotFoundException("The requested Regime Discovery parameter set was not found.");
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<RegimeDiscoveryParameterSet>(result)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask OnExceptionAsync(
        IQueryActorContext<RegimeDiscoveryConfigurationQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception)
    {
        try
        {
            await context.ReplyAsync(threadId, verb,
                new ServiceFailed<ActorEntityId>(query?.ErrorCode ?? 33100, exception.Message)).ConfigureAwait(false);
        }
        catch (Exception replyException)
        {
            ActorContext.Logger.LogError(replyException, "Unable to return configuration query failure.");
        }
    }

    static IRegimeDiscoveryConfigurationQueryContext Typed(
        IQueryActorContext<RegimeDiscoveryConfigurationQueryActor> context)
        => context as IRegimeDiscoveryConfigurationQueryContext
           ?? throw new ArgumentException("A typed configuration Query context is required.", nameof(context));
}

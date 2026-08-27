using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Queries;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Query.Actor;

/// <summary>Serves side-effect-free Regime Discovery terminal projections from TradeDb.</summary>
public sealed class RegimeDiscoveryQueryActor(
    IQueryActorContext<RegimeDiscoveryQueryActor> actorContext)
    : BaseQueryActor<RegimeDiscoveryQueryActor>(actorContext, Typed(actorContext).Logger)
{
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> Parsers =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
        {
            [GetRegimeDiscoveryQuery.Verb] = message =>
                message.AsQuery<GetRegimeDiscoveryQuery, RegimeDiscoveryReadModel>()!
        };

    /// <summary>Gets the Query actor name.</summary>
    public const string ActorName = GetRegimeDiscoveryQuery.Actor;

    IRegimeDiscoveryQueryContext ActorContext { get; } = Typed(actorContext);

    /// <inheritdoc />
    protected override IQuery ParseMessage(
        IQueryActorContext<RegimeDiscoveryQueryActor> context,
        IActorMessage message)
    {
        if (message.Subject is not { ActorType: ActorType.Query, Name: ActorName } ||
            !Parsers.TryGetValue(message.Subject.Verb, out var parse))
            throw new InvalidOperationException($"Unable to resolve {ActorName} query from message: {message.Subject}");
        var query = parse(message);
        context.SetMessageInfo(message.Subject.ThreadId, message.Subject.Verb, new ActorMessageInfo(message, query));
        return query;
    }

    /// <inheritdoc />
    protected override ValueTask ReceiveAsync(
        IQueryActorContext<RegimeDiscoveryQueryActor> context,
        IQuery query)
        => ReceiveAsync(context, query, CancellationToken.None);

    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(
        IQueryActorContext<RegimeDiscoveryQueryActor> context,
        IQuery query,
        CancellationToken cancellationToken)
    {
        if (query is not GetRegimeDiscoveryQuery get)
            throw new InvalidOperationException($"Unsupported Regime Discovery query: {query.GetType().Name}");
        var result = await ActorContext.DbFactory.TradeDb
            .GetRegimeDiscoveryAsync(get.WorkflowId, cancellationToken).ConfigureAwait(false);
        if (result is null)
            throw new KeyNotFoundException($"Regime Discovery result for workflow {get.WorkflowId} was not found.");
        await context.ReplyAsync(query.Subject.ThreadId, get.Subject.Verb,
            new ServiceResult<RegimeDiscoveryReadModel>(result)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask OnExceptionAsync(
        IQueryActorContext<RegimeDiscoveryQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception)
    {
        try
        {
            await context.ReplyAsync(threadId, verb,
                new ServiceFailed<ActorEntityId>(query?.ErrorCode ?? GetRegimeDiscoveryQuery.ErrorId,
                    exception.Message)).ConfigureAwait(false);
        }
        catch (Exception replyException)
        {
            ActorContext.Logger.LogError(replyException,
                "Failed to return Regime Discovery query error for {ThreadId}", threadId);
        }
    }

    static IRegimeDiscoveryQueryContext Typed(IQueryActorContext<RegimeDiscoveryQueryActor> context)
        => context as IRegimeDiscoveryQueryContext
           ?? throw new ArgumentException(
               $"{nameof(context)} must implement {nameof(IRegimeDiscoveryQueryContext)}.", nameof(context));
}

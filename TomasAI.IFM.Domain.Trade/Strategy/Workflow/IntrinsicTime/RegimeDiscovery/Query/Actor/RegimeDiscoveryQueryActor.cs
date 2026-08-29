using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Queries;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Reference;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.ViewModels;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Query.Actor;

/// <summary>Serves side-effect-free Regime Discovery terminal projections from TradeDb.</summary>
public sealed class RegimeDiscoveryQueryActor(
    IQueryActorContext<RegimeDiscoveryQueryActor> actorContext)
    : BaseQueryActor<RegimeDiscoveryQueryActor>(actorContext, Typed(actorContext).Logger)
{
    static readonly Dictionary<string, Func<IActorMessage, IQuery>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
        {
            [GetRegimeDiscoveryQuery.Verb] = message =>
                message.AsQuery<GetRegimeDiscoveryQuery, RegimeDiscoveryReadModel>()!,
            [GetRegimeDiscoveryDecisionReferenceQuery.Verb] = message =>
                message.AsQuery<GetRegimeDiscoveryDecisionReferenceQuery,
                    RegimeDiscoveryDecisionReferenceDto[]>()!
        };

    /// <summary>Gets the Query actor name.</summary>
    public const string ActorName = GetRegimeDiscoveryQuery.Actor;

    IRegimeDiscoveryQueryContext ActorContext { get; } = Typed(actorContext);

    /// <inheritdoc />
    protected override IQuery ParseMessage(
        IQueryActorContext<RegimeDiscoveryQueryActor> context,
        IActorMessage message)
        => ParseMappedQuery(context, message, _parseMap);

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
        var receive = ResolveMappedQueryHandler(query, _receiveMap);
        await receive(this, context, query, cancellationToken).ConfigureAwait(false);
    }

    static readonly Dictionary<Type, Func<RegimeDiscoveryQueryActor,
        IQueryActorContext<RegimeDiscoveryQueryActor>, IQuery, CancellationToken, ValueTask>> _receiveMap = new()
    {
        [typeof(GetRegimeDiscoveryQuery)] = static async (actor, context, query, cancellationToken) =>
        {
            var get = (GetRegimeDiscoveryQuery)query;
            var result = await actor.ActorContext.DbFactory.TradeDb
                .GetRegimeDiscoveryAsync(get.WorkflowId, cancellationToken).ConfigureAwait(false);
            if (result is null)
                throw new KeyNotFoundException($"Regime Discovery result for workflow {get.WorkflowId} was not found.");
            await context.ReplyAsync(query.Subject.ThreadId, get.Subject.Verb,
                new ServiceResult<RegimeDiscoveryReadModel>(result)).ConfigureAwait(false);
        },
        [typeof(GetRegimeDiscoveryDecisionReferenceQuery)] = static async (_, context, query, _) =>
        {
            var get = (GetRegimeDiscoveryDecisionReferenceQuery)query;
            var result = new RegimeDiscoveryDecisionReferenceGenerator().Generate();
            await context.ReplyAsync(query.Subject.ThreadId, get.Subject.Verb,
                new ServiceResult<RegimeDiscoveryDecisionReferenceDto[]>(result)).ConfigureAwait(false);
        }
    };

    /// <inheritdoc />
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap =
        CreateQueryExceptionMap(_receiveMap.Keys);

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<RegimeDiscoveryQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);

    static IRegimeDiscoveryQueryContext Typed(IQueryActorContext<RegimeDiscoveryQueryActor> context)
        => context as IRegimeDiscoveryQueryContext
           ?? throw new ArgumentException(
               $"{nameof(context)} must implement {nameof(IRegimeDiscoveryQueryContext)}.", nameof(context));
}

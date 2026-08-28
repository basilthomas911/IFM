using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventModelActor.Templates.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor.Templates;

/// <summary>
/// Template for a query actor. Add query parsers and handlers to the empty maps.
/// </summary>
public class QueryActorTemplate(
    IQueryActorContext<QueryActorTemplate> actorContext)
    : BaseQueryActor<QueryActorTemplate>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the typed context owned by this actor.</summary>
    protected IQueryActorTemplateContext ActorContext { get; } =
        IsArgumentNull.Set(actorContext as IQueryActorTemplateContext, nameof(actorContext))!;

    /// <summary>Gets the actor mailbox name.</summary>
    public const string ActorName = "QueryActorTemplate";

    static readonly Dictionary<string, Func<IActorMessage, IQuery>> _parseMap = [];

    static readonly Dictionary<Type, Func<
        IQuery,
        IDbContextFactory,
        IQueryActorContext<QueryActorTemplate>,
        CancellationToken,
        ValueTask>> _receiveMap = [];

    protected override IQuery ParseMessage(IQueryActorContext<QueryActorTemplate> context, IActorMessage message)
        => ParseMappedQuery(context, message, _parseMap);

    protected override ValueTask ReceiveAsync(IQueryActorContext<QueryActorTemplate> context, IQuery query)
        => ReceiveAsync(context, query, CancellationToken.None);

    protected override async ValueTask ReceiveAsync(
        IQueryActorContext<QueryActorTemplate> context,
        IQuery query,
        CancellationToken cancellationToken)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(query);
        var handler = ResolveMappedQueryHandler(query, _receiveMap);
        await handler(query, ActorContext.DbFactory, context, cancellationToken).ConfigureAwait(false);
    }

    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap =
        CreateQueryExceptionMap(_receiveMap.Keys);

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<QueryActorTemplate> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);
}

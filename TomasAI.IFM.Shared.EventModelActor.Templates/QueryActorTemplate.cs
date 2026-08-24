using Microsoft.Extensions.Logging;
using NATS.Client.Core;
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

    static readonly Dictionary<string, Func<
        IQuery,
        IDbContextFactory,
        IQueryActorContext<QueryActorTemplate>,
        ValueTask>> _receiveMap = [];

    protected override IQuery ParseMessage(IQueryActorContext<QueryActorTemplate> context, IActorMessage message)
    {
        IsArgumentNull.Check(context);
        var subject = message.Subject;
        if (subject is not { ActorType: ActorType.Query, Name: ActorName }
            || !_parseMap.TryGetValue(subject.Verb, out var parser))
            throw new InvalidOperationException(
                $"Unable to resolve {ActorName} query from message: {message.Subject}");

        var query = parser.Invoke(message);
        IsArgumentNull.Check(query);
        context.SetMessageInfo(
            subject.ThreadId,
            subject.Verb,
            new ActorMessageInfo(message, query));
        return query;
    }

    protected override async ValueTask ReceiveAsync(IQueryActorContext<QueryActorTemplate> context, IQuery query)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(query);

        if (!_receiveMap.TryGetValue(query.GetType().Name, out var handler))
            throw new InvalidOperationException(
                $"Unable to process {ActorName} query: {query.GetType().Name}");

        await handler.Invoke(query, actorContext.DbFactory, context);
    }

    protected override async ValueTask OnExceptionAsync(
        IQueryActorContext<QueryActorTemplate> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(query);
        IsArgumentNull.Check(verb);
        IsArgumentNull.Check(exception.Message);

        try
        {
            await context.ReplyAsync(
                threadId,
                verb,
                new ServiceFailed<ActorEntityId>(9999, exception.Message));
        }
        catch (Exception innerException)
        {
            actorContext.Logger.LogError(
                innerException,
                "Error handling exception in {ActorName} for thread {ThreadId}: {ErrorMessage}",
                ActorName,
                threadId,
                innerException.Message);
        }
    }
}

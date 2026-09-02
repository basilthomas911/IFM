using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Domain.Application.Shared.Queries;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Application.Actor.Query.Actor;

/// <summary>NATS query boundary for the latest Application lifecycle status.</summary>
public sealed class ApplicationQueryActor(IQueryActorContext<ApplicationQueryActor> actorContext)
    : BaseQueryActor<ApplicationQueryActor>(actorContext, Require(actorContext).Logger)
{
    public const string ActorName = GetApplicationStartupStatusQuery.Actor;

    static IApplicationQueryContext Require(IQueryActorContext<ApplicationQueryActor> context) =>
        context as IApplicationQueryContext
        ?? throw new ArgumentException("ApplicationQueryActor requires its typed context.", nameof(context));

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> ParseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
        {
            [GetApplicationStartupStatusQuery.Verb] = static message =>
                message.AsQuery<GetApplicationStartupStatusQuery, ApplicationStartupStatus>()!
        };

    protected override IQuery ParseMessage(
        IQueryActorContext<ApplicationQueryActor> context,
        IActorMessage message) => ParseMappedQuery(context, message, ParseMap);

    protected override ValueTask ReceiveAsync(
        IQueryActorContext<ApplicationQueryActor> context,
        IQuery query) => ReceiveAsync(context, query, CancellationToken.None);

    protected override async ValueTask ReceiveAsync(
        IQueryActorContext<ApplicationQueryActor> context,
        IQuery query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (query is not GetApplicationStartupStatusQuery statusQuery)
            throw new InvalidOperationException($"Unsupported Application query {query.GetType().Name}.");
        await context.ReplyAsync(
            statusQuery.Subject.ThreadId,
            GetApplicationStartupStatusQuery.Verb,
            new ServiceOk<ApplicationStartupStatus>(Require(context).StatusStore.Current))
            .ConfigureAwait(false);
    }

    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> ExceptionMap =
        CreateQueryExceptionMap([typeof(GetApplicationStartupStatusQuery)]);

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<ApplicationQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception) =>
        ExceptionMappedQueryAsync(context, threadId, query, verb, exception, ExceptionMap);
}

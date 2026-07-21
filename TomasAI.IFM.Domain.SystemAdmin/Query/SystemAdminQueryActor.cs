using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.SystemAdmin.Queries;
using TomasAI.IFM.Shared.SystemAdmin.ViewModels;
using TomasAI.IFM.Domain.SystemAdmin.Actor.Command.State;

namespace TomasAI.IFM.Domain.SystemAdmin.Actor.Query;

/// <summary>
/// Represents an actor responsible for managing system admin queries within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="SystemAdminQueryActor"/> is a specialized query actor designed to handle operations
/// related to system administration lookups such as retrieving database names.
/// It processes queries, validates them, and manages the actor's state.</remarks>
/// <param name="logger">The logger used to record diagnostic and operational information for the actor.</param>
public class SystemAdminQueryActor(
    ILogger<SystemAdminQueryActor> logger)
    : BaseQueryActor<SystemAdminQueryActor>(logger, new ActorMailboxId(ActorType.Query, ActorName))
{
    public const string ActorName = "DatabaseNamesQuery";

    /// <summary>
    /// Parses the specified actor message and extracts the thread identifier associated with the message.
    /// </summary>
    /// <param name="context">The query actor context used to store message-related information.</param>
    /// <param name="message">The actor message to parse.</param>
    /// <returns>The parsed query instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message subject cannot be resolved to a valid query for the actor.</exception>
    protected override IQuery ParseMessage(IQueryActorContext context, NatsMsg<byte[]> message)
    {
        IsArgumentNull.Check(context);
        var msgSubject = message.Subject.ToSubject();
        if (msgSubject is not { ActorType: ActorType.Query, Name: ActorName }
            || !_parseMap.TryGetValue(msgSubject.Verb, out var messageParser))
            throw new InvalidOperationException($"Unable to resolve {ActorName} query from message: {message.Subject}");
        var query = messageParser.Invoke(message);
        IsArgumentNull.Check(query);
        context.SetMessageInfo(
            msgSubject.ThreadId,
            verb: msgSubject.Verb,
            new ActorMessageInfo(message, query));
        return query;
    }

    /// <summary>
    /// Provides a mapping from query verb strings to delegate functions that parse a NATS message into the
    /// corresponding query instance.
    /// </summary>
    static readonly Dictionary<string, Func<NatsMsg<byte[]>, IQuery>> _parseMap = new()
    {
        [GetDatabaseNamesQuery.Verb] = msg => msg.AsQuery<GetDatabaseNamesQuery, DatabaseNamesReadModel>()!
    };

    /// <summary>
    /// Handles incoming queries asynchronously and processes them based on their type.
    /// </summary>
    /// <param name="context">The context in which the query is being processed.</param>
    /// <param name="state">The current state of the actor, which must be of type <see cref="SystemAdminQueryState"/>.</param>
    /// <param name="query">The query to process.</param>
    /// <returns>A task that represents the asynchronous query processing operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the query type is not supported.</exception>
    protected override async ValueTask ReceiveAsync(IQueryActorContext context, IActorState state, IQuery query)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(query);
        var systemAdminQueryState = IsArgumentNull.Set(state as SystemAdminQueryState)!;
        var qryName = query.GetType().Name;
        if (!_receiveMap.TryGetValue(qryName, out var receiveFunc))
            throw new InvalidOperationException($"Unable to process {ActorName} query: {qryName}");
        await receiveFunc.Invoke(context, systemAdminQueryState, query);
    }

    /// <summary>
    /// Provides a mapping from query type names to delegate functions that execute the corresponding system admin query
    /// logic against the query state.
    /// </summary>
    static readonly Dictionary<string, Func<IQueryActorContext, SystemAdminQueryState, IQuery, ValueTask>> _receiveMap = new()
    {
        [typeof(GetDatabaseNamesQuery).Name] = (ctx, state, q) =>
        {
            var query = (q as GetDatabaseNamesQuery)!;
            var result = SystemAdminQueryState.GetDatabaseNames();
            return ctx.ReplyAsync(query.Subject.ThreadId, GetDatabaseNamesQuery.Verb, new ServiceOk<DatabaseNamesReadModel>(result));
        }
    };

    /// <summary>
    /// Handles exceptions that occur during the processing of a query in the actor context.
    /// </summary>
    /// <param name="context">The context in which the query is being processed.</param>
    /// <param name="threadId">The identifier of the actor thread where the exception occurred.</param>
    /// <param name="query">The query that caused the exception.</param>
    /// <param name="verb">The verb representing the type of query being processed.</param>
    /// <param name="ex">The exception that was thrown during query processing.</param>
    protected override async ValueTask OnExceptionAsync(IQueryActorContext context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(query);
        IsArgumentNull.Check(verb);
        IsArgumentNull.Check(ex?.Message!);

        try
        {
            var serviceResultTask = default(ValueTask) switch
            {
                _ when query is GetDatabaseNamesQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<DatabaseNamesReadModel>(query.ErrorCode, ex!.Message)),
                _ => context.ReplyAsync(threadId, verb, new ServiceFailed<ActorEntityId>(9999, ex!.Message))
            };
            await serviceResultTask;
        }
        catch (Exception innerEx)
        {
            logger.LogError(innerEx, "Error handling exception in {ActorName} for thread {ThreadId}: {ErrorMessage}", ActorName, threadId, innerEx.Message);
        }
    }

    /// <summary>
    /// Asynchronously loads the current state for the actor associated with the specified query context and thread.
    /// </summary>
    /// <param name="context">The query actor context that provides access to the actor's container and related services.</param>
    /// <param name="threadId">The identifier of the actor thread for which the state is being loaded.</param>
    /// <param name="query">The query that triggered the state load operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the loaded actor state.</returns>
    protected override async ValueTask<IActorState> OnLoadStateAsync(IQueryActorContext context, ActorThreadId threadId, IQuery query)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(query);

        var actorState = context.Container.Resolve<IQueryActorState<SystemAdminQueryState>>();
        actorState.Id = threadId;
        return await ValueTask.FromResult(actorState);
    }
}

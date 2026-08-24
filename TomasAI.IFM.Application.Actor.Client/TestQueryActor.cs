using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Application.Actor.Client.Extensions;

namespace TomasAI.IFM.Application.Actor.Client;

/// <summary>
/// Represents a query actor for handling and processing TestQuery messages within the actor system.
/// </summary>
/// <remarks>This actor specializes BaseQueryActor to support the TestQuery message type. It manages message
/// parsing, state loading, and error handling for queries identified by the actor name "Test". The actor is intended
/// for use within an actor-based messaging infrastructure and is not thread-safe outside the actor system's
/// context.</remarks>
/// <param name="actorContext">The typed query context resolved through open-generic registration.</param>
public class TestQueryActor(IQueryActorContext<TestQueryActor> actorContext)
    : BaseQueryActor<TestQueryActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the typed context owned by this actor.</summary>
    protected ITestQueryContext ActorContext =>
        IsArgumentNull.Set(Context as ITestQueryContext, nameof(Context))!;

    /// <summary>Gets the actor mailbox name.</summary>
    public const string ActorName = "Test";
    protected override IQuery ParseMessage(IQueryActorContext<TestQueryActor> context, IActorMessage message)
    {
        var msgSubject = message.Subject;
        IQuery query = default(IQuery) switch
        {
            _ when msgSubject.Is(ActorType.Query, TestQuery.Actor, TestQuery.Verb) =>
                message.AsQuery<TestQuery, string>()!,
            _ => throw new InvalidOperationException($"Unable to resolve {ActorName} command from message: {message.Subject}")
        };
        IsArgumentNull.Check(query);
        context.SetMessageInfo(
            msgSubject.ThreadId,
            msgSubject.Verb,
            new ActorMessageInfo(message, query));
        return query;

    }

    protected override async ValueTask ReceiveAsync(IQueryActorContext<TestQueryActor> context, IQuery query)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(query);
        var dispatchContext = context;
        var msgInfo = IsArgumentNull.Set(dispatchContext.GetMessageInfo(query.Subject.ThreadId, query.Subject.Verb)).Value;
        var actorMessage = IsArgumentNull.Set(msgInfo.Message);
        await actorMessage.ReplyAsync(new ServiceResult<string>( "The rain in Spain stays mainly in the plain."));
    }

    protected override async ValueTask OnExceptionAsync(IQueryActorContext<TestQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
    {
        try
        {
            var msgInfo = IsArgumentNull.Set(context.GetMessageInfo(threadId, verb)).Value;
            var serviceResultTask = default(ValueTask) switch
            {
                _ when msgInfo.Query is TestQuery
                    => IsArgumentNull.Set(msgInfo.Message).ReplyAsync(
                        new ServiceResult<string>(msgInfo.Query.ErrorCode, ex.Message)),
                _ => throw new InvalidOperationException($"Unable to process {ActorName} query: {msgInfo.Query.GetType().Name}")
            };
            await serviceResultTask;
        }
        catch (Exception innerEx)
        {
            Context.Logger.LogError(innerEx, "Error handling exception in {ActorName} for thread {ThreadId}: {ErrorMessage}", ActorName, threadId, innerEx.Message);
        }
    }
}

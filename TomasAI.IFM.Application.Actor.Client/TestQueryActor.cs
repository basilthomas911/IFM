using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Actor;

/// <summary>
/// Represents a query actor for handling and processing TestQuery messages within the actor system.
/// </summary>
/// <remarks>This actor specializes BaseQueryActor to support the TestQuery message type. It manages message
/// parsing, state loading, and error handling for queries identified by the actor name "Test". The actor is intended
/// for use within an actor-based messaging infrastructure and is not thread-safe outside the actor system's
/// context.</remarks>
/// <param name="logger">The logger used to record diagnostic and operational information for the actor. Cannot be null.</param>
public class TestQueryActor(ILogger<TestQueryActor> logger)
    : BaseQueryActor<TestQueryActor>(logger, new ActorMailboxId(ActorType.Query, ActorName))
{
    const string ActorName = "Test";
    protected override IQuery ParseMessage(IQueryActorContext context, NatsMsg<byte[]> message )
    {
        var msgSubject = message.Subject.ToSubject();
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

    protected override async ValueTask ReceiveAsync(IQueryActorContext context,  IActorState state, IQuery query)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(query);
        var msgInfo = IsArgumentNull.Set(context.GetMessageInfo(state.Id, query.Subject.Verb)).Value;
        var qry = msgInfo.ActorMessage.AsQuery<TestQuery, string>();
        await msgInfo.ActorMessage.ReplyAsync(new ServiceResult<string>( "The rain in Spain stays mainly in the plain."));
    }

    protected override async ValueTask<IActorState> OnLoadStateAsync(IQueryActorContext context, ActorThreadId threadI, IQuery query)
        =>  await ValueTask.FromResult(new TestQueryState());

    protected override async ValueTask OnExceptionAsync(IQueryActorContext context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
    {
        try
        {
            var msgInfo = IsArgumentNull.Set(context.GetMessageInfo(threadId, verb)).Value;
            var serviceResultTask = default(ValueTask) switch
            {
                _ when msgInfo.Query is TestQuery
                    => msgInfo.ActorMessage.ReplyAsync(new ServiceResult<string>(msgInfo.Query.ErrorCode, ex.Message)),
                _ => throw new InvalidOperationException($"Unable to process {ActorName} query: {msgInfo.Query.GetType().Name}")
            };
            await serviceResultTask;
        }
        catch (Exception innerEx)
        {
            logger.LogError(innerEx, "Error handling exception in {ActorName} for thread {ThreadId}: {ErrorMessage}", ActorName, threadId, innerEx.Message);
        }
    }
}

using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Represents a message that can be sent to or received from an actor,  with the ability to be interpreted as a
/// command, event, or query.
/// </summary>
/// <remarks>This interface provides methods to convert the message into specific types  of actor messages, such
/// as commands, events, or queries, and to send replies  asynchronously. It also exposes the subject of the message,
/// which identifies  the actor or entity associated with the message.</remarks>
public interface IActorMessage : IDisposable
{
    /// <summary>Original NATS subject before realtime fanout changes the mailbox destination.</summary>
    ActorSubject SourceSubject => Subject;

    /// <summary>Supervised worker dataset carried outside the business-event payload.</summary>
    string? SourceDataset => null;

    /// <summary>Supervised worker generation carried outside the business-event payload.</summary>
    Guid SourceGenerationId => Guid.Empty;
    /// <summary>
    /// Gets whether the transport supplied a reply subject that remains valid
    /// after the request payload is released.
    /// </summary>
    bool CanReply => false;

    /// <summary>Optional transport context, independent of the payload lifetime.</summary>
    System.Diagnostics.ActivityContext TraceContext => default;

    /// <summary>Serialized payload bytes charged to aggregate admission accounting.</summary>
    int AdmissionSizeBytes => 0;

    TCommand? AsCommand<TCommand>() where TCommand : class, ICommand;
    TEvent? AsEvent<TEvent>() where TEvent : class, IEvent;
    TQuery? AsQuery<TQuery, TResult>()
        where TQuery : class, IQuery<TResult>
        where TResult : class;
    ValueTask ReplyAsync<TResult>(TResult result) where TResult : class;

    /// <summary>
    /// Releases owned payload storage after a typed message has been materialized.
    /// Implementations must make this operation idempotent.
    /// </summary>
    void ReleasePayload();


    ActorSubject Subject { get; }

    // used mainly for send error replies back when sending pub/sub messages...
    ActorSubject ReplySubject { get; set; }
}



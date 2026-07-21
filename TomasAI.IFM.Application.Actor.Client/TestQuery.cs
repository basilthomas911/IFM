using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Actor.Client;

/// <summary>
/// Represents a query for retrieving test data using the specified actor, entity, and message parameters.
/// </summary>
/// <remarks>This type is typically used to request test-related information within an actor-based system. It
/// encapsulates the necessary identifiers and parameters required to perform the query. The class is designed for use
/// with serialization frameworks such as MessagePack and may be used as part of a messaging or CQRS
/// infrastructure.</remarks>
[MessagePackObject(false)]
public class TestQuery : IQuery<string>
{
    [IgnoreMember] public const string Actor = "Test";
    [IgnoreMember] public const string Verb = "GetTest";
    [IgnoreMember] public const int ErrorId = 1002;

    [Key(0)] public ActorSubject Subject { get; set; } = default!;
    [Key(1)] public IActorEntityId EntityId { get; set; } = default!;
    [IgnoreMember] public int ErrorCode { get; set; }
    [IgnoreMember] public string? QueryParams { get; set; } 

    [Key(2)]
    public string MsgString { get; init; } = string.Empty;

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public TestQuery() { }

    /// <summary>
    /// Create a test query with the specified message.
    /// </summary>
    public TestQuery(string msgString)
    {
        MsgString = msgString ?? string.Empty;
        EntityId = new TestId(DateOnly.FromDateTime(DateTime.UtcNow));
        QueryParams = string.Empty;
        ErrorCode = ErrorId;
    }

    [SerializationConstructor]
    public TestQuery(ActorSubject subject, IActorEntityId entityId, string? queryParams, int errorCode, string msgString)
    {
        Subject = subject;
        EntityId = entityId;
        QueryParams = queryParams;
        ErrorCode = errorCode;
        MsgString = msgString ?? string.Empty;
    }
}

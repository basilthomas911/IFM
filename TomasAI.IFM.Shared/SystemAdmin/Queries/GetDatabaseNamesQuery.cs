using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.SystemAdmin.ViewModels;
using TomasAI.IFM.Shared.SystemAdmin.QueryParameters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.SystemAdmin.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the names of available databases.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetDatabaseNamesQuery : IQuery<DatabaseNamesReadModel>
{
    [IgnoreMember] public const string Actor = "DatabaseNamesQuery";
    [IgnoreMember] public const string Verb = "GetDatabaseNames";
    [IgnoreMember] public const int ErrorId = 1006;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public GetDatabaseNamesQuery()
    {
        EntityId = new GetDatabaseNamesParameter();
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetDatabaseNamesQuery(ActorSubject subject, IActorEntityId entityId)
    {
        Subject = subject;
        EntityId = new GetDatabaseNamesParameter();
        ErrorCode = ErrorId;
    }
}


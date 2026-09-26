using System.Text.Json;
using System.Text.Json.Serialization;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

// Explicit JSON transport keeps JsonElement settings out of MessagePack's dynamic object formatter.
[MessagePackObject(AllowPrivate = true)]
public sealed class StrategyCatalogQuery : IQuery<string>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public StrategyCatalogQuery() { }

    /// <summary>Rehydrates the published query fields in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="requestJson">The RequestJson field.</param>
    [SerializationConstructor]
    public StrategyCatalogQuery(ActorSubject subject, IActorEntityId entityId, string requestJson)
    {
        Subject = subject;
        EntityId = entityId;
        RequestJson = requestJson;
    }
    [IgnoreMember] public const string Actor = "ReferenceQuery";
    [IgnoreMember] public const string Verb = "StrategyCatalog";
    [IgnoreMember] public const int ErrorId = 1063;
    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; } = ActorEntityId.Default;
    [Key(2)] public string RequestJson { get; set; } = "";
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => null;
}

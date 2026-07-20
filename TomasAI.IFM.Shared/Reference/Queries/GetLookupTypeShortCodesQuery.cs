using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.Reference.QueryParameters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Reference.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve lookup type short codes by lookup type name.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern used by other queries:
/// - Annotated with <see cref="MessagePackObjectAttribute"/>.
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers and a full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public class GetLookupTypeShortCodesQuery : IQuery<LookupTypeShortCodeReadModel[]>
{
    [IgnoreMember] public const string Actor = "LookupTypeQuery";
    [IgnoreMember] public const string Verb = "GetLookupTypeShortCodes";
    [IgnoreMember] public const int ErrorId = 1039;

    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; }
    [Key(2)] public string LookupTypeName { get; set; }
    [IgnoreMember] public int ErrorCode { get; set; }
    [IgnoreMember] public string QueryParams { get; set; }

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public GetLookupTypeShortCodesQuery()
    {
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// Constructor to create query with lookup type name.
    /// </summary>
    /// <param name="lookupTypeName">The name of the lookup type.</param>
    public GetLookupTypeShortCodesQuery(string lookupTypeName)
    {
        LookupTypeName = lookupTypeName ?? string.Empty;
        EntityId = new GetLookupTypeShortCodesParameter(lookupTypeName!);
        ErrorCode = ErrorId;
        QueryParams = $"lookupTypeName={LookupTypeName}";
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// Parameter order and types must match the keys (0..2).
    /// </summary>
    [SerializationConstructor]
    public GetLookupTypeShortCodesQuery(
        ActorSubject subject,       // Key(0)
        IActorEntityId entityId,    // Key(1)
        string lookupTypeName)      // Key(2)
    {
        Subject = subject;
        EntityId = entityId;
        LookupTypeName = lookupTypeName ?? string.Empty;
        ErrorCode = ErrorId;
    }
}
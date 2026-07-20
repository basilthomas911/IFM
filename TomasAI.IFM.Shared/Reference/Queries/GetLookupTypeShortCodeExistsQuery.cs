using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Reference.QueryParameters;

namespace TomasAI.IFM.Shared.Reference.Queries;

/// <summary>
/// Represents a query to determine whether a specific short code exists for a given lookup type.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern used by other queries:
/// - Annotated with <see cref="MessagePackObjectAttribute"/>.
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers and a full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
/// This query is used to check the existence of a short code within a specified lookup type. It returns
/// a boolean value indicating whether the short code exists.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public class GetLookupTypeShortCodeExistsQuery : IQuery<ScalarReadModel<bool>>
{
    [IgnoreMember] public const string Actor = "LookupTypeQuery";
    [IgnoreMember] public const string Verb = "GetLookupTypeShortCodeExists";
    [IgnoreMember] public const int ErrorId = 1032;

    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; }
    [Key(2)] public string LookupTypeName { get; set; }
    [Key(3)] public string ShortCode { get; set; }
    [IgnoreMember] public int ErrorCode { get; set; }
    [IgnoreMember] public string? QueryParams { get; set; }

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public GetLookupTypeShortCodeExistsQuery()
    {
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// Constructor to create query with lookup type name and short code.
    /// </summary>
    /// <param name="lookupTypeName">The name of the lookup type to search within. Cannot be null or empty.</param>
    /// <param name="shortCode">The short code to check for existence. Cannot be null or empty.</param>
    public GetLookupTypeShortCodeExistsQuery(string lookupTypeName, string shortCode)
    {
        LookupTypeName = lookupTypeName ?? string.Empty;
        ShortCode = shortCode ?? string.Empty;
        EntityId = new GetLookupTypeShortCodeExistsParameter(lookupTypeName!, shortCode!);
        ErrorCode = ErrorId;
        QueryParams = $"lookupTypeName={LookupTypeName}&shortCode={ShortCode}";
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// Parameter order and types must match the keys (0..3).
    /// </summary>
    [SerializationConstructor]
    public GetLookupTypeShortCodeExistsQuery(
        ActorSubject subject,       // Key(0)
        IActorEntityId entityId,    // Key(1)
        string lookupTypeName,      // Key(2)
        string shortCode)           // Key(3)
    {
        Subject = subject;
        EntityId = entityId;
        LookupTypeName = lookupTypeName ?? string.Empty;
        ShortCode = shortCode ?? string.Empty;
        ErrorCode = ErrorId;
        QueryParams = $"lookupTypeName={LookupTypeName}&shortCode={ShortCode}";
    }
}

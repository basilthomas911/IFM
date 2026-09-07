using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.Lookups;

public static class LookupDefinitionGroups
{
    public const string AssetTypes = "AssetTypes";
    public const string Directions = "Directions";
    public const string MarketConditions = "MarketConditions";
}

[MessagePackObject]
public sealed record LookupDefinitionReadModel(
    [property: Key(0)] int Id,
    [property: Key(1)] string GroupName,
    [property: Key(2)] string InternalValue,
    [property: Key(3)] string DisplayName,
    [property: Key(4)] string Description,
    [property: Key(5)] int DisplayOrder,
    [property: Key(6)] bool IsEnabled,
    [property: Key(7)] DateTime CreatedUtc,
    [property: Key(8)] DateTime UpdatedUtc);

[MessagePackObject]
public sealed class GetLookupDefinitionsQuery : IQuery<LookupDefinitionReadModel[]>
{
    [IgnoreMember] public const string Actor = "ReferenceQuery";
    [IgnoreMember] public const string Verb = "GetLookupDefinitions";
    [IgnoreMember] public const int ErrorId = 1064;
    [IgnoreMember] public const string Uri = "/api/reference/lookup-definitions/query";
    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; } = ActorEntityId.Default;
    [Key(2)] public string GroupName { get; set; } = "";
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => GroupName;
}

public sealed record GetLookupDefinitionsParameter(string GroupName) : IQueryParameter
{
    public string? QueryParams => null;
}

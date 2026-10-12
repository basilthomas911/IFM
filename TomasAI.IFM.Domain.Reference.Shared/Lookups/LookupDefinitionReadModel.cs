using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.Lookups;

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

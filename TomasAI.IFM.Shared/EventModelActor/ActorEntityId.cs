using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Represents the unique identifier of an actor entity, which can be initialized with a single string or an array of
/// strings.
/// </summary>
/// <remarks>This is a zero-allocation value type. If the identifier is not provided or is empty,
/// it defaults to the value "none".</remarks>
[MessagePackObject]
public readonly record struct ActorEntityId : IActorEntityId
{
    [Key(0)]
    public readonly string Value;

    public static readonly ActorEntityId Default = new(string.Empty);

    public ActorEntityId()
        => Value = "none";

    public ActorEntityId(string? entityId)
        => Value = string.IsNullOrEmpty(entityId) ? "none" : entityId;

    public ActorEntityId(string[] entityId)
        => Value = entityId is null || entityId.Length == 0 ? "none" : string.Join(",", entityId);

    public string Format()
        => Value ?? "none";
}


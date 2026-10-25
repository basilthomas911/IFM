using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;

/// <summary>Identifies one actor-owned task schedule or its catalog/run aggregate route.</summary>
[MessagePackObject]
public readonly record struct ScheduledTaskId([property: Key(0)] Guid Value) : IActorEntityId
{
    [IgnoreMember] public bool IsValid => Value != Guid.Empty;
    /// <summary>Formats the stable actor route identity.</summary>
    public string Format() => Value.ToString("N");
}

using MessagePack;

namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>
/// Creates one RunId-owned startup stream with its immutable assignments,
/// exact parameter versions and startup plan. Reports and release follow it.
/// </summary>
[MessagePackObject]
public sealed record ParameterStartupCreatedEvent : ParameterStartupChangedEvent
{
    public new const string Actor = "ParameterStartup";
    public new const string Verb = "ParameterStartupCreated";

    /// <summary>Identifies a creation checkpoint imported from the historical registry.</summary>
    [Key(15)]
    public bool ImportedFromRegistry { get; init; }

    /// <summary>Preserves a historical release so a repeated apply cannot reactivate this run.</summary>
    [Key(16)]
    public bool ReleasedAtCreation { get; init; }

    [IgnoreMember]
    public override string EventName => nameof(ParameterStartupCreatedEvent);
}

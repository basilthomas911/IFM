namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Bounds opt-in resident event-sourced command state.</summary>
public sealed class InMemoryEventSourceActorOptions
{
    public const string SectionName = "InMemoryEventSourceActor";

    public bool OptionTradeLegDataEnabled { get; set; } = true;
    public int MaximumResidentStreams { get; set; } = 4096;
    public int MaximumCommandsPerWindow { get; set; } = 64;

    public InMemoryEventSourceActorOptions Validate()
    {
        if (MaximumResidentStreams <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaximumResidentStreams));
        if (MaximumCommandsPerWindow <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaximumCommandsPerWindow));
        return this;
    }
}

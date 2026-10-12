namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Bounds opt-in resident event-sourced command state.</summary>
public sealed class InMemoryEventSourceActorOptions
{
    public const string SectionName = "InMemoryEventSourceActor";

    public bool OptionTradeLegDataEnabled { get; set; } = true;
    public int MaximumResidentStreams { get; set; } = 4096;
    public int MaximumCommandsPerWindow { get; set; } = 64;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "This parameterless configuration validation deliberately identifies the invalid bound configuration property in the exception.")]
    public InMemoryEventSourceActorOptions Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaximumResidentStreams);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaximumCommandsPerWindow);
        return this;
    }
}

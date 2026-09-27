namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Controls bounded actor-runtime startup orchestration.</summary>
public sealed class ActorRuntimeStartupOptions
{
    public const string SectionName = "ActorRuntime:Startup";

    /// <summary>Maximum number of actors allowed to initialize concurrently.</summary>
    public int MaximumConcurrency { get; set; } = 8;

    public ActorRuntimeStartupOptions Validate()
    {
        if (MaximumConcurrency is < 1 or > 64)
            throw new InvalidOperationException(
                $"{nameof(MaximumConcurrency)} must be between 1 and 64.");
        return this;
    }
}

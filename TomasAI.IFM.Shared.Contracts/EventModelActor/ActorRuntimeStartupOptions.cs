namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Controls bounded actor-runtime lifecycle orchestration.</summary>
public sealed class ActorRuntimeStartupOptions
{
    public const string SectionName = "ActorRuntime:Startup";

    /// <summary>Maximum number of actors allowed to initialize concurrently.</summary>
    public int MaximumConcurrency { get; set; } = 8;

    /// <summary>Maximum time allowed for one actor to stop before host recovery is required.</summary>
    public TimeSpan ActorShutdownTimeout { get; set; } = TimeSpan.FromMinutes(2);

    public ActorRuntimeStartupOptions Validate()
    {
        if (MaximumConcurrency is < 1 or > 64)
            throw new InvalidOperationException(
                $"{nameof(MaximumConcurrency)} must be between 1 and 64.");
        if (ActorShutdownTimeout <= TimeSpan.Zero || ActorShutdownTimeout > TimeSpan.FromMinutes(10))
            throw new InvalidOperationException(
                $"{nameof(ActorShutdownTimeout)} must be greater than zero and no more than ten minutes.");
        return this;
    }
}

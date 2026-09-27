namespace TomasAI.IFM.Application.Api.Server;

/// <summary>Controls bounded process-startup work and deadlines.</summary>
public sealed record StartupOrchestrationOptions
{
    public const string SectionName = "StartupOrchestration";

    public TimeSpan ActorStartupTimeout { get; init; } = TimeSpan.FromMinutes(2);
    public int MaximumSchemaInitializationConcurrency { get; init; } = 4;

    public StartupOrchestrationOptions Validate()
    {
        if (ActorStartupTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException(
                $"{nameof(ActorStartupTimeout)} must be greater than zero.");
        if (MaximumSchemaInitializationConcurrency is < 1 or > 16)
            throw new InvalidOperationException(
                $"{nameof(MaximumSchemaInitializationConcurrency)} must be between 1 and 16.");
        return this;
    }
}

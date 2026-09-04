namespace TomasAI.IFM.Application.Api.Server;

public sealed record ApplicationStartupOptions
{
    public const string SectionName = "ApplicationStartup";
    public bool AutoStartAfterBootstrap { get; init; }
    public TimeSpan BootstrapTimeout { get; init; } = TimeSpan.FromMinutes(2);
    public TimeSpan ParticipantTimeout { get; init; } = TimeSpan.FromMinutes(2);
    public TimeSpan HandoffObservationTimeout { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan HandoffRetryDelay { get; init; } = TimeSpan.FromSeconds(1);
    public int HandoffMaximumAttempts { get; init; } = 3;

    public ApplicationStartupOptions Validate()
    {
        if (BootstrapTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(BootstrapTimeout));
        if (ParticipantTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ParticipantTimeout));
        if (HandoffObservationTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(HandoffObservationTimeout));
        if (HandoffRetryDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(HandoffRetryDelay));
        if (HandoffMaximumAttempts <= 0)
            throw new ArgumentOutOfRangeException(nameof(HandoffMaximumAttempts));
        return this;
    }
}

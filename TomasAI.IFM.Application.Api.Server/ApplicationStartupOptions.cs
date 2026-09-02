namespace TomasAI.IFM.Application.Api.Server;

public sealed record ApplicationStartupOptions
{
    public const string SectionName = "ApplicationStartup";
    public bool AutoStartAfterBootstrap { get; init; }
    public TimeSpan BootstrapTimeout { get; init; } = TimeSpan.FromMinutes(2);
    public TimeSpan ParticipantTimeout { get; init; } = TimeSpan.FromMinutes(2);

    public ApplicationStartupOptions Validate()
    {
        if (BootstrapTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(BootstrapTimeout));
        if (ParticipantTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ParticipantTimeout));
        return this;
    }
}

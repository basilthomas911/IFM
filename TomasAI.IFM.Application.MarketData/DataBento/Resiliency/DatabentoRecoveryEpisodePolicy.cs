namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public sealed record DatabentoRecoveryEpisodePolicy
{
    public TimeSpan EpisodeTimeout { get; init; } = TimeSpan.FromMinutes(5);
    public int MaximumContributingReasons { get; init; } = 16;

    public DatabentoRecoveryEpisodePolicy Validate()
    {
        if (EpisodeTimeout <= TimeSpan.Zero || EpisodeTimeout > TimeSpan.FromHours(1)
            || MaximumContributingReasons is < 1 or > 256)
            throw new InvalidOperationException("Recovery episode policy requires a finite deadline and bounded reasons.");
        return this;
    }
}

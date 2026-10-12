namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public sealed record DatabentoSoftGatePolicy
{
    public TimeSpan ProbeTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan OverallTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(2);
    public int MaximumRounds { get; init; } = 3;

    public DatabentoSoftGatePolicy Validate()
    {
        if (ProbeTimeout <= TimeSpan.Zero || ProbeTimeout >= OverallTimeout
            || OverallTimeout > TimeSpan.FromMinutes(10) || RetryDelay < TimeSpan.Zero
            || RetryDelay >= OverallTimeout
            || MaximumRounds is < 1 or > 10)
            throw new InvalidOperationException("Soft recovery probe policy must be finite.");
        return this;
    }
}

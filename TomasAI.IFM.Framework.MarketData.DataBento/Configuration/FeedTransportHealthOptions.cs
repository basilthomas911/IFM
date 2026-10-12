namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed record FeedTransportHealthOptions
{
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan HungConnectionTimeout => HeartbeatInterval + TimeSpan.FromSeconds(5);
    public TimeSpan HealthPollInterval { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan MetricsExportInterval { get; init; } = TimeSpan.FromSeconds(5);
}

namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed record FeedGcOptions
{
    public bool EnableSustainedLowLatency { get; init; } = true;
    public bool RequireGcConfiguration { get; init; }
}

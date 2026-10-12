namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed record FeedRingBackpressureOptions
{
    public int SpinIterations { get; init; } = 256;
    public TimeSpan RingFullTimeout { get; init; } = TimeSpan.FromMilliseconds(2);
}

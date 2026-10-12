namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed record FeedNumaOptions
{
    public NumaLocalityMode Mode { get; init; } = NumaLocalityMode.Auto;
    public ushort? Node { get; init; }
    public bool RequireNumaLocality { get; init; }
}

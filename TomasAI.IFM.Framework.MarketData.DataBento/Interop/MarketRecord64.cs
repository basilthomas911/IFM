using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1815", Justification = "This sequential native ABI buffer is transported by pointer and is not a value identity.")]
[StructLayout(LayoutKind.Explicit, Pack = 8, Size = 64)]
public readonly struct MarketRecord64
{
    [FieldOffset(0)] public readonly MarketRecordHeader32 Header;
    [FieldOffset(0)] public readonly QuoteRecord64 Quote;
    [FieldOffset(0)] public readonly TradeRecord64 Trade;
    [FieldOffset(0)] public readonly MboRecord64 Mbo;
    [FieldOffset(0)] public readonly StatisticsRecord64 Statistics;

    /// <summary>Initializes a new MarketRecord64 instance.</summary>
    /// <param name="quote">The bid and ask snapshot, when available.</param>
    public MarketRecord64(QuoteRecord64 quote)
    {
        this = default;
        Quote = quote;
    }

    /// <summary>Initializes a new MarketRecord64 instance.</summary>
    /// <param name="trade">The last trade snapshot, when available.</param>
    public MarketRecord64(TradeRecord64 trade)
    {
        this = default;
        Trade = trade;
    }

    /// <summary>Initializes a new MarketRecord64 instance.</summary>
    /// <param name="statistics">The statistics.</param>
    public MarketRecord64(StatisticsRecord64 statistics)
    {
        this = default;
        Statistics = statistics;
    }
}

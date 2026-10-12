using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Application.MarketData.MarketOutlook;

/// <summary>Stable, bounded metric identity for one local Market Outlook input.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The serialized message enum has a fixed underlying width; preserve its wire representation.")]
public enum MarketOutlookUpdateKind : byte
{
    Rsi,
    Tdi,
    Iti,
    Ema,
    BollingerBand,
    EsTrade,
    VixPrice,
    Eod,
    TradeSignal,
    FeedHealth,
    HistoricalWarmup,
    Hydration,
    Recompose,
    Vwap,
    Adx,
    Atr,
    Macd
}

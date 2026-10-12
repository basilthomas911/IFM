using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1008", Justification = "Zero is an existing meaningful contract value; introducing an alias would change validation and serialization names.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The published message or native ABI fixes the enum storage width; changing it would alter the wire contract.")]
public enum MarketRecordKind : byte
{
    Quote = 1,
    Trade = 2,
    Mbo = 3,
    Statistics = 4,
    StatisticsReplayComplete = 5,
    TradeReplayComplete = 6
}

namespace TomasAI.IFM.Framework.MarketData.DataBento;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The published message or native ABI fixes the enum storage width; changing it would alter the wire contract.")]
public enum FeedProcessorSelectionKind : byte
{
    Unpinned = 0,
    PerformanceCore = 1,
    AffinityFallback = 2
}

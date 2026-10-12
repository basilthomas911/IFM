namespace TomasAI.IFM.Framework.MarketData.DataBento;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The published message or native ABI fixes the enum storage width; changing it would alter the wire contract.")]
public enum FeedThreadPriority : byte
{
    Normal = 0,
    AboveNormal = 1,
    Highest = 2
}

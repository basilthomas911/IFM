namespace TomasAI.IFM.Framework.MarketData.DataBento;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The published message or native ABI fixes the enum storage width; changing it would alter the wire contract.")]
public enum FeedReadinessState : byte
{
    Closed = 0,
    Ready = 1,
    Suspect = 2,
    Recovering = 3,
    Faulted = 4
}

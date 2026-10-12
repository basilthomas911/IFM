namespace TomasAI.IFM.Framework.MarketData.DataBento;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1008", Justification = "Zero is an existing meaningful contract value; introducing an alias would change validation and serialization names.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The published message or native ABI fixes the enum storage width; changing it would alter the wire contract.")]
public enum FeedRecoveryFaultKind : byte
{
    ConnectionHung = 1,
    Disconnected = 2,
    RingOverrun = 3,
    SlowReader = 4,
    SkippedRecords = 5,
    SequenceGap = 6,
    SequenceReversal = 7,
    Authentication = 8,
    InvalidRequest = 9,
    SymbolResolution = 10,
    ProviderError = 11
}

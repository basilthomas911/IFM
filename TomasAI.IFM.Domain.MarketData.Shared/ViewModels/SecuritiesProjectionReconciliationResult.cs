namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>
/// Summarizes a key-only comparison between canonical Securities rows and their projections.
/// </summary>
public sealed record SecuritiesProjectionReconciliationResult(
    int FuturesContractSourceRows,
    int FuturesContractProjectionRows,
    int FuturesContractMissingKeys,
    int FuturesContractUnexpectedKeys,
    int FuturesOptionContractSourceRows,
    int FuturesOptionContractProjectionRows,
    int FuturesOptionContractMissingKeys,
    int FuturesOptionContractUnexpectedKeys)
{
    /// <summary>Gets whether source and projection row counts and keys agree.</summary>
    public bool IsConsistent =>
        FuturesContractSourceRows == FuturesContractProjectionRows &&
        FuturesContractMissingKeys == 0 &&
        FuturesContractUnexpectedKeys == 0 &&
        FuturesOptionContractSourceRows == FuturesOptionContractProjectionRows &&
        FuturesOptionContractMissingKeys == 0 &&
        FuturesOptionContractUnexpectedKeys == 0;
}

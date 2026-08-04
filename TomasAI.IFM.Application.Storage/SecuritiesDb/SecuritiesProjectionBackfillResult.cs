namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

/// <summary>
/// Counts the idempotent projection upserts completed by a Securities backfill.
/// </summary>
public sealed record SecuritiesProjectionBackfillResult(
    int FuturesContractsUpserted,
    int FuturesOptionContractsUpserted);

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
    public bool IsConsistent =>
        FuturesContractSourceRows == FuturesContractProjectionRows &&
        FuturesContractMissingKeys == 0 &&
        FuturesContractUnexpectedKeys == 0 &&
        FuturesOptionContractSourceRows == FuturesOptionContractProjectionRows &&
        FuturesOptionContractMissingKeys == 0 &&
        FuturesOptionContractUnexpectedKeys == 0;
}

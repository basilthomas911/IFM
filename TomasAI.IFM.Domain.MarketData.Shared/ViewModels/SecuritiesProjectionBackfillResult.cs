namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>
/// Counts the idempotent projection upserts completed by a Securities backfill.
/// </summary>
public sealed record SecuritiesProjectionBackfillResult(
    int FuturesContractsUpserted,
    int FuturesOptionContractsUpserted);

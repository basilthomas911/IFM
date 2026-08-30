namespace TomasAI.IFM.Application.Storage.FundDb;

/// <summary>
/// Read-only boundary over the pre-Portfolio Fund store. This context preserves historical access only;
/// it intentionally exposes no schema, mutation, event, or sequence-allocation capability.
/// </summary>
public interface IFundLegacyDbContext
{
    IFundDbReadContext HistoricalQueries { get; }
}

public sealed class FundLegacyDbContext(IFundDbReadContext historicalQueries) : IFundLegacyDbContext
{
    public IFundDbReadContext HistoricalQueries { get; } =
        historicalQueries ?? throw new ArgumentNullException(nameof(historicalQueries));
}

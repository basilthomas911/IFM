namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Contains financial-authority preparation data read at one revision.</summary>
/// <param name="Book">The financial book configuration.</param>
/// <param name="Revision">The financial authority revision.</param>
/// <param name="Epoch">The authority epoch.</param>
/// <param name="State">The persisted authority state.</param>
/// <param name="AvailableCash">Available cash by fund identifier.</param>
public sealed record FinancialAuthorityPreparationSnapshot(
    FinancialBookConfiguration Book,
    long Revision,
    long Epoch,
    string State,
    IReadOnlyDictionary<int, decimal> AvailableCash);

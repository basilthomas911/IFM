using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
public sealed record LedgerFinancialSnapshot(FinancialBookConfiguration Book, long Revision,
    IReadOnlyList<LedgerPostingRule> Rules, IReadOnlyDictionary<int, decimal> SettledCash,
    IReadOnlyDictionary<int, decimal> PendingWithdrawals, IReadOnlyDictionary<int, decimal> ReservedFunding);

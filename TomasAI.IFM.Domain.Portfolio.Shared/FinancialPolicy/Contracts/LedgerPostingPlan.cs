using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
public sealed record LedgerPostingPlan(IReadOnlyList<PlannedLedgerLine> Lines, decimal WithdrawalObligationDelta,
    bool IsDiscretionarySpending, bool IsActualFinancialFact, string ContentHash = "");




using TomasAI.IFM.Domain.Portfolio.Shared.Financial;



namespace TomasAI.IFM.Domain.Portfolio.Financial.Repository;

public delegate LedgerPostingPlan LedgerCalculation(LedgerPostingRequest request, LedgerPostingRule rule,
    decimal previousUnrealized, IReadOnlyList<PlannedLedgerLine>? reversalLines, decimal remainingReversibleAmount);

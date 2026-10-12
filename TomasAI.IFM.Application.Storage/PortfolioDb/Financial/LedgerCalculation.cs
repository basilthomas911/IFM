using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using static TomasAI.IFM.Application.Storage.PortfolioDb.PortfolioDbFinancialSupport;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

public delegate LedgerPostingPlan LedgerCalculation(LedgerPostingRequest request, LedgerPostingRule rule,
    decimal previousUnrealized, IReadOnlyList<PlannedLedgerLine>? reversalLines, decimal remainingReversibleAmount);

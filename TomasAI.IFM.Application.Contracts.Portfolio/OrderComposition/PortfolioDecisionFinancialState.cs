using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

/// <summary>Trusted owner-published financial inputs. This object is never accepted from a client message.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Producer-owned financial snapshots retain the established array contract.")]
public sealed record PortfolioDecisionFinancialState(FinancialBookConfiguration Book, long FinancialRevision,
    PortfolioFundFinancialSnapshot[] Funds, DateTime CapturedAtUtc, DateTime ValidUntilUtc, long Generation);

public interface IPortfolioDecisionStateReader
{
    bool TryAcquire(int portfolioId, int fundId, DateTime atUtc, out PortfolioDecisionFinancialState? state);
}

public interface IPortfolioEntryDecisionCalculator
{
    PortfolioFundEntryAssessment[] Calculate(EvaluatePortfolioOrderCompositionCommand request,
        PortfolioDecisionFinancialState state, CancellationToken cancellationToken);
}

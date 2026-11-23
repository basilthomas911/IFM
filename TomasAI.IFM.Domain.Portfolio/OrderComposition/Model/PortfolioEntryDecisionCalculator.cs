using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

namespace TomasAI.IFM.Domain.Portfolio.OrderComposition.Model;

/// <summary>Pure financial eligibility. No store, identity allocator, actor or provider dependency.</summary>
public sealed class PortfolioEntryDecisionCalculator : IPortfolioEntryDecisionCalculator
{
    public PortfolioFundEntryAssessment[] Calculate(EvaluatePortfolioOrderCompositionCommand request,
        PortfolioDecisionFinancialState state, CancellationToken cancellationToken) =>
        PortfolioOrderCompositionModel.Assess(request, state.Book, state.Funds, cancellationToken);
}

using System.Collections.Concurrent;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

namespace TomasAI.IFM.Domain.Portfolio.OrderComposition.Model;

public sealed class PortfolioDecisionStateCache : IPortfolioDecisionStateReader
{
    readonly ConcurrentDictionary<int, PortfolioDecisionFinancialState> states = new();
    public void Publish(PortfolioDecisionFinancialState state)
    {
        if (state.FinancialRevision <= 0 || state.CapturedAtUtc.Kind != DateTimeKind.Utc || state.ValidUntilUtc.Kind != DateTimeKind.Utc
            || state.ValidUntilUtc <= state.CapturedAtUtc || state.Book.PortfolioId <= 0
            || state.Funds.Select(value => value.FundId).Distinct().Count() != state.Funds.Length)
            throw new ArgumentException("A complete, versioned financial snapshot is required.", nameof(state));
        states.AddOrUpdate(state.Book.PortfolioId, state, (_, previous) =>
            previous.FinancialRevision > state.FinancialRevision
                || previous.FinancialRevision == state.FinancialRevision && (previous.Generation > state.Generation
                    || previous.Generation == state.Generation && previous.CapturedAtUtc > state.CapturedAtUtc) ? previous : state);
    }
    public bool TryAcquire(int portfolioId, int fundId, DateTime atUtc, out PortfolioDecisionFinancialState? state)
    {
        state = null;
        if (!states.TryGetValue(portfolioId, out var current) || atUtc < current.CapturedAtUtc || atUtc >= current.ValidUntilUtc
            || current.Generation != FinancialDecisionStateInvalidation.Current(portfolioId)) return false;
        var fund = current.Book.Funds.SingleOrDefault(value => value.FundId == fundId);
        if (fund is null || !current.Funds.Any(value => value.FundId == fundId)
            || !current.Book.DevelopmentQualificationsExempt && fund.Reference.ValidUntilUtc <= atUtc) return false;
        state = current;
        return true;
    }
}

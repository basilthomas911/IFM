using System.Collections.Concurrent;

namespace TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

/// <summary>Owner invalidation occurs before financial mutation. A failed transaction may invalidate, never authorize, a snapshot.</summary>
public static class FinancialDecisionStateInvalidation
{
    static readonly ConcurrentDictionary<int, long> generations = new();
    public static long Current(int portfolioId) => generations.GetOrAdd(portfolioId, 1);
    public static long Invalidate(int portfolioId) => generations.AddOrUpdate(portfolioId, 2, (_, previous) => checked(previous + 1));
}

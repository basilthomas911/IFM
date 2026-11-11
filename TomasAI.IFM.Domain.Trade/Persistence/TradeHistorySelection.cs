namespace TomasAI.IFM.Domain.Trade.Persistence;

/// <summary>Qualifies trade history selections before querying stored read models.</summary>
internal static class TradeHistorySelection
{
    internal static void RequireValueDate(int portfolioId, int fundId, DateOnly valueDate,
        int pageSize, int maximumPageSize, string message)
    {
        if (portfolioId <= 0 || fundId <= 0 || valueDate == default || pageSize < 1 || pageSize > maximumPageSize)
            throw new ArgumentException(message, nameof(portfolioId));
    }

    internal static void RequireEstablished(int portfolioId, int fundId, DateTime fromUtc, DateTime toUtc, int pageSize)
    {
        if (portfolioId <= 0 || fundId <= 0 || !ValidWindow(fromUtc, toUtc, pageSize))
            throw new ArgumentException("Valid ownership, UTC range, and page size 1..1000 are required.", nameof(portfolioId));
    }

    internal static void RequirePosition(Guid positionId, DateTime fromUtc, DateTime toUtc, int pageSize)
    {
        if (positionId == Guid.Empty || !ValidWindow(fromUtc, toUtc, pageSize))
            throw new ArgumentException("Valid PositionId, UTC range, and page size 1..1000 are required.", nameof(positionId));
    }

    static bool ValidWindow(DateTime fromUtc, DateTime toUtc, int pageSize) =>
        fromUtc.Kind == DateTimeKind.Utc && toUtc.Kind == DateTimeKind.Utc && fromUtc <= toUtc && pageSize is >= 1 and <= 1000;
}

namespace TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;

/// <summary>The caller's canonical option root and inclusive expiry range.</summary>
public sealed class OptionDefinitionExpiryRange
{
    public OptionDefinitionExpiryRange(string symbol, DateOnly fromExpiry, DateOnly throughExpiry,
        int maximumExpiries = int.MaxValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentOutOfRangeException.ThrowIfLessThan(throughExpiry, fromExpiry);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumExpiries);
        Symbol = symbol.Trim().ToUpperInvariant();
        FromExpiry = fromExpiry;
        ThroughExpiry = throughExpiry;
        MaximumExpiries = maximumExpiries;
    }

    public string Symbol { get; }
    public DateOnly FromExpiry { get; }
    public DateOnly ThroughExpiry { get; }
    public int MaximumExpiries { get; }
}

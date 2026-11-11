namespace TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;

/// <summary>Identifies the caller's canonical option root, underlying contract and expiry.</summary>
public sealed record OptionDefinitionPartition
{
    public OptionDefinitionPartition(string symbol, string underlyingContractId, DateOnly expiryDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(underlyingContractId);
        Symbol = symbol.Trim().ToUpperInvariant();
        UnderlyingContractId = underlyingContractId;
        ExpiryDate = expiryDate;
    }

    public string Symbol { get; }
    public string UnderlyingContractId { get; }
    public DateOnly ExpiryDate { get; }

    public void Deconstruct(out string symbol, out string underlyingContractId, out DateOnly expiryDate)
    {
        symbol = Symbol;
        underlyingContractId = UnderlyingContractId;
        expiryDate = ExpiryDate;
    }
}

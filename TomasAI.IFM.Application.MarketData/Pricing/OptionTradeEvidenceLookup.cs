namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Qualifies the exact source identity used to retrieve an immutable trade observation.</summary>
public sealed class OptionTradeEvidenceLookup
{
    public OptionTradeEvidenceLookup(string contract, DateOnly date, string sourceId)
    {
        ArgumentNullException.ThrowIfNull(sourceId);
        if (string.IsNullOrWhiteSpace(contract) || contract.Length > 128 || date == default || sourceId.Length != 64)
            throw new ArgumentException("Exact trade source identity is required.", nameof(contract));
        Contract = contract;
        Date = date;
        SourceId = sourceId;
    }

    public string Contract { get; }
    public DateOnly Date { get; }
    public string SourceId { get; }
}

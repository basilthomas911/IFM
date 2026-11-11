namespace TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;

/// <summary>Identifies one immutable reference revision for a canonical contract.</summary>
public sealed record ExactReferenceVersion
{
    public ExactReferenceVersion(string contractId, string version)
    {
        if (string.IsNullOrWhiteSpace(contractId) || contractId.Length > 128
            || string.IsNullOrWhiteSpace(version) || version.Length > 128)
            throw new ArgumentException("Exact reference version is required.", nameof(contractId));
        ContractId = contractId;
        Version = version;
    }

    public string ContractId { get; }
    public string Version { get; }
}

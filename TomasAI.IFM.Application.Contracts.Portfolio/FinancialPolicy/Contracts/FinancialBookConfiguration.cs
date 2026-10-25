using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

[MessagePackObject]
public sealed record FinancialBookConfiguration
{
    [Key(0)] public int BookId { get; init; }
    [Key(1)] public int PortfolioId { get; init; }
    [Key(2)] public Guid AccountingEntityId { get; init; }
    [Key(3)] public string Currency { get; init; } = "USD";
    [Key(4)] public string ExecutionAccountReference { get; init; } = string.Empty;
    [Key(5)] public string Environment { get; init; } = string.Empty;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
    [Key(6)] public FinancialFundAuthority[] Funds { get; init; } = [];
    [Key(7)] public long AuthorityEpoch { get; init; }
    [Key(8)] public string SourceWatermark { get; init; } = string.Empty;
    [Key(9)] public string ValuationWatermark { get; init; } = string.Empty;
    [Key(10)] public bool MigrationQualified { get; init; }
    /// <summary>Trusted runtime policy only; never accepted from or stored in a financial message.</summary>
    [IgnoreMember, Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
    public bool DevelopmentQualificationsExempt { get; init; }
}

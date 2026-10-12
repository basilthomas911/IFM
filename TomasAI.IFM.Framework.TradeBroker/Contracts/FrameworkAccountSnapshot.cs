namespace TomasAI.IFM.Framework.TradeBroker.Contracts;

/// <summary>A coherent synthetic or external account observation.</summary>
public sealed record FrameworkAccountSnapshot
{
    public required string AccountAlias { get; init; }
    public required string Currency { get; init; }
    public decimal CashBalance { get; init; }
    public decimal AvailableFunds { get; init; }
    public bool Complete { get; init; }
    public bool NewRiskAllowed { get; init; }
    public long Generation { get; init; }
    public DateTime AsOfUtc { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized message/configuration contract, including enum widths, numeric values, and collection shape.")]
    public FrameworkAccountPosition[] Positions { get; init; } = [];
}

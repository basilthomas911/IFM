using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

/// <summary>A saved Fund default, resolved against Databento contracts at the server value date.</summary>
[MessagePackObject]
public sealed record FundBaseContractRule
{
    /// <summary>Futures is supported; Equities remains a UI placeholder.</summary>
    [Key(0)] public string BaseAsset { get; init; } = "Futures";
    /// <summary>Databento product symbol, such as ES or VX.</summary>
    [Key(1)] public string Symbol { get; init; } = string.Empty;
    /// <summary>Listed expiries in addition to the current contract.</summary>
    [Key(2)] public int ForwardCycles { get; init; }

    /// <summary>Rejects unsupported assets, missing symbols and unbounded forward requests.</summary>
    public IReadOnlyList<string> Validate() => BaseAsset != "Futures" || string.IsNullOrWhiteSpace(Symbol) ||
        Symbol != Symbol.Trim() || ForwardCycles is < 0 or > 120
        ? ["Choose a Futures symbol and zero to 120 forward cycles."] : [];
}

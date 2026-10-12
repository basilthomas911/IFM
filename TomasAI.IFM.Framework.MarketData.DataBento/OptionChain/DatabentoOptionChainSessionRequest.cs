using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;

public sealed record DatabentoOptionChainSessionRequest
{
    /// <summary>Gets the single option contract owning this physical connection; empty selects chain mode.</summary>
    public string OptionContractId { get; init; } = string.Empty;
    /// <summary>Optional logical chain owner, allowing replacement coverage to coexist with manual sessions on the shared discovery connection.</summary>
    public string ConnectionScopeId { get; init; } = string.Empty;
    public required string FuturesContractId { get; init; }
    public required DateOnly ValueDate { get; init; }
    public required OptionChainSubscription Subscription { get; init; }
    public required IReadOnlyList<DatabentoOptionChainRoute> Routes { get; init; }
}

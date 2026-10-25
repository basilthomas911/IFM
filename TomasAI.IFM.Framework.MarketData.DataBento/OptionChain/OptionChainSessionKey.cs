using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;

/// <summary>Initializes a new OptionChainSessionKey instance.</summary>
/// <param name="FuturesContractId">The futures contract id.</param>
/// <param name="MaturityDate">The maturity date.</param>
/// <param name="OptionContractId">The exact leg contract for an isolated connection; empty for a chain connection.</param>
public readonly record struct OptionChainSessionKey(
    string FuturesContractId,
    DateOnly MaturityDate,
    string OptionContractId = "",
    string ConnectionScopeId = "");

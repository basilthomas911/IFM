using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;

public sealed record DatabentoOptionChainRoute
{
    public required string FuturesOptionContractId { get; init; }
    public required OptionContractDefinition Definition { get; init; }
}

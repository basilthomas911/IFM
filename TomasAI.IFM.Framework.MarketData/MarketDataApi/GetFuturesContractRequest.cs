using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Framework.MarketData.MarketDataApi;

public record GetFuturesContractRequest(
    Guid CommandId,
    FuturesContractV2ReadModel QueryForContract);

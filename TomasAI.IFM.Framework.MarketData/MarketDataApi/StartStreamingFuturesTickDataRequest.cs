using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Framework.MarketData.MarketDataApi;

public record StartStreamingFuturesTickDataRequest(
    Guid CommandId,
    DateOnly valueDate,
    FuturesContractV2ReadModel Contract);

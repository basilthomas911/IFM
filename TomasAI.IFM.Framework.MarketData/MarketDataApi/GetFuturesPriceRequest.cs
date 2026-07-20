using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Framework.MarketData.MarketDataApi;

public record GetFuturesPriceRequest(
    Guid CommandId,
    FuturesContractV2ReadModel FuturesContract);

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

public record FuturesTickHLVDataReadModel(
    string ContractId,
    DateOnly ValueDate,
    decimal HighPrice,
    decimal LowPrice,
    long Volume);

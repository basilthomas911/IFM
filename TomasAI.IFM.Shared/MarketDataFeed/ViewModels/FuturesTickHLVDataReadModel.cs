namespace TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

public record FuturesTickHLVDataReadModel(
    string ContractId,
    DateOnly ValueDate,
    decimal HighPrice,
    decimal LowPrice,
    int  Volume);

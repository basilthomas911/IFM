namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S101", Justification = "Preserves the existing externally referenced CLR type identity.")]
public record FuturesTickHLVDataReadModel(
    string ContractId,
    DateOnly ValueDate,
    decimal HighPrice,
    decimal LowPrice,
    long Volume);

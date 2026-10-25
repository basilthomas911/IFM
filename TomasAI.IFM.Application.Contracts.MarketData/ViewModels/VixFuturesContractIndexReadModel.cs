namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

public readonly record struct VixFuturesContractIndexReadModel(
    int Bucket,
    string ContractId);

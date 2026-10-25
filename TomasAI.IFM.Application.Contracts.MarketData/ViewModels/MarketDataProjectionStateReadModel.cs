namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

public readonly record struct MarketDataProjectionStateReadModel(
    string ProjectionName,
    Guid Generation,
    bool IsReady);

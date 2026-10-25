namespace TomasAI.IFM.Domain.Trade.Shared.ViewModels;

public record LossProbabilityDataModel(
    double Value,
    decimal Threshold,
    int ThresholdCount);
